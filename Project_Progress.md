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
- [ ] Quadrant boundary enforcement (movement restriction)
- [ ] Cabin → quadrant ownership assignment on farmhand join
- [ ] Per-quadrant shipping bin placement
- [ ] Dynamic shared zone calculation based on player count
- [ ] Common chests with automatic equal distribution
- [ ] Building placement validation (personal vs shared zones)
- [ ] Bundle reward distribution to all players
- [ ] Visual indicators at boundary approaches
- [ ] Version check at player join

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

#### Open Items:
- [x] Map quadrant boundaries in Tiled (get tile coordinates)
- [x] Identify shared corridor/path coordinates
- [ ] Note exact tile positions for shipping bin and common chest placement
- [x] Verify cabin spawn coordinates across multiple tests
- [x] Test cabin placement with 1, 2, 3 farmhands

#### Discussion Points:
- Grandpa's shrine is in NW quadrant - minor concern, iridium is late-game anyway
- Perfection is shared across all players (native multiplayer behavior)
- Ginger Island: fully communal for v1.0, revisit after playtesting
- Festival teleportation may need exception handling
- Obstacle-blocked passages (big rock, hardwood log) need enforcement once cleared with steel axe

---

## Next Steps
1. ~~Complete cabin placement testing~~ ✓
2. ~~Document tile coordinates from Tiled~~ ✓
3. Set up SMAPI mod project structure
4. Implement boundary enforcement (core mechanic first)
5. Determine shipping bin and common chest placement locations

---

## Repository
- **Local:** Initialized
- **Remote:** https://github.com/tbonehunter/GoodFences.git
