// Patches/MachineEnforcementPatches.cs
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using GoodFences.Handlers;
using SObject = StardewValley.Object;

namespace GoodFences.Patches
{
    /// <summary>
    /// Harmony patches to enforce machine ownership.
    /// Blocks non-owners from using machines without trust permission.
    /// </summary>
    [HarmonyPatch]
    public static class MachineEnforcementPatches
    {
        /// <summary>
        /// Patch Object.performObjectDropInAction to check ownership before allowing item insertion
        /// </summary>
        [HarmonyPatch(typeof(SObject), nameof(SObject.performObjectDropInAction))]
        [HarmonyPrefix]
        public static bool Object_PerformObjectDropInAction_Prefix(
            SObject __instance,
            Item dropInItem,
            bool probe,
            Farmer who,
            ref bool __result)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceMachineOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            // Skip if this isn't a machine or if it's just a probe
            if (!IsMachine(__instance) || probe)
                return true;

            try
            {
                // Get the user
                var user = who ?? Game1.player;
                if (user == null)
                    return true;

                // Check if user has permission to use this machine
                if (CanUseMachine(__instance, user, out string blockMessage))
                    return true;

                // Block machine use
                if (!string.IsNullOrEmpty(blockMessage))
                {
                    Game1.addHUDMessage(new HUDMessage(blockMessage, HUDMessage.error_type));
                }

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from inserting item into machine", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false; // Block insertion
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Object_PerformObjectDropInAction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Patch Object.checkForAction to check ownership before allowing collection/interaction
        /// </summary>
        [HarmonyPatch(typeof(SObject), nameof(SObject.checkForAction))]
        [HarmonyPrefix]
        public static bool Object_CheckForAction_Prefix(
            SObject __instance,
            Farmer who,
            ref bool __result)
        {
            // Skip if enforcement disabled
            if (!ModEntry.StaticConfig.EnforceMachineOwnership)
                return true;

            // Skip if not multiplayer
            if (!Context.IsMultiplayer)
                return true;

            // Skip if this isn't a machine
            if (!IsMachine(__instance))
                return true;

            // Skip if machine isn't ready (no output to collect)
            if (!__instance.readyForHarvest.Value)
                return true;

            try
            {
                // Get the user
                var user = who ?? Game1.player;
                if (user == null)
                    return true;

                // Check if user has permission to use this machine
                if (CanUseMachine(__instance, user, out string blockMessage))
                    return true;

                // Block machine collection
                if (!string.IsNullOrEmpty(blockMessage))
                {
                    Game1.addHUDMessage(new HUDMessage(blockMessage, HUDMessage.error_type));
                }

                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Blocked {user.Name} from collecting from machine", StardewModdingAPI.LogLevel.Debug);

                __result = false;
                return false; // Block collection
            }
            catch (System.Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error in Object_CheckForAction_Prefix: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Check if user can use a machine
        /// </summary>
        private static bool CanUseMachine(SObject machine, Farmer user, out string blockMessage)
        {
            blockMessage = null!;

            if (machine == null || user == null)
                return true;

            // Get machine owner
            long ownerId = GetMachineOwnerId(machine);

            // No owner = allow (untagged machine, pre-mod ownership)
            if (ownerId == 0)
            {
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Machine has no owner, allowing use", StardewModdingAPI.LogLevel.Trace);
                return true;
            }

            // Owner using own machine = allow
            if (user.UniqueMultiplayerID == ownerId)
                return true;

            // Check if machine is marked as common
            if (IsCommonMachine(machine))
            {
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"Common machine, allowing use", StardewModdingAPI.LogLevel.Trace);
                return true;
            }

            // Check if machine is inside a building owned by the user
            var buildingOwner = GetContainingBuildingOwner(machine);
            if (buildingOwner != null && buildingOwner.UniqueMultiplayerID == user.UniqueMultiplayerID)
            {
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"{user.Name} owns the building, allowing machine use", StardewModdingAPI.LogLevel.Trace);
                return true;
            }

            // Check if user has trust permission for machines
            var owner = Game1.GetPlayer(ownerId);
            if (owner != null && TrustSystemHandler.HasTrust(owner, user, TrustSystemHandler.PermissionType.Machines))
            {
                if (ModEntry.StaticConfig.DebugMode)
                    ModEntry.Instance?.Monitor.Log($"{user.Name} has machine trust from {owner?.Name}, allowing use", StardewModdingAPI.LogLevel.Trace);
                return true;
            }

            // Block access
            string ownerName = owner?.Name ?? "another player";
            blockMessage = $"This machine belongs to {ownerName}";
            return false;
        }

        /// <summary>
        /// Get the owner ID of a machine from modData
        /// </summary>
        private static long GetMachineOwnerId(SObject machine)
        {
            if (machine?.modData == null)
                return 0;

            if (machine.modData.TryGetValue("GoodFences.Owner", out string ownerIdStr) &&
                long.TryParse(ownerIdStr, out long ownerId))
            {
                return ownerId;
            }

            return 0;
        }

        /// <summary>
        /// Check if machine is marked as common
        /// </summary>
        private static bool IsCommonMachine(SObject machine)
        {
            if (machine?.modData == null)
                return false;

            return machine.modData.TryGetValue("GoodFences.Common", out string value) && value == "true";
        }

        /// <summary>
        /// Check if object is a machine (has processing functionality)
        /// </summary>
        private static bool IsMachine(SObject obj)
        {
            if (obj == null)
                return false;

            // Check if it's a craftable that processes items
            if (!obj.bigCraftable.Value)
                return false;

            // Common machines: Keg, Preserves Jar, Furnace, Crystalarium, Mayonnaise Machine, 
            // Cheese Press, Loom, Oil Maker, Bee House, etc.
            // We check if it has the key methods for a machine
            return obj.heldObject.Value != null || obj.MinutesUntilReady > 0 || obj.readyForHarvest.Value;
        }

        /// <summary>
        /// Get owner of the building containing this machine (if any)
        /// </summary>
        private static Farmer? GetContainingBuildingOwner(SObject machine)
        {
            if (machine == null)
                return null;

            var currentLocation = Game1.currentLocation;
            if (currentLocation == null)
                return null;

            // Check if current location is inside a building
            if (currentLocation is Farm)
            {
                // Machine is on the farm itself, not in a building
                return null;
            }

            // Check all buildings to find which one contains this location
            foreach (var location in Game1.locations)
            {
                if (location is Farm farm)
                {
                    foreach (var building in farm.buildings)
                    {
                        if (building.indoors.Value == currentLocation)
                        {
                            // Found the building, get its owner
                            long buildingOwnerId = GetBuildingOwnerId(building);
                            if (buildingOwnerId != 0)
                            {
                                return Game1.GetPlayer(buildingOwnerId);
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get building owner ID from modData
        /// </summary>
        private static long GetBuildingOwnerId(StardewValley.Buildings.Building building)
        {
            if (building?.modData == null)
                return 0;

            if (building.modData.TryGetValue("GoodFences.Owner", out string ownerIdStr) &&
                long.TryParse(ownerIdStr, out long ownerId))
            {
                return ownerId;
            }

            return 0;
        }
    }
}


