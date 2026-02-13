# GoodFences (QuadFarms) - Project Progress

## Project Overview
A Stardew Valley SMAPI mod that divides the Four Corners farm map so each player owns their own quadrant with private farmland, while maintaining shared common areas.

---

## Session Log

### Session 1 - February 7, 2026

**Status:** Initial Design Complete

#### Design Decisions Made:
1. **Map approach:** Mod overlay on vanilla Four Corners map - no custom map edits required
2. **Boundary system:** Geographic coordinate checks, not object-level ownership tracking
3. **Private zones:** Farmable interiors within natural rock walls; everything else is shared by default
4. **Shared zones:** Corridors, paths, rock formations, central crossing, farm cave, farmhouse quadrant (NE)
5. **Dynamic sharing:** Unused quadrants become shared in games with fewer than 3 farmhands
6. **Separate wallets:** Native game feature - no mod code needed
7. **Common chest distribution:** Equal split with remainder to depositor
8. **Cabin placement:** Vanilla "separate cabins" option handles this - cabins are deterministic, north wall of each quadrant, facing south

#### Quadrant Resources (confirmed via Tiled):
| Quadrant | Type | Features |
|----------|------|----------|
| NE | Shared | Farmhouse, greenhouse, farm cave, small pond |
| NW | Private | Renewable hardwood stump (forest-style), small pond, Grandpa's shrine |
| SW | Private | Large fishing pond |
| SE | Private | Quarry ore nodes, small pond |

#### v1.0 Feature List:
- [x] Quadrant boundary enforcement (movement restriction)
- [x] Cabin → quadrant ownership assignment on farmhand join
- [x] Host Mode selection (Private/Landlord) via dialog popup
- [x] Player count lock when mode selected
- [x] Landlord 10% cut on farmhand private quadrant shipping
- [x] Per-quadrant shipping bin placement
- [x] Dynamic shared zone calculation based on player count
- [ ] Common item tagging at harvest (common vs private)
- [ ] Common channel restrictions (block common items from private channels)
- [ ] Common shipping bin sale split (equal distribution)
- [ ] Shared cost deduction for common area purchases
- [ ] Building placement validation (personal vs shared zones)
- [ ] Bundle reward distribution to all players
- [ ] Visual indicators at boundary approaches
- [x] Version check at player join

#### Technical Notes:
- Only dependency: SMAPI
- All players must install the mod
- Harmony patches needed for movement/boundary enforcement
- Multiplayer sync must be consistent across clients

#### Cabin Coordinates (confirmed - deterministic with "Separate" placement):
| Cabin Type | Quadrant | Position (upper-left) | Size |
|------------|----------|----------------------|------|
| Log | SW | x17, y43 | 5×3 |
| Stone | SE | x59, y43 | 5×3 |
| Plank | NW | x17, y8 | 5×3 |

#### Boundary Passage Coordinates (tiles to enforce):
**SW Quadrant:**
| Passage | Coordinates | Notes |
|---------|-------------|-------|
| Main | x29-x33, y43 | 5 tiles wide |
| South | x38, y57 | 1 tile |
| West | x11-x12, y42 | 2 tiles (obstacle-blocked at start) |

**SE Quadrant:**
| Passage | Coordinates | Notes |
|---------|-------------|-------|
| Main | x47-x50, y43 | 4 tiles wide |
| South | x42, y57 | 1 tile |
| East | x70-x71, y42 | 2 tiles (obstacle-blocked at start) |

**NW Quadrant:**
| Passage | Coordinates | Notes |
|---------|-------------|-------|
| Main | x32-x36, y29-y30 | 5×2 tiles |
| North | x39, y14-y15 | 1×2 tiles |
| West | x11-x12, y35 | 2 tiles (obstacle-blocked at start) |

#### Shipping Bin Coordinates:
| Quadrant | Position |
|----------|----------|
| NW (Plank) | x22, y10 |
| SW (Log) | x22, y45 |
| SE (Stone) | x64, y45 |

#### Common Chest Coordinates (Distribution Chests):
| Quadrant | Position |
|----------|----------|
| NW (Plank) | x17, y11 |
| SW (Log) | x17, y44 |
| SE (Stone) | x59, y46 |

#### Open Items:
- [x] Map quadrant boundaries in Tiled (get tile coordinates)
- [x] Identify shared corridor/path coordinates
- [x] Note exact tile positions for shipping bin and common chest placement
- [x] Verify cabin spawn coordinates across multiple tests
- [x] Test cabin placement with 1, 2, 3 farmhands

#### Discussion Points:
- Grandpa's shrine is in NW quadrant - minor concern, iridium is late-game anyway
- Perfection is shared across all players (native multiplayer behavior)
- Ginger Island: fully communal for v1.0, revisit after playtesting
- Festival teleportation may need exception handling
- Obstacle-blocked passages (big rock, hardwood log) need enforcement once cleared with steel axe

---

### Session 2 - February 8, 2026

**Status:** Host Mode Design Complete, Boundary Enforcement Tested

#### Testing Results:
- ✓ Boundary enforcement works for all players with mod installed
- ✓ Game doesn't crash if player without mod joins (they just aren't restricted)
- ✗ Host was incorrectly assigned to NE quadrant with no private farmland

#### Host Mode Feature (NEW):

**Problem:** Original design left host without private farmland in the NE shared quadrant.

**Solution:** Two host modes, selected at game start on Day 1:

| Mode | Description | Available |
|------|-------------|-----------|
| Private | Host claims an empty quadrant as private land | 2-3 players |
| Landlord | Host works commons + receives 10% cut from farmhands | 2-4 players (forced at 4) |

**Private Mode Rules:**
- Host gets NW quadrant as private farmland
- Unoccupied quadrants become shared
- Works like farmhand ownership

**Landlord Mode Rules:**
- Host has no private land — NE quadrant is ALL common (split equally)
- Host receives 10% of farmhand shipped revenue from private quadrants only
- All NE production (crops, animal products) split equally among all players
- Greenhouse/Farm cave production split equally among all players
- Common area purchase costs (buildings, animals) split equally among all players
- Purchase blocked if any player can't cover their share (v1.0)
- Building placement in NE: Any player can place, only host can move/demolish
- Off-farm production (mining, fishing, foraging) belongs to whoever earns it
- Common items tagged at harvest, restricted to common channels only

**Zone Ownership Summary (Landlord Mode):**

| Zone | Production Owner | Building Placement |
|------|------------------|-------------------|
| NE quadrant | Split equally (common) | Any places, host controls |
| Greenhouse (center) | Split equally | N/A (fixed) |
| Farm cave (center) | Split equally | N/A (fixed) |
| Private quadrants | Owner (90%), Host (10%) | Owner only |
| Off-farm | Whoever earns it | N/A |

**Income Example (3-player Landlord):**
- Farmhand ships 10,000g from private quadrant → Farmhand gets 9,000g, Host gets 1,000g
- Common chest deposit of 900g → Each player gets 300g
- NE quadrant harvest of 900g → Each player gets 300g (common split)

**Player Count Lock:**
- Roster locks when host selects mode via dialog popup
- No new players can join after roster is locked
- If farmhands don't like terms, they can leave before lock
- Encourages host to discuss terms before decision

**Mode Selection UI (Dialog Popup):**

When a farmhand joins an unlocked roster, host sees:
1. **Lock or Wait** dialog: "Lock roster now?" or "Wait for more players?"
2. If "Lock Now": Shows **Mode Selection** (Private/Landlord) with available options
3. **Confirmation** dialog: Reviews selected mode and player count
4. If 4 players: Auto-forces **Landlord** mode with notification

Dialog flow handles edge cases:
- 2-3 players: Both modes available
- 4 players: Landlord forced automatically
- Already locked: No dialog shown, just version warning if new player

#### Bugs Fixed This Session:

**1. Dialog Callback Not Firing**
- **Symptom:** Mode selection dialog appeared but clicking options did nothing
- **Root Cause:** Stardew Valley doesn't properly register callbacks when `createQuestionDialogue` is called from within another dialog's callback
- **Fix:** Added `DelayedAction.functionAfterDelay(..., 100)` between all chained dialogs in ModeSelectionHandler.cs

**2. Ghost Farmer Assignment**
- **Symptom:** 4 player IDs in quadrant assignments but only 2 real players; SE and NW assigned to empty-named ghost farmers
- **Root Cause:** `Game1.getAllFarmers()` returns disconnected farmer profiles from previous test sessions
- **Fix:** Changed to `Game1.getOnlineFarmers()` in QuadrantManager.AssignFarmhandsToQuadrants()

**3. Farmhand Initialized Before Host Sync**
- **Symptom:** Farmhand blocked from quadrants even when Landlord mode selected (wrong mode applied)
- **Root Cause:** Farmhand called InitializeQuadrants on SaveLoaded before receiving host's mode selection
- **Fix:** Farmhands now wait for ModeSelected sync message; added IsInitialized flag to QuadrantManager

#### Testing Verified:
```
[INIT] Initializing quadrants: 2 players, Landlord mode
[INIT] Online farmers: [Heyyu, Rock]
Farmhand Rock assigned to SW quadrant.
Quadrant NW is unoccupied, marked as shared.
Quadrant SE is unoccupied, marked as shared.
[INIT] Shared quadrants: [NE, NW, SE]
[INIT] Player assignments:
[INIT]   - Rock owns SW
```
- Host (Heyyu) can access NE, NW, SE (all shared)
- Farmhand (Rock) can access SW (private) + NE, NW, SE (shared)
- Host blocked from Rock's SW quadrant as expected

#### Object Placement Feature (NEW):

**ObjectPlacementHandler.cs** created to manage per-quadrant shipping bins and common chests.

**Implementation:**
- Places real `Mini-Shipping Bin` and `Chest` objects at documented coordinates
- Objects marked with `modData["GoodFences.PlacedObject"]` for identification
- Only places in occupied (non-shared) quadrants
- Clears debris/terrain from target tiles before placement
- Re-places automatically on `DayStarted` if removed (safeguard)
- Triggers immediately after mode selection + on each day start

**Current Behavior:**
- NW, SW, SE get shipping bins and common chests when occupied by a player
- NE (farmhouse/shared) does NOT get objects - always shared

#### Open Question for Next Session:

**Should NE (farmhouse) quadrant also get a common chest?**

Options to decide:
1. **No NE chest** - Host uses main shipping bin, farmhands deposit in their quadrant chests
2. **NE gets a chest too** - Central deposit point for shared/greenhouse production
3. **Only NE gets a chest** - Single central distribution point, farmhands travel to deposit

---

## Next Steps
1. ~~Complete cabin placement testing~~ ✓
2. ~~Document tile coordinates from Tiled~~ ✓
3. ~~Determine shipping bin and common chest placement locations~~ ✓
4. ~~Set up SMAPI mod project structure~~ ✓
5. ~~Test boundary enforcement (core mechanic)~~ ✓
6. ~~Implement Host Mode selection (Private/Landlord)~~ ✓
7. ~~Implement 10% landlord cut on farmhand shipping~~ ✓
8. ~~Per-quadrant shipping bin/chest placement~~ ✓
9. ~~Add NE quadrant common chest and shipping bin placement~~ ✓
10. ~~Create CommonGoodsHandler foundation~~ ✓
11. ~~Chest ownership display and toggle~~ ✓
12. ~~Lock mod-placed objects from ownership toggle~~ ✓
13. **Fix mode dialog timing** — current polling triggers on partial name
14. **Block tool destruction of mod-placed objects** — add Harmony patch
15. **Visual ownership indicator** — chest coloring or rendered overlay
16. Implement Harmony patches for harvest tagging
17. Implement chest insertion restrictions (block common→private)
18. Implement common shipping bin sale tracking and split
19. Implement shared cost deduction for common purchases
20. Implement building placement validation

---

### Session 3 - Continued Work

**Status:** Common Goods Foundation Complete

#### Accomplished This Session:

**1. Resolved NE Quadrant Design Conflict**
- Landlord mode: NE is ALL common (no private farmland for host)
- Animal products in NE split equally among all players
- Common area purchase costs split equally (blocked if any player short)
- Documented design decisions in CommonPrivateNEQuadConvo.md

**2. NE Quadrant Common Objects**
- Added NE coordinates to QuadrantData.cs: Bin (71,14), Chest (69,14)
- Updated ObjectPlacementHandler to always place NE common objects
- NE objects placed regardless of occupancy (farmhouse area is always common)

**3. CommonGoodsHandler Created** (Handlers/CommonGoodsHandler.cs)
- `IsCommonItem()` / `TagAsCommon()` - item modData tagging
- `IsCommonAreaTile()` - checks if tile is in common zone
- `OnItemHarvested()` - tags items harvested from common areas
- `CanPlaceInChest()` - validates common items go to common chests
- `CanShipItem()` - validates common items go to common bin
- `ProcessCommonSales()` - end-of-day common sale processing
- `DistributeCommonProceeds()` - equal split of common revenue

**4. ModEntry.cs Wired Up**
- Added CommonGoodsHandler field and initialization
- Added ObjectListChanged event handler for auto-tagging
- Added ProcessCommonSales call to DayEnding event

#### Outstanding for Full Common Goods:

**Requires Harmony Patches:**
- Crop harvest interception (to tag items when picked up)
- Chest insertion interception (to enforce channel restrictions)
- Shipping bin interception (to track common items shipped)

**Current State:**
- Foundation in place for common goods tracking
- Objects placed in common areas get auto-tagged
- Infrastructure ready, but enforcement needs Harmony patches

---

### Session 4 - February 9, 2026

**Status:** Chest Ownership System Refined, Outstanding Issues Identified

#### Bugs Fixed This Session:

**1. DisplayName Patch Wrong Target**
- **Symptom:** Farmhand-placed objects showed no ownership label
- **Root Cause:** Harmony patch targeted `Chest.DisplayName` but mini-bins are `SObject`, not `Chest`
- **Fix:** Changed patch to target `StardewValley.Object.DisplayName` base class

**2. Mode Dialog Timing (Partial Fix)**
- **Symptom:** Mode selection dialog triggered while farmhand was still typing name
- **Root Cause:** Original 500ms delay too short for character creation
- **Fix:** Implemented polling system - checks every 500ms until farmhand has valid name (60s timeout)
- **Known Flaw:** Triggers on partial name as user types, not after OK click

**3. NE Mini-Bin Coordinate Collision**
- **Symptom:** NE mini-bin placed at 71,14 overlapped vanilla shipping bin (71-72)
- **Fix:** Changed to (70,14) — between chest (69,14) and vanilla bin (71-72,14)

**4. Mini-Bin Tagged to Wrong Owner**
- **Symptom:** All mini-bins showed "(Host's)" regardless of quadrant
- **Root Cause:** `ObjectPlacementHandler` always tagged to host ID
- **Fix:** Added `QuadrantManager.GetQuadrantOwnerID()` to look up actual quadrant owner

**5. Auto-Common Tagging Race Condition**
- **Symptom:** Mod-placed objects tagged as common even though they have PlacedObject marker
- **Root Cause:** Harmony postfix fires BEFORE `ObjectPlacementHandler` sets modData
- **Fix:** Skip auto-common tagging for objects already tagged with `GoodFences.PlacedObject`

**6. Ownership Display Only Worked on Chests**
- **Symptom:** Mini-bins didn't show owner in DisplayName
- **Fix:** Changed DisplayName postfix to check any `Object` with `GoodFences.Owner` modData key

**7. Mod-Placed Objects Could Be Toggled**
- **Symptom:** Shift+Right-click could change common/private on mod-placed infrastructure
- **Fix:** Added `IsModPlacedObject()` check in `ToggleOwnership()` — returns "Farm equipment ownership cannot be changed!"

#### New Code Added:

**ChestOwnershipPatches.cs:**
- `PlacedObjectKey` constant for mod-placed object identification
- `GetOwnerDisplayNameFromObject()` — works on any Object, not just Chest
- `IsModPlacedObject()` — checks for `GoodFences.PlacedObject` marker
- Ownership lock for mod-placed objects in `ToggleOwnership()`

**QuadrantManager.cs:**
- `GetQuadrantOwnerID(Quadrant q)` — returns owner's UniqueMultiplayerID

#### Outstanding Issues (Not Yet Resolved):

**1. Mode Dialog Timing (Still Flawed)**
- Current polling triggers when name field has ANY text (as user types)
- Need different trigger: warp-to-farm, or farmhand-initiated ready signal
- **Problem:** Host can't see farmhand warp events — they fire on farmhand client only

**2. Object Destruction Bypass**
- Shift+Right-click toggle is blocked for mod-placed objects
- **BUT:** Farmhands can still axe/pickaxe the objects and destroy them
- Need Harmony patch on `performToolAction` to block tool damage

**3. No Visual Indication of Ownership**
- DisplayName is set correctly but vanilla Stardew doesn't show it on hover
- Chests Anywhere mod shows names, but not all players have it
- Options: chest color tinting, rendered overlay, or accept naming-only

---

## Project Structure
```
GoodFences/
├── GoodFences.csproj          # Project file with SMAPI references
├── manifest.json              # SMAPI mod manifest
├── ModEntry.cs                # Main entry point
├── Models/
│   ├── ModConfig.cs           # Configuration options (HostMode enum)
│   ├── SaveData.cs            # Per-save persistence (mode lock, player count)
│   ├── QuadrantData.cs        # Boundary coordinates and passage definitions
│   └── QuadrantManager.cs     # Player-to-quadrant assignment logic
├── Handlers/
│   ├── BoundaryHandler.cs     # Movement restriction enforcement
│   ├── ShippingHandler.cs     # Landlord 10% cut processing
│   ├── ModeSelectionHandler.cs # Dialog popup for host mode selection
│   ├── ObjectPlacementHandler.cs # Shipping bin and common chest placement
│   └── CommonGoodsHandler.cs  # Common item tagging and channel restrictions
└── Patches/
    ├── ChestOwnershipPatches.cs # Chest/bin ownership display and toggle
    └── CommonGoodsPatches.cs    # Common item restrictions (placeholder)
```

---

## Repository
- **Local:** Initialized
- **Remote:** https://github.com/tbonehunter/GoodFences.git
