// Patches/AutomateCompatibilityPatches.cs
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Linq;
using GoodFences.Handlers;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to preserve ownership tags when Automate moves items
    /// between chests and machines. Automate uses Item.getOne() to create
    /// new item stacks, which doesn't copy modData.
    /// </summary>
    internal class AutomateCompatibilityPatches
    {
        private static IMonitor? Monitor;
        private static bool DebugMode;

        /// <summary>Mod data key for owner tracking (matches OwnershipTagHandler.OwnerKey)</summary>
        private const string OwnerKey = "GoodFences.Owner";

        /// <summary>Mod data key for common items (matches OwnershipTagHandler.CommonKey)</summary>
        private const string CommonKey = "GoodFences.Common";

        /*********
        ** Public Methods
        *********/
        /// <summary>Apply Harmony patches for Automate compatibility.</summary>
        public static void Apply(Harmony harmony, IMonitor monitor, bool debugMode)
        {
            Monitor = monitor;
            DebugMode = debugMode;

            try
            {
                // Patch Item.getOne() to preserve modData on cloned items
                harmony.Patch(
                    original: AccessTools.Method(typeof(Item), nameof(Item.getOne)),
                    postfix: new HarmonyMethod(typeof(AutomateCompatibilityPatches), nameof(Item_GetOne_Postfix))
                );

                if (DebugMode)
                    monitor.Log("Automate compatibility patches applied successfully.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                monitor.Log($"Error applying Automate compatibility patches: {ex}", LogLevel.Error);
            }
        }

        /*********
        ** Private Patches
        *********/
        /// <summary>
        /// Postfix for Item.getOne() - preserves modData from the original item
        /// to the cloned item. This ensures ownership tags persist when Automate
        /// moves items between containers.
        /// </summary>
        private static void Item_GetOne_Postfix(Item __instance, Item __result)
        {
            try
            {
                // Only process if both original and result are Objects with modData
                if (__instance is not StardewValley.Object originalObj || __result is not StardewValley.Object resultObj)
                    return;

                // Copy ownership tag if present
                if (originalObj.modData.ContainsKey(OwnerKey))
                {
                    string ownerId = originalObj.modData[OwnerKey];
                    resultObj.modData[OwnerKey] = ownerId;

                    if (DebugMode && Monitor != null)
                    {
                        string ownerName = GetPlayerName(ownerId);
                        Monitor.Log(
                            $"AUTOMATE-COMPAT: Preserved ownership tag on {__result.Name} → {ownerName}",
                            LogLevel.Debug
                        );
                    }
                }

                // Copy common tag if present (for future step 3)
                if (originalObj.modData.ContainsKey(CommonKey))
                {
                    resultObj.modData[CommonKey] = 
                        originalObj.modData[CommonKey];
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Error in Item_GetOne_Postfix: {ex}", LogLevel.Error);
            }
        }

        /*********
        ** Private Helpers
        *********/
        /// <summary>Get a player's name from their ID, or "Unknown" if not found.</summary>
        private static string GetPlayerName(string ownerId)
        {
            try
            {
                if (!long.TryParse(ownerId, out long id))
                    return "Unknown";

                Farmer? owner = Game1.getAllFarmers()
                    .FirstOrDefault(f => f.UniqueMultiplayerID == id);

                return owner?.Name ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
