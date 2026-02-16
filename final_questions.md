# GoodFences Step 2 - Final Clarifications Before Implementation

**Date:** February 14, 2026  
**Purpose:** Resolve remaining ambiguities from initial design decisions before coding Step 2A

---

## Status: 8 Items Requiring Clarification

Based on your answers in questions_to_resolve.md, these items need additional detail or have conflicts to resolve.

---

## 1. 🔴 Machine/Chest Pickup Prevention (Referenced in A3, A5b)

**Context:** You flagged this issue twice:
> *"Need to block owned machines from being struck/picked up by other players."*

This is a major technical challenge. The game's native behavior is: strike object with tool → pick it up.

**Question:** How should we prevent non-owners from picking up owned machines/chests?

**Options:**

- [ ] **Option 1 - Block Tool Use (RECOMMENDED)**
  - Patch tool interaction methods (axe hit detection)
  - Check object ownership before allowing pickup
  - Show error message: "This belongs to Katie"
  - **Pros:** True ownership enforcement, prevents griefing
  - **Cons:** Requires patching tool use logic

- [ ] **Option 2 - Preserve Ownership on Pickup**
  - Let player pick it up, but tag stays with object
  - When placed again, it's still Katie's machine
  - **Pros:** Simpler to implement
  - **Cons:** Players could grief by moving/hiding others' machines

**Follow-up Question:** Should building owners be able to pick up machines INSIDE their building, even if someone else placed them there?

**Decision:** If we can preserve ownership on pickup that is my preference, with the pickup and placements logged. If a player acts with malice the game host can read the log. If the game host is the culprit people will quit the game.
```


```

---

## 2. 🔴 "Per-Use Permission" for Machines (A2)

**Context:** You chose "Option A (blocked) with per-use permission" for machine access.

**Question:** What exactly does "per-use permission" mean? How does it work?

**Options:**

- [ ] **Interpretation A - Single-Use Token**
  - Katie right-clicks her keg → "Grant 1 use to Stan"
  - Stan can use it once, then it locks again
  - Katie must re-grant permission for each use
  - **Pros:** Maximum control
  - **Cons:** Very tedious for ongoing cooperation

- [ ] **Interpretation B - Session Permission**
  - Katie grants Stan permission for this play session
  - Expires when Stan logs off or at day end
  - **Pros:** Balanced - not permanent but not per-click
  - **Cons:** Need to re-grant each session

- [ ] **Interpretation C - Item-Count Permission**
  - Katie grants Stan "10 uses" across all Katie's machines
  - Tracked globally, decrements with each machine use
  - **Pros:** Flexible quota system
  - **Cons:** Complex tracking

- [ ] **Interpretation D - Per-Machine Permission**
  - Similar to trusted chest model
  - Katie right-clicks keg → "Allow Stan to use this keg"
  - Permission persists until revoked
  - **Pros:** Granular, similar to existing trust system
  - **Cons:** Must set per-machine (could be many machines)

**Alternative:** Should machines just use the same trust system as chests? (Trust player → they can use your machines AND deposit in your chests)

**Decision:** by per-use I meant in the instance that Katie asks me to harvest and process her crops for xxx duration/cycles. once that period is up the use permission no longer exists
```


```

---

## 3. 🔴 Soil Ownership Mechanics (B2 Clarification)

**Context:** You said soil should be "open to anyone when nothing is growing."

**Current Step 1 Behavior:**
- `HoeDirt.plant` postfix tags the soil when someone plants
- Soil tag persists even after harvest (soil stays owned forever)

**Your Expected Behavior:**
- Empty soil is unowned/available
- Anyone can plant in empty tilled soil

**Question:** How should we handle soil ownership?

**Options:**

- [ ] **Option A - No Soil Tagging (RECOMMENDED)**
  - Never tag soil, only tag crops
  - Empty tilled soil is always free-for-all
  - Crop ownership is what matters for enforcement
  - **Pros:** Simplest, matches your vision
  - **Cons:** Can't prevent someone from planting in "your area"

- [ ] **Option B - Tag on Plant, Clear on Harvest**
  - Soil tagged when crop planted
  - Soil tag cleared when crop harvested
  - Empty soil reverts to unowned
  - **Pros:** Enforces "your crop space" while growing
  - **Cons:** More complex, edge cases (what if crop dies?)

- [ ] **Option C - Soil Ownership with Expiration**
  - Soil tagged on plant
  - Tag expires after X days of being empty
  - **Pros:** Prevents immediate re-planting by others
  - **Cons:** Most complex, arbitrary timeout

**Follow-up:** Is there any gameplay reason to track who owns empty soil?

**Decision:** Option A - No Soil Tagging, with Pasture Exception

**Core Rule:** The entire farm soil is common-use. Never tag empty soil. Anyone can till/plant in open soil.

**Exception - Pasture Protection System:**
- Building owners get automatic pasture protection zones for coops/barns
- Blocks non-owners from tilling/planting in protected zones
- Necessary for animal grazing (grass) and truffle hunting (pigs)

**Pasture System Specifications (Step 2A - MVP Fixed Zones):**

1. **Automatic Protection Zones:**
   - **Coops**: 12x12 tile rectangle south of door line (144 tiles)
   - **Barns**: 16x16 tile rectangle south of door line (256 tiles)
   - Zone extends directly south from building entrance
   - Top border = door line of building (south-facing edge)
   - Applies to all tiers (basic/big/deluxe) uniformly

2. **Activation:**
   - Protection activates **immediately** when building owner places building
   - Stored in building modData for persistence
   - Owner must consider protection zone when choosing building placement

3. **Lifecycle:**
   - **Demolition**: Protection zone removed with building
   - **Moving building**: Protection zone moves to new location
   - **Building sale/transfer**: Zone ownership transfers with building

4. **Protection Enforcement:**
   - Non-owners blocked from hoeing/tilling inside protection zone
   - Non-owners blocked from planting in zone
   - Zone enforced regardless of fences (fences optional for aesthetics)
   - Prevents "pasture vs crops" territory conflicts

5. **Technical Notes:**
   - Game constraint: Building doors always face south (not rotatable)
   - Simple rectangle calculation from building position
   - No complex flood-fill or fence detection needed for MVP
   - Future enhancement: Fence-based custom zones with area budgets

**Gameplay Impact:** Farmers start with sufficient pasture budget to support full deluxe building capacity, can expand/reshape as they upgrade without being blocked by others' crops. Common soil remains truly common except for legitimate animal husbandry needs.
```


```

---

## 4. 🔴 Machine Ownership in Buildings (A5b vs H2 Conflict)

**Context:** You have conflicting answers about machines placed inside buildings.

**A5b Answer:** *"Building owner owns/controls machines inside"*  
**H2 Answer:** *"Building owner owns all machines inside"*  
**A2 Answer:** *"Machines blocked, need permission"*

**Conflict Scenario:**
1. Katie buys barn (owns building)
2. Stan gets permission to use barn, places 5 kegs inside
3. Who owns the kegs?

**Question:** Who owns machines placed inside owned buildings?

**Options:**

- [ ] **Option A - Placer Owns (Consistent with A2)**
  - Stan places keg in Katie's barn → Stan owns keg
  - Stan controls access to his keg
  - Katie (building owner) cannot override
  - **Issue:** Conflicts with H2 answer

- [ ] **Option B - Building Owner Owns (Consistent with H2) - RECOMMENDED**
  - Any machine placed in Katie's barn → Katie owns it
  - Simplifies ownership within shared buildings
  - Prevents ownership fragmentation
  - **Proposal:** Machines on farm = placer owns; machines in building = building owner owns

- [ ] **Option C - Joint Ownership**
  - Stan can use machines he placed in Katie's barn
  - Katie can use all machines in her barn
  - Both share access
  - **Cons:** Complex "joint ownership" logic

**My Recommendation:** Option B with clarification:
- Machines placed **directly on farm tiles** → owned by placer (A2 enforcement applies)
- Machines placed **inside buildings** → inherit building ownership (prevents confusion)

**Decision:**
```Option B, building owner owns/controls


```

---

## 5. 🟡 Animal Slot-Selling Mechanism (A5a, A5c, A5e)

**Context:** You mentioned "selling animal slots" or "permission" for animal ownership sharing.

**Question:** Do you want a full slot-selling economy feature, or just permission-based animal access?

**Option A - Full Slot-Selling System (Complex):**
- Katie owns barn
- Right-click barn → "Manage Animal Slots"
- "Sell 4 slots to Stan for 5000g each"
- Stan pays, gets 4 dedicated slots
- Stan can only purchase animals for his slots
- Stan's animals produce items tagged to Stan

**Questions for slot-selling:**
1. Who sets the price? Fixed amount? Negotiate?
2. One-time payment or recurring rent?
3. Can slots be revoked? With refund?
4. What if slot owner's animals die - slot available again?

**Option B - Permission-Based (Simpler) - RECOMMENDED:**
- Katie owns barn
- Right-click barn → "Grant Animal Permission"
- Katie allows Stan to purchase animals in her barn (free permission)
- Stan purchases his own animals (pays Marnie)
- Stan's animals produce items tagged to Stan
- Katie can revoke permission (grace period for Stan to move animals)

**Decision:**Option B, with only single animal permission at a time. Stan can't buy more than 1 animal when Katie gives permission, unless she can specify that in the game without too much difficulty, ie "Stan can purchase 2 ducks for the coop".
```


```

---

## 6. 🟡 Trust Duration System (D2 Clarification)

**Context:** You said trust should have "time or activity designation" with expiration.

**Question:** What trust duration options should be available?

**Time-Based Options (RECOMMENDED for MVP):**
- [ ] 0.5 in-game days (until next morning)
- [ ] 1 in-game day
- [ ] 3 in-game days
- [ ] 7 in-game days
- [ ] 14 in-game days (one season)
- [ ] Until manually revoked (permanent)

**Activity-Based Examples (Complex):**
- "Trust Stan to harvest my pumpkins (crop ID #276)"
- Stan right-clicks final pumpkin → "Mark harvest complete" → trust expires
- **Issue:** Very complex to track and validate

**Hybrid:**
- "Trust Stan to harvest my pumpkins until Fall 14"
- Combines target + time limit

**My Recommendation:** Start with simple time-based trust (selectable duration). Activity-based can be added later if needed.

**Question:** Which time-based durations should be available?

**Decision:**Time-based, with option to input number of days or "until revoked". One day is not 24 hours, if permission for 1 day is given, that is the current day only, thru tomorrow would be 2 days.
```


```

---

## 7. 🔴 Common Chest Creation/Designation (C2, B3 Clarification)

**Context:** You said common chest should be "mod-generated that farmer can move" OR "farmer-generated/placed."

**Question:** How is the common chest created?

**Options:**

- [ ] **Option A - Mod Places Initial Chest**
  - When multiplayer game starts, mod automatically spawns 1 common chest
  - Placed near farmhouse shipping bin
  - Has special "common chest" designation (cannot be removed)
  - Host can move it (retains common status)
  - Can more common chests be created? If yes, how?

- [ ] **Option B - Host Designates (RECOMMENDED)**
  - Host places regular chest wherever they want
  - Right-click chest → "Designate as Common Chest" (host only)
  - Can designate multiple common chests
  - Can un-designate? (What happens to items inside?)
  - More flexible, scales to farm needs

- [ ] **Option C - Hybrid**
  - Mod places 1 initial common chest at game start
  - Host can designate additional common chests
  - Initial chest cannot be un-designated, others can

**Follow-up:** Should there be visual differentiation? (Different chest color/sprite? Glow effect? Hover text only?)

**Decision:**Option C, so host doesn't have to create chests if not needed. Once any chest is designated common it cannot revert to single ownership.
```


```

---

## 8. 🟡 Chest Ownership for Existing Saves (F1 Technical Note)

**Context:** You said "Whoever placed the chest is owner if tracked."

**Technical Reality:** The game **does NOT** natively track who placed objects. We must add this tracking in Step 2.

**Implication:** For saves that existed before Step 2 (including your Step 1 testing saves), chest placement history is unknown.

**Question:** For Step 2 first-load on existing save, how do we handle untagged chests?

**Options:**

- [ ] **Option A - Host Manual Assignment (Simple)**
  - On Step 2 first load: "Found 12 untagged chests"
  - Host walks to each, right-clicks "Claim for [Player]" dropdown
  - One-time assignment session
  - **Pros:** Accurate, host knows who uses what
  - **Cons:** Tedious if many chests

- [ ] **Option B - Proximity Auto-Assignment**
  - Chest assigned to owner of nearest cabin
  - Fallback: If in farmhouse, assign to host
  - **Pros:** Automatic
  - **Cons:** May assign wrong owner

- [ ] **Option C - All Untagged → Common**
  - Existing chests become common by default
  - Players can claim empty common chests by placing personal items
  - **Pros:** Simple, errs on side of sharing
  - **Cons:** May give away someone's chest contents

**My Recommendation:** Option A (host manual assignment) - accurate but requires initial setup session.

**Acknowledgment:** Going forward (new chests placed after Step 2 loads), ownership tracking is automatic.

**Decision:**Host designates, option A
```


```

---

## Summary Checklist

Once these 8 items are clarified, we have everything needed to code Step 2A:

- [ ] 1. Machine/chest pickup blocking method
- [ ] 2. Per-use machine permission UX
- [ ] 3. Soil ownership approach
- [ ] 4. Machine ownership in buildings
- [ ] 5. Animal slot system (full economy vs permission)
- [ ] 6. Trust duration options
- [ ] 7. Common chest creation method
- [ ] 8. Existing save chest assignment

**Date Completed:** _______________  
**Ready for Step 2A Implementation:** [ ] Yes [ ] No

**Additional Notes:**
```










```
