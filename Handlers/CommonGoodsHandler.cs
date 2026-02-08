// Handlers/CommonGoodsHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using GoodFences.Models;
using System.Collections.Generic;
using System.Linq;

namespace GoodFences.Handlers
{
    /// <summary>Handles common goods tagging, channel restrictions, and sale splitting.</summary>
    public class CommonGoodsHandler
    {
        /*********
        ** Constants
        *********/
        /// <summary>Mod data key to identify common-tagged items.</summary>
        public const string CommonTagKey = "GoodFences.Common";

        /// <summary>Mod data key to identify GoodFences-placed objects.</summary>
        private const string PlacedObjectKey = "GoodFences.PlacedObject";

        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly IModHelper Helper;
        private readonly QuadrantManager QuadrantManager;

        /*********
        ** Constructor
        *********/
        public CommonGoodsHandler(IMonitor monitor, IModHelper helper, QuadrantManager quadrantManager)
        {
            this.Monitor = monitor;
            this.Helper = helper;
            this.QuadrantManager = quadrantManager;
        }

        /*********
        ** Public Methods - Item Tagging
        *********/
        /// <summary>Check if an item is tagged as common.</summary>
        public static bool IsCommonItem(Item item)
        {
            if (item is StardewValley.Object obj)
            {
                return obj.modData.ContainsKey(CommonTagKey);
            }
            return false;
        }

        /// <summary>Tag an item as common.</summary>
        public static void TagAsCommon(Item item)
        {
            if (item is StardewValley.Object obj)
            {
                obj.modData[CommonTagKey] = "true";
            }
        }

        /// <summary>Check if a tile is in a common area.</summary>
        public bool IsCommonAreaTile(GameLocation location, Vector2 tile)
        {
            // Only applies on the farm
            if (location?.Name != "Farm")
                return false;

            // NE quadrant is always common
            var quadrant = this.GetQuadrantForTile(tile);
            if (quadrant == Quadrant.NE)
                return true;

            // Shared/unoccupied quadrants are common
            if (quadrant.HasValue && this.QuadrantManager.IsQuadrantShared(quadrant.Value))
                return true;

            return false;
        }

        /// <summary>Called when a crop is harvested. Tags item if from common area.</summary>
        public void OnItemHarvested(Item item, Farmer farmer, Vector2 tile)
        {
            if (this.IsCommonAreaTile(farmer.currentLocation, tile))
            {
                TagAsCommon(item);
                this.Monitor.Log($"[COMMON] Tagged {item.Name} as common (harvested at {tile})", LogLevel.Debug);
            }
        }

        /*********
        ** Public Methods - Channel Restrictions
        *********/
        /// <summary>Check if placing a common item in this chest is allowed.</summary>
        public bool CanPlaceInChest(Item item, Chest chest)
        {
            // If item isn't common, no restrictions
            if (!IsCommonItem(item))
                return true;

            // Common items can go in GoodFences common chests
            if (chest.modData.ContainsKey(PlacedObjectKey))
            {
                var markerValue = chest.modData[PlacedObjectKey];
                if (markerValue.StartsWith("CommonChest"))
                    return true;
            }

            // Block common items from private chests
            this.Monitor.Log($"[COMMON] Blocked common item {item.Name} from private chest", LogLevel.Debug);
            return false;
        }

        /// <summary>Check if a shipping bin is a common shipping bin.</summary>
        public bool IsCommonShippingBin(StardewValley.Object obj)
        {
            if (obj.modData.TryGetValue(PlacedObjectKey, out var value))
            {
                return value.StartsWith("ShippingBin_NE") || value.Contains("Common");
            }
            return false;
        }

        /// <summary>Check if placing a common item in this shipping bin is allowed.</summary>
        public bool CanShipItem(Item item, StardewValley.Object shippingBin)
        {
            // If item isn't common, it can go in any shipping bin
            if (!IsCommonItem(item))
                return true;

            // Common items can only go in the NE common shipping bin
            if (this.IsCommonShippingBin(shippingBin))
                return true;

            // Block common items from private shipping bins
            this.Monitor.Log($"[COMMON] Blocked common item {item.Name} from private shipping bin", LogLevel.Debug);
            return false;
        }

        /*********
        ** Public Methods - Sale Split
        *********/
        /// <summary>Process common item sales at end of day, splitting proceeds equally.</summary>
        public void ProcessCommonSales()
        {
            // Only host processes sales
            if (!Context.IsMainPlayer)
                return;

            var farm = Game1.getFarm();
            if (farm == null)
                return;

            // Find common shipping bin and process items
            // This hooks into the shipping bin's contents before they're sold
            // Note: This is called from DayEnding event
            
            // TODO: Hook into actual shipping bin contents
            // For now, we track common items as they're placed in shipping bins
            // and adjust money distribution after overnight shipping
            
            this.Monitor.Log("[COMMON] Processing common sales...", LogLevel.Debug);
        }

        /// <summary>Distribute common sale proceeds equally among all players.</summary>
        public void DistributeCommonProceeds(int totalAmount)
        {
            var onlineFarmers = Game1.getOnlineFarmers().ToList();
            if (onlineFarmers.Count == 0)
                return;

            int perPlayer = totalAmount / onlineFarmers.Count;
            int remainder = totalAmount % onlineFarmers.Count;

            this.Monitor.Log($"[COMMON] Distributing {totalAmount}g among {onlineFarmers.Count} players ({perPlayer}g each, {remainder}g remainder)", LogLevel.Info);

            foreach (var farmer in onlineFarmers)
            {
                farmer.Money += perPlayer;
            }

            // Remainder goes to host
            if (remainder > 0)
            {
                Game1.MasterPlayer.Money += remainder;
                this.Monitor.Log($"[COMMON] Remainder {remainder}g added to host", LogLevel.Debug);
            }
        }

        /*********
        ** Private Methods
        *********/
        /// <summary>Determine which quadrant a tile is in.</summary>
        private Quadrant? GetQuadrantForTile(Vector2 tile)
        {
            // Four Corners quadrant boundaries (approximate center at x40, y36)
            bool isNorth = tile.Y < 36;
            bool isWest = tile.X < 40;

            if (isNorth && !isWest) return Quadrant.NE;
            if (isNorth && isWest) return Quadrant.NW;
            if (!isNorth && isWest) return Quadrant.SW;
            if (!isNorth && !isWest) return Quadrant.SE;

            return null;
        }
    }
}
