// Patches/OwnershipTagPatches.cs
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using GoodFences.Handlers;
using System;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches for passive ownership tagging across all action types.
    /// Step 1: Tag everything with player ID, no enforcement.
    /// Visual confirmation via display name suffixes and debug logging.
    /// </summary>
    public static class OwnershipTagPatches
    {
        /*********
        ** Fields
        *********/
        private static IMonitor? Monitor;
        private static OwnershipTagHandler? Handler;

        /// <summary>
        /// Shortcut to the owner key constant. Avoids spreading the
        /// string literal across every patch method.
        /// </summary>
        private static string OwnerKey => OwnershipTagHandler.OwnerKey;

        /*********
        ** Initialization
        *********/
        /// <summary>Initialize the patches with required dependencies.</summary>
        public static void Initialize(IMonitor monitor, OwnershipTagHandler handler)
        {
            Monitor = monitor;
            Handler = handler;
        }

        /// <summary>Apply all Harmony patches.</summary>
        public static void Apply(Harmony harmony)
        {
            Monitor?.Log("[PATCH] Applying OwnershipTagPatches...", LogLevel.Debug);

            // ── Crop Chain ──────────────────────────────────────────────
            // Tag soil AND crop when player plants a seed
            harmony.Patch(
                original: AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.plant)),
                postfix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(HoeDirt_Plant_Postfix))
            );

            // Prefix: Store crop owner BEFORE harvest
            harmony.Patch(
                original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
                prefix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Crop_Harvest_Prefix))
            );

            // Postfix: Clear PendingHarvestOwner after harvest completes
            harmony.Patch(
                original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
                postfix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Crop_Harvest_Postfix))
            );

            // ── Inventory Intercept ───────────────────────────────────────
            // Tag items BEFORE they enter inventory (and before stacking).
            // This is the key to correct harvest ownership — the item gets
            // the planter's tag before the game's stacking logic runs.
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.addItemToInventoryBool)),
                prefix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Farmer_AddItemToInventory_Prefix))
            );

            // ── Artisan Machine Chain ───────────────────────────────────
            // Store input owner on machine when item is dropped in
            harmony.Patch(
                original: AccessTools.Method(
                    typeof(StardewValley.Object),
                    nameof(StardewValley.Object.performObjectDropInAction)),
                postfix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Object_DropIn_Postfix))
            );

            // Tag machine output via periodic scan (PreTagMachineOutput)
            // called from ModEntry.OnUpdateTicked. This is more reliable than
            // intercepting the collection method, which varies across SDV versions.

            // Note: Tree and FruitTree planting are both handled via the
            // SMAPI TerrainFeatureListChanged event in ModEntry.cs rather
            // than Harmony patches, since trees are added as terrain features.

            // ── Stacking Prevention ─────────────────────────────────────
            // Prevent items with different owners from stacking
            harmony.Patch(
                original: AccessTools.Method(typeof(Item), nameof(Item.canStackWith)),
                postfix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Item_CanStackWith_Postfix))
            );

            // ── Display Name ────────────────────────────────────────────
            // Add owner name suffix to items for visual confirmation
            harmony.Patch(
                original: AccessTools.PropertyGetter(
                    typeof(StardewValley.Object),
                    nameof(StardewValley.Object.DisplayName)),
                postfix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Object_DisplayName_Postfix))
            );

            Monitor?.Log("[PATCH] OwnershipTagPatches applied successfully.", LogLevel.Debug);
        }

        /*********
        ** Shared State
        *********/
        /// <summary>
        /// Stores the crop owner's ID during a harvest operation. Set by
        /// the Crop.harvest prefix, read by the Farmer.addItemToInventoryBool
        /// prefix, and cleared by the Crop.harvest postfix.
        /// </summary>
        internal static string? PendingHarvestOwner = null;

        /*********
        ** Crop Chain Patches
        *********/
        /// <summary>
        /// Postfix on HoeDirt.plant: Tag both the soil AND the Crop with
        /// the planter's ID. The Crop tag is the primary ownership marker;
        /// the soil tag is kept for potential enforcement in step 2.
        /// </summary>
        private static void HoeDirt_Plant_Postfix(
            HoeDirt __instance,
            bool __result,
            Farmer who)
        {
            try
            {
                if (!__result || who == null)
                    return;

                // Tag the soil
                OwnershipTagHandler.TagSoil(__instance, who);

                // Tag the crop itself
                if (__instance.crop != null)
                {
                    OwnershipTagHandler.TagCrop(__instance.crop, who);
                    Handler?.LogTag("PLANT", __instance.crop.indexOfHarvest?.Value ?? "crop", who, "soil + crop tagged");
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in HoeDirt_Plant_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Prefix on Crop.harvest: Read the planter's ID from the Crop
        /// and store it in PendingHarvestOwner BEFORE the harvest creates
        /// the output item. The Farmer.addItemToInventoryBool prefix will
        /// read this value and tag the item before stacking occurs.
        /// </summary>
        private static void Crop_Harvest_Prefix(
            Crop __instance,
            int xTile,
            int yTile,
            HoeDirt soil)
        {
            try
            {
                // Try crop tag first, fall back to soil tag
                var ownerId = OwnershipTagHandler.GetCropOwnerID(__instance);
                if (ownerId == null && soil != null)
                {
                    ownerId = OwnershipTagHandler.GetSoilOwnerID(soil);
                }

                if (ownerId != null)
                {
                    PendingHarvestOwner = ownerId;
                    Handler?.LogTag("HARVEST-BEGIN", __instance.indexOfHarvest?.Value ?? "crop", ownerId,
                        $"at ({xTile},{yTile}), owner stored from crop");
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Crop_Harvest_Prefix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Postfix on Crop.harvest: Clear PendingHarvestOwner after the
        /// harvest completes. By this point, the Farmer.addItemToInventoryBool
        /// prefix has already tagged the item.
        /// </summary>
        private static void Crop_Harvest_Postfix(
            Crop __instance,
            bool __result)
        {
            PendingHarvestOwner = null;
        }

        /*********
        ** Inventory Intercept Patch
        *********/
        /// <summary>
        /// Prefix on Farmer.addItemToInventoryBool: Tag incoming items with
        /// the pending harvest owner BEFORE the game's stacking logic runs.
        /// This is the critical patch — it stamps ownership on the item
        /// before canStackWith is called, so items from different planters
        /// are correctly kept in separate stacks.
        /// </summary>
        private static void Farmer_AddItemToInventory_Prefix(
            Farmer __instance,
            Item item)
        {
            try
            {
                if (item == null)
                    return;

                // If a harvest is in progress, tag with the planter's ID
                if (PendingHarvestOwner != null)
                {
                    if (!OwnershipTagHandler.HasOwner(item))
                    {
                        OwnershipTagHandler.TagItem(item, PendingHarvestOwner);
                        Handler?.LogTag("HARVEST", item.Name, PendingHarvestOwner,
                            "tagged before inventory stacking");
                    }
                    return;
                }

                // No pending harvest — tag with the receiving farmer
                // (covers foraging, fishing, mining, etc.)
                if (!OwnershipTagHandler.HasOwner(item))
                {
                    OwnershipTagHandler.TagItem(item, __instance);
                    Handler?.LogTag("INVENTORY-PRE", item.Name, __instance,
                        "tagged before inventory stacking");
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Farmer_AddItemToInventory_Prefix: {ex}", LogLevel.Error);
            }
        }

        /*********
        ** Artisan Machine Patches
        *********/
        /// <summary>
        /// Postfix on Object.performObjectDropInAction: When a player places
        /// an item into a machine (keg, preserves jar, etc.), store the input
        /// item's owner on the machine. The output will inherit this owner
        /// when collected.
        /// </summary>
        private static void Object_DropIn_Postfix(
            StardewValley.Object __instance,
            Item dropInItem,
            bool probe,
            Farmer who,
            bool __result)
        {
            try
            {
                // Skip probe calls (game checking if drop-in would work)
                // Skip failed drop-ins
                if (probe || !__result || dropInItem == null)
                    return;

                // Determine the owner: use the input item's owner if tagged,
                // otherwise use the farmer who placed the item
                var inputOwnerId = OwnershipTagHandler.GetOwnerID(dropInItem);
                var ownerId = inputOwnerId ?? who?.UniqueMultiplayerID.ToString();

                if (ownerId != null)
                {
                    OwnershipTagHandler.TagMachine(__instance, ownerId);
                    Handler?.LogTag("MACHINE-IN", __instance.Name, ownerId,
                        $"input: {dropInItem.Name}");
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_DropIn_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Scan machines in a location and pre-tag any ready output with the
        /// stored input owner. Called periodically from ModEntry so the output
        /// is tagged BEFORE any player collects it, regardless of how the
        /// game handles the collection internally.
        /// </summary>
        public static void PreTagMachineOutput(GameLocation location)
        {
            if (location == null) return;

            foreach (var pair in location.Objects.Pairs)
            {
                var obj = pair.Value;

                // Skip objects without our owner tag
                if (!obj.modData.TryGetValue(OwnerKey, out var ownerId))
                    continue;

                // Check if this machine has ready output
                if (obj.heldObject?.Value == null)
                    continue;

                // Only tag if output isn't already tagged
                var output = obj.heldObject.Value;
                if (OwnershipTagHandler.HasOwner(output))
                    continue;

                // Pre-tag the output with the stored input owner
                OwnershipTagHandler.TagItem(output, ownerId);
                Handler?.LogTag("MACHINE-OUT", output.Name, ownerId,
                    $"pre-tagged from {obj.Name} at {pair.Key}");
            }
        }

        /*********
        ** Stacking Prevention Patch
        *********/
        /// <summary>
        /// Postfix on Item.canStackWith: Prevent items with different owners
        /// from stacking. Since the addItemToInventoryBool prefix tags items
        /// before stacking logic runs, both items should have owner tags.
        /// We still allow stacking when either is untagged as a safety net.
        /// </summary>
        private static void Item_CanStackWith_Postfix(
            Item __instance,
            ISalable other,
            ref bool __result)
        {
            try
            {
                // If already not stackable, leave it
                if (!__result)
                    return;

                // Both must be Items
                if (other is not Item otherItem)
                    return;

                var thisOwner = OwnershipTagHandler.GetOwnerID(__instance);
                var otherOwner = OwnershipTagHandler.GetOwnerID(otherItem);

                // Only block stacking when BOTH items have owner tags
                // and the tags are different. If either is untagged, allow
                // stacking — the catch-all tagger will handle it shortly.
                if (thisOwner != null && otherOwner != null && thisOwner != otherOwner)
                {
                    __result = false;
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Item_CanStackWith_Postfix: {ex}", LogLevel.Error);
            }
        }

        /*********
        ** Display Name Patch
        *********/
        /// <summary>
        /// Postfix on Object.DisplayName: Append the owner's name in
        /// parentheses for visual confirmation during testing.
        /// Example: "Parsnip" becomes "Parsnip (Stan)"
        /// </summary>
        private static void Object_DisplayName_Postfix(
            StardewValley.Object __instance,
            ref string __result)
        {
            try
            {
                // Check for owner tag
                if (__instance.modData.TryGetValue(OwnerKey, out var ownerId))
                {
                    string ownerName = OwnershipTagHandler.GetPlayerNameFromID(ownerId);
                    string suffix = $" ({ownerName})";

                    // Don't double-append
                    if (!__result.EndsWith(suffix))
                    {
                        __result += suffix;
                    }
                }
                // Check for common tag (future use)
                else if (__instance.modData.ContainsKey(OwnershipTagHandler.CommonKey))
                {
                    if (!__result.EndsWith(" (C)"))
                    {
                        __result += " (C)";
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_DisplayName_Postfix: {ex}", LogLevel.Error);
            }
        }

    }
}
