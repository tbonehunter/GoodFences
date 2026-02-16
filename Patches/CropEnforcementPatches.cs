// Patches/CropEnforcementPatches.cs
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using GoodFences.Handlers;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to enforce crop ownership.
    /// Blocks non-owners from harvesting crops without trust permission.
    /// </summary>
    [HarmonyPatch]
    public static class CropEnforcementPatches
    {
        /// <summary>
        /// Patch Crop.harvest to check ownership before allowing harvest
        /// </summary>
        [HarmonyPatch(typeof(Crop), nameof(Crop.harvest))]
        [HarmonyPrefix]
        public static bool Crop_Harvest_Prefix(
            Crop __instance,
            int xTile,
            int yTile,
            HoeDirt soil,
            ref bool __result)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceCropOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            try
            {
                // Get the harvesting player
                var harvester = Game1.player;
                if (harvester == null)
                    return true;

                // Get crop owner from soil modData
                long ownerId = GetCropOwnerId(soil);

                // No owner = allow (untagged crop, pre-mod ownership)
                if (ownerId == 0)
                {
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"Crop has no owner, allowing harvest", StardewModdingAPI.LogLevel.Trace);
                    return true;
                }

                // Owner harvesting own crop = allow
                if (harvester.UniqueMultiplayerID == ownerId)
                    return true;

                // Check if crop is marked as common
                if (IsCommonCrop(soil))
                {
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"Common crop, allowing harvest", StardewModdingAPI.LogLevel.Trace);
                    return true;
                }

                // Check if harvester has trust permission for crops
                var owner = Game1.GetPlayer(ownerId);
                if (owner != null && TrustSystemHandler.HasTrust(owner, harvester, TrustSystemHandler.PermissionType.Crops))
                {
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"{harvester.Name} has crop trust from {owner.Name}, allowing harvest", StardewModdingAPI.LogLevel.Trace);
                    return true;
                }

                // Block harvest
                string ownerName = owner?.Name ?? "another player";
                Game1.addHUDMessage(new HUDMessage($"This crop belongs to {ownerName}", HUDMessage.error_type));
                
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {harvester.Name} from harvesting {ownerName}'s crop", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false; // Block harvest
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Crop_Harvest_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error to prevent game breaking
            }
        }

        /// <summary>
        /// Get the owner ID of a crop from soil modData
        /// </summary>
        private static long GetCropOwnerId(HoeDirt soil)
        {
            if (soil?.modData == null)
                return 0;

            if (soil.modData.TryGetValue("GoodFences.Owner", out string ownerIdStr) &&
                long.TryParse(ownerIdStr, out long ownerId))
            {
                return ownerId;
            }

            return 0;
        }

        /// <summary>
        /// Check if crop is marked as common
        /// </summary>
        private static bool IsCommonCrop(HoeDirt soil)
        {
            if (soil?.modData == null)
                return false;

            return soil.modData.TryGetValue("GoodFences.Common", out string value) && value == "true";
        }
    }
}


