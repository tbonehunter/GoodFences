// Handlers/ObjectPlacementHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using GoodFences.Models;
using System.Collections.Generic;
using System.Linq;
using Object = StardewValley.Object;

namespace GoodFences.Handlers
{
    /// <summary>Handles placement and maintenance of per-quadrant shipping bins and common chests.</summary>
    public class ObjectPlacementHandler
    {
        /*********
        ** Constants
        *********/
        /// <summary>Mod data key to identify GoodFences-placed objects.</summary>
        private const string ModDataKey = "GoodFences.PlacedObject";

        /// <summary>Item ID for Mini-Shipping Bin (BigCraftable).</summary>
        private const string MiniShippingBinId = "248";

        /// <summary>Item ID for Chest (BigCraftable).</summary>
        private const string ChestId = "130";

        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly QuadrantManager QuadrantManager;

        /*********
        ** Constructor
        *********/
        public ObjectPlacementHandler(IMonitor monitor, QuadrantManager quadrantManager)
        {
            this.Monitor = monitor;
            this.QuadrantManager = quadrantManager;
        }

        /*********
        ** Public Methods
        *********/
        /// <summary>Ensure all required objects are placed. Called on DayStarted.</summary>
        public void EnsureObjectsPlaced()
        {
            // Only host manages object placement
            if (!Context.IsMainPlayer)
                return;

            // Don't place objects until mode is locked
            if (this.QuadrantManager.SaveData?.ModeLocked != true)
            {
                this.Monitor.Log("[OBJECTS] Mode not locked yet, skipping object placement.", LogLevel.Debug);
                return;
            }

            var farm = Game1.getFarm();
            if (farm == null)
                return;

            this.Monitor.Log("[OBJECTS] Checking and placing objects...", LogLevel.Debug);

            // Reassign unoccupied cabin ownership to host
            this.ReassignUnoccupiedCabins(farm);

            // NE quadrant ALWAYS gets common infrastructure (it's always shared)
            this.PlaceNECommonObjects(farm);

            // Place shipping bins and common chests for each occupied private quadrant
            foreach (var quadrant in new[] { Quadrant.NW, Quadrant.SW, Quadrant.SE })
            {
                // Only place objects in occupied (non-shared) quadrants
                if (this.QuadrantManager.IsQuadrantShared(quadrant))
                {
                    this.Monitor.Log($"[OBJECTS] Skipping {quadrant} - shared/unoccupied.", LogLevel.Debug);
                    continue;
                }

                // Place shipping bin
                if (QuadrantCoordinates.ShippingBins.TryGetValue(quadrant, out var binPos))
                {
                    this.EnsureShippingBin(farm, binPos, quadrant);
                }

                // Place common chest
                if (QuadrantCoordinates.CommonChests.TryGetValue(quadrant, out var chestPos))
                {
                    this.EnsureCommonChest(farm, chestPos, quadrant);
                }
            }
        }

        /// <summary>Reassign unoccupied cabins to the host so all players can enter shared quadrants.</summary>
        private void ReassignUnoccupiedCabins(Farm farm)
        {
            // Map quadrants to their cabin types
            var cabinQuadrants = new Dictionary<string, Quadrant>
            {
                { "Log Cabin", Quadrant.SW },
                { "Stone Cabin", Quadrant.SE },
                { "Plank Cabin", Quadrant.NW }
            };

            foreach (var building in farm.buildings)
            {
                // Check if this is a cabin
                if (!building.buildingType.Value.Contains("Cabin"))
                    continue;

                // Determine which quadrant this cabin is in
                Quadrant? cabinQuadrant = null;
                foreach (var kvp in cabinQuadrants)
                {
                    if (building.buildingType.Value.Contains(kvp.Key.Split(' ')[0])) // Match "Log", "Stone", "Plank"
                    {
                        cabinQuadrant = kvp.Value;
                        break;
                    }
                }

                if (!cabinQuadrant.HasValue)
                    continue;

                // Check if this quadrant is shared (unoccupied)
                if (this.QuadrantManager.IsQuadrantShared(cabinQuadrant.Value))
                {
                    // Get current owner
                    long currentOwner = building.owner.Value;
                    long hostId = Game1.MasterPlayer.UniqueMultiplayerID;

                    // Check if the current owner is an actual online player
                    bool ownerIsOnline = Game1.getOnlineFarmers().Any(f => f.UniqueMultiplayerID == currentOwner);

                    if (!ownerIsOnline && currentOwner != hostId)
                    {
                        // Reassign to host so all players can enter
                        building.owner.Value = hostId;
                        this.Monitor.Log($"[OBJECTS] Reassigned {building.buildingType.Value} in {cabinQuadrant.Value} to host (was owned by offline player {currentOwner})", LogLevel.Info);
                    }
                }
            }
        }

        /// <summary>Place common objects in NE quadrant (always shared).</summary>
        private void PlaceNECommonObjects(Farm farm)
        {
            // Place common shipping bin in NE
            if (QuadrantCoordinates.ShippingBins.TryGetValue(Quadrant.NE, out var binPos))
            {
                this.EnsureShippingBin(farm, binPos, Quadrant.NE);
            }

            // Place common chest in NE
            if (QuadrantCoordinates.CommonChests.TryGetValue(Quadrant.NE, out var chestPos))
            {
                this.EnsureCommonChest(farm, chestPos, Quadrant.NE);
            }
        }

        /// <summary>Get all GoodFences common chests on the farm.</summary>
        public IEnumerable<Chest> GetCommonChests()
        {
            var farm = Game1.getFarm();
            if (farm == null)
                yield break;

            foreach (var obj in farm.Objects.Values)
            {
                if (obj is Chest chest && 
                    chest.modData.TryGetValue(ModDataKey, out var data) && 
                    data.StartsWith("CommonChest"))
                {
                    yield return chest;
                }
            }
        }

        /*********
        ** Private Methods
        *********/
        /// <summary>Ensure a shipping bin exists at the specified position.</summary>
        private void EnsureShippingBin(Farm farm, Vector2 position, Quadrant quadrant)
        {
            // Check if our marked object already exists
            if (this.HasMarkedObject(farm, position, $"ShippingBin_{quadrant}"))
            {
                this.Monitor.Log($"[OBJECTS] Shipping bin already exists at {quadrant} ({position})", LogLevel.Trace);
                return;
            }

            // Clear any debris/objects at the position
            this.ClearTile(farm, position);

            // Place new Mini-Shipping Bin as BigCraftable
            // In SDV 1.6, BigCraftables use "(BC)" prefix with ItemRegistry
            var bin = ItemRegistry.Create<Object>($"(BC){MiniShippingBinId}");
            bin.TileLocation = position;
            bin.modData[ModDataKey] = $"ShippingBin_{quadrant}";
            
            // Use placementAction to properly initialize the object on the farm
            // This mimics what happens when a player places the item
            int x = (int)position.X * 64;
            int y = (int)position.Y * 64;
            bin.placementAction(farm, x, y, Game1.player);
            
            // Verify placement
            if (farm.Objects.ContainsKey(position))
            {
                // Re-apply modData since placementAction may have created a new instance
                if (farm.Objects.TryGetValue(position, out var placedObj))
                {
                    placedObj.modData[ModDataKey] = $"ShippingBin_{quadrant}";
                }
                this.Monitor.Log($"[OBJECTS] Placed shipping bin at {quadrant} ({position})", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"[OBJECTS] Failed to place shipping bin at {quadrant} ({position}) - placementAction failed", LogLevel.Warn);
            }
        }

        /// <summary>Ensure a common chest exists at the specified position.</summary>
        private void EnsureCommonChest(Farm farm, Vector2 position, Quadrant quadrant)
        {
            // Check if our marked object already exists
            if (this.HasMarkedObject(farm, position, $"CommonChest_{quadrant}"))
            {
                this.Monitor.Log($"[OBJECTS] Common chest already exists at {quadrant} ({position})", LogLevel.Trace);
                return;
            }

            // Clear any debris/objects at the position
            this.ClearTile(farm, position);

            // Place new Chest
            var chest = new Chest(true, position, ChestId)
            {
                Name = $"Common Chest ({quadrant})"
            };
            chest.modData[ModDataKey] = $"CommonChest_{quadrant}";
            chest.modData["GoodFences.Owner"] = "Common"; // Mark as common for ownership system
            
            if (farm.Objects.TryAdd(position, chest))
            {
                this.Monitor.Log($"[OBJECTS] Placed common chest at {quadrant} ({position})", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"[OBJECTS] Failed to place common chest at {quadrant} ({position}) - tile occupied", LogLevel.Warn);
            }
        }

        /// <summary>Check if our marked object exists at a position.</summary>
        private bool HasMarkedObject(Farm farm, Vector2 position, string markerValue)
        {
            if (farm.Objects.TryGetValue(position, out var obj))
            {
                return obj.modData.TryGetValue(ModDataKey, out var data) && data == markerValue;
            }
            return false;
        }

        /// <summary>Clear debris and objects from a tile to make room for placement.</summary>
        private void ClearTile(Farm farm, Vector2 position)
        {
            // Remove any existing object (unless it's our marked object)
            if (farm.Objects.TryGetValue(position, out var existingObj))
            {
                if (!existingObj.modData.ContainsKey(ModDataKey))
                {
                    farm.Objects.Remove(position);
                    this.Monitor.Log($"[OBJECTS] Cleared existing object at {position}", LogLevel.Debug);
                }
            }

            // Remove terrain features (grass, debris)
            if (farm.terrainFeatures.ContainsKey(position))
            {
                farm.terrainFeatures.Remove(position);
                this.Monitor.Log($"[OBJECTS] Cleared terrain feature at {position}", LogLevel.Debug);
            }

            // Remove debris (stones, twigs, weeds)
            var debrisToRemove = farm.debris.Where(d => 
                d.Chunks.Any(c => new Vector2((int)(c.position.X / 64), (int)(c.position.Y / 64)) == position)).ToList();
            foreach (var d in debrisToRemove)
            {
                farm.debris.Remove(d);
            }
        }
    }
}
