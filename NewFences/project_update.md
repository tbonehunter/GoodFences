# Good Fences — Project Update: Step 1 Development Log

**Date:** February 13, 2026
**Version:** 2.0.0-alpha.1
**Status:** Step 1 passive tagging — crop harvest fix WORKING, extended testing in progress

---

## Overview

This document captures the chain of thought, problems encountered, and solutions
developed during step 1 (passive tagging) implementation. It serves as continuity
for future development sessions.

---

## Architecture Evolution

### Starting Point: Two-Layer Tagging

The initial design used two layers:

**Layer 1 — Specific Harmony patches** for chain-dependent ownership:
- Crop planting → harvest → artisan processing → sale
- Each step reads the owner from the previous step

**Layer 2 — Catch-all** via SMAPI `InventoryChanged` event tags any untagged
item entering inventory with the receiving player's ID. Covers foraging,
fishing, mining, artifact spots, combat drops, shop purchases.

The catch-all never overwrites existing tags, so Layer 1 takes priority.

### Problem 1: Machine Output Loses Owner

**Symptom:** `MACHINE-IN` logged correctly, but `MACHINE-OUT` never appeared.
When a player collected machine output, the catch-all tagged it to the
collector instead of the original input owner.

**Root cause:** The `Object.checkForAction` prefix we used to tag output
before collection wasn't firing reliably — likely a Stardew Valley 1.6
change in how machine collection works internally.

**Solution:** Replaced the `checkForAction` prefix with a **periodic scan**
(`PreTagMachineOutput`) called from `ModEntry.OnUpdateTicked` every 60
ticks (~1 second). This scans all machines in the current location and
pre-tags any ready output with the stored input owner. By the time anyone
collects, the tag is already present.

**Flow:**
1. Player drops item into machine → `MACHINE-IN` stores owner on machine ✓
2. Machine finishes → periodic scan finds ready output, tags it (`MACHINE-OUT`)
3. Player collects → catch-all sees item already tagged, skips it ✓

**Status:** Working correctly. Ownership persists through full artisan chain.

### Problem 2: Tree Tagging at Day Start

**Symptom:** At the start of each day, pre-existing trees on the farm fired
`TerrainFeatureListChanged` events and got tagged to whichever player's
client processed them first — random assignment between host and farmhand.

**Root cause:** The game reloads/regenerates terrain features during day
transitions, firing "added" events for trees that already exist.

**Solution:** Added a `DayFullyStarted` flag that starts `false` on every
save load and day transition, then gets set `true` after 120 ticks (~2
seconds). The terrain feature handler ignores events until the flag is set.

**Status:** Working. Pre-existing trees no longer get spuriously tagged.

### Problem 3: Harvest Items Tagged Wrong (Item Stacking)

**Symptom:** When Stan harvested Katie's parsnips, ALL parsnips were tagged
to Stan. The log showed "HARVEST: Mermaid Boots → Katie" — clearly tagging
the wrong item entirely.

**Root cause (attempt 1):** The `FindLastInventoryItem` helper walked
inventory backwards to find the "most recently added" item. But when a
harvested parsnip stacked with existing parsnips, no new inventory slot
was created. The helper found whatever was in the last slot (Mermaid Boots)
and tagged that instead.

**Fix attempt 1 — PendingHarvestOwner via catch-all:**
- Crop.harvest prefix stores planter's ID in `PendingHarvestOwner`
- Catch-all in `InventoryChanged` reads it and tags accordingly
- Crop.harvest postfix clears it

**Why it failed:** The postfix cleared `PendingHarvestOwner` synchronously
inside the harvest method. SMAPI's `InventoryChanged` fires at end of tick,
not immediately. By the time the catch-all ran, the pending owner was
already null.

**Fix attempt 2 — Don't clear in postfix, clear in catch-all:**
- Removed the postfix clear
- Catch-all clears after using the value
- Added stale-value safety clear in `OnUpdateTicked` (5 tick timeout)

**Why it failed:** When a harvested parsnip stacks with an existing parsnip
stack, SMAPI doesn't report it in `e.Added` — the stack count silently
increments. The catch-all never sees the item and can't tag it. Only the
first parsnip (creating a new stack) was tagged correctly.

**Fix attempt 3 — Current approach (in testing):**

The fundamental insight: **tag the item BEFORE it enters inventory, not after.**

New architecture:
1. `HoeDirt.plant` postfix tags both the **soil** AND the **Crop** with planter's ID
2. `Crop.harvest` prefix reads owner from the Crop (falls back to soil), stores in `PendingHarvestOwner`
3. **NEW: `Farmer.addItemToInventoryBool` prefix** — runs BEFORE the game's stacking logic. Tags incoming item with `PendingHarvestOwner` if set, otherwise tags to receiving farmer.
4. `canStackWith` postfix runs — both items now have owner tags, different owners stay in separate stacks
5. `Crop.harvest` postfix clears `PendingHarvestOwner` synchronously (safe now because the inventory prefix already used the value)

**Key change:** Tagging the Crop object directly (not just soil) provides
cleaner ownership storage — the owner lives on the thing being harvested.
Soil tagging is retained as a fallback and for future step 2 enforcement
(preventing non-owners from interacting with crops on tagged soil).

**Bonus:** The `Farmer.addItemToInventoryBool` prefix also serves as the
primary tagger for ALL items (foraging, fishing, mining, etc.), not just
harvests. It replaces the catch-all's role for most items. The SMAPI
`InventoryChanged` catch-all remains as a backup for edge cases where
items enter inventory through a path that doesn't call
`addItemToInventoryBool`.

**Status:** In testing. If this doesn't work, the fallback plan is to
implement step 2 enforcement early — tag soil at planting, only the soil
owner can interact with that soil tile (water, fertilize, harvest). This
sidesteps the harvest tagging problem entirely because only the owner
could harvest their own crops, and the catch-all would correctly tag
the output to them.

---

## Current File Structure

```
GoodFences/
├── manifest.json                    # v2.0.0-alpha.1
├── GoodFences.csproj               # net6.0, Harmony, nullable
├── ModEntry.cs                      # Entry point, events, backup catch-all
├── Handlers/
│   └── OwnershipTagHandler.cs      # Core tag read/write/display logic
├── Models/
│   └── ModConfig.cs                # DebugMode only for now
└── Patches/
    └── OwnershipTagPatches.cs      # All Harmony patches
```

---

## Current Harmony Patches

| Patch | Target | Type | Purpose |
|-------|--------|------|---------|
| `HoeDirt_Plant_Postfix` | `HoeDirt.plant` | Postfix | Tag soil + crop with planter's ID |
| `Crop_Harvest_Prefix` | `Crop.harvest` | Prefix | Store crop owner in PendingHarvestOwner |
| `Crop_Harvest_Postfix` | `Crop.harvest` | Postfix | Clear PendingHarvestOwner |
| `Farmer_AddItemToInventory_Prefix` | `Farmer.addItemToInventoryBool` | Prefix | Tag items BEFORE stacking (primary tagger) |
| `Object_DropIn_Postfix` | `Object.performObjectDropInAction` | Postfix | Store input owner on machine |
| `PreTagMachineOutput` | (called from OnUpdateTicked) | Scan | Pre-tag ready machine output |
| `Item_CanStackWith_Postfix` | `Item.canStackWith` | Postfix | Prevent different-owner stacking |
| `Object_DisplayName_Postfix` | `Object.DisplayName` | Postfix | Show (PlayerName) suffix |

## SMAPI Events Used

| Event | Purpose |
|-------|---------|
| `GameLoop.GameLaunched` | Future: GMCM integration |
| `GameLoop.SaveLoaded` | Initialize, check separate wallets |
| `GameLoop.DayStarted` | Reset DayFullyStarted flag |
| `GameLoop.UpdateTicked` | Machine output scan, DayFullyStarted delay |
| `Player.InventoryChanged` | Backup catch-all tagger |
| `World.TerrainFeatureListChanged` | Tag fruit trees / wild trees on planting |
| `Multiplayer.PeerConnected` | Version check |

---

## Mod Data Keys

| Key | Applied To | Value | Purpose |
|-----|-----------|-------|---------|
| `GoodFences.Owner` | Items, soil, crops, machines, trees, fruit trees | Player's UniqueMultiplayerID | Ownership tracking |
| `GoodFences.Common` | Items (future) | "true" | Marks communal items (step 3) |

---

## What Works (Verified by Testing)

- ✅ Items tagged when entering inventory (foraging, fishing, mining, etc.)
- ✅ Planting tags soil AND crop with planter's ID
- ✅ Crop harvest ownership: planter's ID follows through harvest regardless of who harvests
- ✅ Stack separation: different owners' items stay in separate inventory stacks
- ✅ Artisan machines: input owner stored on machine, output pre-tagged before collection
- ✅ Machine output ownership persists regardless of who collects (with Automate disabled)
- ✅ Ownership persists across save/load
- ✅ Ownership persists across day transitions
- ✅ Money credited correctly per separate wallets (no enforcement yet — whoever ships gets the gold)
- ✅ Display name suffix shows (PlayerName) on tagged items
- ✅ Version check warns when peer connects without mod or with different version
- ✅ Pre-existing trees no longer spuriously tagged at day start (120-tick delay)
- ✅ Full chain verified: Plant (Katie) → Harvest (Stan) → Keg → Output correctly shows (Katie)

## What's Being Tested (Extended Testing In Progress)

- 🔄 Multi-day tag persistence — do tags degrade or change over multiple save/load cycles?
- 🔄 Edge cases across various item types and interactions
- 🔄 Behavior with various mod combinations enabled/disabled

## Known Issue: Cross-Player Machine Collection — RESOLVED (Automate Mod)

**Symptom:** When Katie collected all 5 pre-tagged juices from kegs, they
merged into one stack of 5 showing Katie's name. Stan's tag was lost.

**Evidence from log:** Pre-tagging worked correctly — the log shows:
```
MACHINE-OUT: Parsnip Juice → Katie (pre-tagged from Keg at {X:8 Y:9})
MACHINE-OUT: Parsnip Juice → Katie (pre-tagged from Keg at {X:7 Y:9})
MACHINE-OUT: Parsnip Juice → Katie (pre-tagged from Keg at {X:6 Y:9})
MACHINE-OUT: Parsnip Juice → Stan (pre-tagged from Keg at {X:7 Y:9})
MACHINE-OUT: Parsnip Juice → Stan (pre-tagged from Keg at {X:6 Y:9})
```
But only ONE `INVENTORY-PRE` entry appeared when Katie collected all 5.

**Root cause: CONFIRMED — Automate mod.**
Automate (Pathoschild) bypasses all player-facing inventory methods. It
moves items directly between chests and machines using its own transfer
logic, which does not preserve `modData` tags. When Automate was removed
and items were placed directly into kegs by the player, **ownership tags
persisted correctly through the entire chain**: plant → harvest → keg
input → keg output → player collection.

**Our code is working correctly.** The issue is purely mod compatibility.

**Resolution:** Automate integration item added to step 2. Automate is
open source (same Pathoschild collection as Chest Anywhere). We need to
examine its item transfer methods and either Harmony patch them to
preserve `modData` or hook into its API.

## Known Behaviors (Expected for Step 1)

- No enforcement — anyone can interact with anyone's items
- Shipping revenue goes to whoever physically ships (native game behavior)
- Existing save items get tagged to first player who picks them up ("finders keepers" for legacy items)
- Single-player: tagging works but has no gameplay effect
- Automate mod bypasses ownership tags — must be disabled or integrated (step 2)

---

## Fallback Plan If Crop Harvest Fix Fails

**UPDATE: The `Farmer.addItemToInventoryBool` prefix approach WORKS.**
Crop harvest ownership is correctly preserved. This fallback is no longer
needed for the harvest case, but the enforcement strategy remains valid
for step 2 and for resolving the cross-player machine collection issue.

Original fallback plan (retained for reference):

If the `Farmer.addItemToInventoryBool` prefix approach didn't solve the
harvest ownership problem, the fallback was to **implement step 2 enforcement
early**:

1. Keep soil tagging at planting
2. Enforce: only the soil owner can water, fertilize, harvest
3. Since only the owner can harvest, the catch-all correctly tags the output to them
4. This solves the tagging problem by making it physically impossible for someone else to harvest your crops

---

## Next Steps After Step 1 Is Solid

### Step 2: Crop & Machine Ownership Enforcement
- Only crop owner can water, fertilize, harvest
- Only tree owner can harvest fruit/shake trees
- Only machine owner can collect output from their machines
- Visual feedback: "This crop belongs to [PlayerName]"
- Chest ownership: tag on placement, display name suffix, access control
- Edge cases: sprinklers (should they cross ownership?), Junimo huts, crop fairy

### Step 2 Integration: Pathoschild Mod Compatibility
Both mods are open source: https://github.com/Pathoschild/StardewMods

**Automate — CONFIRMED issue, high priority**
- **Problem:** Automate bypasses all player-facing inventory methods. Its
  internal item transfer logic does not preserve `modData` tags. Ownership
  is lost when Automate moves items between chests and machines.
- **Confirmed by testing:** Removing Automate restored correct tag persistence
  through the entire artisan chain.
- **Approach:** Examine Automate's item transfer methods in source. Either
  Harmony patch the transfer to preserve `modData`, or hook into its API
  to re-tag items after transfer. Alternatively, detect Automate presence
  and warn players that automated machines lose ownership tracking.

**Chest Anywhere — gameplay issue, high priority**
- **Problem:** Chest Anywhere shows ALL chests on the farm. Too easy to
  accidentally open another player's chest and take items thinking they're yours.
  Discovered during testing — happened multiple times.
- **Approach options:**
  1. Filter chests by owner — only show chests tagged to the current player
  2. Visual differentiation — keep all visible but with clear owner labels + block access
  3. Both — filter by default, config option to show all (with blocking)
- **Implementation:** Examine chest enumeration method in source and either
  Harmony patch the filter or use its API if one is exposed.

### Step 3: Common Chest Mechanics
- Common chest designation
- Items deposited lose owner tag, gain common tag
- Common items restricted to common channels
- Common shipping bin with equal revenue split

### Open Design Questions
See NewProjectSummary.md for the full list of 24 open questions covering
building slots, animal ownership, cooperation mechanics, game events, and
purchase splitting.

---

## Technical Notes for Future Sessions

### Important API Details Discovered
- `Crop` has its own `modData` dictionary — can be tagged directly
- `HoeDirt` has `modData` — used for soil tagging
- `Tree.plant` doesn't exist as a method — tree planting is handled via SMAPI `TerrainFeatureListChanged`
- `Farmer.addItemToInventoryBool` is the main entry point for items entering player inventory
- Patching `Farmer.addItemToInventoryBool` as a prefix lets us tag items BEFORE stacking logic runs — this was the key breakthrough for harvest ownership
- SMAPI `InventoryChanged.e.Added` does NOT fire when an item stacks onto an existing stack
- Machine output collection path varies across SDV versions — periodic pre-tagging scan is more reliable than patching collection methods
- `Object.checkForAction` is unreliable for intercepting machine output collection in SDV 1.6
- Machine `heldObject` pre-tagging works correctly — the earlier tag loss was caused by Automate mod, not the game engine. With Automate disabled, ownership persists through the full artisan chain when any player collects.

### Gameplay Testing Notes (Feb 13, 2026)
- Tested with 2-player multiplayer: Stan (host) and Katie (farmhand)
- Test pattern: Katie plants 3 parsnips, Stan plants 2, one player harvests all 5
- Confirmed separate stacks by owner in inventory after harvest fix
- Artisan chain preserves ownership through full plant → harvest → machine → collect cycle
- Machine tag loss initially observed — **confirmed caused by Automate mod**, not game engine
- With Automate disabled, all ownership tags persist correctly through every step
- Shipping bin credits whoever physically deposits (native game behavior, no enforcement)
- Chest Anywhere mod creates a gameplay problem: all chests visible regardless of owner
- Tree tagging fires at day start for pre-existing trees — cosmetic issue, low priority
- Both Automate and Chest Anywhere are Pathoschild open source mods — integration planned for step 2

### Conversation History Reference
- Original geographic quadrant design superseded (see QuadFarms_Design_Conversation.txt)
- Pivot to action-based ownership discussed in prior conversation (compacted)
- 22→24 open design questions documented in NewProjectSummary.md

### Repository
- **Remote:** https://github.com/tbonehunter/GoodFences.git
- **Branch strategy:** Not yet established — currently working on main
