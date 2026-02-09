// Models/QuadrantManager.cs
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using System.Collections.Generic;
using System.Linq;

namespace GoodFences.Models
{
    /// <summary>Manages quadrant ownership assignments and shared zone calculations.</summary>
    public class QuadrantManager
    {
        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly IModHelper Helper;

        /// <summary>Maps player IDs to their assigned quadrant.</summary>
        private readonly Dictionary<long, Quadrant> PlayerQuadrants = new();

        /// <summary>Quadrants that are currently shared (NE is always shared).</summary>
        private readonly HashSet<Quadrant> SharedQuadrants = new() { Quadrant.NE };

        /// <summary>Per-save data for this game.</summary>
        public SaveData SaveData { get; private set; } = new();

        /// <summary>Whether this QuadrantManager has been properly initialized (farmhands need sync from host).</summary>
        public bool IsInitialized { get; private set; } = false;

        /*********
        ** Constructor
        *********/
        public QuadrantManager(IMonitor monitor, IModHelper helper)
        {
            this.Monitor = monitor;
            this.Helper = helper;
        }

        /*********
        ** Public Methods - Save Data
        *********/
        /// <summary>Load save data for this game.</summary>
        public void LoadSaveData()
        {
            if (!Context.IsMainPlayer)
                return;

            this.SaveData = this.Helper.Data.ReadSaveData<SaveData>("GoodFences.SaveData") ?? new SaveData();
            this.Monitor.Log($"Loaded save data: Mode={this.SaveData.HostMode}, Locked={this.SaveData.ModeLocked}, Players={this.SaveData.LockedPlayerCount}", LogLevel.Debug);
        }

        /// <summary>Save the current save data.</summary>
        public void WriteSaveData()
        {
            if (!Context.IsMainPlayer)
                return;

            this.Helper.Data.WriteSaveData("GoodFences.SaveData", this.SaveData);
            this.Monitor.Log($"Saved data: Mode={this.SaveData.HostMode}, Locked={this.SaveData.ModeLocked}, Players={this.SaveData.LockedPlayerCount}", LogLevel.Debug);
        }

        /// <summary>Lock the game mode with current settings.</summary>
        public void LockMode(HostMode mode, int playerCount)
        {
            this.SaveData.HostMode = mode;
            this.SaveData.ModeLocked = true;
            this.SaveData.LockedPlayerCount = playerCount;
            this.SaveData.LockDay = Game1.Date.TotalDays;
            this.WriteSaveData();

            this.Monitor.Log($"Mode locked: {mode} with {playerCount} players on day {Game1.Date.TotalDays}", LogLevel.Info);
        }

        /// <summary>Check if a new player can join based on locked player count.</summary>
        public bool CanPlayerJoin()
        {
            if (!this.SaveData.ModeLocked)
                return true;

            return Game1.numberOfPlayers() < this.SaveData.LockedPlayerCount;
        }

        /// <summary>Check if it's Day 1 and mode selection should be prompted.</summary>
        public bool ShouldPromptModeSelection()
        {
            return !this.SaveData.ModeLocked && Game1.Date.TotalDays == 0 && Context.IsMainPlayer;
        }

        /// <summary>Determine available host modes based on player count.</summary>
        public List<HostMode> GetAvailableModes(int playerCount)
        {
            var modes = new List<HostMode>();
            
            // Private mode only available with 2-3 players (need empty quadrant for host)
            if (playerCount <= 3)
                modes.Add(HostMode.Private);

            // Landlord always available
            modes.Add(HostMode.Landlord);

            return modes;
        }

        /*********
        ** Public Methods - Quadrant Management
        *********/
        /// <summary>Initialize quadrant assignments based on player count and host mode.</summary>
        /// <param name="playerCount">Total number of players in the game.</param>
        public void InitializeQuadrants(int playerCount)
        {
            this.Monitor.Log($"[INIT] ===== InitializeQuadrants called =====", LogLevel.Alert);
            this.Monitor.Log($"[INIT] SaveData state: Mode={this.SaveData.HostMode}, Locked={this.SaveData.ModeLocked}, LockedPlayerCount={this.SaveData.LockedPlayerCount}", LogLevel.Alert);
            this.Monitor.Log($"[INIT] Before clear - SharedQuadrants: [{string.Join(", ", this.SharedQuadrants)}]", LogLevel.Debug);
            this.Monitor.Log($"[INIT] Before clear - PlayerQuadrants: [{string.Join(", ", this.PlayerQuadrants.Select(kv => $"{kv.Key}={kv.Value}"))}]", LogLevel.Debug);
            
            this.SharedQuadrants.Clear();
            this.PlayerQuadrants.Clear(); // Also clear player assignments to rebuild
            this.SharedQuadrants.Add(Quadrant.NE); // Always shared

            var hostMode = this.SaveData.HostMode;
            this.Monitor.Log($"[INIT] Initializing quadrants: {playerCount} players, {hostMode} mode", LogLevel.Info);

            // Assign farmhands to their cabin quadrants first
            this.AssignFarmhandsToQuadrants();

            // Handle host assignment based on mode
            if (hostMode == HostMode.Private)
            {
                this.AssignHostPrivateQuadrant();
            }
            else
            {
                // Landlord mode: host has no private quadrant
                // All unoccupied quadrants become shared
                this.MarkUnoccupiedQuadrantsAsShared();
                this.Monitor.Log($"Landlord mode: Host works NE quadrant, receives {ModEntry.Config?.LandlordCutPercent ?? 10}% cut from farmhands.", LogLevel.Debug);
            }

            // Log final state
            this.Monitor.Log($"[INIT] ===== Final State =====", LogLevel.Alert);
            this.Monitor.Log($"[INIT] Shared quadrants: [{string.Join(", ", this.SharedQuadrants)}]", LogLevel.Alert);
            this.Monitor.Log($"[INIT] Player assignments:", LogLevel.Alert);
            foreach (var kvp in this.PlayerQuadrants)
            {
                var farmer = Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == kvp.Key);
                this.Monitor.Log($"[INIT]   - {farmer?.Name ?? kvp.Key.ToString()} owns {kvp.Value}", LogLevel.Alert);
            }
            if (this.PlayerQuadrants.Count == 0)
                this.Monitor.Log($"[INIT]   (no player quadrant assignments)", LogLevel.Alert);
            
            this.IsInitialized = true;
            this.Monitor.Log($"[INIT] QuadrantManager marked as initialized", LogLevel.Alert);
        }

        /// <summary>Assign farmhands to quadrants based on their cabin location.</summary>
        private void AssignFarmhandsToQuadrants()
        {
            // Use getOnlineFarmers() to avoid assigning ghost farmers from previous sessions
            var onlineFarmers = Game1.getOnlineFarmers();
            this.Monitor.Log($"[INIT] Online farmers: [{string.Join(", ", onlineFarmers.Select(f => f.Name))}]", LogLevel.Debug);
            
            foreach (var farmer in onlineFarmers)
            {
                if (farmer.IsMainPlayer)
                    continue;

                var cabin = this.GetFarmerCabin(farmer);
                if (cabin is not null)
                {
                    var quadrant = this.DetermineQuadrantFromCabinPosition(cabin);
                    if (quadrant.HasValue)
                    {
                        this.PlayerQuadrants[farmer.UniqueMultiplayerID] = quadrant.Value;
                        this.SharedQuadrants.Remove(quadrant.Value); // This quadrant is now private
                        this.Monitor.Log($"Farmhand {farmer.Name} assigned to {quadrant.Value} quadrant.", LogLevel.Debug);
                    }
                }
            }
        }

        /// <summary>Assign host to NW quadrant in Private mode.</summary>
        private void AssignHostPrivateQuadrant()
        {
            if (Game1.MasterPlayer == null)
                return;

            // Host gets NW quadrant in Private mode
            this.PlayerQuadrants[Game1.MasterPlayer.UniqueMultiplayerID] = Quadrant.NW;
            this.SharedQuadrants.Remove(Quadrant.NW);
            this.Monitor.Log($"Host ({Game1.MasterPlayer.Name}) assigned to NW quadrant (Private mode).", LogLevel.Debug);

            // Mark remaining unoccupied quadrants as shared
            this.MarkUnoccupiedQuadrantsAsShared();
        }

        /// <summary>Mark any unoccupied quadrants as shared.</summary>
        private void MarkUnoccupiedQuadrantsAsShared()
        {
            var allPrivateQuadrants = new[] { Quadrant.NW, Quadrant.SW, Quadrant.SE };
            foreach (var quadrant in allPrivateQuadrants)
            {
                if (!this.PlayerQuadrants.ContainsValue(quadrant))
                {
                    this.SharedQuadrants.Add(quadrant);
                    this.Monitor.Log($"Quadrant {quadrant} is unoccupied, marked as shared.", LogLevel.Debug);
                }
            }
        }

        /// <summary>Get the cabin building for a farmer.</summary>
        private Building? GetFarmerCabin(Farmer farmer)
        {
            var farm = Game1.getFarm();
            if (farm == null)
                return null;

            return farm.buildings.FirstOrDefault(b =>
                b.isCabin && b.indoors.Value is Cabin cabin &&
                cabin.owner?.UniqueMultiplayerID == farmer.UniqueMultiplayerID);
        }

        /// <summary>Determine which quadrant a cabin is in based on its position.</summary>
        private Quadrant? DetermineQuadrantFromCabinPosition(Building cabin)
        {
            var pos = cabin.tileX.Value;
            var posY = cabin.tileY.Value;

            // Based on documented cabin coordinates:
            // Plank (NW): x17, y8
            // Log (SW): x17, y43
            // Stone (SE): x59, y43

            // Use Y coordinate to determine north vs south
            // Use X coordinate to determine east vs west
            bool isNorth = posY < 30;
            bool isWest = pos < 40;

            if (isNorth && isWest) return Quadrant.NW;
            if (!isNorth && isWest) return Quadrant.SW;
            if (!isNorth && !isWest) return Quadrant.SE;

            // NE is farmhouse, not cabin
            return null;
        }

        /// <summary>Check if a player owns a specific quadrant.</summary>
        public bool PlayerOwnsQuadrant(Farmer farmer, Quadrant quadrant)
        {
            // NE is always shared - everyone can access
            if (quadrant == Quadrant.NE)
                return true;

            // Shared quadrants are accessible by everyone
            if (this.SharedQuadrants.Contains(quadrant))
            {
                return true;
            }

            // Check if this player is assigned to this quadrant
            if (this.PlayerQuadrants.TryGetValue(farmer.UniqueMultiplayerID, out var assignedQuadrant))
            {
                return assignedQuadrant == quadrant;
            }

            // Log denial with full state (only once per check, caller handles spam prevention)
            this.Monitor.Log($"[ACCESS-DENIED] {farmer.Name} denied access to {quadrant}. Mode={this.SaveData.HostMode}, Locked={this.SaveData.ModeLocked}, Shared=[{string.Join(", ", this.SharedQuadrants)}], Assigned=[{string.Join(", ", this.PlayerQuadrants.Select(kv => $"{kv.Key}:{kv.Value}"))}]", LogLevel.Debug);
            return false;
        }

        /// <summary>Get the quadrant for a specific player, if assigned.</summary>
        public Quadrant? GetPlayerQuadrant(Farmer farmer)
        {
            if (this.PlayerQuadrants.TryGetValue(farmer.UniqueMultiplayerID, out var quadrant))
                return quadrant;

            // In Landlord mode, host has no private quadrant
            if (farmer.IsMainPlayer && this.SaveData.HostMode == HostMode.Landlord)
                return null;

            // In Private mode, host has NW
            if (farmer.IsMainPlayer && this.SaveData.HostMode == HostMode.Private)
                return Quadrant.NW;

            return null;
        }

        /// <summary>Get the owner's player ID for a specific quadrant.</summary>
        /// <returns>The owner's UniqueMultiplayerID, or null if unoccupied/shared.</returns>
        public long? GetQuadrantOwnerID(Quadrant quadrant)
        {
            // NE in Landlord mode belongs to host (for mini-bin purposes)
            if (quadrant == Quadrant.NE && this.SaveData.HostMode == HostMode.Landlord)
                return Game1.MasterPlayer?.UniqueMultiplayerID;

            // Find player assigned to this quadrant
            foreach (var kvp in this.PlayerQuadrants)
            {
                if (kvp.Value == quadrant)
                    return kvp.Key;
            }

            return null;
        }

        /// <summary>Check if a quadrant is currently shared.</summary>
        public bool IsQuadrantShared(Quadrant quadrant)
        {
            return this.SharedQuadrants.Contains(quadrant);
        }

        /// <summary>Get all currently shared quadrants.</summary>
        public IReadOnlyCollection<Quadrant> GetSharedQuadrants()
        {
            return this.SharedQuadrants;
        }

        /// <summary>Check if we're in Landlord mode.</summary>
        public bool IsLandlordMode()
        {
            return this.SaveData.HostMode == HostMode.Landlord;
        }
    }
}
