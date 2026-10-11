# PEAK 2.6.b effect-source audit

This is a code-level audit of the local PEAK 2.6.b `Assembly-CSharp.dll`. It records what the tooltip can read safely today; it does not claim that every item's serialized prefab has been mapped or that in-game behavior has been verified.

## Item actions

The local assembly inventory contained 56 `Action_*` classes. Their implementations were decompiled and grouped by how they affect tooltip claims.

| Source family | Current handling | Coverage status |
|---|---|---|
| `Action_RestoreHunger` | Reads the live hunger restoration value and renders a signed row. | Numeric row supported |
| `Action_InflictPoison` | Reads poison rate and duration and renders the aggregate amount. | Numeric row supported; delay is not shown in the row yet |
| `Action_ModifyStatus` | Reads signed status changes. Positive recognized statuses participate in safety checks. | Numeric row supported; `ifSkeleton` makes a categorical safety claim incomplete |
| `Action_GiveExtraStamina` | Reads live extra stamina. | Numeric row supported |
| `Action_AddOrRemoveThorns` | Reads the direct thorn count, renders add/remove values, and detects positive Thorns for safety checks. | Numeric row supported |
| `Action_ClearAllStatus` | Decompilation confirms it only subtracts statuses, with exclusions. | Safe for absence checks; not represented as numeric rows |
| `Action_Consume`, `Action_ReduceUses`, animation actions, `Action_IncrementSteamStat`, `Action_TryHotDogAchievement` | Consumption, visual, or achievement plumbing. | Accounted for when checking adverse-effect claims |
| `Action_ApplyAffliction`, `Action_ApplyMassAffliction` | Can carry an affliction plus extra afflictions; mass form can target nearby characters. Affliction values are not parsed by this tooltip yet. | Unsupported; any active instance suppresses a checked safety claim |
| `Action_RandomMushroomEffect` | Reads the current mushroom-manager roll and can produce delayed positive or negative effects, including Poison/Spores changes. | Random outcome is not available from the item alone; treated as incomplete |
| `Action_ConsumeAndSpawn` | Consumes the source and creates another item after a delay. | Replacement chain is not followed; treated as incomplete |
| Other active `ItemActionBase` subclasses and `Action_*` components | Not assumed harmless based only on their names. | Treated as incomplete unless explicitly accounted for |

When a state description has a `mustNotHaveActive` check, an incomplete action inventory now suppresses that state claim and shows an uncertainty note. A recognized active conflicting status suppresses the claim and reports the conflict. Disabled actions and inactive child branches do not count as active sources.

This is a **safety-claim guard**, not full effect coverage: unsupported effects may still be absent from the numeric rows. In particular, direct afflictions, random mushroom effects, duration/delay details, and non-status buffs still need source-specific rendering and tests.

## Cooking behavior

`ItemCooking.UpdateCookedBehavior` skips vanilla default stat changes when `ignoreDefaultCookBehavior` is true, but still runs additional cooking behaviors. The default `ChangeStatsCooked` behavior changes hunger and extra stamina and, at cook count 4 or above, adds Poison unless `ignoreDefaultPoisonBehavior` is true. Item-specific additional behaviors can disable/enable scripts, change an affliction's duration, adjust a holder's status immediately, run actions, add a Poison action, replace the item, wreck it, or explode it.

Notable verified details:

- `CookingBehavior_DisableScripts` disables selected components; the modifier reader ignores disabled components.
- `CookingBehavior_AddPoisonOnUse` adds an `Action_ModifyStatus` Poison component with an `OnConsumed` or `OnCastFinished` trigger; the existing status reader then sees its live value.
- `CookingBehavior_ChangeAfflictionTime` mutates an `Action_ApplyAffliction` payload, which remains unsupported and therefore cannot support a categorical safety claim.
- `CookingBehavior_ReplaceItem` destroys the source and spawns a replacement, optionally cooking it once.
- `CookingBehavior_Wreck` calls `ItemCooking.Wreck()`, which destroys item actions and sets the private `overrideUsability` option to false without setting `wreckWhenCooked`. State selection now reads that explicit unusable override as well.
- Additional cooking behaviors run even when `ignoreDefaultCookBehavior` skips vanilla stat changes.

The tooltip does not execute cooking or action methods while reading an item. The in-game matrix is still required to confirm each affected prefab's configured thresholds and final behavior.

## Verification and remaining work

- Regression tests cover known numeric sources, direct Thorns, disabled actions, unknown action subclasses, `Action_ApplyAffliction`, random mushroom effects, conditional status actions, conflicting live statuses, and the `CookingBehavior_Wreck` usability override.
- Configuration migration is factored into the production `DescriptionDefaultsMerger` and tested with old/custom JSON fixtures plus file-backed initialization, English/Chinese embedded defaults, HotReload, and invalid-JSON last-good-data retention.
- Current automated suite: 88 regression checks. PEAK 2.6.b Release build: 0 warnings and 0 errors.
- Remaining source work: parse and display supported affliction payloads and their conditions/timing; decide which other action families merit concise tooltip rows; map the relevant serialized item prefabs; then validate the matrix in PEAK itself.
- No item is declared fully effect-covered solely because this guard passed. Only the specific recognized action families above count as supported.
