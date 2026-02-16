# GoodFences Step 2 - Design Questions to Resolve

**Date:** February 14, 2026  
**Purpose:** Resolve ambiguities before implementing Step 2 enforcement

---

## Priority Level Guide
- 🔴 **CRITICAL** - Must resolve before Step 2A implementation
- 🟡 **IMPORTANT** - Should resolve before Step 2A, can adjust during testing
- 🟢 **DEFERRED** - Can wait for Step 2B/3 or discover during implementation

---

## A. Enforcement Granularity - What Can't You Touch?

### 🔴 A1. Crops on Owned Soil

**Current behavior:** Step 1 tags soil and crop with planter's ID

**Question:** What interactions are blocked for non-owners?

- [ ] **Option A - Full Block:** Non-owner cannot water, fertilize, harvest, or scythe
- [ ] **Option B - Partial Block:** Non-owner can water/fertilize (maintenance), but cannot harvest
- [ ] **Option C - Trust-Based:** Non-owner blocked by default, but owner can grant harvest permission

**If Option C (trust-based):**
- [ ] Permission granted per-player globally? (Katie trusts Stan for all crops)
- [ ] Permission granted per-field/area? (Katie trusts Stan for north field only)
- [ ] Permission granted via "trusted helper" designation?

**Follow-up:** Can trusted helper harvest directly, or must they use deposit-only chest access?

**Decision:**Option C with types of permission configurable via gmcm if possible.
```


```

---

### 🔴 A2. Machines - Input Restrictions

**Current behavior:** Input owner stored on machine → output pre-tagged to input owner

**Question:** Can non-owners place items in someone else's machine?

- [ ] **Option A - Blocked:** Your machine = your items only (strict ownership)
- [ ] **Option B - Allowed:** Anyone can use any machine, output tagged to INPUT owner
- [ ] **Option C - Per-Machine Trust:** Machine owner designates who can use it

**My recommendation:** Option B (open machines, tracked ownership) - simpler, follows "what you produce is yours" principle

**Decision:**
```Option A with (if possible) per-use permission: this keeps another player from using all the owner's machines without the owner's knowledge/consent for their goods when they don't have the machines (or enough of them) to process the items.


```

---

### 🔴 A3. Machines - Output Collection

**Scenario:** Stan puts his parsnips in Katie's keg → parsnip juice tagged to Stan

**Question:** Who can collect the output?

- [ ] **Option A:** Only Stan (output owner) can collect
- [ ] **Option B:** Only Katie (machine owner) can collect
- [ ] **Option C:** Either can collect (first come, first served)
- [ ] **Option D:** Stan can collect his own output; Katie can collect if trusted by Stan

**Decision:** The machine owner and output owner can both collect, whichever gets there first. This allows the machine owner to clear them out for their own use if the output owner is not available.  
**Issue** Currently, if a non-owner player strikes a machine (or chest) with an axe, they can pick it up and become the owner. We need to figure out a way to block owned machines from being struck/picked up by other players.
```


```

---

### 🟡 A4. Fruit Trees

**Question:** Can non-owners harvest from owned fruit trees?

- [ ] **Option A:** Owner only (strict)
- [ ] **Option B:** Anyone can harvest, but fruit is tagged to tree owner
- [ ] **Option C:** Trust-based (owner designates who can harvest)

**Follow-up - Ground Fruit:** If fruit falls and lies on ground for 24+ hours:
- [ ] Remains owned by tree planter
- [ ] Becomes unowned (finders keepers)
- [ ] Becomes common after threshold period

**Decision:**Option C, trustbased harvest if not owner. If fruit falls and lies for over 24 hours, it becomes unowned and can be treated as forage (whoever picks it up owns it.)
```


```

---

###   A5. Animals - TECHNICAL FEASIBILITY CONFIRMED

**IMPORTANT FINDING:** Both `Building` and `FarmAnimal` classes support modData tagging!

```csharp
// Buildings CAN be tagged
Building building; // Coop, Barn, etc.
building.modData["GoodFences.Owner"] = purchaser.UniqueMultiplayerID.ToString();

// Animals CAN be tagged
FarmAnimal animal; // Chicken, Cow, etc.
animal.modData["GoodFences.Owner"] = purchaser.UniqueMultiplayerID.ToString();
```

**Caveat:** Animals cannot be picked up/gifted like items - they stay in buildings. Transfer would require special "sell to player" mechanic.

---

#### 🔴 A5a. Animal Product Ownership Model

**Question:** Who owns the egg when a chicken lays it?

- [ ] **Option A - Animal Owner Gets Products** ⭐ RECOMMENDED
  - Katie buys chicken → chicken tagged to Katie
  - When ANYONE collects egg → egg auto-tagged to Katie
  - Supports contract labor: Stan can collect Katie's eggs, but eggs stay Katie's
  - Fits deposit-only chest model perfectly
  - **Pros:** Clear chain (buyer → animal → products), prevents theft, supports helpers
  - **Cons:** More complex patching (need to check animal owner on collection)

- [ ] **Option B - Collector Gets Products**
  - Katie buys chicken → chicken tagged to Katie (informational only)
  - Whoever collects egg → egg tagged to collector
  - Animal ownership is just tracking, not enforcement
  - **Pros:** Simpler (catch-all handles it), flexible
  - **Cons:** Anyone can "steal" products by collecting first

- [ ] **Option C - Building Owner Gets Products**
  - Katie buys barn → barn tagged to Katie
  - Animals in Katie's barn → products tagged to Katie (regardless of who bought animal)
  - Building = production unit
  - **Pros:** Fewer tags, building-centric model
  - **Cons:** Can't share buildings, less granular

**Decision:** Animal owner gets products. Building owner must grant permission (or sell animal slots, my preference) to other players.
```


```

---

#### 🔴 A5b. Building Ownership

**Question:** Who owns buildings (coops, barns, sheds)?

- [ ] Buildings tagged to purchaser/constructor (whoever paid)
- [ ] Buildings tagged to host only (simplification)
- [ ] Buildings are always common (no ownership/enforcement on entry)

**Follow-up:** If buildings are owned, what does that mean?
- [ ] Only owner can enter (strict)
- [ ] Anyone can enter (high trust default), owner controls machines/animals inside
- [ ] Trust-based access per building

**Decision:**Building purchaser owns it, anyone can enter, owner owns/controls machines inside, and if giving permission to another player for animal storage, that player can use the machines and own their own animal's output). Same issue: Need to block other players from striking/picking up machines to own them.
```


```

---

#### 🟡 A5c. Animal Purchase Enforcement

**Question:** Who can purchase animals for a building?

- [ ] Only building owner can purchase animals for that building
- [ ] Anyone can purchase, animal tagged to purchaser (flexible co-stocking)
- [ ] Anyone can purchase, animal inherits building owner (simpler)

**My recommendation:** Option 2 (flexible) - allows multiple players to co-stock a barn

**Decision:**Option 2 with permission or if possible, mechanism for selling animal slots
```


```

---

#### 🟡 A5d. Animal Product Collection Access

**Question:** Who can physically collect animal products?

- [ ] Anyone can collect, product auto-tagged to animal owner (trusted helper model)
- [ ] Only animal owner can collect (strict enforcement)
- [ ] Only building owner can enter building to collect (building-level enforcement)

**My recommendation:** Option 1 (trusted helper) - fits deposit-chest model; Stan collects Katie's eggs → deposits in Katie's chest

**Decision:**Option 1
```


```

---

#### 🟢 A5e. Moving Animals Between Buildings

**Scenario:** Native game allows moving animals between buildings of same type. Katie's chicken in Katie's barn → moved to Stan's barn.

**Question:** Is this allowed?

- [ ] Blocked if buildings have different owners
- [ ] Allowed, animal keeps original owner tag
- [ ] Allowed, animal inherits new building owner
- [ ] Only animal owner can move their animals

**My recommendation:** Option 2 (keep original owner) - animal ownership follows the animal

**Decision:**Allowed with permission/sale, animal ownership follows the animal
```


```

---

## B. Common Item Interactions

### 🔴 B1. Common Items in Owned Machines

**Scenario:** Common parsnip placed in Katie's (owned) keg

**Question:** What happens to the output?

- [ ] **Option A:** Output is common (input determines ownership, not machine)
- [ ] **Option B:** Output is owned by machine owner (machine transforms common → owned)
- [ ] **Option C:** Blocked (cannot put common items in owned machines)

**My recommendation:** Option A (common in → common out, regardless of machine)

**Decision:**Common in, common out, once something is common, it cannot revert to any other ownership. All common goods sale proceeds are split equally between all players active in the game (if some players are not in the game for a turn, common goods are split between the active players.)
```


```

---

### 🟡 B2. Common Soil Designation

**Question:** Can soil itself be marked as "common" (not just harvested items)?

- [ ] **Option A:** Yes - common soil → anyone plants/waters/harvests → output auto-common
- [ ] **Option B:** No - soil always owned, items marked common manually after harvest
- [ ] **Option C:** Yes, but requires explicit chest-style "designation" action

**If Option A - Mechanics:**
- How is soil marked common? Right-click + keybind? Special "common zone" designation?
- Can common soil be reverted to owned? Or one-way like common items?

**Decision:**We need to discuss the ownership of soil: Currently soil becomes owned by planting something in it, not by any other designation. My understanding was that when there's nothing growing, it's open to anyone to plant. Is this not how it is structured? If not, it should be. 
```


```

---

### 🔴 B3. Stacking Common + Owned Items

**Current behavior:** Different owners' items cannot stack (enforced in Step 1)

**Question:** Can common items stack with owned items?

- [ ] **Option A:** No - keep separate (prevents accidental commonization)
- [ ] **Option B:** Yes - they merge, result is owned (common "absorbed")
- [ ] **Option C:** Yes - they merge, result is common (owned "donated")

**My recommendation:** Option A (keep separate for clarity)

**Decision:**Stacking is only allowed per owner, common cannot stack with owned. The only way to make something common is to put it in the "common" chest, the game host should have the ability to make one, or a chest should be placed on the farm at the beginning of the multiplayer game to make a common chest. As close as possible to the shipping bin by farmhouse.
```


```

---

## C. Revenue Splitting

### 🔴 C1. Common Revenue Split Formula

**Scenario:** Common shipping bin has 1000g worth of items at end of day. 4-player farm (Katie, Stan, Alex, Sam). Katie and Stan contributed items. Alex/Sam AFK.

**Question:** How is revenue divided?

- [ ] **Option A - Equal Split (All Players):** 250g each (even AFK players)
- [ ] **Option B - Equal Split (Online Players):** 500g to Katie/Stan (0g to offline)
- [ ] **Option C - Equal Split (Contributors Only):** 500g to Katie/Stan (tracked who deposited)
- [ ] **Option D - Proportional Split:** Track deposit value per player, split proportionally

**My recommendation:** Option C (contributors only) - fairest, prevents AFK exploitation

**Follow-up:** How to track contributors?
- [ ] Track who deposited each item
- [ ] Track total value deposited per player
- [ ] Simple flag: "participated in common shipping today"

**Decision:**Equal split among all active players on date of sale. If a player is not in the game that day, they don't get that revenue.
```


```

---

### 🔴 C2. Common Shipping Bin Mechanics

**Questions:**

1. **How many common bins?**
   - [ ] ONE farm-wide common shipping bin (designated by host?)
   - [ ] Any bin can be marked "common" by owner
   - [ ] Multiple common bins, all feed same revenue pool

2. **Accidental owned items in common bin?**
   - [ ] Item becomes common when deposited (one-way conversion)
   - [ ] Deposit rejected with error message
   - [ ] Deposit allowed, but only common items counted for revenue split

3. **Can you remove items from common bin before shipping?**
   - [ ] Yes, anyone can remove
   - [ ] Yes, but item stays common
   - [ ] No, deposit is final

**Decision:**See above, either mod-generated that farmer can move without destroying ownership, or farmer-generated/placed
```


```

---

### 🟡 C3. Mixed Shipments in Common Bin

**Scenario:** Player accidentally ships owned items in common bin, or common bin contains mix

**Question:** What happens at end of day?

- [ ] **Option A:** Only common items split revenue; owned items credited to owner
- [ ] **Option B:** Warning message: "Personal items found in common bin" + Option A behavior
- [ ] **Option C:** Everything in common bin converts to common at shipping time
- [ ] **Option D:** Reject shipping if mixed content detected

**Decision:**Reject shipping if mixed content detected. Only owners can place their items in a shipping bin, and only common items can go in the common bin.
```


```

---

## D. Trusted Helper Edge Cases

### 🔴 D1. Trusted Helper + Full Chest

**Scenario:** Stan (trusted) harvests Katie's (offline) crops. Items tagged to Katie. Stan tries to deposit in Katie's chest → chest is FULL.

**Question:** What happens?

- [ ] **Option A:** Deposit fails, error message (Stan keeps items until Katie empties chest)
- [ ] **Option B:** Stan gets permission to drop items on ground near chest
- [ ] **Option C:** Game auto-expands chest (add temp overflow slot)
- [ ] **Option D:** Overflow creates a new "Deposit Chest" automatically

**My recommendation:** Option A (deposit fails) + allow Stan to drop on ground as fallback

**Decision:**Currently it is possible to put another's players items in your own chest without losing the owner's tag. Let's keep that for trusted helper with notification to owner
```


```

---

### 🟡 D2. Trust Revocation Timing

**Scenario:** Katie removes Stan from trusted list while Stan is holding Katie's items in inventory.

**Question:** Does Stan lose deposit access immediately?

- [ ] **Option A:** Yes, immediately blocked (now can't return items! BAD)
- [ ] **Option B:** No, maintains deposit access for items already in inventory
- [ ] **Option C:** Grace period: 24 in-game hours to return items before block

**My recommendation:** Option B (maintain access for existing items)

**Technical:** How to detect "items already in inventory"? Check inventory on trust removal, flag those specific items?

**Decision:**I think there should be a time or activity designation to trust (with an option for "all".) Designation will expire at the time period or when the activity is done. 
```


```

---

### 🔴 D3. Trust Scope - Per-Chest or Global?

**Question:** When Katie trusts Stan, what access does he get?

- [ ] **Option A - Per-Chest:** Katie trusts Stan for "Harvest Chest" only (granular)
- [ ] **Option B - Global:** Katie trusts Stan = Stan can deposit in ANY of Katie's chests
- [ ] **Option C - Tiered:** "Deposit trust" (specific chests) vs "Full trust" (all chests)

**My recommendation:** Option A (per-chest) - most flexible, prevents over-sharing

**UI Flow Example:**
```
Katie right-clicks chest → "Chest Options" → "Manage Trusted Players"
☐ Stan (deposit only)
☐ Alex (deposit only)
```

**Decision:**Option A
```


```

---

## E. Enforcement UI/UX

### 🔴 E1. Blocked Action Feedback

**Scenario:** Stan tries to harvest Katie's parsnips → blocked

**Question:** What feedback does Stan get?

- [ ] **Option A:** Nothing (silent ignore) - CONFUSING, don't do this
- [ ] **Option B:** Error sound + HUD message: "This crop belongs to Katie"
- [ ] **Option C:** Tooltip on hover: "Katie's Parsnip - You cannot harvest"
- [ ] **Option D:** Both B + C (tooltip + message on attempt)

**My recommendation:** Option D (tooltip + message)

**Decision:**Option D
```


```

---

### 🟡 E2. Ownership Visibility on World Objects

**Current:** Items in inventory show "(PlayerName)" suffix

**Question:** Should PLACED objects show ownership too?

- [ ] **Yes - All Objects:** Chests, crops, machines, trees
- [ ] **Yes - Selective:** Only chests and machines (interactable)
- [ ] **No:** Only inventory items (current behavior)

**Example tooltips:**
- Hover over chest: "Katie's Chest"
- Hover over crop: "Stan's Parsnip (4 days to harvest)"
- Hover over machine: "Katie's Keg (processing...)"

**Decision:** All Objects with hover tooltip.
```


```

---

### 🟢 E3. Permission Request System (Future)

**Concept:** Player requests permission from owner in real-time

**Flow:**
1. Stan tries to harvest Katie's crops → blocked
2. Stan right-clicks: "Request permission from Katie"
3. Katie gets notification: "Stan wants to harvest your crops [Allow] [Deny]"

**Question:** Implement in Step 2, or defer to future version?

- [ ] Implement in Step 2B (after basic enforcement works)
- [ ] Defer to post-launch feature
- [ ] Skip entirely (use trust system instead)

**Decision:**We should look at this as an alternate mechanism to the trust system, otherwise leave it alone
```
Defer - not critical for Step 2
```

---

## F. Migration & Backwards Compatibility

### 🔴 F1. Pre-Tagged Chests from Step 1 Testing

**Scenario:** Players have been testing Step 1. Some chests may already have owner tags.

**Question:** When Step 2 enforcement begins, how do we handle chests that DON'T have tags?

- [ ] **Option A:** Auto-tag to last player who opened/placed it (use game data)
- [ ] **Option B:** Auto-tag to nearest cabin owner (proximity)
- [ ] **Option C:** Leave untagged = "common chest" by default
- [ ] **Option D:** Require explicit "claim chest" action on first Step 2 load

**My recommendation:** Option D (explicit claim) - prevents wrong assignments

**Claim UI:** 
```
On first Step 2 load:
"Found 12 untagged chests. Walk to each and right-click to claim."
```

**Decision:**Whoever made/placed the chest is the owner if that is tracked. If not tracked, the game host who is starting the multiplayer game (not a new one, taking an existing game and making it multiplayer, as a new game doesnt' have this problem) can designate the owner.
```


```

---

### 🟡 F2. Save Compatibility on Mod Removal

**Question:** If someone disables GoodFences after using Step 2, what happens?

- [ ] Patches safely unload, modData remains but ignored (harmless)
- [ ] Need cleanup routine to remove all GoodFences modData on uninstall
- [ ] Warning message: "Removing GoodFences may leave orphaned data"

**Technical consideration:** SMAPI mods can't run cleanup on uninstall. Best we can do is ensure modData doesn't break vanilla game.

**Decision:**Let's see what happens in testing, I suspect that it won't break a vanilla game.
```


```

---

## G. Griefer/Social Break Scenarios

### 🟡 G1. Malicious Chest Spam

**Scenario:** Player places 100 chests everywhere, "claiming" all farm space.

**Question:** How to prevent/mitigate?

- [ ] **Option A:** No limit (social contract, up to host to kick player)
- [ ] **Option B:** Per-player chest limit (e.g., 20 owned chests max)
- [ ] **Option C:** Host override: "Unclaim all [Player]'s objects" command
- [ ] **Option D:** Space-based limit: Can't place chest within X tiles of non-owned chest

**Decision:**If host override is feasible that's the best option next to social contract. I use more than 20 chests in a game by end of first year, so limit isn't good. I think social contract will work, if someone gets mad they can leave the game.
```


```

---

### 🟡 G2. Abandoned Player Objects

**Scenario:** Player joins, plays 2 days, rage-quits, leaves tagged chests/crops everywhere. Other players can't use that space.

**Question:** How to handle?

- [ ] **Option A:** Host can "unclaim" another player's objects manually
- [ ] **Option B:** Auto-unclaim after X days of player being offline (e.g., 7 game days)
- [ ] **Option C:** No auto-unclaim, host must manually remove (axe chest, scythe crops)
- [ ] **Option D:** "Dormant player" status: after X days offline, their objects become common

**My recommendation:** Option A (host override) + possibly Option B (auto-unclaim after long absence)

**Decision:**D, Dormant player status, with 3 game day limit. 
```


```

---

### 🟢 G3. Common Item Griefing

**Scenario:** Player marks everything common to "share the burden" → does no work but earns common revenue from others' effort.

**Question:** Is this preventable, or social contract issue?

- [ ] **Option A:** Social contract only (host kicks griefer)
- [ ] **Option B:** Track and display: "Katie marked 5% common, earned 25% common revenue" (audit log)
- [ ] **Option C:** Minimum contribution threshold: Must contribute X% to earn common revenue
- [ ] **Option D:** Revenue split proportional to contribution (tracked per player)

**Decision:**A player can only mark their own stuff common, so that isn't a problem. No one is obligated to take care of another player's stuff.
```


```

---

## H. Open Questions from Original Design Doc

### 🟡 H1. Buildings (Coops/Barns)

**Questions:**
1. Who owns buildings?
   - [ ] Person who pays for construction
   - [ ] Closest cabin owner
   - [ ] Host always owns all buildings
   - [ ] Buildings are inherently common

2. Can non-owners enter owned buildings?
   - [ ] Blocked (owner only)
   - [ ] Allowed (buildings are common access)
   - [ ] Trust-based (owner grants access)

3. Items produced in buildings (hay, animal products):
   - [ ] Tagged to building owner
   - [ ] Tagged to collector
   - [ ] Tagged to animal owner (if animals owned separately)

**Decision:**Building owner is purchaser with slot options for coops/barns. Those are only exceptions: fish ponds and slime huts are single owner. Junimos belong to purchaser and can only harvest owner's crops.
```


```

---

### 🟡 H2. Kegs/Machines in Buildings

**Question:** Are machines inside buildings separate or "part of the building"?

- [ ] **Option A:** Separate ownership (machine owner ≠ building owner)
- [ ] **Option B:** Inherit building ownership (building owner owns all machines inside)
- [ ] **Option C:** Machines in buildings are automatically common

**Decision:**Building owner owns all machines inside
```


```

---

### 🟢 H3. Junimo Huts

**Question:** Do Junimos respect ownership or ignore it?

- [ ] **Option A:** Junimos ignore ownership, harvest anything (QOL exception)
- [ ] **Option B:** Junimos respect ownership, only harvest crops matching hut owner
- [ ] **Option C:** Junimo huts are always common = harvest anything → output common

**My recommendation:** Option C (huts are common) - keeps co-op spirit

**Decision:**See above: Junimo harvesting is not always good, I prefer not to have them in my crops.
```


```

---

### 🟢 H4. Sprinklers

**Question:** Can your sprinkler water someone else's crops?

- [ ] **Option A:** Yes, without changing ownership (practical QOL)
- [ ] **Option B:** No, sprinkler respects boundaries (strict ownership)
- [ ] **Option C:** Yes, but changes crop ownership to sprinkler owner (weird)

**My recommendation:** Option A (sprinklers ignore ownership) - watering is maintenance, not production

**Decision:**A, no change to ownership
```


```

---

### 🟢 H5. Special Events (Crop Fairy, Witch, Meteorite)

**Question:** If crop fairy grows someone's crops overnight, who gets credit?

- [ ] Crop ownership unchanged (crop owner gets benefit)
- [ ] Event = game mechanic, not player action (ownership unchanged)
- [ ] All special events bypass ownership (common benefit)

**My recommendation:** Ownership unchanged - events benefit whoever owned the target

**Decision:**crop fairy doesnt' change ownership. All other events (meteor, alien ship) are common.
```


```

---

## Summary: Critical Path for Step 2A

### Must Resolve NOW (before coding Step 2A):

1. ✅ **A2** - Can non-owners use owned machines?
2. ✅ **A3** - Who can collect machine output?
3. ✅ **A5a** - Animal product ownership model (animal owner vs collector vs building owner)
4. ✅ **A5b** - Building ownership model
5. ✅ **B1** - Common items in owned machines behavior
6. ✅ **B3** - Common items stack with owned items?
7. ✅ **C1** - Common revenue split formula
8. ✅ **C2** - Common shipping bin mechanics
9. ✅ **D3** - Trust scope (per-chest or global?)
10. ✅ **E1** - Blocked action feedback
11. ✅ **F1** - Pre-existing chest tagging

### Should Resolve (can adjust during testing):

- **A1** - Crop interaction blocking (can start strict, loosen later)
- **A4** - Fruit tree harvesting
- **A5c** - Animal purchase enforcement
- **A5d** - Animal product collection access
- **D1** - Full chest handling for trusted helpers
- **E2** - Ownership visibility tooltips

### Can Defer:

- **A5e** - Moving animals between buildings
- All other **🟢 DEFERRED** items
- Permission request system
- Griefer mitigation (add if becomes problem)
- Junimo huts, sprinklers, special events

---

## Review Checklist

Go through each question and mark your decision. Once critical questions are resolved, we're ready to implement Step 2A.

**Date Reviewed:** _______________  
**Ready to Implement Step 2A:** [ ] Yes [ ] No [ ] Partial (specify)

**Notes:**
```










```
