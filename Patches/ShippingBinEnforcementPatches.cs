// Patches/ShippingBinEnforcementPatches.cs
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using GoodFences.Handlers;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to enforce shipping bin ownership rules.
    /// Per design: "Only owners can place their items in a shipping bin."
    ///
    /// Patches Utility.highlightShippableObjects to prevent non-owned
    /// items from being selected for shipping. This covers both the
    /// vanilla shipping bin UI and Chests Anywhere's shipping bin
    /// container, which both use the same highlight function.
    /// </summary>
    public static class ShippingBinEnforcementPatches
    {
        /// <summary>
        /// Postfix on Utility.highlightShippableObjects: After the game
        /// determines an item is shippable, additionally check that the
        /// item belongs to the current player (or is common/untagged).
        /// Items tagged to another player are grayed out and cannot be
        /// clicked to ship.
        /// </summary>
        public static void Utility_HighlightShippableObjects_Postfix(
            Item i,
            ref bool __result)
        {
            // If already blocked by vanilla logic, leave it
            if (!__result)
                return;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return;

            try
            {
                var player = Game1.player;
                if (player == null || i == null)
                    return;

                // Common items can always be shipped
                if (OwnershipTagHandler.IsCommon(i))
                    return;

                // Get item owner
                string? ownerIdStr = OwnershipTagHandler.GetOwnerID(i);

                // Untagged items can be shipped (pre-mod items, etc.)
                if (ownerIdStr == null)
                    return;

                // Owner can ship their own items
                if (long.TryParse(ownerIdStr, out long ownerId) &&
                    ownerId == player.UniqueMultiplayerID)
                    return;

                // Block: item belongs to another player
                __result = false;

                if (ModEntry.StaticConfig.DebugMode)
                {
                    string ownerName = OwnershipTagHandler.GetPlayerNameFromID(ownerIdStr);
                    ModEntry.Instance?.Monitor.Log(
                        $"Shipping blocked: {i.Name} belongs to {ownerName}, not {player.Name}",
                        StardewModdingAPI.LogLevel.Trace);
                }
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in Utility_HighlightShippableObjects_Postfix: {ex.Message}",
                    StardewModdingAPI.LogLevel.Error);
            }
        }
    }
}
