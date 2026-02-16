// Patches/ChestEnforcementPatches.cs
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Menus;
using GoodFences.Handlers;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to enforce chest ownership.
    /// Blocks non-owners from opening chests without trust permission.
    /// </summary>
    [HarmonyPatch]
    public static class ChestEnforcementPatches
    {
        /// <summary>
        /// Patch Chest.checkForAction to check ownership before allowing chest to open
        /// </summary>
        [HarmonyPatch(typeof(Chest), nameof(Chest.checkForAction))]
        [HarmonyPrefix]
        public static bool Chest_CheckForAction_Prefix(
            Chest __instance,
            Farmer who,
            ref bool __result)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceChestOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            try
            {
                // Get the user
                var user = who ?? Game1.player;
                if (user == null)
                    return true;

                // Check if common chest - always allow
                if (CommonChestHandler.IsCommonChest(__instance))
                {
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"Common chest, allowing access", StardewModdingAPI.LogLevel.Trace);
                    return true;
                }

                // Get chest owner
                long ownerId = GetChestOwnerId(__instance);

                // No owner = allow (pre-mod chest)
                if (ownerId == 0)
                {
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"Chest has no owner, allowing access", StardewModdingAPI.LogLevel.Trace);
                    return true;
                }

                // Owner opening own chest = allow
                if (user.UniqueMultiplayerID == ownerId)
                    return true;

                // Check if user has trust for this specific chest
                var owner = Game1.GetPlayer(ownerId);
                string chestGuid = GetChestGuid(__instance);
                
                if (owner != null && !string.IsNullOrEmpty(chestGuid) && 
                    TrustSystemHandler.HasChestTrust(owner, user, chestGuid))
                {
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"{user.Name} has chest trust from {owner.Name}, allowing access", StardewModdingAPI.LogLevel.Trace);
                    return true;
                }

                // Block chest access
                string ownerName = owner?.Name ?? "another player";
                Game1.addHUDMessage(new HUDMessage($"This chest belongs to {ownerName}", HUDMessage.error_type));
                
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from opening {ownerName}'s chest", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false; // Block access
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Chest_CheckForAction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Patch Chest.addItem to allow checking permissions on item insertion via automation
        /// (primarily for mod compatibility like Automate)
        /// </summary>
        [HarmonyPatch(typeof(Chest), nameof(Chest.addItem))]
        [HarmonyPrefix]
        public static bool Chest_AddItem_Prefix(
            Chest __instance,
            Item item)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceChestOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            try
            {
                // Get the current player (automation typically runs as the active player)
                var user = Game1.player;
                if (user == null)
                    return true;

                // Check if common chest - always allow
                if (CommonChestHandler.IsCommonChest(__instance))
                    return true;

                // Get chest owner
                long ownerId = GetChestOwnerId(__instance);

                // No owner = allow
                if (ownerId == 0)
                    return true;

                // Owner = allow
                if (user.UniqueMultiplayerID == ownerId)
                    return true;

                // Check if user has trust for this specific chest
                var owner = Game1.GetPlayer(ownerId);
                string chestGuid = GetChestGuid(__instance);
                
                if (owner != null && !string.IsNullOrEmpty(chestGuid) && 
                    TrustSystemHandler.HasChestTrust(owner, user, chestGuid))
                {
                    return true;
                }

                // Block automated item insertion
                if (ModEntry.StaticConfig.DebugMode)
                {
                    string ownerName = owner?.Name ?? "another player";
                    ModEntry.Instance?.Monitor.Log($"Blocked automated item insertion to {ownerName}'s chest by {user.Name}", StardewModdingAPI.LogLevel.Debug);
                }

                return false; // Block insertion
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Chest_AddItem_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Get the owner ID of a chest from modData
        /// </summary>
        private static long GetChestOwnerId(Chest chest)
        {
            if (chest?.modData == null)
                return 0;

            if (chest.modData.TryGetValue("GoodFences.Owner", out string ownerIdStr) &&
                long.TryParse(ownerIdStr, out long ownerId))
            {
                return ownerId;
            }

            return 0;
        }

        /// <summary>
        /// Get or create a unique GUID for a chest
        /// </summary>
        private static string GetChestGuid(Chest chest)
        {
            if (chest?.modData == null)
                return null;

            // Try to get existing GUID
            if (chest.modData.TryGetValue("GoodFences.ChestGuid", out string guid))
                return guid;

            // Generate new GUID
            guid = System.Guid.NewGuid().ToString();
            chest.modData["GoodFences.ChestGuid"] = guid;
            return guid;
        }
    }
}


