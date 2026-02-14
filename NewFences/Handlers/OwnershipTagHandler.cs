// Handlers/OwnershipTagHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Linq;

namespace GoodFences.Handlers
{
    /// <summary>
    /// Core ownership tagging system. Applies player IDs to items, crops,
    /// terrain features, and machines to track who produced what.
    /// </summary>
    public class OwnershipTagHandler
    {
        /*********
        ** Constants
        *********/
        /// <summary>Mod data key for item/object owner. Value is player's UniqueMultiplayerID.</summary>
        public const string OwnerKey = "GoodFences.Owner";

        /// <summary>Mod data key for common items (shared ownership). Used in later phases.</summary>
        public const string CommonKey = "GoodFences.Common";

        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly bool DebugMode;

        /*********
        ** Constructor
        *********/
        public OwnershipTagHandler(IMonitor monitor, bool debugMode)
        {
            this.Monitor = monitor;
            this.DebugMode = debugMode;
        }

        /*********
        ** Public Methods - Tagging
        *********/
        /// <summary>Tag an item with a player's ownership ID.</summary>
        public static void TagItem(Item item, Farmer owner)
        {
            if (item is StardewValley.Object obj)
            {
                obj.modData[OwnerKey] = owner.UniqueMultiplayerID.ToString();
            }
        }

        /// <summary>Tag an item with a raw player ID string.</summary>
        public static void TagItem(Item item, string ownerId)
        {
            if (item is StardewValley.Object obj)
            {
                obj.modData[OwnerKey] = ownerId;
            }
        }

        /// <summary>Tag a HoeDirt tile (soil) with the planter's ID.</summary>
        public static void TagSoil(HoeDirt soil, Farmer owner)
        {
            soil.modData[OwnerKey] = owner.UniqueMultiplayerID.ToString();
        }

        /// <summary>Tag a Crop with the planter's ID.</summary>
        public static void TagCrop(Crop crop, Farmer owner)
        {
            crop.modData[OwnerKey] = owner.UniqueMultiplayerID.ToString();
        }

        /// <summary>Tag a Crop with a raw player ID string.</summary>
        public static void TagCrop(Crop crop, string ownerId)
        {
            crop.modData[OwnerKey] = ownerId;
        }

        /// <summary>Tag a machine/object with an owner (for tracking artisan output).</summary>
        public static void TagMachine(StardewValley.Object machine, string ownerId)
        {
            machine.modData[OwnerKey] = ownerId;
        }

        /// <summary>Tag a tree with the planter's ID.</summary>
        public static void TagTree(Tree tree, Farmer owner)
        {
            tree.modData[OwnerKey] = owner.UniqueMultiplayerID.ToString();
        }

        /// <summary>Tag a fruit tree with the planter's ID.</summary>
        public static void TagFruitTree(FruitTree tree, Farmer owner)
        {
            tree.modData[OwnerKey] = owner.UniqueMultiplayerID.ToString();
        }

        /*********
        ** Public Methods - Reading Tags
        *********/
        /// <summary>Get the owner ID from an item. Returns null if untagged.</summary>
        public static string? GetOwnerID(Item item)
        {
            if (item is StardewValley.Object obj && obj.modData.TryGetValue(OwnerKey, out var ownerId))
            {
                return ownerId;
            }
            return null;
        }

        /// <summary>Get the owner ID from soil. Returns null if untagged.</summary>
        public static string? GetSoilOwnerID(HoeDirt soil)
        {
            if (soil.modData.TryGetValue(OwnerKey, out var ownerId))
            {
                return ownerId;
            }
            return null;
        }

        /// <summary>Get the owner ID from a Crop. Returns null if untagged.</summary>
        public static string? GetCropOwnerID(Crop crop)
        {
            if (crop.modData.TryGetValue(OwnerKey, out var ownerId))
            {
                return ownerId;
            }
            return null;
        }

        /// <summary>Get the owner ID from a machine. Returns null if untagged.</summary>
        public static string? GetMachineOwnerID(StardewValley.Object machine)
        {
            if (machine.modData.TryGetValue(OwnerKey, out var ownerId))
            {
                return ownerId;
            }
            return null;
        }

        /// <summary>Get the owner ID from a tree. Returns null if untagged.</summary>
        public static string? GetTreeOwnerID(Tree tree)
        {
            if (tree.modData.TryGetValue(OwnerKey, out var ownerId))
            {
                return ownerId;
            }
            return null;
        }

        /// <summary>Get the owner ID from a fruit tree. Returns null if untagged.</summary>
        public static string? GetFruitTreeOwnerID(FruitTree tree)
        {
            if (tree.modData.TryGetValue(OwnerKey, out var ownerId))
            {
                return ownerId;
            }
            return null;
        }

        /// <summary>Check if an item is owned by a specific farmer.</summary>
        public static bool IsOwnedBy(Item item, Farmer farmer)
        {
            var ownerId = GetOwnerID(item);
            return ownerId == farmer.UniqueMultiplayerID.ToString();
        }

        /// <summary>Check if an item has any owner tag.</summary>
        public static bool HasOwner(Item item)
        {
            return GetOwnerID(item) != null;
        }

        /// <summary>Check if an item is tagged as common (shared).</summary>
        public static bool IsCommon(Item item)
        {
            if (item is StardewValley.Object obj)
            {
                return obj.modData.ContainsKey(CommonKey);
            }
            return false;
        }

        /*********
        ** Public Methods - Display
        *********/
        /// <summary>Get the display name of an item's owner. Returns empty string if untagged.</summary>
        public static string GetOwnerDisplayName(Item item)
        {
            var ownerId = GetOwnerID(item);
            if (ownerId == null)
                return "";

            return GetPlayerNameFromID(ownerId);
        }

        /// <summary>Get a player's display name from their ID string.</summary>
        public static string GetPlayerNameFromID(string playerIdStr)
        {
            if (long.TryParse(playerIdStr, out var playerId))
            {
                foreach (var farmer in Game1.getAllFarmers())
                {
                    if (farmer.UniqueMultiplayerID == playerId)
                        return farmer.Name;
                }
            }
            return "Unknown";
        }

        /*********
        ** Public Methods - Logging
        *********/
        /// <summary>Log a tagging event if debug mode is enabled.</summary>
        public void LogTag(string action, string itemName, Farmer owner, string? detail = null)
        {
            if (!this.DebugMode)
                return;

            string msg = $"[TAG] {action}: {itemName} → {owner.Name}";
            if (detail != null)
                msg += $" ({detail})";

            this.Monitor.Log(msg, LogLevel.Debug);
        }

        /// <summary>Log a tagging event with raw owner ID if debug mode is enabled.</summary>
        public void LogTag(string action, string itemName, string ownerId, string? detail = null)
        {
            if (!this.DebugMode)
                return;

            string ownerName = GetPlayerNameFromID(ownerId);
            string msg = $"[TAG] {action}: {itemName} → {ownerName}";
            if (detail != null)
                msg += $" ({detail})";

            this.Monitor.Log(msg, LogLevel.Debug);
        }
    }
}
