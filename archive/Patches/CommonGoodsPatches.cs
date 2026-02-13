// Patches/CommonGoodsPatches.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using GoodFences.Handlers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GoodFences.Patches
{
    /// <summary>Harmony patches for common goods tagging and channel restrictions.</summary>
    public static class CommonGoodsPatches
    {
        /*********
        ** Fields
        *********/
        private static IMonitor? Monitor;
        private static CommonGoodsHandler? Handler;

        /// <summary>Tracks common items being shipped this session for sale split calculation.</summary>
        internal static int CommonItemsShippedValue = 0;

        /*********
        ** Public Methods
        *********/
        /// <summary>Initialize the patches with required dependencies.</summary>
        public static void Initialize(IMonitor monitor, CommonGoodsHandler handler)
        {
            Monitor = monitor;
            Handler = handler;
        }

        /// <summary>Apply all Harmony patches.</summary>
        public static void Apply(Harmony harmony)
        {
            Monitor?.Log("[PATCH] Applying CommonGoodsPatches...", LogLevel.Debug);

            // Patch Crop.harvest for crop harvesting
            harmony.Patch(
                original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
                postfix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Crop_Harvest_Postfix))
            );

            // Patch Chest.addItem for chest insertion restrictions
            harmony.Patch(
                original: AccessTools.Method(typeof(Chest), nameof(Chest.addItem)),
                prefix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Chest_AddItem_Prefix))
            );

            // Patch Farm.shipItem for shipping bin tracking
            harmony.Patch(
                original: AccessTools.Method(typeof(Farm), nameof(Farm.shipItem)),
                prefix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Farm_ShipItem_Prefix))
            );

            // Patch Object.performObjectDropInAction for artisan machine output tagging
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.performObjectDropInAction)),
                postfix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Object_PerformObjectDropInAction_Postfix))
            );

            // Patch Item.canStackWith to prevent common/private items from stacking
            harmony.Patch(
                original: AccessTools.Method(typeof(Item), nameof(Item.canStackWith)),
                postfix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Item_CanStackWith_Postfix))
            );

            // Patch Object.DisplayName getter to add (C) suffix for common items
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(StardewValley.Object), nameof(StardewValley.Object.DisplayName)),
                postfix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Object_DisplayName_Postfix))
            );

            // Patch Object.getDescription for tooltip enhancement
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.getDescription)),
                postfix: new HarmonyMethod(typeof(CommonGoodsPatches), nameof(Object_GetDescription_Postfix))
            );

            Monitor?.Log("[PATCH] CommonGoodsPatches applied successfully.", LogLevel.Debug);
        }

        /// <summary>Reset common shipping value at day start.</summary>
        public static void ResetDailyTracking()
        {
            CommonItemsShippedValue = 0;
            Monitor?.Log("[PATCH] Reset common shipping tracking for new day.", LogLevel.Debug);
        }

        /// <summary>Get the total value of common items shipped today.</summary>
        public static int GetCommonShippedValue()
        {
            return CommonItemsShippedValue;
        }

        /*********
        ** Harmony Patches
        *********/
        /// <summary>Tag harvested crops from common areas.</summary>
        /// <remarks>
        /// This postfix runs after Crop.harvest() completes.
        /// We check if the harvest location is a common area and tag the last added item.
        /// </remarks>
        private static void Crop_Harvest_Postfix(Crop __instance, int xTile, int yTile, HoeDirt soil, JunimoHarvester junimoHarvester)
        {
            try
            {
                if (Handler == null) return;

                // Get the harvesting farmer (null if Junimo)
                Farmer? harvester = junimoHarvester == null ? Game1.player : null;
                var location = Game1.currentLocation;

                // Only process on the farm
                if (location?.Name != "Farm") return;

                var tile = new Vector2(xTile, yTile);

                // Check if this tile is in a common area
                if (Handler.IsCommonAreaTile(location, tile))
                {
                    // Tag the most recently added item to the player's inventory
                    // The crop harvest adds the item to player inventory before this postfix runs
                    if (harvester != null || Game1.player != null)
                    {
                        var farmer = harvester ?? Game1.player;
                        var lastItem = GetLastAddedItem(farmer);

                        if (lastItem != null)
                        {
                            CommonGoodsHandler.TagAsCommon(lastItem);
                            Monitor?.Log($"[PATCH] Tagged harvested {lastItem.Name} at ({xTile},{yTile}) as common", LogLevel.Debug);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Crop_Harvest_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Block common items from being added to private chests.</summary>
        /// <returns>False to skip the original method (block insertion), true to continue.</returns>
        private static bool Chest_AddItem_Prefix(Chest __instance, Item item, ref Item? __result)
        {
            try
            {
                if (Handler == null || item == null) return true;

                // Check if the item is common-tagged
                if (!CommonGoodsHandler.IsCommonItem(item))
                    return true; // Not common, allow insertion

                // Check if this chest allows common items
                if (Handler.CanPlaceInChest(item, __instance))
                    return true; // Common chest, allow insertion

                // Block insertion - common item trying to go into private chest
                Monitor?.Log($"[PATCH] Blocked common item {item.Name} from private chest", LogLevel.Debug);
                
                // Show feedback to player
                Game1.showRedMessage("Common items can only go in common chests!");
                
                // Return the item to indicate failure (don't add to chest)
                __result = item;
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Chest_AddItem_Prefix: {ex}", LogLevel.Error);
                return true; // On error, allow insertion
            }
        }

        /// <summary>Track common items being shipped and optionally block from private bins.</summary>
        /// <returns>False to skip shipping (block), true to continue.</returns>
        private static bool Farm_ShipItem_Prefix(Farm __instance, Item i, Farmer who)
        {
            try
            {
                if (Handler == null || i == null) return true;

                // Check if the item is common-tagged
                if (!CommonGoodsHandler.IsCommonItem(i))
                    return true; // Not common, proceed normally

                // Track the value for end-of-day split calculation
                int itemValue = GetItemShipValue(i);
                CommonItemsShippedValue += itemValue;

                Monitor?.Log($"[PATCH] Common item {i.Name} shipped, value: {itemValue}g (total common: {CommonItemsShippedValue}g)", LogLevel.Debug);

                // Allow shipping - we'll adjust money distribution at day end
                return true;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Farm_ShipItem_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        /// <summary>Tag artisan machine output if input was common.</summary>
        /// <remarks>
        /// When a common item is processed in any machine, the output should also be common.
        /// This postfix checks if the machine is in a common area and tags output accordingly.
        /// </remarks>
        private static void Object_PerformObjectDropInAction_Postfix(
            StardewValley.Object __instance,
            Item dropInItem,
            bool probe,
            Farmer who,
            bool returnFalseIfItemConsumed,
            bool __result)
        {
            try
            {
                if (Handler == null || probe || !__result) return;

                // Check if the input item was common-tagged
                if (dropInItem != null && CommonGoodsHandler.IsCommonItem(dropInItem))
                {
                    // The machine now holds the item - we need to track that this machine
                    // should produce common output. Store in the machine's modData.
                    __instance.modData["GoodFences.CommonInput"] = "true";
                    Monitor?.Log($"[PATCH] Machine {__instance.Name} received common input, will produce common output", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_PerformObjectDropInAction_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Prevent common and private versions of the same item from stacking.</summary>
        /// <remarks>
        /// This ensures players can visually distinguish common from private items in inventory.
        /// Without this patch, common items would merge into private stacks and lose their tag.
        /// </remarks>
        private static void Item_CanStackWith_Postfix(Item __instance, ISalable other, ref bool __result)
        {
            try
            {
                // If already determined not stackable, leave it
                if (!__result) return;

                // Both must be Items (not just ISalable)
                if (other is not Item otherItem) return;

                // Check if one is common and the other isn't
                bool thisIsCommon = CommonGoodsHandler.IsCommonItem(__instance);
                bool otherIsCommon = CommonGoodsHandler.IsCommonItem(otherItem);

                if (thisIsCommon != otherIsCommon)
                {
                    // Prevent stacking - one is common, one is private
                    __result = false;
                    Monitor?.Log($"[PATCH] Prevented stacking: {__instance.Name} (common={thisIsCommon}) with (common={otherIsCommon})", LogLevel.Trace);
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Item_CanStackWith_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Add (C) suffix to display name for common items.</summary>
        /// <remarks>
        /// Provides clear visual indication in inventory, chests, and tooltips.
        /// Example: "Parsnip" becomes "Parsnip (C)"
        /// </remarks>
        private static void Object_DisplayName_Postfix(StardewValley.Object __instance, ref string __result)
        {
            try
            {
                if (CommonGoodsHandler.IsCommonItem(__instance))
                {
                    // Add (C) suffix if not already present
                    if (!__result.EndsWith(" (C)"))
                    {
                        __result = __result + " (C)";
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_DisplayName_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Add "Common Item" explanation to item tooltip/description.</summary>
        /// <remarks>
        /// Helps players understand why items have the (C) suffix and what it means.
        /// </remarks>
        private static void Object_GetDescription_Postfix(StardewValley.Object __instance, ref string __result)
        {
            try
            {
                if (CommonGoodsHandler.IsCommonItem(__instance))
                {
                    // Add common item explanation to description
                    __result = __result + "\n\n[Common Item]\nHarvested from shared farmland.\nSale proceeds split equally.";
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_GetDescription_Postfix: {ex}", LogLevel.Error);
            }
        }

        /*********
        ** Helper Methods
        *********/
        /// <summary>Get the most recently added item to a farmer's inventory.</summary>
        private static Item? GetLastAddedItem(Farmer farmer)
        {
            // The crop harvest typically adds to the last non-null slot or stacks with existing
            // We need to find what was just added - check the cursor item first
            if (farmer.CursorSlotItem != null)
                return farmer.CursorSlotItem;

            // Otherwise check the last filled inventory slot
            for (int i = farmer.Items.Count - 1; i >= 0; i--)
            {
                if (farmer.Items[i] != null)
                    return farmer.Items[i];
            }

            return null;
        }

        /// <summary>Calculate the shipping value of an item.</summary>
        private static int GetItemShipValue(Item item)
        {
            if (item is StardewValley.Object obj)
            {
                // Use the game's sell-to-shipping calculation
                return (int)(obj.sellToStorePrice() * obj.Stack);
            }
            return 0;
        }
    }
}
