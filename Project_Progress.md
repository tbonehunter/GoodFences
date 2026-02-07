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

#### Open Items:
- [ ] Map quadrant boundaries in Tiled (get tile coordinates)
- [ ] Identify shared corridor/path coordinates
- [ ] Note exact tile positions for shipping bin and common chest placement
- [ ] Verify cabin spawn coordinates across multiple tests
- [ ] Test cabin placement with 1, 2, 3 farmhands

#### Discussion Points:
- Grandpa's shrine is in NW quadrant - minor concern, iridium is late-game anyway
- Perfection is shared across all players (native multiplayer behavior)
- Ginger Island: fully communal for v1.0, revisit after playtesting
- Festival teleportation may need exception handling

---

## Next Steps
1. Complete cabin placement testing
2. Document tile coordinates from Tiled
3. Set up SMAPI mod project structure
4. Implement boundary enforcement (core mechanic first)

---

## Repository
- **Local:** Initialized
- **Remote:** Pending (GitHub repo to be created)
