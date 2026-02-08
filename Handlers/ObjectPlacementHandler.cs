// Handlers/ObjectPlacementHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using GoodFences.Models;
using System.Collections.Generic;
using System.Linq;

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

            // Place shipping bins and common chests for each occupied quadrant
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

            // Place new Mini-Shipping Bin
            var bin = ItemRegistry.Create<StardewValley.Object>($"(BC){MiniShippingBinId}");
            bin.modData[ModDataKey] = $"ShippingBin_{quadrant}";
            
            if (farm.Objects.TryAdd(position, bin))
            {
                this.Monitor.Log($"[OBJECTS] Placed shipping bin at {quadrant} ({position})", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"[OBJECTS] Failed to place shipping bin at {quadrant} ({position}) - tile occupied", LogLevel.Warn);
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
