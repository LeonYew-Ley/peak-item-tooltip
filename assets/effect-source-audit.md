# PEAK 2.6.b effect-source audit

This is a code-level audit of the local PEAK 2.6.b `Assembly-CSharp.dll`. It records what the tooltip can read safely today; it does not claim that every item's serialized prefab has been mapped or that in-game behavior has been verified.

## Item actions

The local assembly inventory contained 56 `Action_*` classes. Their implementations were decompiled and grouped by how they affect tooltip claims.

| Source family | Current handling | Coverage status |
|---|---|---|
| `Action_RestoreHunger` | Reads the live hunger restoration value and renders a signed row. | Numeric row supported |
| `Action_InflictPoison` | Reads poison rate and duration, renders the aggregate amount, and labels delay/duration. | Numeric row and timing supported |
| `Action_ModifyStatus` | Reads signed status changes. Positive recognized statuses participate in safety checks. | Numeric row supported; `ifSkeleton` makes a categorical safety claim incomplete |
| `Action_GiveExtraStamina` | Reads live extra stamina. | Numeric row supported |
| `Action_AddOrRemoveThorns` | Reads the direct thorn count, renders add/remove values, and detects positive Thorns for safety checks. | Numeric row supported |
| `Action_ClearAllStatus` | Decompilation confirms it only subtracts statuses, with exclusions. | Safe for absence checks; not represented as numeric rows |
| `Action_Consume`, `Action_ReduceUses`, animation actions, `Action_IncrementSteamStat`, `Action_TryHotDogAchievement` | Consumption, visual, or achievement plumbing. | Accounted for when checking adverse-effect claims |
| `Action_ApplyAffliction` with `Affliction_AdjustStatus` | Reads the signed live status amount from the primary and extra afflictions. | Numeric row supported; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_PoisonOverTime` | Reads poison rate, total time, and delay from primary and extra afflictions; shows the aggregate amount plus timing. | Numeric row supported; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_AdjustColdOverTime` / `Affliction_AdjustDrowsyOverTime` | Reads the signed per-second status rate and duration from primary and extra afflictions. | Numeric row supported; aggregate amount and duration shown; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_AddBonusStamina` | Reads extra stamina and duration from primary and extra afflictions. | Numeric row supported; participates in safety checks |
| `Peak.Action_SuperJumpAmulet` (derived from `Action_ApplyAffliction`) | Reads supported primary/extra afflictions and its direct `petrifyPerUse` value. | Numeric rows supported; Petrify participates in safety checks |
| Other `Action_ApplyAffliction` payloads | Other affliction types can contain distinct buffs, conditions, or status behavior. | Unsupported; any active instance suppresses a checked safety claim |
| `Action_ApplyMassAffliction` | Can apply afflictions to nearby characters depending on radius and caster conditions. | Target/radius behavior is not rendered; remains incomplete |
| `Action_RandomMushroomEffect` | Reads the current mushroom-manager roll and can produce delayed positive or negative effects, including Poison/Spores changes. | Random outcome is not available from the item alone; treated as incomplete |
| `Action_ConsumeAndSpawn` | Consumes the source and creates another item after a delay. | Replacement chain is not followed; treated as incomplete |
| Other active `ItemActionBase` subclasses and `Action_*` components | Not assumed harmless based only on their names. | Treated as incomplete unless explicitly accounted for |

When a state description has a `mustNotHaveActive` check, an incomplete action inventory now suppresses that state claim and shows an uncertainty note. A recognized active conflicting status suppresses the claim and reports the conflict. Disabled actions and inactive child branches do not count as active sources.

This is a **safety-claim guard**, not full effect coverage: unsupported effects may still be absent from the numeric rows. In particular, non-status buffs, conditional afflictions, area effects, and replacement chains still need source-specific rendering and tests.

## Affliction inventory

The PEAK 2.6.b assembly contains 26 `Peak.Afflictions` classes. The five payload families listed above are parsed into numeric rows. The remaining 21 are inventoried below; they are deliberately not treated as harmless by the current reader, so an active instance keeps a checked safety claim unverified.

| Affliction payloads | Decompiled behavior | Current reader decision |
|---|---|---|
| `Affliction_AdjustStatus`, `Affliction_PoisonOverTime`, `Affliction_AdjustColdOverTime`, `Affliction_AdjustDrowsyOverTime`, `Affliction_AddBonusStamina` | Signed status adjustment; timed Poison; timed Cold/Drowsy; or temporary bonus stamina. | Parsed when fields are readable; numeric amount and applicable timing are shown. |
| `Affliction_AdjustStatusOverTime` | Carries a status rate and duration but has no `UpdateEffect` override in this game build; the base update is empty. | Not rendered or whitelisted; remains conservative until its source is intentionally supported. |
| `Affliction_Chaos` | Clears statuses, then randomly distributes status and bonus-stamina amounts. | Unsupported: random outcome depends on the runtime roll. |
| `Affliction_ZombieBite` | After a delay, adds Spores only while the target already has a minimum Spores status; otherwise it ends. | Unsupported: conditional delayed status behavior. |
| `Affliction_PreventPoisonHealing` | Temporarily prevents Poison healing. | Unsupported: conditional gameplay rule, not a numeric status delta. |
| `Affliction_Exhaustion`, `Affliction_FasterBoi` | Drains stamina over time; or changes movement/climbing and Drowsy, including an end-time Drowsy effect. | Unsupported: resource drain, movement modifiers, and delayed side effects. |
| `Affliction_InfiniteStamina` | Temporarily grants infinite stamina and may apply a serialized Drowsy affliction when it ends. | Unsupported: delayed secondary payload is not yet described. |
| `Affliction_RadiateInfiniteStam`, `Affliction_MassSuperJump` | Grant stamina or movement effects to nearby characters based on radius and target state. | Unsupported: area/teammate effects and target conditions. |
| `Affliction_HealAll`, `Affliction_ClearAllStatus` | Reduce selected adverse statuses, bounded by a healing amount or exclusions. | Source behavior is reduction-only, but no numeric row is emitted and the safety reader remains conservative. |
| `Affliction_BingBongShield`, `Affliction_Invincibility`, `Affliction_Blind`, `Affliction_Numb` | Change invincibility or sensory/control behavior for a duration. | Unsupported: non-status gameplay effects are not represented in numeric rows. |
| `Affliction_LowGravity`, `Affliction_ClimbingChalk`, `Affliction_DoubleJumpAmulet`, `Affliction_Glowing`, `Affliction_NoHunger`, `Affliction_Sunscreen` | Change gravity, climbing cost, jump count, visibility, hunger, or sun protection. | Unsupported: non-status gameplay effects are not represented in numeric rows. |

This inventory records decompiled behavior, not which items instantiate each payload. The installed resource scan found no serialized food `ItemCooking`/`CookingBehavior` instances, so mapping these classes to specific food variants and verifying their final cooking state remains open.

## Cooking behavior

`ItemCooking.UpdateCookedBehavior` skips vanilla default stat changes when `ignoreDefaultCookBehavior` is true, but still runs additional cooking behaviors. The default `ChangeStatsCooked` behavior changes hunger and extra stamina and, at cook count 4 or above, adds Poison unless `ignoreDefaultPoisonBehavior` is true. Item-specific additional behaviors can disable/enable scripts, change an affliction's duration, adjust a holder's status immediately, run actions, add a Poison action, replace the item, wreck it, or explode it.

Notable verified details:

- `CookingBehavior_DisableScripts` disables selected components; the modifier reader ignores disabled components.
- `CookingBehavior_AddPoisonOnUse` adds an `Action_ModifyStatus` Poison component with an `OnConsumed` or `OnCastFinished` trigger; the existing status reader then sees its live value.
- `CookingBehavior_ChangeAfflictionTime` mutates an `Action_ApplyAffliction` payload's `totalTime`. The modifier reader reflects the updated duration for supported timed Poison, Cold, Drowsy, and bonus-stamina payloads; other affliction types remain incomplete.
- `CookingBehavior_ReplaceItem` destroys the source and spawns a replacement, optionally cooking it once.
- `CookingBehavior_Wreck` calls `ItemCooking.Wreck()`, which destroys item actions and sets the private `overrideUsability` option to false without setting `wreckWhenCooked`. State selection now reads that explicit unusable override as well.
- Additional cooking behaviors run even when `ignoreDefaultCookBehavior` skips vanilla stat changes.

The tooltip does not execute cooking or action methods while reading an item. The in-game matrix is still required to confirm each affected prefab's configured thresholds and final behavior.

## Verification and remaining work

- Regression tests cover known numeric sources, direct Thorns, signed `Affliction_AdjustStatus`, primary/extra `Affliction_PoisonOverTime` payloads with timing, timed Cold/Drowsy and bonus stamina payloads, the derived Super Jump Amulet action and its Petrify value, unsupported afflictions, mass-target conditions, disabled actions, unknown action subclasses, random mushroom effects, conditional status actions, conflicting live statuses, and the `CookingBehavior_Wreck` usability override.
- Configuration migration is factored into the production `DescriptionDefaultsMerger` and tested with old/custom JSON fixtures plus file-backed initialization, English/Chinese embedded defaults, HotReload, and invalid-JSON last-good-data retention.
- Current automated suite: 108 regression checks. PEAK 2.6.b Release build: 0 warnings and 0 errors.
- Remaining source work: add explicit support for useful non-status/conditional afflictions, mass-action targets/conditions, and random mushroom outcomes; map the relevant serialized item prefabs where available; then validate the matrix in PEAK itself.
- No item is declared fully effect-covered solely because this guard passed. Only the specific recognized action families above count as supported.
