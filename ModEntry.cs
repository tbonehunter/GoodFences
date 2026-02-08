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

        /// <summary>The mode selection dialog handler.</summary>
        private ModeSelectionHandler? ModeSelectionHandler;

        /// <summary>Mod configuration options.</summary>
        internal static ModConfig? Config;

        /// <summary>Static reference to the mod's monitor for logging.</summary>
        internal static IMonitor? StaticMonitor;

        /// <summary>Static reference to the mod helper.</summary>
        internal static IModHelper? StaticHelper;

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
            this.ModeSelectionHandler = new ModeSelectionHandler(this.Monitor, this.QuadrantManager, this.OnModeSelected);

            // Register event handlers
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;

            this.Monitor.Log("GoodFences loaded successfully.", LogLevel.Alert);
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
            // Only apply on Four Corners farm
            if (!this.IsFourCornersFarm())
            {
                this.Monitor.Log("GoodFences only works on Four Corners farm. Mod disabled for this save.", LogLevel.Info);
                return;
            }

            // Load save data (host only has meaningful data)
            this.QuadrantManager?.LoadSaveData();

            // Only host initializes quadrants on save load
            // Farmhands will initialize when they receive the sync message
            if (Context.IsMainPlayer)
            {
                this.QuadrantManager?.InitializeQuadrants(Game1.numberOfPlayers());
                this.Monitor.Log($"GoodFences activated for {Game1.numberOfPlayers()} player(s).", LogLevel.Info);

                // Check if mode selection is pending (multiplayer with unlocked mode)
                this.ModeSelectionHandler?.CheckForPendingSelection();
            }
            else
            {
                this.Monitor.Log($"GoodFences loaded on farmhand. Waiting for host sync...", LogLevel.Alert);
            }
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

            // Remind host if mode is still unlocked with multiple players
            if (this.QuadrantManager?.SaveData.ModeLocked == false && Game1.numberOfPlayers() > 1)
            {
                Game1.addHUDMessage(new HUDMessage(
                    "GoodFences: Roster not locked. New players can join.",
                    HUDMessage.newQuest_type));
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

        /// <summary>Raised when a peer connects in multiplayer.</summary>
        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            if (!this.IsFourCornersFarm())
                return;

            // Version check
            var peerMod = e.Peer.GetMod(this.ModManifest.UniqueID);
            if (peerMod == null)
            {
                this.Monitor.Log($"Player connected without GoodFences installed. They may experience desync.", LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage("Warning: Player joined without GoodFences mod!", HUDMessage.error_type));
            }
            else if (peerMod.Version.IsOlderThan(this.ModManifest.Version))
            {
                this.Monitor.Log($"Player connected with older GoodFences version ({peerMod.Version}). Current: {this.ModManifest.Version}", LogLevel.Warn);
            }

            // Check if roster is locked
            if (this.QuadrantManager?.SaveData.ModeLocked == true)
            {
                int lockedCount = this.QuadrantManager.SaveData.LockedPlayerCount;
                if (Game1.numberOfPlayers() > lockedCount)
                {
                    this.Monitor.Log($"Player tried to join but roster is locked at {lockedCount} players.", LogLevel.Warn);
                    Game1.addHUDMessage(new HUDMessage($"Warning: Roster locked at {lockedCount} players. New player may cause issues!", HUDMessage.error_type));
                }
                
                // Recalculate quadrant assignments
                this.QuadrantManager.InitializeQuadrants(Game1.numberOfPlayers());
                
                // Broadcast current state to the new farmhand
                this.Monitor.Log($"[SYNC] Broadcasting locked state to new farmhand: Mode={this.QuadrantManager.SaveData.HostMode}", LogLevel.Alert);
                this.Helper.Multiplayer.SendMessage(
                    new ModeSelectedMessage { 
                        Mode = this.QuadrantManager.SaveData.HostMode, 
                        PlayerCount = this.QuadrantManager.SaveData.LockedPlayerCount 
                    },
                    "ModeSelected",
                    modIDs: new[] { this.ModManifest.UniqueID },
                    playerIDs: new[] { e.Peer.PlayerID }
                );
                return;
            }

            // Roster not locked - show mode selection dialog to host
            // Get the farmhand name (we need to wait a tick for player data to sync)
            DelayedAction.functionAfterDelay(() =>
            {
                string farmhandName = "A farmhand";
                foreach (var farmer in Game1.getAllFarmers())
                {
                    if (!farmer.IsMainPlayer && farmer.UniqueMultiplayerID == e.Peer.PlayerID)
                    {
                        farmhandName = farmer.Name;
                        break;
                    }
                }
                
                this.ModeSelectionHandler?.OnFarmhandJoined(farmhandName);
            }, 500); // Small delay to let player data sync
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
                this.Monitor.Log($"[SYNC] Received ModeSelected from host: Mode={data.Mode}, Players={data.PlayerCount}", LogLevel.Alert);
                
                if (this.QuadrantManager != null)
                {
                    this.QuadrantManager.SaveData.HostMode = data.Mode;
                    this.QuadrantManager.SaveData.ModeLocked = true;
                    this.QuadrantManager.SaveData.LockedPlayerCount = data.PlayerCount;
                    this.Monitor.Log($"[SYNC] SaveData updated. Calling InitializeQuadrants...", LogLevel.Alert);
                    this.QuadrantManager.InitializeQuadrants(Game1.numberOfPlayers());
                }
                
                string modeText = data.Mode == HostMode.Private ? "Private" : "Landlord";
                Game1.addHUDMessage(new HUDMessage(
                    $"Host locked roster: {modeText} mode with {data.PlayerCount} players.",
                    HUDMessage.newQuest_type));
                
                this.Monitor.Log($"Received mode selection: {data.Mode} with {data.PlayerCount} players", LogLevel.Debug);
            }
        }

        /// <summary>Called when host selects and locks a mode via dialog.</summary>
        private void OnModeSelected(HostMode mode, int playerCount)
        {
            this.Monitor.Log($"[MODE] ===== OnModeSelected callback fired =====", LogLevel.Alert);
            this.Monitor.Log($"[MODE] Mode={mode}, PlayerCount={playerCount}", LogLevel.Alert);
            
            this.QuadrantManager?.LockMode(mode, playerCount);
            this.Monitor.Log($"[MODE] LockMode completed. Calling InitializeQuadrants...", LogLevel.Alert);
            
            this.QuadrantManager?.InitializeQuadrants(playerCount);

            // Broadcast to farmhands
            if (Context.IsMultiplayer)
            {
                this.Monitor.Log($"[MODE] Broadcasting ModeSelected to farmhands", LogLevel.Info);
                this.Helper.Multiplayer.SendMessage(
                    new ModeSelectedMessage { Mode = mode, PlayerCount = playerCount },
                    "ModeSelected",
                    modIDs: new[] { this.ModManifest.UniqueID }
                );
            }

            this.Monitor.Log($"[MODE] Mode lock complete: {mode} with {playerCount} players", LogLevel.Info);
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
