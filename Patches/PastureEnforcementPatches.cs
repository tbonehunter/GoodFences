// Patches/PastureEnforcementPatches.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using GoodFences.Handlers;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to enforce pasture protection.
    /// Blocks non-owners from tilling in designated pasture zones.
    /// </summary>
    [HarmonyPatch]
    public static class PastureEnforcementPatches
    {
        /// <summary>
        /// Patch Hoe.DoFunction to check pasture protection before allowing tilling
        /// </summary>
        [HarmonyPatch(typeof(Hoe), nameof(Hoe.DoFunction))]
        [HarmonyPrefix]
        public static bool Hoe_DoFunction_Prefix(
            Hoe __instance,
            GameLocation location,
            int x,
            int y,
            int power,
            Farmer who)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforcePastureProtection)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            // Skip if not on farm
            if (!(location is Farm))
                return true;

            try
            {
                // Get the user
                var user = who ?? Game1.player;
                if (user == null)
                    return true;

                // Get tile position being hoed
                Vector2 tile = new Vector2(x / 64, y / 64);

                // Check if tile is in a pasture protection zone
                if (PastureZoneHandler.IsInPastureZone(location, tile, out long ownerId))
                {
                    // Check if user is the pasture owner
                    if (user.UniqueMultiplayerID == ownerId)
                    {
                        if (ModEntry.StaticConfig.DebugMode)
                            ModEntry.Instance?.Monitor.Log($"{user.Name} is pasture owner, allowing tilling", StardewModdingAPI.LogLevel.Trace);
                        return true;
                    }

                    // Block tilling in pasture
                    var owner = Game1.GetPlayer(ownerId);
                    string ownerName = owner?.Name ?? "another player";
                    Game1.addHUDMessage(new HUDMessage($"Cannot till in {ownerName}'s pasture", HUDMessage.error_type));
                    
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from tilling in {ownerName}'s pasture at ({tile.X}, {tile.Y})", StardewModdingAPI.LogLevel.Debug);

                    return false; // Block tilling
                }

                // Not in pasture zone, allow tilling
                return true;
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Hoe_DoFunction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Patch GameLocation.checkAction to prevent planting seeds in pastures
        /// (Additional protection layer - seeds require tilled soil first, but this catches edge cases)
        /// </summary>
        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkAction))]
        [HarmonyPrefix]
        public static bool GameLocation_CheckAction_Prefix(
            GameLocation __instance,
            xTile.Dimensions.Location tileLocation,
            xTile.Dimensions.Rectangle viewport,
            Farmer who,
            ref bool __result)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforcePastureProtection)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            // Skip if not on farm
            if (!(__instance is Farm))
                return true;

            try
            {
                // Get the user
                var user = who ?? Game1.player;
                if (user == null)
                    return true;

                // Check if user is holding seeds (trying to plant)
                var activeItem = user.ActiveObject;
                if (activeItem == null || activeItem.Category != StardewValley.Object.SeedsCategory)
                    return true;

                // Get tile position
                Vector2 tile = new Vector2(tileLocation.X, tileLocation.Y);

                // Check if tile is in a pasture protection zone
                if (PastureZoneHandler.IsInPastureZone(__instance, tile, out long ownerId))
                {
                    // Check if user is the pasture owner
                    if (user.UniqueMultiplayerID == ownerId)
                        return true;

                    // Block planting in pasture
                    var owner = Game1.GetPlayer(ownerId);
                    string ownerName = owner?.Name ?? "another player";
                    Game1.addHUDMessage(new HUDMessage($"Cannot plant in {ownerName}'s pasture", HUDMessage.error_type));
                    
                    if (ModEntry.StaticConfig.DebugMode)
                        ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from planting in {ownerName}'s pasture at ({tile.X}, {tile.Y})", StardewModdingAPI.LogLevel.Debug);

                    __result = false;
                    return false; // Block planting
                }

                return true;
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in GameLocation_CheckAction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }
    }
}


