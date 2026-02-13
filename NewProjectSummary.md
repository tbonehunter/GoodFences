# Good Fences — New Project Summary

**Date:** February 13, 2026
**Status:** Design Phase — Major Pivot from Geographic to Action-Based Ownership

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

Common-tagged items cannot stack with private-tagged items of the same type. If a player has both common parsnips and private parsnips, they appear as two separate stacks in inventory. This prevents mixing and makes ownership visually clear.

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

Stardew Valley's native "separate money" toggle handles per-player finances. Each player maintains their own wallet and shipping bin revenue is credited to whoever deposited the items. This is a game setting, not mod code.

---

## Technical Requirements

- **Only dependency:** SMAPI
- **All players must install the mod** — each client runs its own copy of the game; the host cannot force mod behavior on unmodded clients
- **Version check at player join** — warn if players are running different mod versions
- **Multiplayer sync** — tagging must be consistent across all clients
- **Save persistence** — tags must survive save/load cycles
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

7. **Fruit trees:** Who owns a fruit tree? Whoever planted it? Can only they harvest it?
8. **Foraged items:** Tagged to whoever picks them up?
9. **Wild seeds/fiber:** Pickup-based ownership?
10. **Fishing:** Catch tagged to the fisher?
11. **Mining/geodes:** Resources tagged to whoever mines/breaks them?
12. **Artifact spots:** Tagged to whoever digs them up?

### Cooperation Mechanics

13. **Player-to-player gifting:** Can Player A give a tagged item directly to Player B, transferring ownership?
14. **Partnerships:** Is there a mechanism for voluntary partnerships beyond the common chest?
15. **Hired help:** Can Player A pay Player B to water their crops? How would that work with tag restrictions?

### Game Events and Edge Cases

16. **Starting items:** Who owns the initial parsnip seeds, tools, etc.? Host by default?
17. **NPC-placed items:** If a game event or NPC places something on the farm, who owns it?
18. **Crop fairy:** If a fairy grows someone's crop overnight, does ownership persist?
19. **Meteorites/debris:** Farm events that spawn objects — who owns the resource?
20. **Community Center bundles:** Currently planned to distribute rewards to all players. Still correct in new model?

### Purchase Blocking

21. **Common area purchases:** When purchasing something communal (e.g., items for common chest), is cost split among all players?
22. **Insufficient funds:** If a common purchase requires cost-splitting and a player can't cover their share, block the purchase? (Previously decided yes for v1.0 — still valid?)

---

## Development Approach

### Proof of Concept (First Priority)
Start with the crop ownership chain as the core proof of concept:
Plant → Water → Harvest → Process → Sell

If tagging works reliably through this chain across multiplayer with save/load persistence, the same pattern extends to everything else.

### Implementation Order (Tentative)
1. Player ID tagging system — how tags are applied, inherited, and enforced
2. Crop ownership chain (proof of concept)
3. Common chest mechanics
4. Animal/building ownership and slot marketplace
5. Edge cases (foraging, fishing, mining, game events)
6. UI elements (visual indicators, slot purchase interface)
7. Version check and multiplayer sync validation

---

## Repository
- **Local:** Initialized
- **Remote:** https://github.com/tbonehunter/GoodFences.git
