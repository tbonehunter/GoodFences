// Handlers/GMCMIntegration.cs
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using GoodFences.Models;

namespace GoodFences.Handlers
{
    /// <summary>
    /// Handles Generic Mod Config Menu (GMCM) integration.
    /// Allows players to configure mod settings in-game.
    /// </summary>
    public static class GMCMIntegration
    {
        /// <summary>
        /// Register mod configuration with GMCM if it's installed.
        /// </summary>
        /// <param name="helper">Mod helper for accessing GMCM API.</param>
        /// <param name="manifest">Mod manifest for registration.</param>
        /// <param name="config">Current mod configuration.</param>
        /// <param name="reset">Action to reset config to defaults.</param>
        /// <param name="save">Action to save current config.</param>
        public static void Register(IModHelper helper, IManifest manifest, ModConfig config, System.Action reset, System.Action save)
        {
            var gmcm = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            // Register mod
            gmcm.Register(
                mod: manifest,
                reset: reset,
                save: save
            );

            // ===== DEBUG & LOGGING =====
            gmcm.AddSectionTitle(
                mod: manifest,
                text: () => "Debug & Logging"
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Debug Mode",
                tooltip: () => "Enable verbose debug logging and debug keybinds.",
                getValue: () => config.DebugMode,
                setValue: value => config.DebugMode = value
            );

            // ===== PASTURE PROTECTION =====
            gmcm.AddSectionTitle(
                mod: manifest,
                text: () => "Pasture Protection"
            );

            gmcm.AddNumberOption(
                mod: manifest,
                name: () => "Coop Pasture Size",
                tooltip: () => "Size of pasture protection zone around coops (in tiles).",
                getValue: () => config.CoopPastureSize,
                setValue: value => config.CoopPastureSize = value,
                min: 8,
                max: 20
            );

            gmcm.AddNumberOption(
                mod: manifest,
                name: () => "Barn Pasture Size",
                tooltip: () => "Size of pasture protection zone around barns (in tiles).",
                getValue: () => config.BarnPastureSize,
                setValue: value => config.BarnPastureSize = value,
                min: 12,
                max: 24
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Enforce Pasture Protection",
                tooltip: () => "Block non-owners from tilling in pasture zones.",
                getValue: () => config.EnforcePastureProtection,
                setValue: value => config.EnforcePastureProtection = value
            );

            // ===== ENFORCEMENT TOGGLES =====
            gmcm.AddSectionTitle(
                mod: manifest,
                text: () => "Enforcement"
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Enforce Crop Ownership",
                tooltip: () => "Block non-owners from harvesting crops.",
                getValue: () => config.EnforceCropOwnership,
                setValue: value => config.EnforceCropOwnership = value
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Enforce Machine Ownership",
                tooltip: () => "Block non-owners from using machines.",
                getValue: () => config.EnforceMachineOwnership,
                setValue: value => config.EnforceMachineOwnership = value
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Enforce Chest Ownership",
                tooltip: () => "Block non-owners from opening chests.",
                getValue: () => config.EnforceChestOwnership,
                setValue: value => config.EnforceChestOwnership = value
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Enforce Animal Ownership",
                tooltip: () => "Block non-owners from interacting with animals.",
                getValue: () => config.EnforceAnimalOwnership,
                setValue: value => config.EnforceAnimalOwnership = value
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Enforce Building Ownership",
                tooltip: () => "Block non-owners from entering buildings.",
                getValue: () => config.EnforceBuildingOwnership,
                setValue: value => config.EnforceBuildingOwnership = value
            );

            // ===== TRUST SYSTEM =====
            gmcm.AddSectionTitle(
                mod: manifest,
                text: () => "Trust System"
            );

            gmcm.AddNumberOption(
                mod: manifest,
                name: () => "Default Trust Days",
                tooltip: () => "Default number of days for trust expiration (0 = permanent).",
                getValue: () => config.DefaultTrustDays,
                setValue: value => config.DefaultTrustDays = value,
                min: 0,
                max: 28
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Allow Permanent Trust",
                tooltip: () => "Allow trusts that never expire (until manually revoked).",
                getValue: () => config.AllowPermanentTrust,
                setValue: value => config.AllowPermanentTrust = value
            );

            // ===== COMMON CHEST SYSTEM =====
            gmcm.AddSectionTitle(
                mod: manifest,
                text: () => "Common Chest"
            );

            gmcm.AddBoolOption(
                mod: manifest,
                name: () => "Create Initial Common Chest",
                tooltip: () => "Automatically create a common chest when starting a multiplayer game.",
                getValue: () => config.CreateInitialCommonChest,
                setValue: value => config.CreateInitialCommonChest = value
            );

            // ===== KEYBINDS =====
            gmcm.AddSectionTitle(
                mod: manifest,
                text: () => "Keybinds"
            );

            gmcm.AddKeybindList(
                mod: manifest,
                name: () => "Show All Trusts",
                tooltip: () => "Debug command: Display all trust relationships (requires Debug Mode).",
                getValue: () => KeybindList.Parse(config.KeyShowTrusts),
                setValue: value => config.KeyShowTrusts = value.ToString()
            );

            gmcm.AddKeybindList(
                mod: manifest,
                name: () => "Show All Pastures",
                tooltip: () => "Debug command: Display all pasture zones (requires Debug Mode).",
                getValue: () => KeybindList.Parse(config.KeyShowPastures),
                setValue: value => config.KeyShowPastures = value.ToString()
            );

            gmcm.AddKeybindList(
                mod: manifest,
                name: () => "Show Common Chests",
                tooltip: () => "Debug command: List all common chests (requires Debug Mode).",
                getValue: () => KeybindList.Parse(config.KeyShowCommonChests),
                setValue: value => config.KeyShowCommonChests = value.ToString()
            );

            gmcm.AddKeybindList(
                mod: manifest,
                name: () => "Designate Common Chest",
                tooltip: () => "Designate/undesignate a chest as common (host only, use while hovering over chest).",
                getValue: () => KeybindList.Parse(config.KeyDesignateCommonChest),
                setValue: value => config.KeyDesignateCommonChest = value.ToString()
            );
        }
    }

    /// <summary>
    /// Generic Mod Config Menu API interface.
    /// Only declare methods that actually exist in the GMCM API and that we use.
    /// Pintail (SMAPI's proxy system) requires ALL methods to match the real API.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, System.Action reset, System.Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, System.Func<string> text, System.Func<string>? tooltip = null);
        void AddBoolOption(IManifest mod, System.Func<bool> getValue, System.Action<bool> setValue, System.Func<string> name, System.Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, System.Func<int> getValue, System.Action<int> setValue, System.Func<string> name, System.Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, System.Func<int, string>? formatValue = null, string? fieldId = null);
        void AddTextOption(IManifest mod, System.Func<string> getValue, System.Action<string> setValue, System.Func<string> name, System.Func<string>? tooltip = null, string[]? allowedValues = null, System.Func<string, string>? formatAllowedValue = null, string? fieldId = null);
        void AddKeybindList(IManifest mod, System.Func<KeybindList> getValue, System.Action<KeybindList> setValue, System.Func<string> name, System.Func<string>? tooltip = null, string? fieldId = null);
    }
}
