# Step 2A Implementation Roadmap
**GoodFences Mod - Enforcement Phase**  
**Date:** February 15, 2026  
**Status:** Design Complete - Ready for Implementation

---

## Overview

Step 2A implements the **enforcement layer** for the GoodFences ownership system. Step 1 (passive tagging) is complete and tested. Step 2A adds:

1. **Access Control**: Block non-owners from interacting with owned resources
2. **Trust System**: Time-based permissions for sharing access
3. **Pasture Protection**: Fixed zones preventing tilling near animal buildings
4. **Common Resources**: Shared chests accessible to all players
5. **ChestsAnywhere Integration**: Filter chests by ownership in UI

**Core Principle:** "What you produce is yours from seed to sale" - enforcement ensures this by blocking unauthorized access while allowing voluntary sharing through trust.

---

## Architecture Overview

### Data Structures

#### Trust/Permission Storage
Store in player's `modData` for persistence:

```csharp
// Per-player trust data stored in Farmer.modData
"GoodFences.TrustedPlayers" = JSON array of trusted player records

TrustedPlayerRecord:
{
    "PlayerId": long,              // Trusted player's unique ID
    "GrantedDate": string,         // In-game date permission granted
    "ExpirationDays": int,         // 0 = until revoked, >0 = days until expiration
    "Permissions": {               // What they can access
        "Crops": bool,             // Can harvest this player's crops
        "Machines": bool,          // Can use this player's machines
        "Chests": string[],        // Array of chest GUIDs they can access
        "Animals": bool,           // Can interact with this player's animals
        "Buildings": bool          // Can enter this player's buildings
    }
}
```

#### Pasture Zone Storage
Store in building's `modData`:

```csharp
// Per-building pasture zone
"GoodFences.PastureZone" = JSON serialized PastureZone

PastureZone:
{
    "BuildingType": string,        // "Coop" or "Barn"
    "X": int,                      // Top-left X coordinate
    "Y": int,                      // Top-left Y coordinate
    "Width": int,                  // 12 for coops, 16 for barns
    "Height": int,                 // 12 for coops, 16 for barns
    "OwnerId": long                // Building owner's unique ID
}
```

#### Common Chest Storage
Store in chest's `modData`:

```csharp
// Flag chest as common
"GoodFences.Common" = "true"
```

Store common chest locations in save data for quick lookup:

```csharp
// Global save data
"GoodFences.CommonChests" = JSON array of chest GUIDs
```

---

## Implementation Tasks

### Phase 1: Core Infrastructure (Foundation)

#### Task 1.1: Trust System Handler
**File:** `Handlers/TrustSystemHandler.cs` (new)

**Methods:**
- `GrantTrust(Farmer granter, Farmer trustee, int days, Permissions perms)`
- `RevokeTrust(Farmer granter, Farmer trustee)`
- `HasTrust(Farmer owner, Farmer accessor, PermissionType type)`
- `UpdateTrustExpirations()` - Called daily to remove expired trusts
- `GetTrustedPlayers(Farmer owner)` - Returns list for UI
- `GetTrustDetails(Farmer owner, Farmer trustee)` - Get specific trust record

**Storage:**
- Read/write trust data from `Farmer.modData["GoodFences.TrustedPlayers"]`
- JSON serialization for complex data structures

**Testing:**
- Grant trust → verify JSON stored correctly
- Expire trust → verify removal on day transition
- Revoke trust → verify immediate removal

---

#### Task 1.2: Pasture Zone Handler
**File:** `Handlers/PastureZoneHandler.cs` (new)

**Methods:**
- `CreatePastureZone(Building building)` - Calculate and store zone on building placement
- `RemovePastureZone(Building building)` - Clear zone on demolition
- `UpdatePastureZone(Building building, xTile.Dimensions.Location newLocation)` - Move zone
- `IsInPastureZone(GameLocation location, Vector2 tile, out long ownerId)` - Check if tile is protected
- `GetPastureZoneBounds(Building building)` - Return Rectangle for visualization

**Zone Calculation:**
```csharp
// For Coop (12x12 south of doors)
int zoneX = building.tileX.Value;
int zoneY = building.tileY.Value + building.tilesHigh.Value; // Start below building
int zoneWidth = 12;
int zoneHeight = 12;

// For Barn (16x16 south of doors)
int zoneWidth = 16;
int zoneHeight = 16;
```

**Storage:**
- Write zone to `building.modData["GoodFences.PastureZone"]`
- Use JSON for easy serialization

**Testing:**
- Place coop → verify 12x12 zone created
- Place barn → verify 16x16 zone created
- Move building → verify zone moves
- Demolish → verify zone removed

---

#### Task 1.3: Common Chest Handler
**File:** `Handlers/CommonChestHandler.cs` (new)

**Methods:**
- `InitializeCommonChest()` - Create initial common chest on multiplayer game start
- `DesignateCommonChest(Chest chest)` - Host marks chest as common
- `UndesignateCommonChest(Chest chest)` - Host removes common flag
- `IsCommonChest(Chest chest)` - Check if chest is common
- `GetAllCommonChests()` - Return list for ChestsAnywhere integration

**Initial Chest Placement:**
- Triggered on `SaveLoaded` for new multiplayer saves
- Place near shipping bin: `Farm.objects.Add(new Vector2(71, 14), commonChest)`
- Use Stone Chest (ID 130) with custom displayName

**Storage:**
- Set `chest.modData["GoodFences.Common"] = "true"`
- Track GUIDs in global save data for quick access

**Testing:**
- Start new multiplayer → verify common chest spawned
- Host designate chest → verify common flag set
- All players access common chest → verify no blocking

---

### Phase 2: Enforcement Patches

#### Task 2.1: Crop Harvesting Protection
**File:** `Patches/CropEnforcementPatches.cs` (new)

**Patch:** `Crop.harvest` Prefix

**Logic:**
1. Get crop owner from `HoeDirt.modData["GoodFences.Owner"]`
2. If no owner → allow (untagged crop, pre-mod ownership)
3. If harvester == owner → allow
4. If common crop → allow
5. If harvester has trust for crops from owner → allow
6. Else → block with message: "This crop belongs to [OwnerName]"

**Return:** `false` to block harvest, `true` to allow

**Testing:**
- Owner harvests own crop → success
- Non-owner without trust → blocked
- Non-owner with crop trust → success
- Common crop → success for anyone

---

#### Task 2.2: Machine Access Protection
**File:** `Patches/MachineEnforcementPatches.cs` (new)

**Patches:**
1. `Object.performObjectDropInAction` Prefix - Block item insertion
2. `Object.checkForAction` Prefix - Block collection/interaction

**Logic:**
1. Get machine owner from `machine.modData["GoodFences.Owner"]`
2. If no owner → allow
3. If user == owner → allow
4. If common machine → allow
5. If inside building owned by user → allow (building owner controls machines inside)
6. If user has machine trust from owner → allow
7. Else → block with message: "This machine belongs to [OwnerName]"

**Special Cases:**
- Check building ownership first (building owner override)
- Allow owner to pick up machine (preserve ownership with logging)

**Testing:**
- Owner uses own machine → success
- Non-owner without trust → blocked
- Non-owner with machine trust → success
- Machine in owned building → building owner can use

---

#### Task 2.3: Chest Access Protection
**File:** `Patches/ChestEnforcementPatches.cs` (new)

**Patch:** `Chest.checkForAction` Prefix

**Logic:**
1. If common chest → allow everyone
2. Get chest owner from `chest.modData["GoodFences.Owner"]`
3. If no owner → allow (pre-mod chest)
4. If user == owner → allow
5. If chest GUID in user's trusted chest list → allow
6. Else → block with message: "This chest belongs to [OwnerName]"

**Trusted Chest System:**
- Owner right-clicks chest → "Trust Players" menu
- Select player + duration
- Add chest GUID to `TrustedPlayerRecord.Permissions.Chests[]`

**Testing:**
- Owner opens own chest → success
- Non-owner without trust → blocked
- Non-owner with chest trust → success
- Common chest → everyone can access

---

#### Task 2.4: Animal Interaction Protection
**File:** `Patches/AnimalEnforcementPatches.cs` (new)

**Patches:**
1. `FarmAnimal.pet` Prefix - Allow petting by anyone
2. `FarmAnimal.behaviors` - Block shearing/milking
3. Milk pail tool use - Block non-owners
4. Shears tool use - Block non-owners

**Logic:**
1. Get animal owner from `animal.modData["GoodFences.Owner"]`
2. If no owner → allow
3. If user == owner → allow
4. Petting → allow for anyone (animals need love!)
5. Product collection (milk/shear/truffle) → check animal trust
6. Else → block with message: "This animal belongs to [OwnerName]"

**Testing:**
- Owner milks own cow → success
- Non-owner pets animal → success (always allowed)
- Non-owner shears sheep without trust → blocked
- Non-owner with animal trust → success

---

#### Task 2.5: Building Entry Protection
**File:** `Patches/BuildingEnforcementPatches.cs` (new)

**Patches:**
1. `Building.doAction` Prefix - Block door interaction
2. Animal purchase - Block non-owners unless permission granted

**Logic:**
1. Get building owner from `building.modData["GoodFences.Owner"]`
2. If no owner → allow
3. If user == owner → allow
4. If user has building trust from owner → allow
5. Else → block with message: "This building belongs to [OwnerName]"

**Animal Purchase Permission:**
- Check `building.modData["GoodFences.AnimalPermission"]` for player ID
- If matches → allow 1 animal purchase, then clear permission
- Support count: `"GoodFences.AnimalPermission.Stan.Count" = "2"` for 2 animals

**Testing:**
- Owner enters own barn → success
- Non-owner without trust → blocked
- Non-owner with building trust → success
- Animal purchase with permission → success, then reverts

---

#### Task 2.6: Pasture Tilling Protection
**File:** `Patches/PastureEnforcementPatches.cs` (new)

**Patch:** `Farm.makeShared` Prefix (or tool use for hoe)

**Better Approach:** Patch hoe tool usage in `Tool.beginUsing` or `Hoe.DoFunction`

**Logic:**
1. Get tile being hoed
2. Call `PastureZoneHandler.IsInPastureZone(location, tile, out long ownerId)`
3. If not in any zone → allow
4. If user ID == ownerId → allow (owner can till their own pasture)
5. Else → block with message: "This pasture belongs to [BuildingOwnerName]"

**Testing:**
- Till outside pasture → success
- Building owner tills own pasture → success
- Non-owner tills pasture → blocked
- Move building → new zone protected, old zone released

---

### Phase 3: Integration & UI

#### Task 3.1: ChestsAnywhere Compatibility
**File:** `Patches/ChestsAnywherePatches.cs` (new)

**Approach:** Patch ChestsAnywhere's chest enumeration method

**Research Needed:**
1. Identify ChestsAnywhere's method that builds chest list
2. Find where it exposes chest filtering API (if any)
3. Determine patch point to filter chests

**Logic:**
- Include chests owned by current player
- Include common chests
- Include chests where player has trust
- Exclude all other chests

**Fallback:** If direct patching difficult, create tooltip indicator showing "(Not Accessible)" on locked chests

**Testing:**
- Open ChestsAnywhere UI → verify only accessible chests shown
- Gain trust to chest → verify it appears
- Lose trust → verify it disappears

---

#### Task 3.2: Trust Management UI
**File:** `UI/TrustManagementMenu.cs` (new)

**Trigger:** Player right-clicks owned resource (crop, machine, chest, animal, building)

**Menu Options:**
1. **Grant Trust**
   - Submenu: Select player
   - Submenu: Select duration (1 day, 3 days, 7 days, 14 days, custom, until revoked)
   - Submenu: Select permissions (crops, machines, chests, animals, buildings) - multi-select
   - Confirm → store trust record
   
2. **View Trusted Players**
   - List all players with trust
   - Show expiration dates
   - Option to revoke each

3. **Revoke Trust**
   - Select player from list
   - Confirm → remove trust record

**Implementation:**
- Extend `MessageBox` or create custom UI
- Use SMAPI's `IClickableMenu` framework
- Integrate with controller navigation

**Testing:**
- Right-click owned machine → menu appears
- Grant trust with 3-day expiration → verify stored
- View trusted players → verify list correct
- Revoke trust → verify immediate removal

---

#### Task 3.3: Animal Permission UI
**File:** `UI/AnimalPermissionMenu.cs` (new)

**Trigger:** Building owner right-clicks owned coop/barn

**Menu Options:**
1. **Grant Animal Permission**
   - Select player
   - Enter count (default 1)
   - Confirm → store permission in building modData

2. **View Permissions**
   - Show active permissions
   - Option to revoke

**Implementation:**
- Simple number input for count
- Store in `building.modData["GoodFences.AnimalPermission.{PlayerId}"] = count`
- Decrement on each animal purchase
- Remove when count reaches 0

**Testing:**
- Grant 2-animal permission → verify stored
- Permitted player buys 1 animal → count = 1
- Permitted player buys 1 animal → count = 0, permission removed

---

#### Task 3.4: Common Chest Designation UI
**File:** `UI/CommonChestMenu.cs` (new)

**Trigger:** Host right-clicks any chest

**Menu Options (Host Only):**
1. **Designate as Common Chest**
   - Confirm → set common flag
   - Add to global common chest list

2. **Remove Common Status** (if already common)
   - Confirm → remove flag
   - Remove from global list
   - Warning: "Items inside will remain but chest becomes normal chest"

**Implementation:**
- Check `Game1.player.IsMainPlayer` to restrict to host
- Use `CommonChestHandler` methods
- Simple confirmation dialog

**Testing:**
- Host designates chest → verify flag set
- Farmhand tries to designate → menu option not shown
- Host removes common status → verify flag cleared

---

### Phase 4: Polish & Feedback

#### Task 4.1: Enhanced Feedback Messages
**File:** `Handlers/FeedbackHandler.cs` (new)

**Purpose:** Centralize all user-facing messages

**Messages:**
- Block messages: "This [resource] belongs to [PlayerName]"
- Trust granted: "You granted [PlayerName] access to your [resources]"
- Trust expired: "[PlayerName]'s access to your [resources] has expired"
- Trust revoked: "You revoked [PlayerName]'s access"
- Pasture protected: "You cannot till [PlayerName]'s pasture"
- Common chest created: "Common chest available near shipping bin"

**Localization Ready:**
- Use i18n keys for all strings
- Create `i18n/default.json` with English text
- Structure keys: `goodfences.blocked.crop`, `goodfences.trust.granted`, etc.

---

#### Task 4.2: Debug Mode Enhancements
**File:** `ModEntry.cs` updates

**New Debug Commands:**
- `gf_trust` - Show all trust relationships
- `gf_pastures` - Highlight all pasture zones on map
- `gf_common` - List all common chests
- `gf_grant_trust [player] [days] [permission]` - Test trust system
- `gf_test_block` - Attempt to access blocked resources

**Debug Visualization:**
- Draw pasture zone boundaries on screen when debug mode enabled
- Show ownership labels on hover for all resources
- Log all access checks to console

---

#### Task 4.3: ModConfig Additions
**File:** `Models/ModConfig.cs` updates

**New Config Options:**
```csharp
public class ModConfig
{
    public bool DebugMode { get; set; } = false;
    
    // Pasture zone sizes
    public int CoopPastureSize { get; set; } = 12;
    public int BarnPastureSize { get; set; } = 16;
    
    // Enforcement toggles (for gradual rollout/testing)
    public bool EnforceCropOwnership { get; set; } = true;
    public bool EnforceMachineOwnership { get; set; } = true;
    public bool EnforceChestOwnership { get; set; } = true;
    public bool EnforceAnimalOwnership { get; set; } = true;
    public bool EnforceBuildingOwnership { get; set; } = true;
    public bool EnforcePastureProtection { get; set; } = true;
    
    // Trust defaults
    public int DefaultTrustDays { get; set; } = 7;
    public bool AllowPermanentTrust { get; set; } = true;
    
    // Common chest
    public bool CreateInitialCommonChest { get; set; } = true;
}
```

**GMCM Integration:**
- Use Generic Mod Config Menu API
- Group settings into categories
- Add tooltips explaining each option

---

### Phase 5: Testing & Validation

#### Test Suite 1: Trust System
- [ ] Grant trust with 1-day expiration → verify expires next day
- [ ] Grant trust with custom 5 days → verify expires on day 5
- [ ] Grant trust "until revoked" → verify never expires
- [ ] Revoke trust → verify immediate effect
- [ ] Trust multiple players → verify independent tracking
- [ ] Save/load with active trusts → verify persistence

#### Test Suite 2: Crop Enforcement
- [ ] Stan plants crop → Katie blocked from harvesting
- [ ] Katie grants Stan crop trust → Stan harvests Katie's crop
- [ ] Trust expires → Stan blocked again
- [ ] Common crop → both can harvest
- [ ] Untagged crop (pre-mod) → both can harvest

#### Test Suite 3: Machine Enforcement
- [ ] Stan places keg → Katie blocked from using
- [ ] Katie grants Stan machine trust → Stan uses Katie's keg
- [ ] Machine in building: Katie owns barn, Stan places keg → Katie can use (building owner override)
- [ ] Stan picks up Katie's machine → ownership preserved, logged
- [ ] Save/load → machine ownership persists

#### Test Suite 4: Chest Enforcement
- [ ] Stan places chest → Katie blocked from opening
- [ ] Katie grants Stan chest trust → Stan opens Katie's chest
- [ ] Common chest → both can open
- [ ] Host designates existing chest as common → both can open

#### Test Suite 5: Animal Enforcement
- [ ] Stan buys cow → Katie blocked from milking
- [ ] Katie pets Stan's cow → allowed (petting always works)
- [ ] Katie grants Stan animal trust → Stan milks Katie's cow
- [ ] Animal products (eggs, milk, wool) → correct owner tag

#### Test Suite 6: Building Enforcement
- [ ] Stan builds barn → Katie blocked from entering
- [ ] Katie grants Stan building trust → Stan enters Katie's barn
- [ ] Stan requests animal permission → Katie grants 2 slots → Stan buys 2 animals → permission removed
- [ ] Building demolition → owner check

#### Test Suite 7: Pasture Protection
- [ ] Stan places coop → 12x12 zone created south of doors
- [ ] Katie tries to till in zone → blocked
- [ ] Stan tills own pasture → allowed
- [ ] Stan moves coop → zone moves
- [ ] Stan demolishes coop → zone removed
- [ ] Barn creates 16x16 zone → correct size

#### Test Suite 8: ChestsAnywhere Integration
- [ ] Open ChestsAnywhere → only accessible chests shown
- [ ] Gain trust to chest → appears in list
- [ ] Common chest → appears in list
- [ ] Unowned chest → able to access
- [ ] Trust expires → chest disappears from list

#### Test Suite 9: Edge Cases
- [ ] Save/load with all systems active → verify persistence
- [ ] Multiplayer: 3+ players with complex trust web → verify isolation
- [ ] Player leaves server with active trusts → cleanup
- [ ] Building sale/transfer → ownership and pasture updates
- [ ] Automate mod running → ownership preserved through automation
- [ ] Time manipulation (fast forward) → trust expirations work

---

## File Structure

```
GoodFences/
├── ModEntry.cs (updated)
├── manifest.json (update to v2.1.0-alpha)
├── Models/
│   └── ModConfig.cs (updated)
├── Handlers/
│   ├── OwnershipTagHandler.cs (existing)
│   ├── TrustSystemHandler.cs (new)
│   ├── PastureZoneHandler.cs (new)
│   ├── CommonChestHandler.cs (new)
│   └── FeedbackHandler.cs (new)
├── Patches/
│   ├── OwnershipTagPatches.cs (existing)
│   ├── AutomateCompatibilityPatches.cs (existing)
│   ├── CropEnforcementPatches.cs (new)
│   ├── MachineEnforcementPatches.cs (new)
│   ├── ChestEnforcementPatches.cs (new)
│   ├── AnimalEnforcementPatches.cs (new)
│   ├── BuildingEnforcementPatches.cs (new)
│   ├── PastureEnforcementPatches.cs (new)
│   └── ChestsAnywherePatches.cs (new)
└── UI/
    ├── TrustManagementMenu.cs (new)
    ├── AnimalPermissionMenu.cs (new)
    └── CommonChestMenu.cs (new)
```

---

## Implementation Phases

### Phase A: Foundation (Days 1-2)
1. Trust system data structures & handler
2. Pasture zone handler
3. Common chest handler
4. ModConfig updates

### Phase B: Core Enforcement (Days 3-5)
1. Crop enforcement patches
2. Machine enforcement patches
3. Chest enforcement patches
4. Animal enforcement patches
5. Building enforcement patches
6. Pasture enforcement patches

### Phase C: Integration (Days 6-7)
1. ChestsAnywhere compatibility
2. Trust management UI
3. Animal permission UI
4. Common chest designation UI

### Phase D: Polish (Day 8)
1. Feedback messages
2. Debug enhancements
3. GMCM integration

### Phase E: Testing (Days 9-10)
1. Run all test suites
2. Multiplayer testing with 2-3 players
3. Compatibility testing with Automate & ChestsAnywhere
4. Edge case validation

---

## Success Criteria

Step 2A is complete when:

✅ **All enforcement patches block unauthorized access**
- Non-owners cannot harvest crops without trust
- Non-owners cannot use machines without trust
- Non-owners cannot open chests without trust
- Non-owners cannot milk/shear animals without trust
- Non-owners cannot enter buildings without trust
- Non-owners cannot till pastures

✅ **Trust system works reliably**
- Time-based permissions expire correctly
- Manual revocation works immediately
- Trust persists through save/load
- Multiple trust relationships tracked independently

✅ **Pasture protection functions**
- 12x12 zones created for coops
- 16x16 zones created for barns
- Zones move with buildings
- Zones removed on demolition
- Owner can till own pasture
- Non-owners blocked

✅ **Common chests accessible to all**
- Initial chest spawns in new games
- Host can designate additional chests
- All players can access common chests
- Common chests bypass ownership checks

✅ **ChestsAnywhere integration works**
- Only accessible chests shown in UI
- Trust changes reflected immediately
- No crashes or conflicts

✅ **Owner experience feels natural**
- Clear feedback messages when blocked
- Easy-to-use trust UI
- Resources secure by default, shareable by choice

✅ **Compatibility maintained**
- Automate mod works correctly
- ChestsAnywhere works correctly
- No conflicts with other popular mods

✅ **Performance acceptable**
- No noticeable lag from ownership checks
- Trust expiration checks efficient
- Pasture zone lookups fast

---

## Future Enhancements (Post-Step 2A)

These features are deferred to later phases:

1. **Advanced Pasture System**
   - Fence-based custom zones
   - Area budgets (200 coop, 400 barn)
   - Flood-fill detection
   - Multiple zones per building

2. **Revenue Splitting**
   - Common item sales tracked
   - Active player revenue split
   - Contribution tracking

3. **Ownership Transfer**
   - Gift items to other players
   - Transfer building ownership
   - Batch permission changes

4. **Advanced Trust**
   - Activity-based trust (harvest specific crops)
   - Conditional trust (use machines only on Fridays)
   - Trust templates (copy trust settings)

5. **Multiplayer Features**
   - Trading system
   - Resource rental (pay gold for temporary access)
   - Partnership contracts

6. **Visual Enhancements**
   - On-screen ownership indicators
   - Pasture zone visualization
   - Trust status icons

---

## Notes & Decisions

### Design Philosophy
- **Secure by default**: Everything owned by default, sharing opt-in
- **Clear feedback**: Always tell player why they're blocked
- **Reversible**: All permissions can be revoked
- **Persistent**: Ownership survives save/load, season changes
- **Compatible**: Works with existing mods where possible

### Key Technical Decisions
1. Use `modData` for all persistence (leverages game's save system)
2. JSON serialization for complex data structures
3. Trust stored per-player, not per-resource (simpler UI)
4. Pasture zones stored per-building (natural lifecycle)
5. Common chests flagged individually (flexible)

### Known Limitations
1. Cannot prevent item dropping/picking up (game limitation)
2. ChestsAnywhere patch may need updates if CA updates
3. Pasture zones always rectangular for MVP (complex shapes deferred)
4. Trust expiration checks daily, not hourly (performance trade-off)

---

## Questions for User

Before implementation begins:

1. **Priority**: Implement full Step 2A (2-3 weeks), or MVP subset first?
2. **UI Style**: Simple message boxes, or custom graphical menus?
3. **Testing**: Solo testing sufficient, or need multiplayer co-testers?
4. **Mod Dependencies**: Hard require ChestsAnywhere, or soft compatibility?

---

**End of Roadmap**  
*Ready for implementation approval*
