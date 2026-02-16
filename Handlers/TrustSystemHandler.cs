// Handlers/TrustSystemHandler.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewValley;

namespace GoodFences.Handlers
{
    /// <summary>
    /// Manages trust relationships between players for resource sharing.
    /// Trust data is stored in player modData and persists through save/load.
    /// </summary>
    public static class TrustSystemHandler
    {
        // ModData key for storing trust records
        private const string TrustDataKey = "GoodFences.TrustedPlayers";

        /// <summary>
        /// Types of permissions that can be granted
        /// </summary>
        [Flags]
        public enum PermissionType
        {
            None = 0,
            Crops = 1,
            Machines = 2,
            Chests = 4,
            Animals = 8,
            Buildings = 16
        }

        /// <summary>
        /// Represents a trust relationship with another player
        /// </summary>
        public class TrustedPlayerRecord
        {
            public long PlayerId { get; set; }              // Trusted player's unique ID
            public string GrantedDate { get; set; } = "";  // In-game date permission granted (e.g., "spring 1")
            public int ExpirationDays { get; set; }         // 0 = until revoked, >0 = days until expiration
            public PermissionFlags Permissions { get; set; } = new PermissionFlags(); // What they can access
        }

        /// <summary>
        /// Detailed permission settings
        /// </summary>
        public class PermissionFlags
        {
            public bool Crops { get; set; }
            public bool Machines { get; set; }
            public List<string> Chests { get; set; } = new List<string>(); // Chest GUIDs
            public bool Animals { get; set; }
            public bool Buildings { get; set; }
        }

        /// <summary>
        /// Grant trust to another player with specified permissions and duration
        /// </summary>
        /// <param name="granter">Player granting trust (owner)</param>
        /// <param name="trustee">Player receiving trust</param>
        /// <param name="days">Days until expiration (0 = until manually revoked)</param>
        /// <param name="permissions">Permission flags to grant</param>
        public static void GrantTrust(Farmer granter, Farmer trustee, int days, PermissionFlags permissions)
        {
            if (granter == null || trustee == null)
            {
                ModEntry.Instance?.Monitor.Log("GrantTrust: granter or trustee is null", StardewModdingAPI.LogLevel.Warn);
                return;
            }

            if (granter.UniqueMultiplayerID == trustee.UniqueMultiplayerID)
            {
                ModEntry.Instance?.Monitor.Log("GrantTrust: cannot grant trust to yourself", StardewModdingAPI.LogLevel.Warn);
                return;
            }

            var trustedPlayers = GetTrustedPlayers(granter);

            // Check if trust already exists for this player
            var existingTrust = trustedPlayers.FirstOrDefault(t => t.PlayerId == trustee.UniqueMultiplayerID);
            if (existingTrust != null)
            {
                // Update existing trust
                existingTrust.GrantedDate = Game1.Date.ToString();
                existingTrust.ExpirationDays = days;
                existingTrust.Permissions = permissions;
                ModEntry.Instance?.Monitor.Log($"Updated trust: {granter.Name} -> {trustee.Name}, expires in {days} days", StardewModdingAPI.LogLevel.Debug);
            }
            else
            {
                // Create new trust record
                var newTrust = new TrustedPlayerRecord
                {
                    PlayerId = trustee.UniqueMultiplayerID,
                    GrantedDate = Game1.Date.ToString(),
                    ExpirationDays = days,
                    Permissions = permissions
                };
                trustedPlayers.Add(newTrust);
                ModEntry.Instance?.Monitor.Log($"Granted trust: {granter.Name} -> {trustee.Name}, expires in {days} days", StardewModdingAPI.LogLevel.Debug);
            }

            // Save updated trust list
            SaveTrustedPlayers(granter, trustedPlayers);

            // Notify players
            string expirationText = days == 0 ? "until revoked" : $"for {days} day(s)";
            Game1.addHUDMessage(new HUDMessage($"Granted access to {trustee.Name} {expirationText}", HUDMessage.newQuest_type));
        }

        /// <summary>
        /// Grant trust to a specific chest
        /// </summary>
        public static void GrantChestTrust(Farmer granter, Farmer trustee, string chestGuid, int days)
        {
            if (granter == null || trustee == null || string.IsNullOrEmpty(chestGuid))
            {
                ModEntry.Instance?.Monitor.Log("GrantChestTrust: invalid parameters", StardewModdingAPI.LogLevel.Warn);
                return;
            }

            var trustedPlayers = GetTrustedPlayers(granter);
            var existingTrust = trustedPlayers.FirstOrDefault(t => t.PlayerId == trustee.UniqueMultiplayerID);

            if (existingTrust != null)
            {
                // Add chest to existing trust
                if (!existingTrust.Permissions.Chests.Contains(chestGuid))
                {
                    existingTrust.Permissions.Chests.Add(chestGuid);
                    ModEntry.Instance?.Monitor.Log($"Added chest trust: {granter.Name} -> {trustee.Name} for chest {chestGuid}", StardewModdingAPI.LogLevel.Debug);
                }
            }
            else
            {
                // Create new trust with just chest access
                var newTrust = new TrustedPlayerRecord
                {
                    PlayerId = trustee.UniqueMultiplayerID,
                    GrantedDate = Game1.Date.ToString(),
                    ExpirationDays = days,
                    Permissions = new PermissionFlags()
                };
                newTrust.Permissions.Chests.Add(chestGuid);
                trustedPlayers.Add(newTrust);
                ModEntry.Instance?.Monitor.Log($"Granted chest trust: {granter.Name} -> {trustee.Name} for chest {chestGuid}", StardewModdingAPI.LogLevel.Debug);
            }

            SaveTrustedPlayers(granter, trustedPlayers);
            Game1.addHUDMessage(new HUDMessage($"Granted {trustee.Name} access to chest", HUDMessage.newQuest_type));
        }

        /// <summary>
        /// Revoke all trust from a player
        /// </summary>
        public static void RevokeTrust(Farmer granter, Farmer trustee)
        {
            if (granter == null || trustee == null)
            {
                ModEntry.Instance?.Monitor.Log("RevokeTrust: granter or trustee is null", StardewModdingAPI.LogLevel.Warn);
                return;
            }

            var trustedPlayers = GetTrustedPlayers(granter);
            var trustToRemove = trustedPlayers.FirstOrDefault(t => t.PlayerId == trustee.UniqueMultiplayerID);

            if (trustToRemove != null)
            {
                trustedPlayers.Remove(trustToRemove);
                SaveTrustedPlayers(granter, trustedPlayers);
                ModEntry.Instance?.Monitor.Log($"Revoked trust: {granter.Name} -> {trustee.Name}", StardewModdingAPI.LogLevel.Debug);
                Game1.addHUDMessage(new HUDMessage($"Revoked access from {trustee.Name}", HUDMessage.newQuest_type));
            }
        }

        /// <summary>
        /// Revoke trust for a specific chest
        /// </summary>
        public static void RevokeChestTrust(Farmer granter, Farmer trustee, string chestGuid)
        {
            if (granter == null || trustee == null || string.IsNullOrEmpty(chestGuid))
            {
                ModEntry.Instance?.Monitor.Log("RevokeChestTrust: invalid parameters", StardewModdingAPI.LogLevel.Warn);
                return;
            }

            var trustedPlayers = GetTrustedPlayers(granter);
            var existingTrust = trustedPlayers.FirstOrDefault(t => t.PlayerId == trustee.UniqueMultiplayerID);

            if (existingTrust != null && existingTrust.Permissions.Chests.Contains(chestGuid))
            {
                existingTrust.Permissions.Chests.Remove(chestGuid);
                SaveTrustedPlayers(granter, trustedPlayers);
                ModEntry.Instance?.Monitor.Log($"Revoked chest trust: {granter.Name} -> {trustee.Name} for chest {chestGuid}", StardewModdingAPI.LogLevel.Debug);
                Game1.addHUDMessage(new HUDMessage($"Revoked {trustee.Name}'s access to chest", HUDMessage.newQuest_type));
            }
        }

        /// <summary>
        /// Check if accessor has trust from owner for specific permission type
        /// </summary>
        public static bool HasTrust(Farmer owner, Farmer accessor, PermissionType permissionType)
        {
            if (owner == null || accessor == null)
                return false;

            // Owner always has access to own resources
            if (owner.UniqueMultiplayerID == accessor.UniqueMultiplayerID)
                return true;

            var trustedPlayers = GetTrustedPlayers(owner);
            var trust = trustedPlayers.FirstOrDefault(t => t.PlayerId == accessor.UniqueMultiplayerID);

            if (trust == null)
                return false;

            // Check if trust has expired
            if (trust.ExpirationDays > 0)
            {
                int daysSinceGranted = GetDaysSince(trust.GrantedDate);
                if (daysSinceGranted >= trust.ExpirationDays)
                {
                    ModEntry.Instance?.Monitor.Log($"Trust expired: {owner.Name} -> {accessor.Name}", StardewModdingAPI.LogLevel.Debug);
                    return false;
                }
            }

            // Check specific permission
            switch (permissionType)
            {
                case PermissionType.Crops:
                    return trust.Permissions.Crops;
                case PermissionType.Machines:
                    return trust.Permissions.Machines;
                case PermissionType.Animals:
                    return trust.Permissions.Animals;
                case PermissionType.Buildings:
                    return trust.Permissions.Buildings;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if accessor has trust for a specific chest
        /// </summary>
        public static bool HasChestTrust(Farmer owner, Farmer accessor, string chestGuid)
        {
            if (owner == null || accessor == null || string.IsNullOrEmpty(chestGuid))
                return false;

            // Owner always has access to own resources
            if (owner.UniqueMultiplayerID == accessor.UniqueMultiplayerID)
                return true;

            var trustedPlayers = GetTrustedPlayers(owner);
            var trust = trustedPlayers.FirstOrDefault(t => t.PlayerId == accessor.UniqueMultiplayerID);

            if (trust == null)
                return false;

            // Check if trust has expired
            if (trust.ExpirationDays > 0)
            {
                int daysSinceGranted = GetDaysSince(trust.GrantedDate);
                if (daysSinceGranted >= trust.ExpirationDays)
                    return false;
            }

            return trust.Permissions.Chests.Contains(chestGuid);
        }

        /// <summary>
        /// Update trust expirations for all players - called daily
        /// </summary>
        public static void UpdateTrustExpirations()
        {
            foreach (var farmer in Game1.getAllFarmers())
            {
                var trustedPlayers = GetTrustedPlayers(farmer);
                var expiredTrusts = new List<TrustedPlayerRecord>();

                foreach (var trust in trustedPlayers)
                {
                    if (trust.ExpirationDays > 0)
                    {
                        int daysSinceGranted = GetDaysSince(trust.GrantedDate);
                        if (daysSinceGranted >= trust.ExpirationDays)
                        {
                            expiredTrusts.Add(trust);
                            
                            // Notify owner if they're online
                            if (farmer == Game1.player)
                            {
                                var trustedPlayer = Game1.GetPlayer(trust.PlayerId);
                                string playerName = trustedPlayer?.Name ?? "Unknown";
                                Game1.addHUDMessage(new HUDMessage($"{playerName}'s access has expired", HUDMessage.error_type));
                            }
                        }
                    }
                }

                // Remove expired trusts
                if (expiredTrusts.Count > 0)
                {
                    foreach (var expired in expiredTrusts)
                    {
                        trustedPlayers.Remove(expired);
                        var trustedPlayer = Game1.GetPlayer(expired.PlayerId);
                        ModEntry.Instance?.Monitor.Log($"Removed expired trust: {farmer.Name} -> {trustedPlayer?.Name ?? "Unknown"}", StardewModdingAPI.LogLevel.Debug);
                    }
                    SaveTrustedPlayers(farmer, trustedPlayers);
                }
            }
        }

        /// <summary>
        /// Get all trusted players for a given owner
        /// </summary>
        public static List<TrustedPlayerRecord> GetTrustedPlayers(Farmer owner)
        {
            if (owner == null || owner.modData == null)
                return new List<TrustedPlayerRecord>();

            if (!owner.modData.TryGetValue(TrustDataKey, out string json) || string.IsNullOrEmpty(json))
                return new List<TrustedPlayerRecord>();

            try
            {
                return JsonSerializer.Deserialize<List<TrustedPlayerRecord>>(json) ?? new List<TrustedPlayerRecord>();
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error deserializing trust data for {owner.Name}: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                return new List<TrustedPlayerRecord>();
            }
        }

        /// <summary>
        /// Get trust details for a specific player
        /// </summary>
        public static TrustedPlayerRecord GetTrustDetails(Farmer owner, Farmer trustee)
        {
            if (owner == null || trustee == null)
                return null;

            var trustedPlayers = GetTrustedPlayers(owner);
            return trustedPlayers.FirstOrDefault(t => t.PlayerId == trustee.UniqueMultiplayerID);
        }

        /// <summary>
        /// Save trusted players list to owner's modData
        /// </summary>
        private static void SaveTrustedPlayers(Farmer owner, List<TrustedPlayerRecord> trustedPlayers)
        {
            if (owner == null || owner.modData == null)
                return;

            try
            {
                string json = JsonSerializer.Serialize(trustedPlayers);
                owner.modData[TrustDataKey] = json;
                ModEntry.Instance?.Monitor.Log($"Saved trust data for {owner.Name}: {trustedPlayers.Count} trusted players", StardewModdingAPI.LogLevel.Trace);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error serializing trust data for {owner.Name}: {ex.Message}", StardewModdingAPI.LogLevel.Error);
            }
        }

        /// <summary>
        /// Calculate days since a given date string
        /// </summary>
        private static int GetDaysSince(string dateString)
        {
            try
            {
                // Parse date string like "spring 1" or "winter 28"
                var parts = dateString.Split(' ');
                if (parts.Length != 2)
                    return 0;

                string season = parts[0];
                int day = int.Parse(parts[1]);

                // Get current date
                string currentSeason = Game1.currentSeason;
                int currentDay = Game1.dayOfMonth;

                // Calculate total days (simplified - assumes 28 days per season)
                int grantedTotalDays = SeasonToNumber(season) * 28 + day;
                int currentTotalDays = SeasonToNumber(currentSeason) * 28 + currentDay;

                return currentTotalDays - grantedTotalDays;
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"Error calculating days since {dateString}: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
                return 0;
            }
        }

        /// <summary>
        /// Convert season name to number for calculation
        /// </summary>
        private static int SeasonToNumber(string season)
        {
            switch (season.ToLower())
            {
                case "spring": return 0;
                case "summer": return 1;
                case "fall": return 2;
                case "winter": return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// Debug method to display all trust relationships
        /// </summary>
        public static void DebugShowAllTrusts()
        {
            ModEntry.Instance?.Monitor.Log("=== ALL TRUST RELATIONSHIPS ===", StardewModdingAPI.LogLevel.Info);
            
            foreach (var farmer in Game1.getAllFarmers())
            {
                var trustedPlayers = GetTrustedPlayers(farmer);
                if (trustedPlayers.Count > 0)
                {
                    ModEntry.Instance?.Monitor.Log($"{farmer.Name} trusts:", StardewModdingAPI.LogLevel.Info);
                    foreach (var trust in trustedPlayers)
                    {
                        var trustedPlayer = Game1.GetPlayer(trust.PlayerId);
                        string expiry = trust.ExpirationDays == 0 ? "permanent" : $"{trust.ExpirationDays} days";
                        ModEntry.Instance?.Monitor.Log($"  - {trustedPlayer?.Name ?? "Unknown"} ({expiry})", StardewModdingAPI.LogLevel.Info);
                        ModEntry.Instance?.Monitor.Log($"    Crops: {trust.Permissions.Crops}, Machines: {trust.Permissions.Machines}, Animals: {trust.Permissions.Animals}, Buildings: {trust.Permissions.Buildings}, Chests: {trust.Permissions.Chests.Count}", StardewModdingAPI.LogLevel.Info);
                    }
                }
            }
        }
    }
}

