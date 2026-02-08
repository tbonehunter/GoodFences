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
- [ ] Host Mode selection (Private/Landlord) on Day 1
- [ ] Player count lock on Day 1
- [ ] Landlord 10% cut on farmhand shipping revenue
- [ ] Per-quadrant shipping bin placement
- [ ] Dynamic shared zone calculation based on player count
- [ ] Common chests with automatic equal distribution
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
- Host has no private land
- Host receives 10% of all farmhand shipped revenue (private + common)
- NE quadrant production belongs to host (100%)
- Greenhouse/Farm cave production split equally among all players
- Building placement in NE: Any player can place, only host can move/demolish
- Off-farm production (mining, fishing, foraging) belongs to whoever earns it

**Zone Ownership Summary (Landlord Mode):**

| Zone | Production Owner | Building Placement |
|------|------------------|-------------------|
| NE quadrant | Host (100%) | Any places, host controls |
| Greenhouse (center) | Split equally | N/A (fixed) |
| Farm cave (center) | Split equally | N/A (fixed) |
| Private quadrants | Owner (90%), Host (10%) | Owner only |
| Off-farm | Whoever earns it | N/A |

**Income Example (3-player Landlord):**
- Farmhand ships 10,000g from private quadrant → Farmhand gets 9,000g, Host gets 1,000g
- Common chest deposit of 900g → Each player gets 300g
- Host ships from NE quadrant → Host gets 100%

**Player Count Lock:**
- Roster locks on Day 1 when host selects mode
- No new players after Day 1
- If farmhands don't like terms, they can leave before lock
- Encourages host to discuss terms before decision

---

## Next Steps
1. ~~Complete cabin placement testing~~ ✓
2. ~~Document tile coordinates from Tiled~~ ✓
3. ~~Determine shipping bin and common chest placement locations~~ ✓
4. ~~Set up SMAPI mod project structure~~ ✓
5. ~~Test boundary enforcement (core mechanic)~~ ✓
6. Implement Host Mode selection (Private/Landlord)
7. Implement 10% landlord cut on farmhand shipping
8. Implement common chest distribution
9. Implement building placement validation

---

## Project Structure
```
GoodFences/
├── GoodFences.csproj          # Project file with SMAPI references
├── manifest.json              # SMAPI mod manifest
├── ModEntry.cs                # Main entry point
├── Models/
│   ├── ModConfig.cs           # Configuration options
│   ├── QuadrantData.cs        # Boundary coordinates and passage definitions
│   └── QuadrantManager.cs     # Player-to-quadrant assignment logic
└── Handlers/
    └── BoundaryHandler.cs     # Movement restriction enforcement
```

---

## Repository
- **Local:** Initialized
- **Remote:** https://github.com/tbonehunter/GoodFences.git
