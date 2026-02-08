// Patches/ChestOwnershipPatches.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using GoodFences.Handlers;
using GoodFences.Models;
using System;

namespace GoodFences.Patches
{
    /// <summary>Harmony patches for chest ownership and access control.</summary>
    public static class ChestOwnershipPatches
    {
        /*********
        ** Constants
        *********/
        /// <summary>Mod data key for chest owner. Value is player ID or "Common".</summary>
        public const string OwnerKey = "GoodFences.Owner";

        /// <summary>Special value indicating common ownership (all players can access).</summary>
        public const string CommonOwner = "Common";

        /*********
        ** Fields
        *********/
        private static IMonitor? Monitor;
        private static QuadrantManager? QuadrantManager;

        /*********
        ** Public Methods
        *********/
        /// <summary>Initialize the patches with required dependencies.</summary>
        public static void Initialize(IMonitor monitor, QuadrantManager quadrantManager)
        {
            Monitor = monitor;
            QuadrantManager = quadrantManager;
        }

        /// <summary>Apply all Harmony patches.</summary>
        public static void Apply(Harmony harmony)
        {
            Monitor?.Log("[PATCH] Applying ChestOwnershipPatches...", LogLevel.Debug);

            // Patch Chest.checkForAction for access control and toggle
            harmony.Patch(
                original: AccessTools.Method(typeof(Chest), nameof(Chest.checkForAction)),
                prefix: new HarmonyMethod(typeof(ChestOwnershipPatches), nameof(Chest_CheckForAction_Prefix))
            );

            // Patch Object.placementAction to tag owner on placement
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.placementAction)),
                postfix: new HarmonyMethod(typeof(ChestOwnershipPatches), nameof(Object_PlacementAction_Postfix))
            );

            // Patch Chest.DisplayName getter to show owner
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(Chest), nameof(Chest.DisplayName)),
                postfix: new HarmonyMethod(typeof(ChestOwnershipPatches), nameof(Chest_DisplayName_Postfix))
            );

            Monitor?.Log("[PATCH] ChestOwnershipPatches applied successfully.", LogLevel.Debug);
        }

        /*********
        ** Public Helpers
        *********/
        /// <summary>Check if a chest is owned by a specific player.</summary>
        public static bool IsOwnedBy(Chest chest, Farmer farmer)
        {
            if (!chest.modData.TryGetValue(OwnerKey, out var owner))
                return true; // No owner tag = anyone can access (legacy/vanilla chest)

            if (owner == CommonOwner)
                return true; // Common chest = everyone can access

            // Check if owner matches farmer ID
            return owner == farmer.UniqueMultiplayerID.ToString();
        }

        /// <summary>Check if a chest is marked as common.</summary>
        public static bool IsCommonChest(Chest chest)
        {
            return chest.modData.TryGetValue(OwnerKey, out var owner) && owner == CommonOwner;
        }

        /// <summary>Get the owner's display name for a chest.</summary>
        public static string GetOwnerDisplayName(Chest chest)
        {
            if (!chest.modData.TryGetValue(OwnerKey, out var owner))
                return ""; // No owner

            if (owner == CommonOwner)
                return "Common";

            // Find farmer by ID
            if (long.TryParse(owner, out var ownerId))
            {
                foreach (var farmer in Game1.getAllFarmers())
                {
                    if (farmer.UniqueMultiplayerID == ownerId)
                        return farmer.Name;
                }
            }

            return "Unknown";
        }

        /// <summary>Toggle chest between private and common ownership.</summary>
        public static void ToggleOwnership(Chest chest, Farmer farmer)
        {
            if (!chest.modData.TryGetValue(OwnerKey, out var currentOwner))
            {
                // No tag - set to farmer's private
                chest.modData[OwnerKey] = farmer.UniqueMultiplayerID.ToString();
                Game1.addHUDMessage(new HUDMessage($"Chest marked as Private ({farmer.Name})", HUDMessage.newQuest_type));
                Monitor?.Log($"[OWNERSHIP] {farmer.Name} marked chest as private", LogLevel.Debug);
                return;
            }

            // Check if farmer can toggle this chest
            bool canToggle = currentOwner == farmer.UniqueMultiplayerID.ToString() || 
                             currentOwner == CommonOwner ||
                             farmer.IsMainPlayer; // Host can toggle any chest they placed

            if (!canToggle)
            {
                Game1.addHUDMessage(new HUDMessage("You don't own this chest!", HUDMessage.error_type));
                return;
            }

            // Toggle between private and common
            if (currentOwner == CommonOwner)
            {
                chest.modData[OwnerKey] = farmer.UniqueMultiplayerID.ToString();
                Game1.addHUDMessage(new HUDMessage($"Chest marked as Private ({farmer.Name})", HUDMessage.newQuest_type));
                Monitor?.Log($"[OWNERSHIP] {farmer.Name} changed chest from Common to Private", LogLevel.Debug);
            }
            else
            {
                chest.modData[OwnerKey] = CommonOwner;
                Game1.addHUDMessage(new HUDMessage("Chest marked as Common", HUDMessage.newQuest_type));
                Monitor?.Log($"[OWNERSHIP] {farmer.Name} changed chest from Private to Common", LogLevel.Debug);
            }
        }

        /*********
        ** Harmony Patches
        *********/
        /// <summary>Intercept chest interaction for access control and toggle.</summary>
        /// <returns>False to block opening, true to allow.</returns>
        private static bool Chest_CheckForAction_Prefix(Chest __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            try
            {
                if (justCheckingForActivity)
                    return true; // Don't interfere with cursor checks

                // Check for Shift+RightClick to toggle ownership
                var keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
                bool isHoldingShift = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || 
                                      keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                
                if (isHoldingShift)
                {
                    // Only toggle if this is the actual action (not just checking)
                    ToggleOwnership(__instance, who);
                    __result = true; // Action was handled
                    return false; // Skip original method
                }

                // Check if player can access this chest
                if (!IsOwnedBy(__instance, who))
                {
                    string ownerName = GetOwnerDisplayName(__instance);
                    Game1.addHUDMessage(new HUDMessage($"This chest belongs to {ownerName}!", HUDMessage.error_type));
                    Monitor?.Log($"[OWNERSHIP] {who.Name} blocked from {ownerName}'s chest", LogLevel.Debug);
                    __result = false;
                    return false; // Block opening
                }

                return true; // Allow original method
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Chest_CheckForAction_Prefix: {ex}", LogLevel.Error);
                return true; // On error, allow access
            }
        }

        /// <summary>Tag chests with owner when placed.</summary>
        private static void Object_PlacementAction_Postfix(
            StardewValley.Object __instance,
            GameLocation location,
            int x,
            int y,
            Farmer who,
            bool __result)
        {
            try
            {
                if (!__result) return; // Placement failed

                // Check if this is a chest
                Vector2 tileLocation = new Vector2(x / 64, y / 64);
                if (location.Objects.TryGetValue(tileLocation, out var placedObj) && placedObj is Chest chest)
                {
                    // Don't re-tag if already tagged (e.g., GoodFences-placed common chests)
                    if (chest.modData.ContainsKey(OwnerKey))
                        return;

                    // Tag with placer's ID
                    chest.modData[OwnerKey] = who.UniqueMultiplayerID.ToString();
                    Monitor?.Log($"[OWNERSHIP] {who.Name} placed chest at {tileLocation}, tagged as private", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_PlacementAction_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Add owner indicator to chest display name.</summary>
        private static void Chest_DisplayName_Postfix(Chest __instance, ref string __result)
        {
            try
            {
                string ownerName = GetOwnerDisplayName(__instance);
                if (!string.IsNullOrEmpty(ownerName) && !__result.Contains($"({ownerName})"))
                {
                    __result = $"{__result} ({ownerName})";
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Chest_DisplayName_Postfix: {ex}", LogLevel.Error);
            }
        }
    }
}
