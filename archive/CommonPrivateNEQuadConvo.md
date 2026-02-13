# CommonPrivateNEQuadConvo.md
# NE Quadrant: Common vs Private Production Discussion

**Date:** February 8, 2026  
**Status:** OPEN - Decision Required

---

## The Core Question

In Landlord mode, what production in the NE quadrant belongs to the landlord (host) privately, and what is common (split equally among all players)?

---

## Current Documentation (Conflicting)

From Project_Progress.md, the current design states:

**Landlord Mode Rules:**
> - Host has no private land
> - Host receives 10% of farmhand shipped revenue from private quadrants only
> - Common chest distributions split equally (no additional host cut)
> - **NE quadrant production belongs to host (100%)**
> - **Greenhouse/Farm cave production split equally among all players**

**Zone Ownership Summary (Landlord Mode):**

| Zone | Production Owner | Building Placement |
|------|------------------|-------------------|
| NE quadrant | Host (100%) | Any places, host controls |
| Greenhouse (center) | Split equally | N/A (fixed) |
| Farm cave (center) | Split equally | N/A (fixed) |
| Private quadrants | Owner (90%), Host (10%) | Owner only |
| Off-farm | Whoever earns it | N/A |

**Conflict:** This says NE is 100% host, but also says "Host has no private land" and greenhouse/farm cave (which are IN NE) are split equally.

---

## Common Goods System (New Design)

During this session, we established a tagging system for common goods:

### The Rule
- Items harvested from common areas get tagged `modData["GoodFences.Common"] = "true"`
- **Common-tagged items are locked to common channels only**
- Common items can ONLY go into: common chests, common machines, common shipping bins
- Common items are BLOCKED from: private chests, private machines, private shipping bins
- When common items are sold, proceeds are split equally (no landlord cut)

### Flow Diagram

```
COMMON AREA HARVEST              PRIVATE AREA / OFF-FARM
        ↓                                  ↓
  Tagged "common"                      No tag (private)
        ↓                                  ↓
  MUST go into:                       Goes anywhere:
  - Common chest                      - Private chest
  - Common artisan machine            - Private machine
  - Common shipping bin               - Private shipping bin
        ↓                                  ↓
  Output still "common"               100% to owner
        ↓                             (minus landlord 10% rent)
  Common sale → split equally
```

### Actions on Common Items

| Action on Common Item | Result |
|----------------------|--------|
| **Shipped/Sold** | Proceeds split equally (no landlord cut) |
| **Gifted to NPC** | Consumed, no split |
| **Eaten** | Consumer gets energy/buff |
| **Community Center** | Reward shared (money split, items communal) |
| **Quest completion** | Money split, items shared |
| **Cooking input** | Output is common dish |
| **Artisan machine** | Output inherits common tag |

---

## The NE Quadrant Problem

With this common goods system, we need to decide what in NE is common vs landlord-private.

### User's Key Question:
> "We still need to resolve the question of what in the NE quadrant is the private production of the landlord, and what is common."

### Options Presented:

| Option | Landlord's Private | Common Areas | Gameplay Impact |
|--------|-------------------|--------------|-----------------|
| **A: NE all private** | Entire NE quadrant | Greenhouse, Farm cave only | Landlord has significant farming advantage (full quadrant) + rental income |
| **B: NE all common** | None (rent income only) | NE + Greenhouse + Farm cave | Landlord is pure "investor" - no farming, just manages common areas and collects rent. Hardest early game. |
| **C: NE split** | Small area around farmhouse | Rest of NE + Greenhouse + Farm cave | Landlord has small homestead + rent. Middle ground. |

### Gameplay Implications:

- **Option A:** Landlord gets both land AND rent - farmhands might feel this is unfair
- **Option B:** Purest "landlord" roleplay but risky early game with no private farmland
- **Option C:** Middle ground - landlord has small private area near farmhouse

---

## Related Design Decisions

### NE Needs Common Infrastructure

User noted:
> "I think there should be one at the NE farmhouse too, for any common items that will be processed in that quadrant, otherwise we leave the farmer no way to sell the goods he's made with common produce."

**Resolution:** NE quadrant will get:
- Common Chest (for depositing/storing common goods)
- Common Shipping Bin (for selling common goods with split proceeds)

### Machine Placement Question

For artisan machines, should ANY machine placed in common area be communal, or should we place mod-specific "Common Keg", "Common Preserves Jar" etc.?

**User's response implies:** Any machine placed in common area processes common goods. Common input → common output.

---

## Income Structure Summary (Once NE Decision Made)

### If Option A (NE all private to landlord):

| Source | Landlord Gets | Farmhand Gets |
|--------|---------------|---------------|
| Landlord farms NE | 100% | 0% |
| Farmhand farms private quadrant | 10% (rent) | 90% |
| Anyone harvests greenhouse/cave | Equal split | Equal split |
| Off-farm (mining, fishing, foraging) | 0% | 100% to whoever |

### If Option B (NE all common):

| Source | Landlord Gets | Farmhand Gets |
|--------|---------------|---------------|
| Anyone farms NE | Equal split | Equal split |
| Farmhand farms private quadrant | 10% (rent) | 90% |
| Anyone harvests greenhouse/cave | Equal split | Equal split |
| Off-farm (mining, fishing, foraging) | 0% | 100% to whoever |

### If Option C (NE split - small private area):

| Source | Landlord Gets | Farmhand Gets |
|--------|---------------|---------------|
| Landlord farms private homestead | 100% | 0% |
| Anyone farms rest of NE (common) | Equal split | Equal split |
| Farmhand farms private quadrant | 10% (rent) | 90% |
| Anyone harvests greenhouse/cave | Equal split | Equal split |
| Off-farm (mining, fishing, foraging) | 0% | 100% to whoever |

---

## Decision Required

**Which option for NE quadrant in Landlord mode?**

- [ ] **A:** NE all landlord-private (host gets 100% of NE crops)
- [x] **B:** NE all common (split equally, landlord has no private farmland)
- [ ] **C:** NE split (define boundaries for private homestead vs common farmland)

If Option C, need to determine tile boundaries for the private area.

---

## Related: Objects to Place in NE

Once NE ownership is decided:

| Object | Needed? | Purpose |
|--------|---------|---------|
| Common Chest | Yes | Store/distribute common harvest |
| Common Shipping Bin | Yes | Sell common goods (split proceeds) |
| Private Shipping Bin | No (Option B selected) | N/A |

**Coordinates TBD** - need to pick location near farmhouse for NE objects.

---

## Continuation: Animal Production & Shared Costs Discussion

**Date:** February 8, 2026 (continued from Claude.ai conversation)

---

### Animal Production in NE Quadrant

#### The Question:
Should the landlord get all (or a larger portion of) income from animal production in the NE quadrant since they'd be doing most of the daily work (petting, milking, collecting eggs, shearing, feeding)?

#### Discussion Points:

**Arguments for landlord getting more:**
- Landlord does the daily repetitive labor — petting, milking, egg collecting, shearing, feeding
- Equal split on daily chore-intensive work could feel punishing
- Landlord already chose a role with less direct farming income

**Arguments for keeping it equal:**
- Everyone contributes to animal production indirectly — cutting grass fills silos with hay, gathering stone contributes to building purchase prices
- Landlord already receives 10% rent from all farmhand private production as passive income
- Players need eggs, milk, cheese etc. so they should participate in both the work and the production
- Trying to track individual labor contributions to animal welfare would be a nightmare to implement

#### Decision: All NE production split equally

All production in common areas, including animal products, is split equally among all players. The 10% rent from private quadrants is the landlord's compensation for not having private farmland.

---

### Shared Costs for Common Area Purchases

#### The Principle:
Shared benefits require shared costs. When purchasing buildings or animals for common areas, the cost is divided equally among all players.

**Examples:**
- Coop/Barn construction in NE: cost split equally
- Animal purchases (e.g., $16,000 pig): split equally among all players
- Common area upgrades: split equally

#### The Cash Shortfall Problem:
What happens if a player can't cover their share of a common purchase?

**Options considered:**

| Approach | Description | Pros | Cons |
|----------|-------------|------|------|
| **Block the purchase** | If any player can't cover their share, purchase can't happen | Simple, no debt tracking | One broke player holds everyone back |
| **Debt system** | Purchase goes through, short player carries IOU, future revenue garnished | Doesn't stall progress | Complex to implement |
| **Willing participants only** | Only opt-in players share cost and production | Most flexible | Adds UI complexity, partial ownership tracking |
| **Remaining players cover** | Other players cover shortfall, get proportionally more production until balanced | Fair compensation | Proportional ownership system needed |

#### Decision: Block the purchase (v1.0)

For version 1.0, if any player cannot cover their equal share of a common area purchase, the purchase is blocked. This:
- Is the simplest to implement
- Encourages player communication and coordination
- Avoids building debt or proportional ownership systems
- Can be revisited in future versions if playtesting reveals it's too restrictive

---

## Updated NE Quadrant Summary (Landlord Mode)

| Aspect | Rule |
|--------|------|
| **NE crops** | Common — split equally |
| **NE animal products** | Common — split equally |
| **Greenhouse production** | Common — split equally |
| **Farm cave production** | Common — split equally |
| **NE building/animal costs** | Split equally among all players |
| **Purchase blocked if** | Any player can't cover their share |
| **Landlord compensation** | 10% rent on farmhand private quadrant revenue |
| **Common item tagging** | All NE harvested items tagged common, restricted to common channels |

---

## Additional Rule: No Stacking Common with Private Items

**Decision:** Common-tagged items cannot stack with identical private (untagged) items in inventory or chests.

**Rationale:** If common and private items of the same type could stack, it would be impossible to distinguish which portion is common and which is private. A player could harvest 10 common parsnips and 10 private parsnips, and if they merged into a stack of 20, the tagging system breaks — there's no way to know how many should be split vs kept.

**Implementation:** When a player picks up or moves a common-tagged item, it must remain in a separate stack from any identical private items. The mod needs to intercept the inventory stacking logic to prevent merging items with different `modData["GoodFences.Common"]` values.

**Player-facing effect:** A player could see two separate stacks of parsnips in their inventory — one common, one private. This is a visual cue reinforcing which items go to common channels and which are theirs to use freely.

---

---

## MAJOR DESIGN PIVOT — February 13, 2026

### The Realization

After extensive coding and thought, the geographic boundary approach (tying the mod to Four Corners quadrants) is too limiting. A true multiplayer ownership system should:
- Work on ANY farm map (vanilla or modded)
- Not require predefined boundary coordinates
- Not preclude custom farm map mods
- Make map layout irrelevant to ownership

### New Core Principle: Action-Based Ownership

**Ownership follows the player's actions, not the land.** Any player can farm anywhere on any map, but what you produce is yours from seed to sale.

This replaces geographic boundary enforcement entirely. The map becomes a shared space — ownership is determined by who performed the action, tracked through player ID tagging on items and objects.

---

### How It Works: The Ownership Chain

| Action | Ownership Rule |
|--------|---------------|
| **Player plants seed** | Crop tagged with player ID |
| **Watering/fertilizing** | Only the owner can do this |
| **Harvesting** | Only the owner can harvest; item carries the tag |
| **Artisan processing** | Only the owner can put it in a machine; output inherits tag |
| **Shipping/selling** | Only the owner can ship/sell; 100% revenue to owner |
| **Cooking** | Only the owner can use it (goes in their fridge only) |
| **Eating** | Only the owner gets energy/buff |
| **Gifting** | Only the owner can gift it to NPCs |

The tag persists through the entire production chain. A parsnip planted by Player A becomes parsnip juice that only Player A can process, ship, cook with, or consume.

---

### Voluntary Sharing: Common Chests

Sharing is opt-in, not forced. Two possible approaches:

**Community common chest:** A shared chest where any player can deposit items. Items placed in a common chest lose their player tag and become communal — split equally among all players or available to anyone.

**Per-player common chests:** Each player has their own common chest. Items they place in it are offered to the community. This gives players more control over what they share.

Either way, the key principle is: **nothing is shared unless a player actively chooses to share it.**

---

### Building Slot Marketplace

Buildings default to private ownership — whoever built it, owns it. But owners can sell access to other players.

#### How It Works:
- Player A builds a coop (costs X gold)
- Coop holds 8 animal slots
- Player B wants to house 2 animals in Player A's coop
- Player B pays Player A 25% of construction cost (2/8 = 25%)
- Player B now has 2 slots and can purchase/place animals in those slots
- Animals owned by Player B are tagged to Player B
- Only Player B can collect products from their animals

#### This Creates Natural Economic Dynamics:
- Players who rush animal buildings early can sell slots to others
- Players can specialize (crops vs animals) and trade access
- No need to duplicate expensive infrastructure
- Entirely voluntary and negotiated between players

---

### Open Questions Requiring Resolution

#### Building Slot Ownership:
1. **Permanent or rental?** Does the slot buyer own those slots permanently after the one-time payment, or is there an ongoing rental/lease fee?
2. **Eviction?** Can the building owner revoke access, or is it locked once purchased?
3. **Building upgrades:** If the owner upgrades (e.g., coop → deluxe coop), do existing slot-holders pay their proportional share of the upgrade cost? What if they refuse or can't afford it?

#### Animal Ownership in Shared Buildings:
4. **Animal tagging:** Animals purchased by the slot-buyer are tagged to them — their chickens produce eggs only they can collect. Correct?
5. **Animal care:** Can only the animal owner pet/feed their animals, or can any player with building access do it? If someone else pets your animal, does it still count for friendship?
6. **Hay consumption:** Animals eat hay from the silo. If multiple players have animals in one building, how is hay cost attributed?

#### Other Ownership Scenarios:
7. **Trees:** Who owns a fruit tree? Whoever planted it? Can only they harvest it?
8. **Foraged items:** Tagged to whoever picks them up? (No planting action involved)
9. **Wild seeds/fiber:** Same question — pickup-based ownership?
10. **Fishing:** Catch tagged to the fisher? (Likely yes, straightforward)
11. **Mining/geodes:** Resources tagged to whoever mines/breaks them?
12. **Artifact spots:** Tagged to whoever digs them up?

#### Cooperation Mechanics:
13. **Can two players choose to share?** Is there a mechanism for voluntary partnerships beyond the common chest?
14. **Gift between players:** Can Player A give a tagged item directly to Player B, transferring ownership?
15. **Hired help:** Can Player A pay Player B to water their crops? How would that work with tag restrictions?

#### Technical Considerations:
16. **Existing farm objects at game start:** Who owns the initial parsnip seeds, tools, etc.? Host by default?
17. **NPC-planted items:** If a game event or NPC places something on the farm, who owns it?
18. **Crop fairy:** If a fairy grows someone's crop overnight, does ownership persist? (Should be yes)
19. **Meteorites/debris:** Farm events that spawn objects — who owns the resource?

---

### What This Replaces

| Old Design (Geographic) | New Design (Action-Based) |
|--------------------------|---------------------------|
| Four Corners map only | Any farm map |
| Quadrant boundaries | No boundaries needed |
| Host assigns quadrants | Players farm anywhere |
| Private zones via coordinates | Private ownership via tagging |
| Common zones by location | Common chests by player choice |
| Landlord mode with rent | Building slot marketplace |
| Map-specific cabin placement | Standard cabin mechanics |
| Boundary enforcement (Harmony patches on movement) | Item/action tagging (Harmony patches on plant/harvest/process/sell) |

### What Carries Forward

These concepts from the original design are still valid:
- Common item tagging system (`modData["GoodFences.Common"]`)
- No stacking common with private items
- Common chest distribution mechanics
- Bundle reward distribution to all players
- Separate wallets (native game feature)
- All players must install the mod
- Version check at player join

---

## Next Steps (Revised)

1. ~~**DECIDED:** Option B — NE all common in Landlord mode~~ *(superseded by pivot)*
2. ~~**DECIDED:** Animal production in NE split equally~~ *(superseded by pivot)*
3. ~~**DECIDED:** Common area purchase costs split equally~~ *(superseded by pivot)*
4. **RESOLVE:** Open questions listed above (building slots, animal ownership, foraged items, etc.)
5. **DESIGN:** Player ID tagging system — how tags are applied, inherited, and enforced at each action point
6. **DESIGN:** Building slot purchase UI/interaction flow
7. **DESIGN:** Common chest mechanics in new action-based model
8. **PROTOTYPE:** Start with crop ownership chain (plant → water → harvest → process → sell) as proof of concept
9. **TEST:** Verify tagging works across save/load and multiplayer sync
