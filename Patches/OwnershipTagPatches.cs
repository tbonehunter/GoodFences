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
    ///
    /// Also includes universal tool-strike protection for any tagged object.
    /// Without this, ownership tags are meaningless because anyone can
    /// destroy/pick up tagged objects with an axe or pickaxe.
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

            // Postfix: Clear PendingHarvestOwner and clear soil if crop consumed
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

            // ── Object Placement Tagging ─────────────────────────────────
            // Tag objects (chests, machines, craftables) when placed in world.
            // Without this, placed objects have no owner and enforcement
            // falls through to "no owner = allow".
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object),
                    nameof(StardewValley.Object.placementAction)),
                postfix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Object_PlacementAction_Postfix))
            );

            // ── Universal Tool-Strike Protection ─────────────────────────
            // Block non-owners from destroying/picking up owned objects.
            // This protects chests, machines, and any other tagged object
            // from being struck with axe/pickaxe/hoe by non-owners.
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object),
                    nameof(StardewValley.Object.performToolAction)),
                prefix: new HarmonyMethod(typeof(OwnershipTagPatches), nameof(Object_PerformToolAction_Prefix))
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

                // Only tag soil if it doesn't already have an owner.
                // Fertilizer application also calls HoeDirt.plant(), but
                // the soil already has an owner from the original seed
                // planting — don't overwrite it.
                if (OwnershipTagHandler.GetSoilOwnerID(__instance) == null)
                {
                    OwnershipTagHandler.TagSoil(__instance, who);
                }

                // Only tag the crop if it doesn't already have an owner.
                // A new crop from seed planting has no tag yet. An existing
                // crop during fertilizer application already has the
                // planter's tag — don't overwrite it.
                if (__instance.crop != null && OwnershipTagHandler.GetCropOwnerID(__instance.crop) == null)
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
        /// harvest completes. If the crop was fully consumed (non-regrow),
        /// also clear soil ownership so the tile is open for anyone to replant.
        /// Per design: soil with no growing crop is open to any player.
        /// </summary>
        private static void Crop_Harvest_Postfix(
            Crop __instance,
            bool __result,
            HoeDirt soil)
        {
            try
            {
                // Clear pending harvest regardless of success
                PendingHarvestOwner = null;

                // If harvest succeeded and the crop was removed from soil,
                // clear soil ownership so the tile is open for replanting.
                // For regrow crops, soil.crop is still set — soil stays owned.
                if (__result && soil != null && soil.crop == null)
                {
                    OwnershipTagHandler.ClearSoilOwner(soil);

                    if (ModEntry.StaticConfig.DebugMode)
                    {
                        Monitor?.Log("[TAG] SOIL-CLEAR: Crop harvested and consumed, soil now open", LogLevel.Debug);
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Crop_Harvest_Postfix: {ex}", LogLevel.Error);
            }
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
        ** Object Placement Tagging Patch
        *********/
        /// <summary>
        /// Postfix on Object.placementAction: When a player places an object
        /// in the world (chest, machine, craftable, etc.), tag the newly
        /// placed world object with the placer's ownership ID.
        ///
        /// Note: The __instance is the inventory item being consumed. The
        /// actual placed object is a NEW instance at the target tile in
        /// location.objects. We must look it up by position.
        /// </summary>
        private static void Object_PlacementAction_Postfix(
            StardewValley.Object __instance,
            bool __result,
            GameLocation location,
            int x,
            int y,
            Farmer who)
        {
            try
            {
                // Placement failed — nothing to tag
                if (!__result)
                    return;

                // Skip if not multiplayer
                if (!Context.IsMultiplayer)
                    return;

                var placer = who ?? Game1.player;
                if (placer == null)
                    return;

                // Convert pixel coordinates to tile coordinates
                Vector2 tilePos = new Vector2(x / 64, y / 64);

                // Find the newly placed object at this tile
                if (location?.objects != null && location.objects.TryGetValue(tilePos, out var placedObj))
                {
                    // Only tag if not already tagged (avoid overwriting)
                    if (!placedObj.modData.ContainsKey(OwnerKey))
                    {
                        OwnershipTagHandler.TagPlacedObject(placedObj, placer);
                        Handler?.LogTag("PLACED", placedObj.Name, placer,
                            $"at ({tilePos.X},{tilePos.Y}) in {location.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_PlacementAction_Postfix: {ex}", LogLevel.Error);
            }
        }

        /*********
        ** Universal Tool-Strike Protection Patch
        *********/
        /// <summary>
        /// Prefix on Object.performToolAction: Block non-owners from
        /// destroying or picking up owned objects with tools (axe, pickaxe,
        /// hoe, etc.). Without this, anyone can break ownership by striking
        /// a tagged object — the item drops with no owner tag, and the
        /// attacker picks it up and becomes the owner.
        ///
        /// Checks EnforceChestOwnership for chests, EnforceMachineOwnership
        /// for machines/craftables. If neither flag applies, allows the action.
        /// </summary>
        public static bool Object_PerformToolAction_Prefix(
            StardewValley.Object __instance,
            ref bool __result)
        {
            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            try
            {
                // Get the tool user
                var user = Game1.player;
                if (user == null)
                    return true;

                // Get object owner
                string? ownerIdStr = OwnershipTagHandler.GetObjectOwnerID(__instance);

                // No owner = allow (untagged, pre-mod object)
                if (ownerIdStr == null)
                    return true;

                if (!long.TryParse(ownerIdStr, out long ownerId))
                    return true;

                // Owner hitting own object = allow
                if (user.UniqueMultiplayerID == ownerId)
                    return true;

                // Check if common
                if (__instance.modData.ContainsKey(OwnershipTagHandler.CommonKey))
                    return true;

                // Determine which enforcement flag applies
                bool isChest = __instance is Chest;
                bool isBigCraftable = __instance.bigCraftable.Value;

                // If it's a chest, check chest enforcement flag
                if (isChest && !ModEntry.StaticConfig.EnforceChestOwnership)
                    return true;

                // If it's a big craftable (machine), check machine enforcement flag
                if (!isChest && isBigCraftable && !ModEntry.StaticConfig.EnforceMachineOwnership)
                    return true;

                // For non-chest, non-big-craftable objects: if neither flag
                // covers it, still protect it if it has an owner tag.
                // (This catches edge cases like placed furniture, etc.)

                // Check trust — allow if user has trust from owner for the
                // relevant category
                var owner = Game1.GetPlayer(ownerId);
                if (owner != null)
                {
                    var permType = isChest
                        ? TrustSystemHandler.PermissionType.Chests
                        : TrustSystemHandler.PermissionType.Machines;

                    if (TrustSystemHandler.HasTrust(owner, user, permType))
                        return true;
                }

                // Block the tool action
                string ownerName = owner?.Name ?? "another player";
                Game1.addHUDMessage(new HUDMessage($"This belongs to {ownerName}", HUDMessage.error_type));

                if (ModEntry.StaticConfig.DebugMode)
                {
                    Monitor?.Log($"Blocked {user.Name} from striking {ownerName}'s {__instance.Name}", LogLevel.Debug);
                }

                __result = false;
                return false; // Skip original — object is protected
            }
            catch (Exception ex)
            {
                Monitor?.Log($"[PATCH] Error in Object_PerformToolAction_Prefix: {ex}", LogLevel.Error);
                return true; // Allow on error to prevent game breaking
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
