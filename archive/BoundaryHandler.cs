// Handlers/BoundaryHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using GoodFences.Models;
using System.Collections.Generic;
using System.Linq;

namespace GoodFences.Handlers
{
    /// <summary>Handles boundary enforcement - blocking players from entering restricted quadrants.</summary>
    public class BoundaryHandler
    {
        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly QuadrantManager QuadrantManager;

        /// <summary>Cache of all passage tiles mapped to their target quadrant.</summary>
        private readonly Dictionary<Vector2, Quadrant> PassageTileMap = new();

        /// <summary>Track last valid position for each player to push them back if they enter restricted area.</summary>
        private readonly Dictionary<long, Vector2> LastValidPositions = new();

        /// <summary>Track players who have been warned recently to avoid spam.</summary>
        private readonly Dictionary<long, int> WarningCooldowns = new();

        private const int WarningCooldownTicks = 120; // 2 seconds at 60 ticks/second

        /*********
        ** Constructor
        *********/
        public BoundaryHandler(IMonitor monitor, QuadrantManager quadrantManager)
        {
            this.Monitor = monitor;
            this.QuadrantManager = quadrantManager;

            // Build the passage tile map once
            this.BuildPassageTileMap();
        }

        /*********
        ** Public Methods
        *********/
        /// <summary>Check and enforce boundaries for all players on the farm.</summary>
        public void EnforceBoundaries()
        {
            // Only enforce on the farm map
            if (Game1.currentLocation?.Name != "Farm")
                return;

            // Don't enforce until QuadrantManager is properly initialized
            // (farmhands need to wait for sync from host)
            if (!this.QuadrantManager.IsInitialized)
                return;

            // Process the current player
            var player = Game1.player;
            if (player == null)
                return;

            var playerTile = player.Tile;
            var playerId = player.UniqueMultiplayerID;

            // Decrement warning cooldowns
            this.UpdateCooldowns();

            // Check if player is on a passage tile
            if (this.PassageTileMap.TryGetValue(playerTile, out var targetQuadrant))
            {
                // Check if player is allowed in this quadrant
                if (!this.QuadrantManager.PlayerOwnsQuadrant(player, targetQuadrant))
                {
                    // Player is trying to enter a restricted quadrant
                    this.BlockPlayer(player, targetQuadrant);
                    return;
                }
            }

            // Update last valid position
            this.LastValidPositions[playerId] = playerTile;
        }

        /// <summary>Check if a specific tile is in a restricted area for a player.</summary>
        public bool IsTileRestricted(Farmer farmer, Vector2 tile)
        {
            if (this.PassageTileMap.TryGetValue(tile, out var targetQuadrant))
            {
                return !this.QuadrantManager.PlayerOwnsQuadrant(farmer, targetQuadrant);
            }
            return false;
        }

        /*********
        ** Private Methods
        *********/
        /// <summary>Build the lookup map of passage tiles to target quadrants.</summary>
        private void BuildPassageTileMap()
        {
            this.PassageTileMap.Clear();

            foreach (var passage in QuadrantCoordinates.GetAllPassages())
            {
                foreach (var tile in passage.Tiles)
                {
                    this.PassageTileMap[tile] = passage.TargetQuadrant;
                }
            }

            this.Monitor.Log($"Built passage tile map with {this.PassageTileMap.Count} tiles.", LogLevel.Debug);
        }

        /// <summary>Block a player from entering a restricted quadrant.</summary>
        private void BlockPlayer(Farmer player, Quadrant restrictedQuadrant)
        {
            var playerId = player.UniqueMultiplayerID;

            // Push player back to last valid position
            if (this.LastValidPositions.TryGetValue(playerId, out var lastValid))
            {
                player.Position = lastValid * 64f; // Convert tile to pixel position
            }

            // Show warning message (with cooldown to prevent spam)
            if (!this.WarningCooldowns.ContainsKey(playerId) || this.WarningCooldowns[playerId] <= 0)
            {
                var ownerName = this.GetQuadrantOwnerName(restrictedQuadrant);
                var message = ownerName != null
                    ? $"You cannot enter {ownerName}'s farm!"
                    : $"This area is restricted.";

                Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
                this.WarningCooldowns[playerId] = WarningCooldownTicks;

                // Always log blocks for debugging
                this.Monitor.Log($"[BLOCK] {player.Name} blocked from {restrictedQuadrant}. Owner={ownerName ?? "(none)"}. IsShared={this.QuadrantManager.IsQuadrantShared(restrictedQuadrant)}", LogLevel.Alert);
            }
        }

        /// <summary>Get the name of the player who owns a quadrant, if any.</summary>
        private string? GetQuadrantOwnerName(Quadrant quadrant)
        {
            foreach (var farmer in Game1.getAllFarmers())
            {
                if (this.QuadrantManager.GetPlayerQuadrant(farmer) == quadrant)
                {
                    return farmer.Name;
                }
            }
            return null;
        }

        /// <summary>Update warning cooldowns for all players.</summary>
        private void UpdateCooldowns()
        {
            var keysToUpdate = this.WarningCooldowns.Keys.ToList();
            foreach (var key in keysToUpdate)
            {
                if (this.WarningCooldowns[key] > 0)
                {
                    this.WarningCooldowns[key]--;
                }
            }
        }
    }
}
