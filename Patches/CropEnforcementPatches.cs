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
    /// Blocks non-owners from planting on owned soil, harvesting,
    /// or destroying crops without trust permission.
    ///
    /// Owner resolution is consistent across ALL patches:
    ///   1. Check crop modData first (most authoritative)
    ///   2. Fall back to soil modData
    /// This prevents the split-brain problem where enforcement and
    /// tagging read from different sources.
    /// </summary>
    [HarmonyPatch]
    public static class CropEnforcementPatches
    {
        /*********
        ** Shared Owner Resolution
        *********/
        /// <summary>
        /// Get the owner ID for a crop/soil combination. Checks crop
        /// modData first (most authoritative), falls back to soil.
        /// Returns 0 if no owner found.
        /// </summary>
        private static long ResolveCropOwner(HoeDirt soil)
        {
            if (soil == null)
                return 0;

            // Check crop tag first (primary authority)
            if (soil.crop != null)
            {
                string? cropOwner = OwnershipTagHandler.GetCropOwnerID(soil.crop);
                if (cropOwner != null && long.TryParse(cropOwner, out long cropOwnerId))
                    return cropOwnerId;
            }

            // Fall back to soil tag
            string? soilOwner = OwnershipTagHandler.GetSoilOwnerID(soil);
            if (soilOwner != null && long.TryParse(soilOwner, out long soilOwnerId))
                return soilOwnerId;

            return 0;
        }

        /// <summary>
        /// Check if crop/soil is marked as common.
        /// </summary>
        private static bool IsCommonCrop(HoeDirt soil)
        {
            if (soil?.modData == null)
                return false;

            return soil.modData.TryGetValue("GoodFences.Common", out string value) && value == "true";
        }

        /*********
        ** Planting / Fertilizing Enforcement
        *********/
        /// <summary>
        /// Prefix on HoeDirt.plant: Block non-owners from planting seeds
        /// or applying fertilizer to owned soil. Per design decision A1:
        /// "Full Block — non-owner cannot water, fertilize, harvest, or scythe."
        ///
        /// HoeDirt.plant() handles BOTH seed planting and fertilizer
        /// application. If the soil already has an owner and the acting
        /// player is not that owner, block entirely. This prevents the
        /// split-brain ownership corruption where fertilizer changes
        /// one tag but not the other.
        /// </summary>
        public static bool HoeDirt_Plant_Prefix(
            HoeDirt __instance,
            Farmer who,
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
                if (who == null)
                    return true;

                // Get the current soil/crop owner
                long ownerId = ResolveCropOwner(__instance);

                // Unowned soil — anyone can plant here
                if (ownerId == 0)
                    return true;

                // Owner planting/fertilizing own soil — allow
                if (who.UniqueMultiplayerID == ownerId)
                    return true;

                // Common crop — allow
                if (IsCommonCrop(__instance))
                    return true;

                // Check trust
                var owner = Game1.GetPlayer(ownerId);
                if (owner != null && TrustSystemHandler.HasTrust(owner, who, TrustSystemHandler.PermissionType.Crops))
                    return true;

                // Block planting/fertilizing
                string ownerName = owner?.Name ?? "another player";
                Game1.addHUDMessage(new HUDMessage($"This soil belongs to {ownerName}", HUDMessage.error_type));

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {who.Name} from planting/fertilizing on {ownerName}'s soil", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false; // Block — soil is owned by someone else
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in HoeDirt_Plant_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true;
            }
        }

        /*********
        ** Harvest Enforcement
        *********/
        /// <summary>
        /// Prefix on Crop.harvest: Block non-owners from right-click
        /// harvesting. Uses consistent ResolveCropOwner (crop first,
        /// soil fallback) to stay in sync with the tagging system.
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

                // Get crop/soil owner using consistent resolution
                long ownerId = ResolveCropOwner(soil);

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

        /*********
        ** Tool Enforcement
        *********/
        /// <summary>
        /// Prefix on HoeDirt.performToolAction: Block non-owners from using
        /// destructive tools (pickaxe, axe, scythe) on owned soil with crops.
        /// Uses consistent ResolveCropOwner (crop first, soil fallback).
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

                // Get owner using consistent resolution
                long ownerId = ResolveCropOwner(__instance);

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
    }
}
