// Handlers/CommonChestHandler.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace GoodFences.Handlers
{
    /// <summary>
    /// Manages common chests that are accessible to all players.
    /// Common chests bypass ownership checks and allow shared storage.
    /// </summary>
    public static class CommonChestHandler
    {
        // ModData keys
        private const string CommonChestFlagKey = "GoodFences.Common";
        private const string CommonChestListKey = "GoodFences.CommonChests";

        /// <summary>
        /// Initialize common chest system - create initial chest for new multiplayer games
        /// </summary>
        public static void InitializeCommonChest()
        {
            if (!Context.IsMultiplayer || !ModEntry.StaticConfig.CreateInitialCommonChest)
                return;

            // Only create if no common chests exist yet
            var existingCommonChests = GetAllCommonChests();
            if (existingCommonChests.Count > 0)
            {
                ModEntry.Instance?.Monitor.Log($"Common chest system already initialized ({existingCommonChests.Count} chest(s))", StardewModdingAPI.LogLevel.Debug);
                return;
            }

            // Place initial common chest near shipping bin
            if (Game1.getFarm() is Farm farm)
            {
                Vector2 chestLocation = new Vector2(71, 14); // Near shipping bin
                
                // Check if location is already occupied
                if (farm.objects.ContainsKey(chestLocation))
                {
                    // Try alternate locations
                    Vector2[] alternateLocations = new[]
                    {
                        new Vector2(72, 14),
                        new Vector2(71, 15),
                        new Vector2(70, 14),
                        new Vector2(71, 13)
                    };

                    bool placed = false;
                    foreach (var loc in alternateLocations)
                    {
                        if (!farm.objects.ContainsKey(loc))
                        {
                            chestLocation = loc;
                            placed = true;
                            break;
                        }
                    }

                    if (!placed)
                    {
                        ModEntry.Instance?.Monitor.Log("Could not find empty location for initial common chest", StardewModdingAPI.LogLevel.Warn);
                        return;
                    }
                }

                // Create the chest (Stone Chest, ID 130)
                Chest commonChest = new Chest(true, chestLocation)
                {
                    Name = "Common Chest"
                };
                
                // Mark as common
                DesignateCommonChest(commonChest);
                
                // Place on farm
                farm.objects.Add(chestLocation, commonChest);
                
                ModEntry.Instance?.Monitor.Log($"Created initial common chest at ({chestLocation.X}, {chestLocation.Y})", StardewModdingAPI.LogLevel.Info);
                
                // Notify all players
                Game1.addHUDMessage(new HUDMessage("Common chest available near shipping bin", HUDMessage.achievement_type));
            }
        }

        /// <summary>
        /// Designate an existing chest as common (host only)
        /// </summary>
        public static void DesignateCommonChest(Chest chest)
        {
            if (chest == null)
                return;

            // Mark chest as common
            chest.modData[CommonChestFlagKey] = "true";
            
            // Update chest name to indicate common status
            if (!chest.Name.Contains("Common"))
            {
                chest.Name = chest.Name + " (Common)";
            }

            // Add to global list
            AddToCommonChestList(chest);

            ModEntry.Instance?.Monitor.Log($"Designated chest as common: {GetChestGuid(chest)}", StardewModdingAPI.LogLevel.Debug);
        }

        /// <summary>
        /// Remove common status from a chest (host only)
        /// </summary>
        public static void UndesignateCommonChest(Chest chest)
        {
            if (chest == null)
                return;

            // Remove common flag
            if (chest.modData.ContainsKey(CommonChestFlagKey))
            {
                chest.modData.Remove(CommonChestFlagKey);
            }

            // Update chest name
            if (chest.Name.Contains(" (Common)"))
            {
                chest.Name = chest.Name.Replace(" (Common)", "");
            }

            // Remove from global list
            RemoveFromCommonChestList(chest);

            ModEntry.Instance?.Monitor.Log($"Removed common status from chest: {GetChestGuid(chest)}", StardewModdingAPI.LogLevel.Debug);
        }

        /// <summary>
        /// Check if a chest is designated as common
        /// </summary>
        public static bool IsCommonChest(Chest chest)
        {
            if (chest == null || chest.modData == null)
                return false;

            return chest.modData.TryGetValue(CommonChestFlagKey, out string value) && value == "true";
        }

        /// <summary>
        /// Get all common chests in the game
        /// </summary>
        public static List<Chest> GetAllCommonChests()
        {
            var commonChests = new List<Chest>();

            // Scan all locations for common chests
            foreach (var location in Game1.locations)
            {
                // Check objects in location
                if (location.objects != null)
                {
                    foreach (var obj in location.objects.Values)
                    {
                        if (obj is Chest chest && IsCommonChest(chest))
                        {
                            commonChests.Add(chest);
                        }
                    }
                }

                // Check inside buildings
                if (location is Farm farm)
                {
                    foreach (var building in farm.buildings)
                    {
                        if (building.indoors.Value != null && building.indoors.Value.objects != null)
                        {
                            foreach (var obj in building.indoors.Value.objects.Values)
                            {
                                if (obj is Chest chest && IsCommonChest(chest))
                                {
                                    commonChests.Add(chest);
                                }
                            }
                        }
                    }
                }
            }

            return commonChests;
        }

        /// <summary>
        /// Get unique identifier for a chest
        /// </summary>
        private static string GetChestGuid(Chest chest)
        {
            if (chest == null)
                return null;

            // Try to get existing GUID from modData
            if (chest.modData.TryGetValue("GoodFences.ChestGuid", out string guid))
                return guid;

            // Generate new GUID
            guid = Guid.NewGuid().ToString();
            chest.modData["GoodFences.ChestGuid"] = guid;
            return guid;
        }

        /// <summary>
        /// Add chest to global common chest list
        /// </summary>
        private static void AddToCommonChestList(Chest chest)
        {
            if (chest == null)
                return;

            string guid = GetChestGuid(chest);
            var commonChestGuids = GetCommonChestGuids();

            if (!commonChestGuids.Contains(guid))
            {
                commonChestGuids.Add(guid);
                SaveCommonChestGuids(commonChestGuids);
            }
        }

        /// <summary>
        /// Remove chest from global common chest list
        /// </summary>
        private static void RemoveFromCommonChestList(Chest chest)
        {
            if (chest == null)
                return;

            string guid = GetChestGuid(chest);
            var commonChestGuids = GetCommonChestGuids();

            if (commonChestGuids.Contains(guid))
            {
                commonChestGuids.Remove(guid);
                SaveCommonChestGuids(commonChestGuids);
            }
        }

        /// <summary>
        /// Get list of common chest GUIDs from save data
        /// </summary>
        private static List<string> GetCommonChestGuids()
        {
            if (Game1.player?.modData == null)
                return new List<string>();

            if (!Game1.player.modData.TryGetValue(CommonChestListKey, out string json) || string.IsNullOrEmpty(json))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error deserializing common chest list: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return new List<string>();
            }
        }

        /// <summary>
        /// Save list of common chest GUIDs to save data
        /// </summary>
        private static void SaveCommonChestGuids(List<string> guids)
        {
            if (Game1.player?.modData == null)
                return;

            try
            {
                string json = JsonSerializer.Serialize(guids);
                Game1.player.modData[CommonChestListKey] = json;
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error serializing common chest list: {ex.Message}", StardewModdingAPI.LogLevel.Error);
            }
        }

        /// <summary>
        /// Show chest designation menu (host only)
        /// </summary>
        public static void ShowChestDesignationMenu(Chest chest)
        {
            if (chest == null)
                return;

            // Only host can designate common chests
            if (!Game1.player.IsMainPlayer)
            {
                Game1.addHUDMessage(new HUDMessage("Only the host can designate common chests", HUDMessage.error_type));
                return;
            }

            bool isCommon = IsCommonChest(chest);

            var responses = new List<Response>();

            if (isCommon)
            {
                responses.Add(new Response("RemoveCommon", "Remove Common Status"));
            }
            else
            {
                responses.Add(new Response("MakeCommon", "Make Common Chest"));
            }
            
            responses.Add(new Response("Cancel", "Cancel"));

            Game1.currentLocation.createQuestionDialogue(
                isCommon ? "This is a common chest. Remove common status?" : "Make this chest accessible to all players?",
                responses.ToArray(),
                (who, whichAnswer) =>
                {
                    if (whichAnswer == "MakeCommon")
                    {
                        DesignateCommonChest(chest);
                        Game1.addHUDMessage(new HUDMessage("Chest designated as common", HUDMessage.achievement_type));
                    }
                    else if (whichAnswer == "RemoveCommon")
                    {
                        UndesignateCommonChest(chest);
                        Game1.addHUDMessage(new HUDMessage("Removed common status from chest", HUDMessage.newQuest_type));
                    }
                }
            );
        }

        /// <summary>
        /// Debug method to list all common chests
        /// </summary>
        public static void DebugShowAllCommonChests()
        {
            ModEntry.Instance?.Monitor.Log("=== ALL COMMON CHESTS ===", StardewModdingAPI.LogLevel.Info);

            var commonChests = GetAllCommonChests();
            ModEntry.Instance?.Monitor.Log($"Found {commonChests.Count} common chest(s)", StardewModdingAPI.LogLevel.Info);

            foreach (var chest in commonChests)
            {
                string location = "Unknown";
                Vector2? position = null;

                // Find chest location
                foreach (var loc in Game1.locations)
                {
                    foreach (var kvp in loc.objects.Pairs)
                    {
                        if (kvp.Value == chest)
                        {
                            location = loc.Name;
                            position = kvp.Key;
                            break;
                        }
                    }
                    if (position.HasValue)
                        break;
                }

                ModEntry.Instance?.Monitor.Log($"  - {chest.Name} at {location} {(position.HasValue ? $"({position.Value.X}, {position.Value.Y})" : "")}", StardewModdingAPI.LogLevel.Info);
            }
        }
    }
}



