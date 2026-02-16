// Patches/CropEnforcementPatches.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using GoodFences.Handlers;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to enforce crop ownership.
    /// Blocks non-owners from harvesting or destroying crops without trust.
    /// </summary>
    [HarmonyPatch]
    public static class CropEnforcementPatches
    {
        /// <summary>
        /// Patch Crop.harvest to check ownership before allowing harvest.
        /// This covers right-click harvesting and Junimo harvesting.
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
        /// Prefix on HoeDirt.performToolAction: Block non-owners from using
        /// destructive tools (pickaxe, axe, scythe) on owned soil with crops.
        /// This prevents problem #8: another player pickaxing your crops.
        ///
        /// Note: Watering cans do NOT go through performToolAction — they
        /// modify HoeDirt.state directly. So this only blocks destructive
        /// tools, which is the intended behavior.
        /// </summary>
        public static bool HoeDirt_PerformToolAction_Prefix(
            HoeDirt __instance,
            Tool t,
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
                // Only protect soil that has a crop growing
                if (__instance.crop == null)
                    return true;

                // Get the tool user
                var user = Game1.player;
                if (user == null)
                    return true;

                // Get soil/crop owner — try crop first, fall back to soil
                long ownerId = 0;
                string? cropOwnerStr = OwnershipTagHandler.GetCropOwnerID(__instance.crop);
                if (cropOwnerStr != null && long.TryParse(cropOwnerStr, out long cropOwnerId))
                {
                    ownerId = cropOwnerId;
                }
                else
                {
                    ownerId = GetCropOwnerId(__instance);
                }

                // No owner = allow
                if (ownerId == 0)
                    return true;

                // Owner using tool on own crop = allow
                if (user.UniqueMultiplayerID == ownerId)
                    return true;

                // Check if crop is common
                if (IsCommonCrop(__instance))
                    return true;

                // Check trust
                var owner = Game1.GetPlayer(ownerId);
                if (owner != null && TrustSystemHandler.HasTrust(owner, user, TrustSystemHandler.PermissionType.Crops))
                    return true;

                // Block the tool action
                string ownerName = owner?.Name ?? "another player";
                Game1.addHUDMessage(new HUDMessage($"This crop belongs to {ownerName}", HUDMessage.error_type));

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from using {t?.Name ?? "tool"} on {ownerName}'s crop", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false; // Block tool action — crop is protected
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in HoeDirt_PerformToolAction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Postfix on HoeDirt.performToolAction: If the tool action was
        /// allowed (owner destroying own crop) and the crop is now gone,
        /// clear soil ownership so the tile is open for anyone to replant.
        /// </summary>
        public static void HoeDirt_PerformToolAction_Postfix(
            HoeDirt __instance,
            bool __result)
        {
            try
            {
                // If action succeeded and crop is now gone, clear soil ownership
                if (__result || __instance.crop == null)
                {
                    string? soilOwner = OwnershipTagHandler.GetSoilOwnerID(__instance);
                    if (soilOwner != null)
                    {
                        OwnershipTagHandler.ClearSoilOwner(__instance);

                        if (ModEntry.StaticConfig.DebugMode)
                        {
                            ModEntry.Instance?.Monitor.Log(
                                "[TAG] SOIL-CLEAR: Crop destroyed by tool, soil now open",
                                StardewModdingAPI.LogLevel.Debug);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in HoeDirt_PerformToolAction_Postfix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
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
