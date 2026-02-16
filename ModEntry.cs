// ModEntry.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.TerrainFeatures;
using GoodFences.Models;
using GoodFences.Handlers;
using GoodFences.Patches;
using System.Linq;

namespace GoodFences
{
    /// <summary>
    /// GoodFences — Action-based multiplayer ownership.
    /// What you produce is yours from seed to sale.
    ///
    /// Step 2A: Passive tagging + enforcement layer.
    /// Tags all items and objects, then enforces ownership with trust/permission system.
    /// </summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        /// <summary>Static instance for access from handlers and patches.</summary>
        internal static ModEntry? Instance;

        /// <summary>The ownership tagging handler.</summary>
        private OwnershipTagHandler? TagHandler;

        /// <summary>
        /// Flag to suppress terrain feature tagging during save load and
        /// day transitions. Set to true after a short delay, ensuring we
        /// only tag player-initiated actions.
        /// </summary>
        private bool DayFullyStarted = false;

        /// <summary>Tick count since the day started, for delayed flag setting.</summary>
        private int TicksSinceDayStart = 0;

        /// <summary>Mod configuration options.</summary>
        internal ModConfig Config { get; private set; } = null!;

        /// <summary>Static configuration accessor for patches and handlers.</summary>
        internal static ModConfig StaticConfig => Instance!.Config;

        /// <summary>Static reference to the monitor for logging.</summary>
        internal static IMonitor? StaticMonitor;

        /*********
        ** Entry Point
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            StaticMonitor = this.Monitor;
            Config = helper.ReadConfig<ModConfig>();

            // Initialize the tagging handler
            this.TagHandler = new OwnershipTagHandler(
                this.Monitor,
                Config.DebugMode
            );

            // Apply Harmony patches
            var harmony = new Harmony(this.ModManifest.UniqueID);
            
            // Step 1: Ownership tagging patches (includes placement tagging
            // and universal tool-strike protection for all owned objects)
            OwnershipTagPatches.Initialize(this.Monitor, this.TagHandler);
            OwnershipTagPatches.Apply(harmony);

            // Automate compatibility patches to preserve ownership tags
            AutomateCompatibilityPatches.Apply(harmony, this.Monitor, Config.DebugMode);

            // Step 2A: Enforcement patches
            if (Config.EnforceCropOwnership)
            {
                // Block non-owners from right-click harvesting
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Crop), nameof(StardewValley.Crop.harvest)),
                    prefix: new HarmonyMethod(typeof(CropEnforcementPatches), nameof(CropEnforcementPatches.Crop_Harvest_Prefix))
                );

                // Block non-owners from using tools on owned crops (pickaxe, axe, scythe)
                // and clear soil ownership when owner destroys own crop
                harmony.Patch(
                    original: AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.performToolAction)),
                    prefix: new HarmonyMethod(typeof(CropEnforcementPatches), nameof(CropEnforcementPatches.HoeDirt_PerformToolAction_Prefix))
                );
                harmony.Patch(
                    original: AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.performToolAction)),
                    postfix: new HarmonyMethod(typeof(CropEnforcementPatches), nameof(CropEnforcementPatches.HoeDirt_PerformToolAction_Postfix))
                );

                this.Monitor.Log("Applied crop enforcement patches (harvest + tool protection)", LogLevel.Debug);
            }

            if (Config.EnforceMachineOwnership)
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.performObjectDropInAction)),
                    prefix: new HarmonyMethod(typeof(MachineEnforcementPatches), nameof(MachineEnforcementPatches.Object_PerformObjectDropInAction_Prefix))
                );
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction)),
                    prefix: new HarmonyMethod(typeof(MachineEnforcementPatches), nameof(MachineEnforcementPatches.Object_CheckForAction_Prefix))
                );
                this.Monitor.Log("Applied machine enforcement patches", LogLevel.Debug);
            }

            if (Config.EnforceChestOwnership)
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Objects.Chest), nameof(StardewValley.Objects.Chest.checkForAction)),
                    prefix: new HarmonyMethod(typeof(ChestEnforcementPatches), nameof(ChestEnforcementPatches.Chest_CheckForAction_Prefix))
                );
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Objects.Chest), nameof(StardewValley.Objects.Chest.addItem)),
                    prefix: new HarmonyMethod(typeof(ChestEnforcementPatches), nameof(ChestEnforcementPatches.Chest_AddItem_Prefix))
                );

                // Block Chests Anywhere from bypassing ownership via remote
                // item transfer. CA uses grabItemFromInventory (deposit) and
                // grabItemFromChest (withdraw) instead of checkForAction.
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Objects.Chest), nameof(StardewValley.Objects.Chest.grabItemFromInventory)),
                    prefix: new HarmonyMethod(typeof(ChestEnforcementPatches), nameof(ChestEnforcementPatches.Chest_GrabItemFromInventory_Prefix))
                );
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Objects.Chest), nameof(StardewValley.Objects.Chest.grabItemFromChest)),
                    prefix: new HarmonyMethod(typeof(ChestEnforcementPatches), nameof(ChestEnforcementPatches.Chest_GrabItemFromChest_Prefix))
                );

                this.Monitor.Log("Applied chest enforcement patches (vanilla + Chests Anywhere)", LogLevel.Debug);
            }

            if (Config.EnforcePastureProtection)
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(StardewValley.Tools.Hoe), nameof(StardewValley.Tools.Hoe.DoFunction)),
                    prefix: new HarmonyMethod(typeof(PastureEnforcementPatches), nameof(PastureEnforcementPatches.Hoe_DoFunction_Prefix))
                );
                harmony.Patch(
                    original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.checkAction)),
                    prefix: new HarmonyMethod(typeof(PastureEnforcementPatches), nameof(PastureEnforcementPatches.GameLocation_CheckAction_Prefix))
                );
                this.Monitor.Log("Applied pasture enforcement patches", LogLevel.Debug);
            }

            // Shipping bin enforcement — always active in multiplayer.
            // Grays out items in the shipping bin UI that belong to
            // another player, preventing cross-player revenue theft.
            // Works with both vanilla shipping bin and Chests Anywhere.
            harmony.Patch(
                original: AccessTools.Method(typeof(Utility), nameof(Utility.highlightShippableObjects)),
                postfix: new HarmonyMethod(typeof(ShippingBinEnforcementPatches), nameof(ShippingBinEnforcementPatches.Utility_HighlightShippableObjects_Postfix))
            );
            this.Monitor.Log("Applied shipping bin enforcement patch", LogLevel.Debug);

            // Register SMAPI events
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Player.InventoryChanged += this.OnInventoryChanged;
            helper.Events.World.TerrainFeatureListChanged += this.OnTerrainFeatureListChanged;
            helper.Events.World.BuildingListChanged += this.OnBuildingListChanged;
            helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            this.Monitor.Log("GoodFences v2.1 loaded — Step 2A enforcement active.", LogLevel.Info);
        }

        /*********
        ** Event Handlers
        *********/
        /// <summary>Raised after the game is launched.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Register Generic Mod Config Menu integration
            GMCMIntegration.Register(
                helper: this.Helper,
                manifest: this.ModManifest,
                config: this.Config,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );
        }

        /// <summary>Raised after a save is loaded.</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.DayFullyStarted = false;
            this.TicksSinceDayStart = 0;
            
            if (!Context.IsMultiplayer)
            {
                this.Monitor.Log("Single-player game detected. GoodFences tagging is active but enforcement is disabled.", LogLevel.Info);
                return;
            }

            this.Monitor.Log($"GoodFences active for multiplayer. Separate wallets: {Game1.player.useSeparateWallets}", LogLevel.Info);

            if (!Game1.player.useSeparateWallets)
            {
                this.Monitor.Log("Warning: Separate wallets are not enabled. GoodFences works best with separate wallets turned on.", LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage(
                    "GoodFences: Enable 'Separate Wallets' in game options for best experience.",
                    HUDMessage.error_type));
            }

            // Initialize common chest system (host only)
            if (Game1.player.IsMainPlayer)
            {
                CommonChestHandler.InitializeCommonChest();
            }

            // Initialize pasture zones for existing buildings
            PastureZoneHandler.InitializeExistingBuildings();
        }

        /// <summary>Raised when a new day starts. Reset the terrain feature flag.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.DayFullyStarted = false;
            this.TicksSinceDayStart = 0;

            // Update trust expirations (check for expired trust relationships)
            if (Context.IsMultiplayer)
            {
                TrustSystemHandler.UpdateTrustExpirations();
            }
        }

        /// <summary>
        /// Periodic scan for machine output tagging. Runs every 60 ticks
        /// (~1 second) to pre-tag ready output on machines that have a
        /// stored input owner. This ensures the output is tagged before
        /// any player collects it, regardless of the collection method.
        /// Also manages the DayFullyStarted flag and PendingHarvestOwner cleanup.
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Wait 120 ticks (~2 seconds) after day start before allowing
            // terrain feature tagging. This ensures all day-transition
            // terrain feature events have settled.
            if (!this.DayFullyStarted)
            {
                this.TicksSinceDayStart++;
                if (this.TicksSinceDayStart >= 120)
                {
                    this.DayFullyStarted = true;
                }
                return;
            }

            // Only scan machines every 60 ticks (~1 second)
            if (e.Ticks % 60 != 0)
                return;

            // Scan the player's current location for machines with ready output
            OwnershipTagPatches.PreTagMachineOutput(Game1.currentLocation);
        }

        /// <summary>
        /// Backup catch-all tagger. The primary tagging now happens in the
        /// Farmer.addItemToInventoryBool prefix (before stacking). This
        /// catches any items that enter inventory through a path that
        /// doesn't go through addItemToInventoryBool.
        /// </summary>
        private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
        {
            if (!e.IsLocalPlayer)
                return;

            var farmer = e.Player;

            foreach (var item in e.Added)
            {
                if (OwnershipTagHandler.HasOwner(item))
                    continue;

                if (OwnershipTagHandler.IsCommon(item))
                    continue;

                OwnershipTagHandler.TagItem(item, farmer);
                this.TagHandler?.LogTag("INVENTORY", item.Name, farmer, "backup catch-all");
            }
        }

        /// <summary>
        /// Track terrain feature changes for fruit tree planting.
        /// When a player plants a fruit tree sapling, a new FruitTree
        /// terrain feature is added. We tag it with the planter's ID.
        /// </summary>
        private void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
        {
            // Skip during save load and day transitions to avoid
            // tagging pre-existing trees to the wrong player
            if (!this.DayFullyStarted)
                return;
            foreach (var added in e.Added)
            {
                // Tag newly planted fruit trees
                if (added.Value is FruitTree fruitTree)
                {
                    var farmer = Game1.player;
                    if (farmer != null)
                    {
                        OwnershipTagHandler.TagFruitTree(fruitTree, farmer);
                        this.TagHandler?.LogTag("FRUIT-TREE-PLANT", "fruit tree", farmer,
                            $"at ({added.Key.X},{added.Key.Y})");
                    }
                }

                // Tag newly planted wild trees (backup for Harmony patch)
                if (added.Value is Tree tree)
                {
                    if (!tree.modData.ContainsKey(OwnershipTagHandler.OwnerKey))
                    {
                        var farmer = Game1.player;
                        if (farmer != null)
                        {
                            OwnershipTagHandler.TagTree(tree, farmer);
                            this.TagHandler?.LogTag("TREE-PLANT", "tree", farmer,
                                $"at ({added.Key.X},{added.Key.Y}), event backup");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Track building placement/removal for pasture zone management.
        /// When a coop or barn is placed, create automatic pasture protection zone.
        ///
        /// FIX: Uses building.owner.Value (the game's own purchaser tracking)
        /// instead of Game1.player. Game1.player is always the host on the
        /// host's machine, even when a farmhand initiates the construction.
        /// </summary>
        private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
        {
            if (!Context.IsMultiplayer || !Config.EnforcePastureProtection)
                return;

            // Handle new buildings
            foreach (var building in e.Added)
            {
                // Tag building with owner using the game's own purchaser data.
                // building.owner.Value is the UniqueMultiplayerID of the player
                // who paid for construction. Falls back to Game1.player only if
                // the game's owner field is unset (shouldn't happen normally).
                if (building.modData != null && !building.modData.ContainsKey("GoodFences.Owner"))
                {
                    long buildingOwnerId = building.owner.Value;

                    // Fall back to current player if game's owner tracking failed
                    if (buildingOwnerId == 0)
                    {
                        buildingOwnerId = Game1.player.UniqueMultiplayerID;
                    }

                    building.modData["GoodFences.Owner"] = buildingOwnerId.ToString();

                    // Look up the owner name for logging
                    var ownerFarmer = Game1.GetPlayer(buildingOwnerId);
                    string ownerName = ownerFarmer?.Name ?? "Unknown";
                    this.Monitor.Log($"Tagged building {building.buildingType.Value} with owner {ownerName} (ID: {buildingOwnerId})", LogLevel.Debug);
                }

                // Create pasture zone for coops/barns
                PastureZoneHandler.CreatePastureZone(building);
            }

            // Handle removed buildings
            foreach (var building in e.Removed)
            {
                PastureZoneHandler.RemovePastureZone(building);
            }
        }

        /// <summary>
        /// Handle button presses for debug commands and common chest designation.
        /// </summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Debug commands (only in debug mode)
            if (Config.DebugMode)
            {
                var showTrustsKeys = KeybindList.Parse(Config.KeyShowTrusts);
                var showPasturesKeys = KeybindList.Parse(Config.KeyShowPastures);
                var showCommonChestsKeys = KeybindList.Parse(Config.KeyShowCommonChests);

                if (showTrustsKeys.JustPressed())
                {
                    TrustSystemHandler.DebugShowAllTrusts();
                    this.Helper.Input.SuppressActiveKeybinds(showTrustsKeys);
                }
                else if (showPasturesKeys.JustPressed())
                {
                    PastureZoneHandler.DebugShowAllPastures();
                    this.Helper.Input.SuppressActiveKeybinds(showPasturesKeys);
                }
                else if (showCommonChestsKeys.JustPressed())
                {
                    CommonChestHandler.DebugShowAllCommonChests();
                    this.Helper.Input.SuppressActiveKeybinds(showCommonChestsKeys);
                }
            }

            // Host can designate common chests with configured keybind
            if (Game1.player.IsMainPlayer)
            {
                var designateChestKeys = KeybindList.Parse(Config.KeyDesignateCommonChest);
                if (designateChestKeys.JustPressed())
                {
                    var tile = e.Cursor.GrabTile;
                    var location = Game1.currentLocation;
                    
                    if (location?.objects != null && location.objects.TryGetValue(tile, out var obj))
                    {
                        if (obj is StardewValley.Objects.Chest chest)
                        {
                            // Show common chest designation menu
                            this.Helper.Input.SuppressActiveKeybinds(designateChestKeys);
                            CommonChestHandler.ShowChestDesignationMenu(chest);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Version check when a peer connects. Warns if the connecting
        /// player doesn't have GoodFences or has a different version.
        /// </summary>
        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            var peerMod = e.Peer.GetMod(this.ModManifest.UniqueID);

            if (peerMod == null)
            {
                this.Monitor.Log(
                    $"Player connected without GoodFences installed. They will not have ownership tagging.",
                    LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage(
                    "Warning: A player joined without GoodFences!",
                    HUDMessage.error_type));
            }
            else if (!peerMod.Version.Equals(this.ModManifest.Version))
            {
                this.Monitor.Log(
                    $"Player connected with GoodFences version {peerMod.Version} (host: {this.ModManifest.Version})",
                    LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage(
                    $"Warning: Player has GoodFences v{peerMod.Version}, host has v{this.ModManifest.Version}",
                    HUDMessage.error_type));
            }
            else
            {
                this.Monitor.Log(
                    $"Player connected with matching GoodFences v{peerMod.Version}.",
                    LogLevel.Info);
            }
        }
    }
}
