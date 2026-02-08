// ModEntry.cs
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using GoodFences.Models;
using GoodFences.Handlers;
using System.Collections.Generic;

namespace GoodFences
{
    /// <summary>
    /// GoodFences - A Stardew Valley mod that divides the Four Corners farm
    /// so each player owns their own quadrant with private farmland.
    /// </summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        /// <summary>The quadrant data manager containing all boundary definitions.</summary>
        private QuadrantManager? QuadrantManager;

        /// <summary>The boundary enforcement handler.</summary>
        private BoundaryHandler? BoundaryHandler;

        /// <summary>The shipping revenue handler for landlord mode.</summary>
        private ShippingHandler? ShippingHandler;

        /// <summary>Mod configuration options.</summary>
        internal static ModConfig? Config;

        /// <summary>Static reference to the mod's monitor for logging.</summary>
        internal static IMonitor? StaticMonitor;

        /// <summary>Static reference to the mod helper.</summary>
        internal static IModHelper? StaticHelper;

        /// <summary>Whether we've shown the mode selection prompt this session.</summary>
        private bool HasPromptedModeSelection = false;

        /*********
        ** Public Methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            StaticMonitor = this.Monitor;
            StaticHelper = helper;
            Config = helper.ReadConfig<ModConfig>();

            // Initialize managers
            this.QuadrantManager = new QuadrantManager(this.Monitor, helper);
            this.BoundaryHandler = new BoundaryHandler(this.Monitor, this.QuadrantManager);
            this.ShippingHandler = new ShippingHandler(this.Monitor, this.QuadrantManager);

            // Register event handlers
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            this.Monitor.Log("GoodFences loaded successfully.", LogLevel.Info);
        }

        /*********
        ** Private Methods
        *********/
        /// <summary>Raised after the game is launched, right before the first update tick.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Future: Register Generic Mod Config Menu integration here
        }

        /// <summary>Raised after a save is loaded.</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.HasPromptedModeSelection = false;

            // Only apply on Four Corners farm
            if (!this.IsFourCornersFarm())
            {
                this.Monitor.Log("GoodFences only works on Four Corners farm. Mod disabled for this save.", LogLevel.Info);
                return;
            }

            // Load save data
            this.QuadrantManager?.LoadSaveData();

            // Initialize quadrant assignments based on player count
            this.QuadrantManager?.InitializeQuadrants(Game1.numberOfPlayers());
            this.Monitor.Log($"GoodFences activated for {Game1.numberOfPlayers()} player(s).", LogLevel.Info);
        }

        /// <summary>Raised before the game saves.</summary>
        private void OnSaving(object? sender, SavingEventArgs e)
        {
            // Save data is written when mode is locked, but ensure it's saved
            this.QuadrantManager?.WriteSaveData();
        }

        /// <summary>Raised when a new day starts.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!this.IsFourCornersFarm() || !Context.IsMainPlayer)
                return;

            // Check if we need to prompt for mode selection (Day 1, not yet locked)
            if (this.QuadrantManager?.ShouldPromptModeSelection() == true && !this.HasPromptedModeSelection)
            {
                this.PromptModeSelection();
                this.HasPromptedModeSelection = true;
            }
        }

        /// <summary>Raised when the day is ending (before save).</summary>
        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (!this.IsFourCornersFarm())
                return;

            // Apply landlord cut to shipping revenue
            this.ShippingHandler?.ProcessDailyShipping();
        }

        /// <summary>Raised after the game state is updated (60 times per second).</summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Only check boundaries every 4 ticks (15 times per second) for performance
            if (!Context.IsWorldReady || e.Ticks % 4 != 0)
                return;

            // Only apply on Four Corners farm
            if (!this.IsFourCornersFarm())
                return;

            // Check and enforce boundaries
            this.BoundaryHandler?.EnforceBoundaries();
        }

        /// <summary>Raised when a button is pressed.</summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Future: Handle mode selection UI interactions
        }

        /// <summary>Raised when a peer connects in multiplayer.</summary>
        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            // Version check
            var peerMod = e.Peer.GetMod(this.ModManifest.UniqueID);
            if (peerMod == null)
            {
                this.Monitor.Log($"Player connected without GoodFences installed. They may experience desync.", LogLevel.Warn);
            }
            else if (peerMod.Version.IsOlderThan(this.ModManifest.Version))
            {
                this.Monitor.Log($"Player connected with older GoodFences version ({peerMod.Version}). Current: {this.ModManifest.Version}", LogLevel.Warn);
            }

            // Check if player can join (roster locked)
            if (this.QuadrantManager?.CanPlayerJoin() == false)
            {
                this.Monitor.Log($"Player tried to join but roster is locked at {this.QuadrantManager.SaveData.LockedPlayerCount} players.", LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage("This farm's roster is locked. New players cannot join.", HUDMessage.error_type));
                // Note: SMAPI doesn't provide a clean way to kick players, but the warning will alert the host
            }

            // Recalculate quadrant assignments based on new player count
            this.QuadrantManager?.InitializeQuadrants(Game1.numberOfPlayers());
        }

        /// <summary>Raised when a mod message is received from another player.</summary>
        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            // Handle sync messages for mode selection, etc.
            if (e.FromModID != this.ModManifest.UniqueID)
                return;

            if (e.Type == "ModeSelected")
            {
                // Sync mode selection to farmhands
                var data = e.ReadAs<ModeSelectedMessage>();
                if (this.QuadrantManager != null)
                {
                    this.QuadrantManager.SaveData.HostMode = data.Mode;
                    this.QuadrantManager.SaveData.ModeLocked = true;
                    this.QuadrantManager.SaveData.LockedPlayerCount = data.PlayerCount;
                    this.QuadrantManager.InitializeQuadrants(Game1.numberOfPlayers());
                }
                this.Monitor.Log($"Received mode selection: {data.Mode} with {data.PlayerCount} players", LogLevel.Debug);
            }
        }

        /// <summary>Prompt the host to select a mode on Day 1.</summary>
        private void PromptModeSelection()
        {
            int playerCount = Game1.numberOfPlayers();
            var availableModes = this.QuadrantManager?.GetAvailableModes(playerCount) ?? new List<HostMode>();

            this.Monitor.Log($"Prompting mode selection for {playerCount} players. Available: {string.Join(", ", availableModes)}", LogLevel.Debug);

            // For 4 players, auto-select Landlord
            if (playerCount >= 4)
            {
                this.SelectMode(HostMode.Landlord, playerCount);
                Game1.addHUDMessage(new HUDMessage("GoodFences: Landlord mode activated (4 players).", HUDMessage.newQuest_type));
                return;
            }

            // For 2-3 players, show selection dialog
            // Using a simple approach: default to Private, player can change via config
            // A proper UI would use Generic Mod Config Menu or custom dialog
            
            // For now, check config for preferred mode, or default to Private
            var preferredMode = HostMode.Private;
            if (availableModes.Contains(preferredMode))
            {
                this.SelectMode(preferredMode, playerCount);
                Game1.addHUDMessage(new HUDMessage($"GoodFences: {preferredMode} mode activated. Host owns NW quadrant.", HUDMessage.newQuest_type));
            }
            else
            {
                this.SelectMode(HostMode.Landlord, playerCount);
                Game1.addHUDMessage(new HUDMessage("GoodFences: Landlord mode activated.", HUDMessage.newQuest_type));
            }
        }

        /// <summary>Lock in the selected mode.</summary>
        private void SelectMode(HostMode mode, int playerCount)
        {
            this.QuadrantManager?.LockMode(mode, playerCount);
            this.QuadrantManager?.InitializeQuadrants(playerCount);

            // Broadcast to farmhands
            if (Context.IsMultiplayer)
            {
                this.Helper.Multiplayer.SendMessage(
                    new ModeSelectedMessage { Mode = mode, PlayerCount = playerCount },
                    "ModeSelected",
                    modIDs: new[] { this.ModManifest.UniqueID }
                );
            }

            this.Monitor.Log($"Mode locked: {mode} with {playerCount} players", LogLevel.Info);
        }

        /// <summary>Check if the current farm is Four Corners.</summary>
        private bool IsFourCornersFarm()
        {
            return Game1.whichFarm == Farm.fourCorners_layout;
        }
    }

    /// <summary>Message for syncing mode selection to farmhands.</summary>
    public class ModeSelectedMessage
    {
        public HostMode Mode { get; set; }
        public int PlayerCount { get; set; }
    }
}
