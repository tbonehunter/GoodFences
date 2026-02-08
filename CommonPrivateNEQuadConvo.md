# CommonPrivateNEQuadConvo.md
# NE Quadrant: Common vs Private Production Discussion

**Date:** February 8, 2026  
**Status:** RESOLVED - Implementing

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

## Next Steps

1. **DECIDED:** Option B — NE all common in Landlord mode
2. **DECIDED:** Animal production in NE split equally
3. **DECIDED:** Common area purchase costs split equally, blocked if any player short
4. Update ObjectPlacementHandler to place common chest/bin in NE
5. Implement common item tagging and channel restrictions
6. Implement shared cost deduction on common area purchases
7. Implement purchase blocking when player funds insufficient
