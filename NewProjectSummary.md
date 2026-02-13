# Good Fences — New Project Summary

**Date:** February 13, 2026
**Status:** Step 1 — Passive Tagging (code generated, ready for testing)

---

## Project Overview

Good Fences is a Stardew Valley SMAPI mod that creates true multiplayer ownership in co-op farming. Rather than a shared communal farm, each player owns what they produce — from seed to sale — regardless of where on the farm they work.

The mod works on any farm map (vanilla or modded) with no map-specific boundaries or coordinate definitions required.

**"Good fences make good neighbors."** — Robert Frost, *Mending Wall*

---

## Core Principle

**Ownership follows the player's actions, not the land.**

Any player can farm anywhere on any map. What you produce is yours from the moment of creation through the entire production chain. The map is shared space — ownership is tracked through player ID tagging on items and objects.

---

## How It Works

### The Ownership Chain

Every item is tagged with the player ID of whoever created it. That tag persists through the entire production chain and restricts who can interact with the item at each stage.

| Action | Ownership Rule |
|--------|---------------|
| Player plants seed | Crop tagged with player ID |
| Watering/fertilizing | Only the owner can do this |
| Harvesting | Only the owner can harvest; item carries the tag |
| Artisan processing | Only the owner can input it; output inherits tag |
| Shipping/selling | Only the owner can ship/sell; 100% revenue to owner |
| Cooking | Only the owner can use it (goes in their fridge only) |
| Eating | Only the owner gets energy/buff |
| Gifting to NPCs | Only the owner can gift it |

### Item Stacking Rule

Items with different owner tags cannot stack. If a player has parsnips tagged to Player A and parsnips tagged to Player B, they appear as two separate stacks in inventory. This prevents mixing and makes ownership visually clear.

---

## Current Codebase (v2.0.0-alpha.1)

### Architecture: Two-Layer Tagging

**Layer 1 — Specific Harmony patches** handle cases where ownership must follow a chain rather than going to the current holder:

| Patch Target | Action | Tagging Logic |
|-------------|--------|---------------|
| `HoeDirt.plant` (postfix) | Player plants seed | Soil tagged with planter's ID |
| `Crop.harvest` (postfix) | Crop harvested | Item tagged with planter's ID (from soil), not harvester |
| `Object.performObjectDropInAction` (postfix) | Item dropped into machine | Machine stores input item's owner |
| `Object.checkForAction` (prefix) | Player collects machine output | Output tagged with stored owner from machine |
| `Tree.plant` (postfix) | Wild tree planted | Tree tagged with planter's ID |
| SMAPI `TerrainFeatureListChanged` event | Fruit tree planted | Fruit tree tagged with planter's ID |

**Layer 2 — Catch-all** via SMAPI's `InventoryChanged` event tags any new untagged item with the receiving player's ID. This covers:
- Foraging
- Fishing
- Mining / geode breaking
- Artifact spots
- Combat drops
- Shop purchases
- Any other item acquisition not handled by a specific patch

The catch-all **never overwrites** existing owner tags, so chain-specific patches take priority.

### File Structure

```
GoodFences/
├── manifest.json                    # Mod metadata
├── GoodFences.csproj               # Build config (net6.0, Harmony, nullable)
├── ModEntry.cs                      # Entry point, SMAPI events, catch-all tagger
├── Handlers/
│   └── OwnershipTagHandler.cs      # Core tagging logic, read/write/display
├── Models/
│   └── ModConfig.cs                # Config (DebugMode only for now)
└── Patches/
    └── OwnershipTagPatches.cs      # All Harmony patches for passive tagging
```

### Mod Data Keys

| Key | Applied To | Value | Purpose |
|-----|-----------|-------|---------|
| `GoodFences.Owner` | Items, soil, machines, trees | Player's UniqueMultiplayerID | Tracks who produced/owns the object |
| `GoodFences.Common` | Items (future) | "true" | Marks item as communal (step 3) |

### Visual Feedback (Testing)

- Tagged items display `(PlayerName)` suffix in inventory — e.g., "Parsnip (Stan)"
- Common items will display `(C)` suffix (future)
- SMAPI console logs `[TAG]` prefixed messages for every tagging event when DebugMode is true

---

## Implementation Plan

### Step 1: Player ID Tagging System ← CURRENT
**Status:** Code generated, ready for testing

- [x] Core tagging handler (read/write/display owner tags)
- [x] Crop chain: soil tagged at planting, harvest item inherits planter's ID
- [x] Artisan chain: machine stores input owner, output inherits it
- [x] Tree/fruit tree planting tagged
- [x] Catch-all inventory tagger for foraging, fishing, mining, etc.
- [x] Stack prevention (different owners can't merge)
- [x] Display name suffix for visual verification
- [x] Version check on peer connect
- [ ] **TEST:** Verify tags apply correctly in single-player
- [ ] **TEST:** Verify tags persist across save/load
- [ ] **TEST:** Verify tags work in multiplayer (two players see correct names)
- [ ] **TEST:** Verify artisan chain (e.g., parsnip → preserves jar → pickles keeps owner)
- [ ] **TEST:** Verify catch-all covers foraging, fishing, mining
- [ ] **TEST:** Verify stacking prevention works (two players' parsnips don't merge)

### Step 2: Crop Ownership Enforcement
**Status:** Not started — depends on Step 1 testing

- [ ] Only crop owner can water their crops
- [ ] Only crop owner can fertilize their crops
- [ ] Only crop owner can harvest their crops
- [ ] Only tree owner can harvest fruit/shake trees
- [ ] Visual feedback when blocked ("This crop belongs to [PlayerName]")
- [ ] Handle edge cases: sprinklers, Junimo huts, crop fairy
- [ ] **TEST:** Multiplayer enforcement verification

### Step 3: Common Chest Mechanics
**Status:** Not started — depends on Step 2 testing

- [ ] Common chest designation (place or toggle)
- [ ] Items placed in common chest lose owner tag, gain common tag
- [ ] Common items restricted to common channels only
- [ ] Common items cannot stack with private items
- [ ] Common shipping bin with equal revenue split
- [ ] Common item display: `(C)` suffix and tooltip explanation
- [ ] **TEST:** Distribution mechanics, sale splitting

### Future Steps (not yet scoped in detail)
- Animal/building ownership and slot marketplace
- Building slot purchase UI
- Player-to-player item gifting (ownership transfer)
- Shared cost splitting for common purchases
- Edge cases: game events, NPC actions, festivals

---

## Voluntary Sharing: Common Chests

Nothing is shared unless a player actively chooses to share it.

Players can deposit items into a common chest. Items placed in a common chest lose their player tag and become communal — split equally among all players. The remainder from odd splits goes to the player who deposited the items.

Common-tagged items are restricted to common channels only:
- Can go into: common chests, common machines, common shipping bins
- Blocked from: private chests, private machines, private shipping bins

### Actions on Common Items

| Action | Result |
|--------|--------|
| Shipped/Sold | Proceeds split equally among all players |
| Gifted to NPC | Consumed, no split |
| Eaten | Consumer gets energy/buff |
| Community Center bundle | Reward shared (money split, items communal) |
| Quest completion | Money split, items shared |
| Cooking input | Output is a common dish |
| Artisan machine | Output inherits common tag |

---

## Building Slot Marketplace

Buildings default to private ownership — whoever built it, owns it. But owners can sell access to other players.

### How It Works

1. Player A builds a coop (costs X gold)
2. Coop holds 8 animal slots
3. Player B wants to house 2 animals in Player A's coop
4. Player B pays Player A 25% of construction cost (2/8 = 25%)
5. Player B now has 2 slots and can purchase/place animals in those slots
6. Animals owned by Player B are tagged to Player B
7. Only Player B can collect products from their animals

### Economic Dynamics

- Players who rush animal buildings early can sell slots to others
- Players can specialize (crops vs animals) and trade access
- No need to duplicate expensive infrastructure
- Entirely voluntary and negotiated between players

---

## Separate Wallets

Stardew Valley's native "separate money" toggle handles per-player finances. Each player maintains their own wallet and shipping bin revenue is credited to whoever deposited the items. This is a game setting, not mod code. The mod warns on load if separate wallets are not enabled.

---

## Technical Requirements

- **Only dependency:** SMAPI
- **All players must install the mod** — each client runs its own copy of the game; the host cannot force mod behavior on unmodded clients
- **Version check at player join** — warns if players are running different mod versions
- **Multiplayer sync** — tagging must be consistent across all clients
- **Save persistence** — tags stored via `modData` which persists natively through save/load
- **Map agnostic** — no hardcoded coordinates or map-specific logic

---

## Open Questions Requiring Resolution

### Building Slot Ownership

1. **Permanent or rental?** Does the slot buyer own slots permanently after the one-time payment, or is there an ongoing rental/lease fee?
2. **Eviction?** Can the building owner revoke access once sold, or is it locked?
3. **Building upgrades:** If the owner upgrades (e.g., coop to deluxe coop), do existing slot-holders pay their proportional share of the upgrade cost? What if they refuse or can't afford it?

### Animal Ownership in Shared Buildings

4. **Animal tagging:** Animals purchased by the slot-buyer are tagged to them — their chickens produce eggs only they can collect. Correct?
5. **Animal care:** Can only the animal owner pet/feed their animals, or can any player with building access do it? If someone else pets your animal, does it still count for friendship?
6. **Hay consumption:** Animals eat hay from the silo. If multiple players have animals in one building, how is hay cost attributed?

### Ownership at Point of Creation

7. **Fruit trees:** Who owns a fruit tree? Whoever planted it? Can only they harvest it? *(Currently: tagged to planter)*
8. **Foraged items:** Tagged to whoever picks them up? *(Currently: yes, via catch-all)*
9. **Wild seeds/fiber:** Pickup-based ownership? *(Currently: yes, via catch-all)*
10. **Fishing:** Catch tagged to the fisher? *(Currently: yes, via catch-all)*
11. **Mining/geodes:** Resources tagged to whoever mines/breaks them? *(Currently: yes, via catch-all)*
12. **Artifact spots:** Tagged to whoever digs them up? *(Currently: yes, via catch-all)*

### Cooperation Mechanics

13. **Player-to-player gifting:** Can Player A give a tagged item directly to Player B, transferring ownership?
14. **Partnerships:** Is there a mechanism for voluntary partnerships beyond the common chest?
15. **Hired help:** Can Player A pay Player B to water their crops? How would that work with tag restrictions?

### Game Events and Edge Cases

16. **Starting items:** Who owns the initial parsnip seeds, tools, etc.? *(Currently: tagged to whoever has them in inventory at first inventory change)*
17. **NPC-placed items:** If a game event or NPC places something on the farm, who owns it?
18. **Crop fairy:** If a fairy grows someone's crop overnight, does ownership persist? *(Should be yes — soil tag is unaffected)*
19. **Meteorites/debris:** Farm events that spawn objects — who owns the resource?
20. **Community Center bundles:** Distribute rewards to all players?
21. **Sprinklers:** Can a player's sprinkler water another player's crops? Should it?
22. **Junimo huts:** Auto-harvest crosses ownership boundaries. Block? Allow? Tag output to crop owner?

### Purchase Blocking

23. **Common area purchases:** When purchasing something communal, is cost split among all players?
24. **Insufficient funds:** If a common purchase requires cost-splitting and a player can't cover their share, block the purchase? *(Previously decided yes for v1.0 — still valid?)*

---

## Repository
- **Local:** Initialized
- **Remote:** https://github.com/tbonehunter/GoodFences.git
