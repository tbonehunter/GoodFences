// Handlers/PastureZoneHandler.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Text.Json;
using StardewValley;
using StardewValley.Buildings;

namespace GoodFences.Handlers
{
    /// <summary>
    /// Manages automatic pasture protection zones for coops and barns.
    /// Zones are fixed rectangles south of building doors (12x12 for coops, 16x16 for barns).
    /// </summary>
    public static class PastureZoneHandler
    {
        // ModData key for storing pasture zone on buildings
        private const string PastureZoneKey = "GoodFences.PastureZone";

        // Default zone sizes (configurable via ModConfig)
        private static int CoopZoneSize => ModEntry.StaticConfig.CoopPastureSize;
        private static int BarnZoneSize => ModEntry.StaticConfig.BarnPastureSize;

        /// <summary>
        /// Represents a pasture protection zone
        /// </summary>
        public class PastureZone
        {
            public string BuildingType { get; set; } = "";  // "Coop" or "Barn"
            public int X { get; set; }                // Top-left X coordinate
            public int Y { get; set; }                // Top-left Y coordinate
            public int Width { get; set; }            // Zone width
            public int Height { get; set; }           // Zone height
            public long OwnerId { get; set; }         // Building owner's unique ID
        }

        /// <summary>
        /// Create pasture zone when building is placed
        /// </summary>
        public static void CreatePastureZone(Building building)
        {
            if (building == null)
                return;

            // Only create zones for coops and barns
            string buildingType = GetBuildingType(building);
            if (buildingType == null)
                return;

            // Get owner ID from building
            long ownerId = GetBuildingOwnerId(building);
            if (ownerId == 0)
            {
                ModEntry.Instance?.Monitor.Log($"Cannot create pasture zone: building has no owner", StardewModdingAPI.LogLevel.Warn);
                return;
            }

            // Calculate zone dimensions
            int zoneSize = buildingType == "Coop" ? CoopZoneSize : BarnZoneSize;
            
            // Zone starts at bottom-left of building (south edge) and extends southward
            int zoneX = building.tileX.Value;
            int zoneY = building.tileY.Value + building.tilesHigh.Value;
            
            // Ensure zone width matches building width or specified size (whichever is larger)
            int zoneWidth = Math.Max(building.tilesWide.Value, zoneSize);
            int zoneHeight = zoneSize;

            var zone = new PastureZone
            {
                BuildingType = buildingType,
                X = zoneX,
                Y = zoneY,
                Width = zoneWidth,
                Height = zoneHeight,
                OwnerId = ownerId
            };

            // Store zone in building modData
            SavePastureZone(building, zone);

            var owner = Game1.GetPlayer(ownerId);
            ModEntry.Instance?.Monitor.Log($"Created {buildingType} pasture zone: {zoneWidth}x{zoneHeight} at ({zoneX}, {zoneY}) for {owner?.Name ?? "Unknown"}", StardewModdingAPI.LogLevel.Debug);
            
            // Notify owner
            if (owner == Game1.player)
            {
                Game1.addHUDMessage(new HUDMessage($"{buildingType} pasture protected ({zoneWidth}x{zoneHeight} tiles)", HUDMessage.achievement_type));
            }
        }

        /// <summary>
        /// Remove pasture zone when building is demolished
        /// </summary>
        public static void RemovePastureZone(Building building)
        {
            if (building == null)
                return;

            if (building.modData.ContainsKey(PastureZoneKey))
            {
                building.modData.Remove(PastureZoneKey);
                ModEntry.Instance?.Monitor.Log($"Removed pasture zone for {building.buildingType.Value}", StardewModdingAPI.LogLevel.Debug);
            }
        }

        /// <summary>
        /// Update pasture zone when building is moved
        /// </summary>
        public static void UpdatePastureZone(Building building, Point newTileLocation)
        {
            if (building == null)
                return;

            var zone = GetPastureZone(building);
            if (zone == null)
                return;

            // Recalculate zone position based on new building location
            zone.X = newTileLocation.X;
            zone.Y = newTileLocation.Y + building.tilesHigh.Value;

            SavePastureZone(building, zone);
            ModEntry.Instance?.Monitor.Log($"Updated pasture zone to ({zone.X}, {zone.Y})", StardewModdingAPI.LogLevel.Debug);
        }

        /// <summary>
        /// Check if a tile is within any pasture protection zone on this location
        /// </summary>
        /// <param name="location">Game location to check</param>
        /// <param name="tile">Tile coordinates</param>
        /// <param name="ownerId">Output: owner ID if tile is in protected zone</param>
        /// <returns>True if tile is in a protected pasture zone</returns>
        public static bool IsInPastureZone(GameLocation location, Vector2 tile, out long ownerId)
        {
            ownerId = 0;

            if (location == null || !(location is Farm farm))
                return false;

            // Check all buildings on the farm
            foreach (var building in farm.buildings)
            {
                var zone = GetPastureZone(building);
                if (zone == null)
                    continue;

                // Check if tile is within zone bounds
                if (tile.X >= zone.X && tile.X < zone.X + zone.Width &&
                    tile.Y >= zone.Y && tile.Y < zone.Y + zone.Height)
                {
                    ownerId = zone.OwnerId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the rectangular bounds of a building's pasture zone
        /// </summary>
        public static Rectangle? GetPastureZoneBounds(Building building)
        {
            if (building == null)
                return null;

            var zone = GetPastureZone(building);
            if (zone == null)
                return null;

            return new Rectangle(zone.X * 64, zone.Y * 64, zone.Width * 64, zone.Height * 64);
        }

        /// <summary>
        /// Get pasture zone from building modData
        /// </summary>
        public static PastureZone GetPastureZone(Building building)
        {
            if (building == null || building.modData == null)
                return null;

            if (!building.modData.TryGetValue(PastureZoneKey, out string json) || string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<PastureZone>(json);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error deserializing pasture zone for {building.buildingType.Value}: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Save pasture zone to building modData
        /// </summary>
        private static void SavePastureZone(Building building, PastureZone zone)
        {
            if (building == null || building.modData == null || zone == null)
                return;

            try
            {
                string json = JsonSerializer.Serialize(zone);
                building.modData[PastureZoneKey] = json;
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error serializing pasture zone: {ex.Message}", StardewModdingAPI.LogLevel.Error);
            }
        }

        /// <summary>
        /// Get owner ID from building
        /// </summary>
        private static long GetBuildingOwnerId(Building building)
        {
            if (building == null || building.modData == null)
                return 0;

            if (building.modData.TryGetValue("GoodFences.Owner", out string ownerIdStr) && 
                long.TryParse(ownerIdStr, out long ownerId))
            {
                return ownerId;
            }

            return 0;
        }

        /// <summary>
        /// Determine if building is a coop or barn (or variant)
        /// </summary>
        private static string GetBuildingType(Building building)
        {
            if (building == null)
                return null;

            string type = building.buildingType.Value?.ToLower() ?? "";

            // Check for coop variants
            if (type.Contains("coop"))
                return "Coop";

            // Check for barn variants
            if (type.Contains("barn"))
                return "Barn";

            return null;
        }

        /// <summary>
        /// Debug method to show all pasture zones
        /// </summary>
        public static void DebugShowAllPastures()
        {
            ModEntry.Instance?.Monitor.Log("=== ALL PASTURE ZONES ===", StardewModdingAPI.LogLevel.Info);

            if (Game1.getFarm() is Farm farm)
            {
                foreach (var building in farm.buildings)
                {
                    var zone = GetPastureZone(building);
                    if (zone != null)
                    {
                        var owner = Game1.GetPlayer(zone.OwnerId);
                        ModEntry.Instance?.Monitor.Log($"{building.buildingType.Value} at ({building.tileX.Value}, {building.tileY.Value})", StardewModdingAPI.LogLevel.Info);
                        ModEntry.Instance?.Monitor.Log($"  Owner: {owner?.Name ?? "Unknown"}", StardewModdingAPI.LogLevel.Info);
                        ModEntry.Instance?.Monitor.Log($"  Zone: {zone.Width}x{zone.Height} at ({zone.X}, {zone.Y})", StardewModdingAPI.LogLevel.Info);
                    }
                }
            }
        }

        /// <summary>
        /// Scan all existing buildings and create missing pasture zones (for existing saves)
        /// </summary>
        public static void InitializeExistingBuildings()
        {
            if (Game1.getFarm() is Farm farm)
            {
                int created = 0;
                foreach (var building in farm.buildings)
                {
                    // Only create if building is a coop/barn and doesn't have a zone
                    if (GetBuildingType(building) != null && GetPastureZone(building) == null)
                    {
                        CreatePastureZone(building);
                        created++;
                    }
                }

                if (created > 0)
                {
                    ModEntry.Instance?.Monitor.Log($"Initialized pasture zones for {created} existing building(s)", StardewModdingAPI.LogLevel.Info);
                }
            }
        }
    }
}


