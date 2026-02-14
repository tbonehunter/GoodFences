// ModEntry.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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
    /// Step 1: Passive tagging only. Tags all items and objects with
    /// player IDs for visual verification. No enforcement.
    /// </summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
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
        internal static ModConfig? Config;

        /// <summary>Static reference to the monitor for logging.</summary>
        internal static IMonitor? StaticMonitor;

        /*********
        ** Entry Point
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        public override void Entry(IModHelper helper)
        {
            StaticMonitor = this.Monitor;
            Config = helper.ReadConfig<ModConfig>();

            // Initialize the tagging handler
            this.TagHandler = new OwnershipTagHandler(
                this.Monitor,
                Config.DebugMode
            );

            // Apply Harmony patches for ownership tagging
            var harmony = new Harmony(this.ModManifest.UniqueID);
            OwnershipTagPatches.Initialize(this.Monitor, this.TagHandler);
            OwnershipTagPatches.Apply(harmony);

            // Register SMAPI events
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Player.InventoryChanged += this.OnInventoryChanged;
            helper.Events.World.TerrainFeatureListChanged += this.OnTerrainFeatureListChanged;
            helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;

            this.Monitor.Log("GoodFences v2 loaded — passive tagging active.", LogLevel.Info);
        }

        /*********
        ** Event Handlers
        *********/
        /// <summary>Raised after the game is launched.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Future: Register Generic Mod Config Menu integration
        }

        /// <summary>Raised after a save is loaded.</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.DayFullyStarted = false;
            this.TicksSinceDayStart = 0;
            if (!Context.IsMultiplayer)
            {
                this.Monitor.Log("Single-player game detected. GoodFences tagging is active but has no gameplay effect.", LogLevel.Info);
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
        }

        /// <summary>Raised when a new day starts. Reset the terrain feature flag.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.DayFullyStarted = false;
            this.TicksSinceDayStart = 0;
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
