# GoodFences - Needs Work & Known Issues
**Date:** February 16, 2026  
**Version:** 2.1.0-alpha (post-GMCM integration)  
**Status:** Testing Phase

---

## Session Summary: Keybind Remapping Implementation

### Problem Identified During Testing

**Issue:** Right-click chest designation was triggering on every chest open attempt.

**User Report:**
> "The right-click chest to designate as common is absolutely the wrong keystroke: right-click chest to open results in persistent 'do you want to designate this chest as common' message."

**Root Cause:**
- Original implementation used `e.Button.IsUseToolButton()` with check for empty hand
- This triggered on every chest interaction (right-click to open)
- No modifier key, so menu appeared every time host tried to open any chest
- Menu needed to be dismissed before chest could be opened (poor UX)

---

## Solution Implemented

### Changes Made

#### 1. **Added Keybind Configuration to ModConfig.cs**

Added 4 new configurable keybinds:

```csharp
// ===== KEYBINDS =====

/// <summary>Keybind to show all trust relationships (debug).</summary>
public string KeyShowTrusts { get; set; } = "F5";

/// <summary>Keybind to show all pasture zones (debug).</summary>
public string KeyShowPastures { get; set; } = "F6";

/// <summary>Keybind to list all common chests (debug).</summary>
public string KeyShowCommonChests { get; set; } = "F7";

/// <summary>Keybind to designate/undesignate common chests (host only).</summary>
public string KeyDesignateCommonChest { get; set; } = "LeftShift + RightMouseButton";
```

**Key Decision:** Changed default common chest designation to `LeftShift + RightMouseButton` to avoid conflict with normal chest opening.

#### 2. **Created GMCMIntegration.cs Handler**

**File:** `Handlers/GMCMIntegration.cs` (222 lines)

**Features:**
- Full Generic Mod Config Menu integration
- All mod settings exposed in-game UI
- Keybind remapping via GMCM's keybind interface
- Organized into sections:
  - Debug & Logging
  - Pasture Protection
  - Enforcement toggles
  - Trust System
  - Common Chest
  - Keybinds

**Dependencies:**
- Uses GMCM API (optional dependency)
- Gracefully handles GMCM not being installed
- `IGenericModConfigMenuApi` interface defined

#### 3. **Updated ModEntry.cs**

**Changes to OnGameLaunched():**
```csharp
private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    // Register Generic Mod Config Menu integration
    GMCMIntegration.Register(
        helper: this.Helper,
        manifest: this.ModManifest,
        config: this.Config,
        reset: () => this.Config = new ModConfig(),
        save: () => this.Helper.WriteConfig(this.Config)
    );
}
```

**Changes to OnButtonPressed():**
- Replaced hardcoded `SButton.F5/F6/F7` checks with `KeybindList.Parse()`
- Changed from `e.Button.IsUseToolButton()` to specific keybind check
- Uses `JustPressed()` for proper key detection
- Properly suppresses keybinds after activation

**New Behavior:**
```csharp
// Parse stored keybind string into KeybindList
var designateChestKeys = KeybindList.Parse(Config.KeyDesignateCommonChest);

// Check if keybind was just pressed (not held)
if (designateChestKeys.JustPressed())
{
    // Only show menu when hovering over chest
    // Only for host player
    // Suppress keybind to prevent multiple triggers
}
```

#### 4. **Added Required Using Directives**

```csharp
// ModEntry.cs
using StardewModdingAPI.Utilities; // For KeybindList

// GMCMIntegration.cs
using StardewModdingAPI.Utilities; // For KeybindList
```

---

## Build Status

✅ **Compilation Successful**
- 0 Errors
- 10 Warnings (nullable reference warnings - pre-existing, non-critical)
- All GMCM integration code compiles cleanly
- v2.1.0-alpha updated with keybind system

---

## Testing Requirements

### Critical Testing (Before Release)

1. **GMCM Integration**
   - [ ] Install Generic Mod Config Menu mod
   - [ ] Verify GoodFences appears in GMCM list
   - [ ] Confirm all settings sections display correctly
   - [ ] Test changing each setting and verify it saves

2. **Keybind Remapping**
   - [ ] Test default keybinds work as expected
   - [ ] Change each keybind via GMCM
   - [ ] Verify new keybinds are saved to config.json
   - [ ] Test keybinds persist across game restarts
   - [ ] Test multi-key combinations (e.g., Shift + Click)
   - [ ] Verify keybind conflicts are handled by GMCM

3. **Common Chest Designation**
   - [ ] Verify `LeftShift + RightMouseButton` opens designation menu (host only)
   - [ ] Verify normal right-click still opens chest normally
   - [ ] Verify menu doesn't appear when not hovering over chest
   - [ ] Test designating a chest as common via new keybind
   - [ ] Test undesignating a common chest via new keybind
   - [ ] Verify farmhands cannot trigger designation (host-only feature)

4. **Debug Commands**
   - [ ] Test F5 shows trust relationships (debug mode only)
   - [ ] Test F6 shows pasture zones (debug mode only)
   - [ ] Test F7 lists common chests (debug mode only)
   - [ ] Verify commands are disabled when debug mode is off
   - [ ] Test remapping debug keybinds to different keys

5. **GMCM Not Installed**
   - [ ] Test mod works correctly when GMCM is not installed
   - [ ] Verify no errors in SMAPI console
   - [ ] Confirm config.json keybinds still work when manually edited

---

## Known Issues

### High Priority

**None currently identified** - waiting for multiplayer testing results.

### Medium Priority

1. **No In-Game Trust Management UI**
   - Currently must manually edit save files to grant trust
   - GMCM provides settings, but not trust granting interface
   - Workaround: Debug commands provide visibility
   - **Future:** Create interactive trust menu (right-click owned resource)

2. **Animal & Building Enforcement Not Implemented**
   - AnimalEnforcementPatches.cs not created
   - BuildingEnforcementPatches.cs not created
   - Animals can be milked/sheared by anyone
   - Buildings can be entered by anyone
   - **Future:** Add these patches in post-alpha iteration

3. **ChestsAnywhere Integration Missing**
   - Chest filtering not implemented
   - All chests visible in CA UI regardless of ownership
   - Can potentially see but not access other players' chests
   - **Future:** Patch CA's chest enumeration to filter by access

### Low Priority

1. **10 Nullable Reference Warnings**
   - Non-critical compiler warnings
   - Do not affect functionality
   - All in helper methods that check for null before use
   - **Future:** Add null-forgiving operators or additional null checks

2. **No Localization (i18n) Support**
   - All text is English-only
   - GMCM UI labels hardcoded
   - Enforcement messages hardcoded
   - **Future:** Add translation files for multi-language support

3. **Pasture Zone Visual Indicators**
   - No in-game visual showing pasture boundaries
   - Players must use F6 debug command to see zones
   - **Future:** Add optional colored overlay or boundary markers

---

## Configuration File Format

**Location:** `Stardew Valley/Mods/GoodFences/config.json`

**New Keybind Format:**
```json
{
  "DebugMode": true,
  "CoopPastureSize": 12,
  "BarnPastureSize": 16,
  "EnforceCropOwnership": true,
  "EnforceMachineOwnership": true,
  "EnforceChestOwnership": true,
  "EnforceAnimalOwnership": true,
  "EnforceBuildingOwnership": true,
  "EnforcePastureProtection": true,
  "DefaultTrustDays": 7,
  "AllowPermanentTrust": true,
  "CreateInitialCommonChest": true,
  "KeyShowTrusts": "F5",
  "KeyShowPastures": "F6",
  "KeyShowCommonChests": "F7",
  "KeyDesignateCommonChest": "LeftShift + RightMouseButton"
}
```

**Keybind Syntax:**
- Single key: `"F5"`, `"Q"`, `"LeftMouseButton"`
- Modifier + key: `"LeftShift + F5"`, `"LeftControl + Q"`
- Multiple modifiers: `"LeftShift + LeftControl + Q"`
- Mouse buttons: `"LeftMouseButton"`, `"RightMouseButton"`, `"MiddleMouseButton"`
- Valid modifiers: `LeftShift`, `RightShift`, `LeftControl`, `RightControl`, `LeftAlt`, `RightAlt`

---

## User Experience Improvements

### Before GMCM Integration

**Problems:**
- Chest designation conflicted with normal chest usage
- No way to change keybinds without editing JSON manually
- Debug commands used F-keys that might conflict with other mods
- Poor feedback when accidentally triggering chest designation

### After GMCM Integration

**Improvements:**
- ✅ Chest designation now requires modifier key (Shift + Right-click)
- ✅ All keybinds fully remappable via in-game UI
- ✅ GMCM handles keybind conflicts and validation
- ✅ Users can avoid conflicts with other mods
- ✅ All settings accessible without JSON editing
- ✅ Settings grouped logically with tooltips
- ✅ Changes save automatically via GMCM

---

## Mod Compatibility

### Tested Compatible

- **SMAPI 3.18.0+** ✅
- **Stardew Valley 1.6** ✅
- **Automate** ✅ (via AutomateCompatibilityPatches.cs)

### Newly Compatible

- **Generic Mod Config Menu** ✅ (as of this session)

### Compatibility Unknown (Needs Testing)

- **Chests Anywhere** 🟡 (works but no filtering yet)
- **Tractor Mod** 🟡
- **Farm Type Manager** 🟡
- Other mods that use F5/F6/F7 keybinds 🟡 (users can now remap)

---

## Dependencies

### Required

- **SMAPI** 3.18.0 or higher
- **.NET 6.0** (for C# compilation)
- **Harmony** (included with SMAPI)

### Optional

- **Generic Mod Config Menu** (for in-game settings UI)
  - If not installed: Config still works via manual JSON editing
  - Keybinds still functional

### Future (Not Yet Implemented)

- **Chests Anywhere** (for ownership filtering)
- **Content Patcher** (for visual zone indicators)

---

## Next Steps

### Immediate (Before Public Release)

1. **Test GMCM Integration**
   - Verify all settings display and save correctly
   - Test keybind remapping functionality
   - Confirm new chest designation keybind resolves original issue

2. **Multiplayer Testing with New Keybinds**
   - Test all enforcement with configurable keys
   - Verify host-only keybind restrictions work
   - Check for any keybind-related bugs in multiplayer

3. **Documentation Updates**
   - Update README with GMCM requirement/recommendation
   - Document default keybinds
   - Add "Remapping Keybinds" section to user guide

### Short-Term (Post-Alpha)

4. **Trust Management UI**
   - Design in-game menu for granting/revoking trust
   - Add keybind for opening trust menu (configurable via GMCM)
   - Integrate with existing trust system

5. **Animal & Building Enforcement**
   - Implement AnimalEnforcementPatches.cs
   - Implement BuildingEnforcementPatches.cs
   - Add corresponding GMCM settings

6. **ChestsAnywhere Integration**
   - Patch chest enumeration to filter by access
   - Add GMCM toggle for CA integration

### Long-Term (Future Versions)

7. **Localization Support**
   - Create i18n folder structure
   - Extract all UI text to translation files
   - Support major languages (en, es, fr, de, pt, ru, zh, ja, ko)

8. **Visual Indicators**
   - Optional pasture zone overlays
   - Ownership indicators on objects
   - Trust status visual feedback

9. **Advanced Features**
   - Revenue splitting for common items
   - Fence-based custom pasture zones (MVP currently uses fixed rectangles)
   - Transfer ownership command
   - Trust templates/presets

---

## Technical Notes

### GMCM API Usage

**Registration Pattern:**
```csharp
var gmcm = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
if (gmcm == null) return; // Graceful degradation

gmcm.Register(mod: manifest, reset: resetAction, save: saveAction);
// Add sections and options...
```

**Keybind Pattern:**
```csharp
gmcm.AddKeybindList(
    mod: manifest,
    name: () => "Setting Name",
    tooltip: () => "Description",
    getValue: () => KeybindList.Parse(config.StringValue),
    setValue: value => config.StringValue = value.ToString()
);
```

### KeybindList API

**Parsing:**
```csharp
var keys = KeybindList.Parse("LeftShift + RightMouseButton");
```

**Checking:**
```csharp
if (keys.JustPressed()) { /* Triggered once per press */ }
if (keys.IsDown()) { /* Held down */ }
```

**Suppressing:**
```csharp
this.Helper.Input.SuppressActiveKeybinds(keys);
```

---

## User Feedback & Testing Notes

### Original Issue (Resolved)

**Date:** February 16, 2026  
**Reporter:** User (tbonehunter)  
**Issue:** "Right-click chest to designate as common results in persistent 'do you want to designate this chest as common' message."

**Resolution:** Changed to `LeftShift + RightMouseButton` with full GMCM remapping support.

**Status:** ✅ Fixed, awaiting user testing confirmation.

---

## Version History Related to This Session

**v2.1.0-alpha (Initial)** - February 15, 2026
- Step 2A implementation complete
- Enforcement + trust + pasture protection
- Hardcoded F5/F6/F7 debug commands
- Hardcoded right-click chest designation

**v2.1.0-alpha (Post-GMCM)** - February 16, 2026
- Added GMCMIntegration.cs handler
- Added 4 keybind configuration options
- Changed chest designation to Shift + Right-click default
- All mod settings now in GMCM UI
- Full keybind remapping support

---

## Files Modified This Session

### Created
- `Handlers/GMCMIntegration.cs` (222 lines)

### Modified
- `Models/ModConfig.cs` - Added 4 keybind properties
- `ModEntry.cs` - GMCM registration, KeybindList usage, added using directive
- `manifest.json` - (no version bump, still 2.1.0-alpha)

### Build Artifacts
- `bin/Debug/net6.0/GoodFences.dll` - Updated with GMCM integration
- `config.json` - Will auto-generate with new keybind defaults on first run

---

## Testing Checklist

Use this checklist when testing the GMCM integration:

### GMCM Installation
- [ ] Install Generic Mod Config Menu from Nexus Mods
- [ ] Verify GMCM version 1.11.0 or higher
- [ ] Launch game, check SMAPI console for GMCM load confirmation

### Settings UI
- [ ] Open SMAPI console, type `patch summary`
- [ ] Open in-game menu → Options → Mod Config (cog icon)
- [ ] Locate "GoodFences" in mod list
- [ ] Click to open settings

### Section Visibility
- [ ] "Debug & Logging" section displays
- [ ] "Pasture Protection" section displays
- [ ] "Enforcement" section displays
- [ ] "Trust System" section displays
- [ ] "Common Chest" section displays
- [ ] "Keybinds" section displays

### Setting Changes
- [ ] Toggle "Debug Mode" on/off
- [ ] Change "Coop Pasture Size" (slider 8-20)
- [ ] Change "Barn Pasture Size" (slider 12-24)
- [ ] Toggle each enforcement setting
- [ ] Change "Default Trust Days" (0-28)
- [ ] Toggle "Allow Permanent Trust"
- [ ] Toggle "Create Initial Common Chest"

### Keybind Remapping
- [ ] Click "Show All Trusts" keybind field
- [ ] Press new key combination (e.g., F9)
- [ ] Verify displayed keybind updates
- [ ] Test new keybind in-game
- [ ] Repeat for other 3 keybinds

### Keybind Testing
- [ ] Default F5 shows trust relationships
- [ ] Default F6 shows pasture zones
- [ ] Default F7 lists common chests
- [ ] Default Shift + Right-click designates chest
- [ ] Normal right-click opens chest (no designation menu)
- [ ] Remapped keybinds work correctly

### Persistence
- [ ] Change multiple settings via GMCM
- [ ] Exit to title screen
- [ ] Reload save
- [ ] Verify settings persisted
- [ ] Open `Mods/GoodFences/config.json`
- [ ] Verify JSON contains correct values

### Graceful Degradation
- [ ] Disable/remove GMCM mod
- [ ] Launch game
- [ ] Verify no errors in SMAPI console
- [ ] Verify keybinds still work
- [ ] Manually edit config.json
- [ ] Reload config via game restart
- [ ] Verify manual changes applied

---

## Success Criteria

✅ **GMCM integration is successful when:**
1. GoodFences appears in GMCM UI with all settings
2. All keybinds are remappable via UI
3. Common chest designation no longer conflicts with chest opening
4. Settings persist across game sessions
5. Mod works with or without GMCM installed
6. No errors in SMAPI console related to GMCM integration

**Current Status:** Compilation successful, awaiting testing confirmation from user.

---

## Contact & Support

**Mod Author:** tbonehunter  
**Current Version:** 2.1.0-alpha (post-GMCM integration)  
**GitHub:** tbonehunter/GoodFences (planned)  
**Testing Phase:** Alpha (closed testing with user)

---

## Notes for Future Development

1. **KeybindList vs SButton:**
   - Always use `KeybindList` for multi-key combinations
   - Store as strings in config for GMCM compatibility
   - Use `JustPressed()` not `IsDown()` for one-shot actions

2. **GMCM Section Organization:**
   - Group related settings together
   - Use descriptive tooltips
   - Set reasonable min/max values for numbers
   - Consider adding "Advanced" section for debug/experimental features

3. **Keybind Best Practices:**
   - Always provide tooltips explaining what the keybind does
   - Choose defaults that unlikely to conflict (F-keys for debug, modifiers for actions)
   - Test common mod keybind patterns to avoid conflicts
   - Allow users to disable keybinds by setting to "None"

4. **Host-Only Features:**
   - Always check `Game1.player.IsMainPlayer` before allowing host-only actions
   - Provide clear feedback when farmhands attempt host-only keybinds
   - Document host-only restrictions in tooltips

---

## Conclusion

**Problem:** Chest designation conflicted with normal gameplay.  
**Solution:** Added GMCM integration with fully remappable keybinds.  
**Status:** Implemented and compiled successfully, awaiting user testing.  
**Next:** User will test mod with new keybinds and provide feedback.

All keybinds now configurable, common chest designation requires modifier key by default, and entire mod configuration accessible via in-game GMCM UI. This resolves the original UX issue and provides users with full control over keybinds to avoid conflicts with other mods.
