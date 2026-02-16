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
    /// Blocks non-owners from opening chests or transferring items
    /// without trust permission. Covers both vanilla right-click
    /// access AND remote access through Chests Anywhere.
    /// </summary>
    [HarmonyPatch]
    public static class ChestEnforcementPatches
    {
        /*********
        ** Shared Ownership Check
        *********/
        /// <summary>
        /// Check if a player can access a chest. Used by all enforcement
        /// patches to ensure consistent logic across vanilla and mod paths.
        /// </summary>
        /// <param name="chest">The chest being accessed.</param>
        /// <param name="user">The player attempting access.</param>
        /// <param name="ownerName">Output: the owner's name (for HUD messages).</param>
        /// <returns>True if access is allowed, false if blocked.</returns>
        private static bool CanAccessChest(Chest chest, Farmer user, out string ownerName)
        {
            ownerName = "another player";

            if (chest == null || user == null)
                return true;

            // Common chest — always allow
            if (CommonChestHandler.IsCommonChest(chest))
                return true;

            // Get chest owner
            long ownerId = GetChestOwnerId(chest);

            // No owner — allow (pre-mod chest)
            if (ownerId == 0)
                return true;

            // Owner accessing own chest — allow
            if (user.UniqueMultiplayerID == ownerId)
                return true;

            // Check if user has trust for this specific chest
            var owner = Game1.GetPlayer(ownerId);
            string chestGuid = GetChestGuid(chest);

            if (owner != null && !string.IsNullOrEmpty(chestGuid) &&
                TrustSystemHandler.HasChestTrust(owner, user, chestGuid))
            {
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"{user.Name} has chest trust from {owner.Name}, allowing access", StardewModdingAPI.LogLevel.Trace);
                return true;
            }

            // Access denied
            ownerName = owner?.Name ?? "another player";
            return false;
        }

        /*********
        ** Vanilla Access Patches
        *********/
        /// <summary>
        /// Patch Chest.checkForAction to check ownership before allowing
        /// chest to open via right-click in the world.
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
                var user = who ?? Game1.player;

                if (CanAccessChest(__instance, user, out string ownerName))
                    return true;

                // Block chest access
                Game1.addHUDMessage(new HUDMessage($"This chest belongs to {ownerName}", HUDMessage.error_type));

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from opening {ownerName}'s chest", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false;
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Chest_CheckForAction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true;
            }
        }

        /// <summary>
        /// Patch Chest.addItem to check permissions on item insertion
        /// via automation (primarily for Automate mod compatibility).
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
                var user = Game1.player;

                if (CanAccessChest(__instance, user, out _))
                    return true;

                // Block automated item insertion
                if (ModEntry.StaticConfig.DebugMode)
                {
                    string ownerIdStr = OwnershipTagHandler.GetObjectOwnerID(__instance) ?? "unknown";
                    string ownerName = OwnershipTagHandler.GetPlayerNameFromID(ownerIdStr);
                    ModEntry.Instance?.Monitor.Log($"Blocked automated item insertion to {ownerName}'s chest by {user.Name}", StardewModdingAPI.LogLevel.Debug);
                }

                return false;
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Chest_AddItem_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true;
            }
        }

        /*********
        ** Chests Anywhere Compatibility Patches
        **
        ** Chests Anywhere (CA) bypasses Chest.checkForAction entirely.
        ** It opens chests remotely using Chest.grabItemFromInventory
        ** (player → chest) and Chest.grabItemFromChest (chest → player).
        ** These patches enforce ownership on those transfer paths.
        *********/

        /// <summary>
        /// Patch Chest.grabItemFromInventory to block non-owners from
        /// depositing items into an owned chest via Chests Anywhere.
        /// </summary>
        public static bool Chest_GrabItemFromInventory_Prefix(
            Chest __instance,
            Item item,
            Farmer who)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceChestOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            try
            {
                var user = who ?? Game1.player;

                if (CanAccessChest(__instance, user, out string ownerName))
                    return true;

                // Block item deposit
                Game1.addHUDMessage(new HUDMessage($"This chest belongs to {ownerName}", HUDMessage.error_type));

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from depositing into {ownerName}'s chest (remote access)", StardewModdingAPI.LogLevel.Debug);

                return false; // Skip original — deposit blocked
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Chest_GrabItemFromInventory_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true;
            }
        }

        /// <summary>
        /// Patch Chest.grabItemFromChest to block non-owners from
        /// withdrawing items from an owned chest via Chests Anywhere.
        /// </summary>
        public static bool Chest_GrabItemFromChest_Prefix(
            Chest __instance,
            Item item,
            Farmer who)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceChestOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            try
            {
                var user = who ?? Game1.player;

                if (CanAccessChest(__instance, user, out string ownerName))
                    return true;

                // Block item withdrawal
                Game1.addHUDMessage(new HUDMessage($"This chest belongs to {ownerName}", HUDMessage.error_type));

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from taking from {ownerName}'s chest (remote access)", StardewModdingAPI.LogLevel.Debug);

                return false; // Skip original — withdrawal blocked
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Chest_GrabItemFromChest_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true;
            }
        }

        /*********
        ** Helpers
        *********/
        /// <summary>
        /// Get the owner ID of a chest from modData.
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
        /// Get or create a unique GUID for a chest.
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
