// Models/ModConfig.cs
namespace GoodFences.Models
{
    /// <summary>Configuration options for the GoodFences mod.</summary>
    public class ModConfig
    {
        // ===== DEBUG & LOGGING =====
        
        /// <summary>Whether to enable verbose debug logging for tag operations.</summary>
        public bool DebugMode { get; set; } = true;

        // ===== PASTURE PROTECTION =====
        
        /// <summary>Pasture zone size for coops (default: 12x12 tiles).</summary>
        public int CoopPastureSize { get; set; } = 12;

        /// <summary>Pasture zone size for barns (default: 16x16 tiles).</summary>
        public int BarnPastureSize { get; set; } = 16;

        // ===== ENFORCEMENT TOGGLES =====
        
        /// <summary>Whether to enforce crop ownership (block non-owners from harvesting).</summary>
        public bool EnforceCropOwnership { get; set; } = true;

        /// <summary>Whether to enforce machine ownership (block non-owners from using).</summary>
        public bool EnforceMachineOwnership { get; set; } = true;

        /// <summary>Whether to enforce chest ownership (block non-owners from opening).</summary>
        public bool EnforceChestOwnership { get; set; } = true;

        /// <summary>Whether to enforce animal ownership (block non-owners from interacting).</summary>
        public bool EnforceAnimalOwnership { get; set; } = true;

        /// <summary>Whether to enforce building ownership (block non-owners from entering).</summary>
        public bool EnforceBuildingOwnership { get; set; } = true;

        /// <summary>Whether to protect pastures from tilling by non-owners.</summary>
        public bool EnforcePastureProtection { get; set; } = true;

        // ===== TRUST SYSTEM =====
        
        /// <summary>Default number of days for trust expiration when not specified (default: 7).</summary>
        public int DefaultTrustDays { get; set; } = 7;

        /// <summary>Whether to allow permanent trust (until manually revoked).</summary>
        public bool AllowPermanentTrust { get; set; } = true;

        // ===== COMMON CHEST SYSTEM =====
        
        /// <summary>Whether to automatically create an initial common chest in new multiplayer games.</summary>
        public bool CreateInitialCommonChest { get; set; } = true;

        // ===== KEYBINDS =====
	// SMAPI Button Names Reference:
	// Mouse: MouseLeft, MouseRight, MouseMiddle, MouseX1, MouseX2
	// Modifiers: LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt
	// Combination syntax: "LeftShift + MouseRight"
	// See: https://stardewvalleywiki.com/Modding:Player_Guide/Key_Bindings
        
        /// <summary>Keybind to show all trust relationships (debug).</summary>
        public string KeyShowTrusts { get; set; } = "F5";

        /// <summary>Keybind to show all pasture zones (debug).</summary>
        public string KeyShowPastures { get; set; } = "F6";

        /// <summary>Keybind to list all common chests (debug).</summary>
        public string KeyShowCommonChests { get; set; } = "F7";

        /// <summary>Keybind to designate/undesignate common chests (host only).</summary>
        public string KeyDesignateCommonChest { get; set; } = "LeftShift + MouseRight";
    }
}
