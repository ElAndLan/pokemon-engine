# Move Audit Results

Status: generated from `docs/pokeapi-results/move/*.json` against the current Cgm.Core battle effect-op surface. This is a design-time mechanics audit only; it is not runtime/demo content.

## Audit Rules

- Every local move JSON file is listed exactly once below.
- PASS means the current engine can express the move with existing generic data ops, without bespoke per-move code.
- FAIL means one or more required mechanics are missing or the local JSON does not contain enough effect text to author the behavior exactly.
- For `no-meta` moves, the audit used English `effect_entries` when present. If those were empty, it used the latest English `flavor_text_entries` as a fallback and marks that source explicitly.
- The audit is conservative: conditional, multi-target, item/ability/type mutation, dynamic power, multi-stat, and no-text effects fail unless a current generic op covers them exactly.

## Summary

| Metric | Count |
|---|---:|
| Move JSON files audited | 937 |
| PASS | 468 |
| FAIL | 469 |
| no-meta moves | 110 |
| no-meta audited from effect_entries | 18 |
| no-meta audited from flavor_text fallback | 87 |
| no-meta with no English effect/flavor text | 5 |
| Completed 20-move batches | 47 |

## Current Engine Support Baseline

Supported move primitives found in the current engine: damage, drain, recoil, heal, hpCost, multiHit, fixedDamage, ohko, critBoost, selfDestruct, leechSeed, spikes, stealthRock, weather, bind, protect, forceSwitch, counterDamage, accuracyBypass, chargeTurn, multiTurnLock, ailment, statStage, statStageAll, statStageReset, statStageCopy, statStageSwap, statStageInvert, damageStatOverride, targetHpThresholdPower, hpRatioPower, flinch, plus Phase 15 ability/held-item/form hooks for authored project data.

Known ceilings used by this audit: no doubles/ally/full multi-creature handling, no terrain/room/screen side effects beyond supported hazards/weather, no item or ability mutation moves, no type-changing moves, no move copy/call/replace, no substitute/perish/disable/encore/taunt/torment family, no dynamic power formulas except target-HP-threshold and HP-ratio base-power modifiers, no recharge turns, no delayed damage, and no semi-invulnerable charge turns. Multiple stat-stage effects and accuracy/evasion stages are now implemented through the shared statStage path. Authored targets now compile into battle moves, in singles `all-opponents` / `all-other-pokemon` resolve to the active opponent through the shared active-target resolver, and `users-field` / `entire-field` classify as side/field scopes for field-scoped ops.

Update (2026-07-10): the Core now provides generic `moveGate` (`firstAction`,
`notPreviousMove`) and `queueActionGate` operations. These cover the reusable engine behavior for
first-action-only, cannot-repeat, and recharge-style action skips. Existing FAIL rows remain FAIL
until their normalized definitions compile with these operations and per-move conformance tests
prove the required context and event sequence; capability alone is not certification.

Update (2026-07-10): the HP-mutation handler now supports recipient-aware `heal` and strict
`hpFraction` operations over current or maximum HP. They reuse the shared healing and non-move
damage primitives, so the same operations can support self, target, and later ally contexts. As
with all Phase 15 capabilities, no audit row advances until normalized data and a conformance test
exercise the exact move behavior.

Update (2026-07-10): `statusPower` now provides reusable user/target persistent-status power
multipliers with an optional condition-matched burn-penalty exception. This is engine capability,
not certification; affected rows advance only after normalized definitions and conformance vectors.

## Completed Batches

| Batch | Move rows | Status |
|---:|---:|---|
| 1 | 1-20 | Complete |
| 2 | 21-40 | Complete |
| 3 | 41-60 | Complete |
| 4 | 61-80 | Complete |
| 5 | 81-100 | Complete |
| 6 | 101-120 | Complete |
| 7 | 121-140 | Complete |
| 8 | 141-160 | Complete |
| 9 | 161-180 | Complete |
| 10 | 181-200 | Complete |
| 11 | 201-220 | Complete |
| 12 | 221-240 | Complete |
| 13 | 241-260 | Complete |
| 14 | 261-280 | Complete |
| 15 | 281-300 | Complete |
| 16 | 301-320 | Complete |
| 17 | 321-340 | Complete |
| 18 | 341-360 | Complete |
| 19 | 361-380 | Complete |
| 20 | 381-400 | Complete |
| 21 | 401-420 | Complete |
| 22 | 421-440 | Complete |
| 23 | 441-460 | Complete |
| 24 | 461-480 | Complete |
| 25 | 481-500 | Complete |
| 26 | 501-520 | Complete |
| 27 | 521-540 | Complete |
| 28 | 541-560 | Complete |
| 29 | 561-580 | Complete |
| 30 | 581-600 | Complete |
| 31 | 601-620 | Complete |
| 32 | 621-640 | Complete |
| 33 | 641-660 | Complete |
| 34 | 661-680 | Complete |
| 35 | 681-700 | Complete |
| 36 | 701-720 | Complete |
| 37 | 721-740 | Complete |
| 38 | 741-760 | Complete |
| 39 | 761-780 | Complete |
| 40 | 781-800 | Complete |
| 41 | 801-820 | Complete |
| 42 | 821-840 | Complete |
| 43 | 841-860 | Complete |
| 44 | 861-880 | Complete |
| 45 | 881-900 | Complete |
| 46 | 901-920 | Complete |
| 47 | 921-937 | Complete |

## Move-by-Move Audit

### Batch 1 (moves 1-20)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 1 | `10-000-000-volt-thunderbolt` | PASS | damage | meta | damage power=195; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage; crit rate=2 | crit-stage support; damage op / normal damage pipeline |
| 2 | `absorb` | PASS | damage-heal | meta | damage power=20; target=selected-pokemon; category=damage-heal; drain=50% | drain op; damage plus drain op; known covered move family from current op palette |
| 3 | `accelerock` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 4 | `acid` | PASS | damage-lower | meta | damage power=40; target=all-opponents; category=damage-lower; stat Spd -1 | singles active-target resolver maps all-opponents to the active opponent; damage plus single statStage op |
| 5 | `acid-armor` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 2 | single statStage op |
| 6 | `acid-downpour--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 7 | `acid-downpour--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 8 | `acid-spray` | PASS | damage-lower | meta | damage power=40; target=selected-pokemon; category=damage-lower; stat Spd -2 | single statStage op |
| 9 | `acrobatics` | FAIL | damage | meta | damage power=55; target=selected-pokemon; category=damage | held-item absence power modifier is not implemented |
| 10 | `acupressure` | FAIL | unique | meta | target=user-or-ally; accuracy bypass/no accuracy check; category=unique | target 'user-or-ally' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 11 | `aerial-ace` | PASS | damage | meta | damage power=60; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 12 | `aeroblast` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 13 | `after-you` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 14 | `agility` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spe 2 | single statStage op |
| 15 | `air-cutter` | PASS | damage | meta | damage power=60; target=all-opponents; category=damage; crit rate=1 | singles active-target resolver maps all-opponents to the active opponent; crit-stage support; damage op / normal damage pipeline |
| 16 | `air-slash` | PASS | damage | meta | damage power=75; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 17 | `all-out-pummeling--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 18 | `all-out-pummeling--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 19 | `alluring-voice` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; status: confusion; conditional secondary/effect | conditional move effect is not implemented exactly by current move ops |
| 20 | `ally-switch` | FAIL | unique | meta | target=user; priority 2; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |

### Batch 2 (moves 21-40)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 21 | `amnesia` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spd 2 | single statStage op |
| 22 | `anchor-shot` | FAIL | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage plus no-switch trapping volatile is not implemented |
| 23 | `ancient-power` | PASS | damage-raise | meta | damage power=60; target=selected-pokemon; category=damage-raise; stat Atk 1; stat Def 1; stat Spa 1; stat Spd 1; stat Spe 1 | `statStageAll` op gates and applies Atk/Def/Spa/Spd/Spe in order |
| 24 | `apple-acid` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 25 | `aqua-cutter` | PASS | no-meta | flavor_text fallback | damage power=70; target=selected-pokemon; crit-stage interaction | critStage; no-meta audited from flavor text because effect_entries is empty |
| 26 | `aqua-jet` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 27 | `aqua-ring` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 28 | `aqua-step` | PASS | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; self Speed boost | single statStage; no-meta audited from flavor text because effect_entries is empty |
| 29 | `aqua-tail` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 30 | `armor-cannon` | FAIL | no-meta | flavor_text fallback | damage power=120; target=selected-pokemon; multiple stat changes | multiple simultaneous stat changes cannot be represented exactly |
| 31 | `arm-thrust` | PASS | damage | meta | damage power=15; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 32 | `aromatherapy` | FAIL | unique | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=unique | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 33 | `aromatic-mist` | FAIL | net-good-stats | meta | target=ally; accuracy bypass/no accuracy check; category=net-good-stats; stat Spd 1 | target 'ally' is not implemented by the current singles battle UI/resolver |
| 34 | `assist` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | party move call is not implemented; unique behavior is not proven expressible by current generic ops |
| 35 | `assurance` | FAIL | damage | meta | damage power=60; target=selected-pokemon; category=damage | same-turn damage-taken power modifier is not implemented |
| 36 | `astonish` | PASS | damage | meta | damage power=30; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 37 | `astral-barrage` | PASS | damage | meta | damage power=120; target=all-opponents; category=damage | singles active-target resolver maps all-opponents to the active opponent; damage op / normal damage pipeline |
| 38 | `attack-order` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 39 | `attract` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=infatuation | ailment 'infatuation' is not implemented |
| 40 | `aura-sphere` | PASS | damage | meta | damage power=80; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |

### Batch 3 (moves 41-60)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 41 | `aura-wheel` | PASS | damage-raise | meta | damage power=110; target=selected-pokemon; category=damage-raise; stat Spe 1 | single statStage op |
| 42 | `aurora-beam` | PASS | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat Atk -1 | single statStage op |
| 43 | `aurora-veil` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | screen side condition is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 44 | `autotomize` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spe 2 | single statStage op |
| 45 | `avalanche` | FAIL | damage | meta | damage power=60; target=selected-pokemon; priority -4; category=damage | move-after-hit power modifier is not implemented |
| 46 | `axe-kick` | FAIL | no-meta | flavor_text fallback | damage power=120; target=selected-pokemon; status: confusion; recoil/crash damage | crash recoil maps to existing `recoil` with `onMiss: true`; local no-meta text lacks exact confusion chance needed to author exactly |
| 47 | `baby-doll-eyes` | PASS | net-good-stats | meta | target=selected-pokemon; priority 1; category=net-good-stats; stat Atk -1 | single statStage op |
| 48 | `baddy-bad` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 49 | `baneful-bunker` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | protect variant with contact poison is not implemented; unique behavior is not proven expressible by current generic ops |
| 50 | `barb-barrage` | FAIL | no-meta | flavor_text fallback | damage power=60; target=selected-pokemon; status: poison; dynamic power/modifier; conditional secondary/effect | dynamic/conditional power or damage modifier is not implemented; conditional move effect is not implemented exactly by current move ops |
| 51 | `barrage` | PASS | damage | meta | damage power=15; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 52 | `barrier` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 2 | single statStage op |
| 53 | `baton-pass` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | stat/volatile passing on switch is not implemented; unique behavior is not proven expressible by current generic ops |
| 54 | `beak-blast` | FAIL | damage | meta | damage power=100; target=selected-pokemon; priority -3; category=damage | charge/contact-burn timing is not implemented |
| 55 | `beat-up` | FAIL | damage | meta | target=selected-pokemon; category=damage; multiHit=6-6 | party-based multi-hit damage is not implemented |
| 56 | `behemoth-bash` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 57 | `behemoth-blade` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 58 | `belch` | FAIL | damage | meta | damage power=120; target=selected-pokemon; category=damage | requires prior berry consumption, which is not implemented as a move condition |
| 59 | `belly-drum` | PASS | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | `hpCost` plus a self `statStage` boost exactly represents the HP-gated setup |
| 60 | `bestow` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |

### Batch 4 (moves 61-80)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 61 | `bide` | FAIL | damage | meta | target=user; priority 1; accuracy bypass/no accuracy check; category=damage | delayed stored-damage release is not implemented |
| 62 | `bind` | PASS | damage-ailment | meta | damage power=15; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 63 | `bite` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 64 | `bitter-blade` | PASS | no-meta | flavor_text fallback | damage power=90; target=selected-pokemon; drain heal from damage | drain; no-meta audited from flavor text because effect_entries is empty; known covered move family from current op palette |
| 65 | `bitter-malice` | PASS | no-meta | flavor_text fallback | damage power=75; target=selected-pokemon; target Attack drop | single statStage; no-meta audited from flavor text because effect_entries is empty |
| 66 | `black-hole-eclipse--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 67 | `black-hole-eclipse--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 68 | `blast-burn` | FAIL | damage | meta | damage power=150; target=selected-pokemon; category=damage | recharge turn is not implemented |
| 69 | `blaze-kick` | PASS | damage-ailment | meta | damage power=85; target=selected-pokemon; category=damage-ailment; ailment=burn; crit rate=1 | ailment op; crit-stage support; damage plus ailment if listed as supported |
| 70 | `blazing-torque` | FAIL | no-meta | no English text | damage power=80; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | local JSON has no English effect text to audit exactly |
| 71 | `bleakwind-storm` | FAIL | no-meta | flavor_text fallback | damage power=100; target=all-opponents | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 72 | `blizzard` | FAIL | damage-ailment | meta | damage power=110; target=all-opponents; category=damage-ailment; ailment=freeze | weather-dependent accuracy exception is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 73 | `block` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | no-switch trapping volatile is not implemented; unique behavior is not proven expressible by current generic ops |
| 74 | `blood-moon` | FAIL | no-meta | flavor_text fallback | damage power=140; target=selected-pokemon; repeat-use lockout | repeat-use lockout is not implemented |
| 75 | `bloom-doom--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 76 | `bloom-doom--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 77 | `blue-flare` | PASS | damage-ailment | meta | damage power=130; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 78 | `body-press` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | `damageStatOverride` uses user Defense as the offensive stat through the normal damage pipeline |
| 79 | `body-slam` | FAIL | damage-ailment | meta | damage power=85; target=selected-pokemon; category=damage-ailment; ailment=paralysis | Minimize-dependent accuracy exception is not implemented |
| 80 | `bolt-beak` | FAIL | damage | meta | damage power=85; target=selected-pokemon; category=damage | pre-target-move power doubling is not implemented |

### Batch 5 (moves 81-100)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 81 | `bolt-strike` | PASS | damage-ailment | meta | damage power=130; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 82 | `bone-club` | PASS | damage | meta | damage power=65; target=selected-pokemon; category=damage; flinch chance=10 | flinch op; damage op / normal damage pipeline |
| 83 | `bonemerang` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 84 | `bone-rush` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 85 | `boomburst` | PASS | damage | meta | damage power=140; target=all-other-pokemon; category=damage | singles active-target resolver maps all-other-pokemon to the active opponent; damage op / normal damage pipeline |
| 86 | `bounce` | FAIL | damage-ailment | meta | damage power=85; target=selected-pokemon; category=damage-ailment; ailment=paralysis | semi-invulnerable charge turn is not implemented |
| 87 | `bouncy-bubble` | PASS | damage-heal | meta | damage power=60; target=selected-pokemon; category=damage-heal; drain=100% | drain op; damage plus drain op |
| 88 | `branch-poke` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 89 | `brave-bird` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage; recoil=33% | recoil op; damage op / normal damage pipeline |
| 90 | `breaking-swipe` | PASS | damage-lower | meta | damage power=60; target=all-opponents; category=damage-lower; stat Atk -1 | singles active-target resolver maps all-opponents to the active opponent; damage plus single statStage op |
| 91 | `breakneck-blitz--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 92 | `breakneck-blitz--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 93 | `brick-break` | PASS | damage | meta | damage power=75; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 94 | `brine` | PASS | damage | meta | damage power=65; target=selected-pokemon; category=damage | `targetHpThresholdPower` doubles base power when the active target is at or below the authored HP threshold |
| 95 | `brutal-swing` | PASS | damage | meta | damage power=60; target=all-other-pokemon; category=damage | singles active-target resolver maps all-other-pokemon to the active opponent; damage op / normal damage pipeline |
| 96 | `bubble` | PASS | damage-lower | meta | damage power=40; target=all-opponents; category=damage-lower; stat Spe -1 | singles active-target resolver maps all-opponents to the active opponent; damage plus single statStage op |
| 97 | `bubble-beam` | PASS | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat Spe -1 | single statStage op |
| 98 | `bug-bite` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 99 | `bug-buzz` | PASS | damage-lower | meta | damage power=90; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 100 | `bulk-up` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Def 1 | multiple simultaneous stat changes cannot be represented exactly |

### Batch 6 (moves 101-120)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 101 | `bulldoze` | PASS | damage-lower | meta | damage power=60; target=all-other-pokemon; category=damage-lower; stat Spe -1 | singles active-target resolver maps all-other-pokemon to the active opponent; damage plus single statStage op |
| 102 | `bullet-punch` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 103 | `bullet-seed` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 104 | `burning-bulwark` | FAIL | no-meta | flavor_text fallback | target=user; priority 4; status: burn | protect variant with contact burn is not implemented; protect contact side effects are not implemented as move effects |
| 105 | `burning-jealousy` | FAIL | damage-ailment | meta | damage power=70; target=all-opponents; category=damage-ailment; ailment=burn | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 106 | `burn-up` | FAIL | damage | meta | damage power=130; target=selected-pokemon; category=damage | post-use type removal is not implemented |
| 107 | `buzzy-buzz` | PASS | damage-ailment | meta | damage power=60; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 108 | `calm-mind` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spa 1; stat Spd 1 | multiple simultaneous stat changes cannot be represented exactly |
| 109 | `camouflage` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | terrain-dependent type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 110 | `captivate` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Spa -2 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 111 | `catastropika` | PASS | damage | meta | damage power=210; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 112 | `ceaseless-edge` | PASS | no-meta | flavor_text fallback | damage power=65; target=selected-pokemon; sets spikes hazard | spikes/entry hazard; no-meta audited from flavor text because effect_entries is empty |
| 113 | `celebrate` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 114 | `charge` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spd 1 | charge volatile plus later Electric damage boost is not implemented |
| 115 | `charge-beam` | PASS | damage-raise | meta | damage power=50; target=selected-pokemon; category=damage-raise; stat Spa 1 | single statStage op |
| 116 | `charm` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Atk -2 | single statStage op |
| 117 | `chatter` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 118 | `chilling-water` | PASS | no-meta | flavor_text fallback | damage power=50; target=selected-pokemon; target Attack drop | single statStage; no-meta audited from flavor text because effect_entries is empty |
| 119 | `chilly-reception` | FAIL | no-meta | flavor_text fallback | target=entire-field; accuracy bypass/no accuracy check; user switch effect; snow weather | weather plus user switch is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; after-move user switch is not implemented; snow weather is not implemented; engine has rain/sun/sandstorm/hail |
| 120 | `chip-away` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage | damage op / normal damage pipeline |

### Batch 7 (moves 121-140)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 121 | `chloroblast` | FAIL | no-meta | flavor_text fallback | damage power=150; target=selected-pokemon; recoil/crash damage | local no-meta text lacks exact recoil/crash amount needed to author exactly |
| 122 | `circle-throw` | PASS | damage | meta | damage power=60; target=selected-pokemon; priority -6; category=damage | damage op / normal damage pipeline |
| 123 | `clamp` | PASS | damage-ailment | meta | damage power=35; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 124 | `clanging-scales` | FAIL | damage-raise | meta | damage power=110; target=all-opponents; category=damage-raise; stat Def -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 125 | `clangorous-soul` | PASS | net-good-stats | meta | target=user; category=net-good-stats; stat Atk 1; stat Def 1; stat Spa 1; stat Spd 1; stat Spe 1 | `hpCost` plus `statStageAll` exactly represents the HP-cost all-stat setup |
| 126 | `clangorous-soulblaze` | FAIL | damage-raise | meta | damage power=185; target=all-opponents; accuracy bypass/no accuracy check; category=damage-raise; stat Atk 1; stat Def 1; stat Spa 1; stat Spd 1; stat Spe 1 | full all-opponents topology is not implemented beyond the current singles active opponent |
| 127 | `clear-smog` | PASS | damage | meta | damage power=50; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage plus target `statStageReset` is expressible with generic ops |
| 128 | `close-combat` | FAIL | damage-raise | meta | damage power=120; target=selected-pokemon; category=damage-raise; stat Def -1; stat Spd -1 | multiple simultaneous stat changes cannot be represented exactly |
| 129 | `coaching` | FAIL | net-good-stats | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Def 1 | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 130 | `coil` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Def 1; stat accuracy 1 | multi-stat self boosts including accuracy cannot be represented exactly; stat stage 'accuracy' is not implemented; multiple simultaneous stat changes cannot be represented exactly |
| 131 | `collision-course` | FAIL | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon; dynamic power/modifier | dynamic/conditional power or damage modifier is not implemented |
| 132 | `combat-torque` | FAIL | no-meta | no English text | damage power=100; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | local JSON has no English effect text to audit exactly |
| 133 | `comet-punch` | PASS | damage | meta | damage power=18; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 134 | `comeuppance` | FAIL | no-meta | flavor_text fallback | damage power=1; target=specific-move | retaliation against last damage source is not implemented; target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 135 | `confide` | PASS | net-good-stats | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=net-good-stats; stat Spa -1 | single statStage op |
| 136 | `confuse-ray` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=confusion | ailment op; status move with ailment op when supported |
| 137 | `confusion` | PASS | damage-ailment | meta | damage power=50; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 138 | `constrict` | PASS | damage-lower | meta | damage power=10; target=selected-pokemon; category=damage-lower; stat Spe -1 | single statStage op |
| 139 | `continental-crush--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 140 | `continental-crush--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |

### Batch 8 (moves 141-160)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 141 | `conversion` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 142 | `conversion-2` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 143 | `copycat` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | copy last move is not implemented; unique behavior is not proven expressible by current generic ops |
| 144 | `core-enforcer` | FAIL | damage | meta | damage power=100; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 145 | `corkscrew-crash--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 146 | `corkscrew-crash--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 147 | `corrosive-gas` | FAIL | unique | meta | target=all-other-pokemon; category=unique | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 148 | `cosmic-power` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 1; stat Spd 1 | multiple simultaneous stat changes cannot be represented exactly |
| 149 | `cotton-guard` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 3 | single statStage op |
| 150 | `cotton-spore` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Spe -2 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 151 | `counter` | FAIL | damage | meta | target=specific-move; priority -5; category=damage | target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 152 | `court-change` | FAIL | unique | meta | target=entire-field; category=unique | target 'entire-field' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 153 | `covet` | FAIL | damage | meta | damage power=60; target=selected-pokemon; category=damage | held item stealing is not implemented |
| 154 | `crabhammer` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 155 | `crafty-shield` | FAIL | field-effect | meta | target=users-field; priority 3; accuracy bypass/no accuracy check; category=field-effect | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 156 | `cross-chop` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 157 | `cross-poison` | PASS | damage-ailment | meta | damage power=70; target=selected-pokemon; category=damage-ailment; ailment=poison; crit rate=1 | ailment op; crit-stage support; damage plus ailment if listed as supported |
| 158 | `crunch` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 159 | `crush-claw` | PASS | damage-lower | meta | damage power=75; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 160 | `crush-grip` | PASS | damage | meta | target=selected-pokemon; category=damage | `hpRatioPower` with target source scales base power by the active target's current HP ratio |

### Batch 9 (moves 161-180)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 161 | `curse` | FAIL | unique | meta | target=specific-move; accuracy bypass/no accuracy check; category=unique | target 'specific-move' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 162 | `cut` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 163 | `darkest-lariat` | PASS | damage | meta | damage power=85; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 164 | `dark-pulse` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=20 | flinch op; damage op / normal damage pipeline |
| 165 | `dark-void` | FAIL | ailment | meta | target=all-opponents; category=ailment; ailment=sleep | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 166 | `dazzling-gleam` | PASS | damage | meta | damage power=80; target=all-opponents; category=damage | singles active-target resolver maps all-opponents to the active opponent; damage op / normal damage pipeline |
| 167 | `decorate` | FAIL | net-good-stats | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 2; stat Spa 2 | multiple simultaneous stat changes cannot be represented exactly |
| 168 | `defend-order` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 1; stat Spd 1 | multiple simultaneous stat changes cannot be represented exactly |
| 169 | `defense-curl` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 1 | Rollout/Ice Ball power flag is not implemented |
| 170 | `defog` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique; stat evasion -1 | hazard/screen removal and evasion drop are not implemented; stat stage 'evasion' is not implemented; unique behavior is not proven expressible by current generic ops |
| 171 | `destiny-bond` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 172 | `detect` | PASS | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | covered by existing generic op palette; known covered move family from current op palette |
| 173 | `devastating-drake--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 174 | `devastating-drake--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 175 | `diamond-storm` | FAIL | damage-raise | meta | damage power=100; target=all-opponents; category=damage-raise; stat Def 2 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 176 | `dig` | FAIL | damage | meta | damage power=80; target=selected-pokemon; category=damage | semi-invulnerable charge turn is not implemented |
| 177 | `dire-claw` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; status: poison; status: paralysis; status: sleep | random one-of-many ailment selection is not implemented |
| 178 | `disable` | FAIL | unique | meta | target=selected-pokemon; category=unique; ailment=disable | disable move lockout is not implemented; ailment 'disable' is not implemented; unique behavior is not proven expressible by current generic ops |
| 179 | `disarming-voice` | PASS | damage | meta | damage power=40; target=all-opponents; accuracy bypass/no accuracy check; category=damage | singles active-target resolver maps all-opponents to the active opponent; accuracyBypass; damage op / normal damage pipeline |
| 180 | `discharge` | FAIL | damage-ailment | meta | damage power=80; target=all-other-pokemon; category=damage-ailment; ailment=paralysis | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |

### Batch 10 (moves 181-200)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 181 | `dive` | FAIL | damage | meta | damage power=80; target=selected-pokemon; category=damage | semi-invulnerable charge turn is not implemented |
| 182 | `dizzy-punch` | PASS | damage-ailment | meta | damage power=70; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 183 | `doodle` | FAIL | no-meta | flavor_text fallback | target=selected-pokemon; ability mutation/copy | ability mutation/copy is not implemented |
| 184 | `doom-desire` | FAIL | unique | meta | damage power=140; target=selected-pokemon; category=unique | delayed damage is not implemented; unique behavior is not proven expressible by current generic ops |
| 185 | `double-edge` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage; recoil=33% | recoil op; damage op / normal damage pipeline |
| 186 | `double-hit` | PASS | damage | meta | damage power=35; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 187 | `double-iron-bash` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; flinch chance=30; multiHit=2-2 | flinch op; multiHit op; damage op / normal damage pipeline |
| 188 | `double-kick` | PASS | damage | meta | damage power=30; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 189 | `double-shock` | FAIL | no-meta | flavor_text fallback | damage power=120; target=selected-pokemon; type mutation | post-use type removal is not implemented; type mutation is not implemented |
| 190 | `double-slap` | PASS | damage | meta | damage power=15; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 191 | `double-team` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat evasion 1 | stat stage 'evasion' is not implemented |
| 192 | `draco-meteor` | PASS | damage-raise | meta | damage power=130; target=selected-pokemon; category=damage-raise; stat Spa -2 | single statStage op |
| 193 | `dragon-ascent` | FAIL | damage-raise | meta | damage power=120; target=selected-pokemon; category=damage-raise; stat Def -1; stat Spd -1 | multiple simultaneous stat changes cannot be represented exactly |
| 194 | `dragon-breath` | PASS | damage-ailment | meta | damage power=60; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 195 | `dragon-cheer` | FAIL | no-meta | flavor_text fallback | target=all-allies; crit-stage interaction; multi-target or ally targeting | target 'all-allies' is not implemented by the current singles battle UI/resolver; multi-target/ally targeting is not implemented |
| 196 | `dragon-claw` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 197 | `dragon-dance` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Spe 1 | multi-stat self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 198 | `dragon-darts` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline |
| 199 | `dragon-energy` | FAIL | damage | meta | damage power=150; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 200 | `dragon-hammer` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage | damage op / normal damage pipeline |

### Batch 11 (moves 201-220)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 201 | `dragon-pulse` | PASS | damage | meta | damage power=85; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 202 | `dragon-rage` | PASS | damage | meta | target=selected-pokemon; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 203 | `dragon-rush` | FAIL | damage | meta | damage power=100; target=selected-pokemon; category=damage; flinch chance=20 | Minimize-dependent damage/hit exception is not implemented |
| 204 | `dragon-tail` | PASS | damage | meta | damage power=60; target=selected-pokemon; priority -6; category=damage | damage op / normal damage pipeline |
| 205 | `draining-kiss` | PASS | damage-heal | meta | damage power=50; target=selected-pokemon; category=damage-heal; drain=75% | drain op; damage plus drain op; known covered move family from current op palette |
| 206 | `drain-punch` | PASS | damage-heal | meta | damage power=75; target=selected-pokemon; category=damage-heal; drain=50% | drain op; damage plus drain op |
| 207 | `dream-eater` | FAIL | damage-heal | meta | damage power=100; target=selected-pokemon; category=damage-heal; drain=50% | target-must-be-asleep gate plus drain is not implemented |
| 208 | `drill-peck` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 209 | `drill-run` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 210 | `drum-beating` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Spe -1 | single statStage op |
| 211 | `dual-chop` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline |
| 212 | `dual-wingbeat` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline |
| 213 | `dynamax-cannon` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 214 | `dynamic-punch` | PASS | damage-ailment | meta | damage power=100; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 215 | `earth-power` | PASS | damage-lower | meta | damage power=90; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 216 | `earthquake` | FAIL | damage | meta | damage power=100; target=all-other-pokemon; category=damage | semi-invulnerable target exception/double damage is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 217 | `echoed-voice` | FAIL | damage | meta | damage power=40; target=selected-pokemon; category=damage | consecutive-use field power ramp is not implemented |
| 218 | `eerie-impulse` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Spa -2 | single statStage op |
| 219 | `eerie-spell` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 220 | `egg-bomb` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |

### Batch 12 (moves 221-240)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 221 | `electric-terrain` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 222 | `electrify` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | target move type-changing effect is not implemented; unique behavior is not proven expressible by current generic ops |
| 223 | `electro-ball` | FAIL | damage | meta | target=selected-pokemon; category=damage | speed-ratio power is not implemented |
| 224 | `electro-drift` | FAIL | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon; dynamic power/modifier | dynamic/conditional power or damage modifier is not implemented |
| 225 | `electro-shot` | FAIL | no-meta | flavor_text fallback | damage power=130; target=selected-pokemon | charge-turn stat boost and rain skip are not implemented |
| 226 | `electroweb` | FAIL | damage-lower | meta | damage power=55; target=all-opponents; category=damage-lower; stat Spe -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 227 | `embargo` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=embargo | ailment 'embargo' is not implemented |
| 228 | `ember` | PASS | damage-ailment | meta | damage power=40; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 229 | `encore` | FAIL | unique | meta | target=selected-pokemon; category=unique | encore move lock is not implemented; unique behavior is not proven expressible by current generic ops |
| 230 | `endeavor` | FAIL | damage | meta | target=selected-pokemon; category=damage | target-current-HP matching damage is not implemented |
| 231 | `endure` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 232 | `energy-ball` | PASS | damage-lower | meta | damage power=90; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 233 | `entrainment` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 234 | `eruption` | PASS | damage | meta | damage power=150; target=all-opponents; category=damage | `hpRatioPower` with user source scales base power by the user's current HP ratio; singles active-target resolver maps all-opponents to the active opponent |
| 235 | `esper-wing` | PASS | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; crit-stage interaction; self Speed boost | critStage; single statStage; no-meta audited from flavor text because effect_entries is empty |
| 236 | `eternabeam` | PASS | damage | meta | damage power=160; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 237 | `expanding-force` | FAIL | damage | meta | damage power=80; target=selected-pokemon; category=damage | terrain-dependent target/power is not implemented |
| 238 | `explosion` | FAIL | damage | meta | damage power=250; target=all-other-pokemon; category=damage | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 239 | `extrasensory` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=10 | flinch op; damage op / normal damage pipeline |
| 240 | `extreme-evoboost` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 2; stat Def 2; stat Spa 2; stat Spd 2; stat Spe 2 | `accuracyBypass` plus `statStageAll` exactly represents the all-stat self boost |

### Batch 13 (moves 241-260)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 241 | `extreme-speed` | PASS | damage | meta | damage power=80; target=selected-pokemon; priority 2; category=damage | damage op / normal damage pipeline |
| 242 | `facade` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | status-dependent power is not implemented |
| 243 | `fairy-lock` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 244 | `fairy-wind` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 245 | `fake-out` | FAIL | damage | meta | damage power=40; target=selected-pokemon; priority 3; category=damage; flinch chance=100 | first-turn-only condition is not implemented |
| 246 | `fake-tears` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Spd -2 | single statStage op |
| 247 | `false-surrender` | PASS | damage | meta | damage power=80; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 248 | `false-swipe` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 249 | `feather-dance` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Atk -2 | single statStage op |
| 250 | `feint` | PASS | damage | meta | damage power=30; target=selected-pokemon; priority 2; category=damage | damage op / normal damage pipeline |
| 251 | `feint-attack` | PASS | damage | meta | damage power=60; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 252 | `fell-stinger` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 253 | `fickle-beam` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 254 | `fiery-dance` | PASS | damage-raise | meta | damage power=80; target=selected-pokemon; category=damage-raise; stat Spa 1 | single statStage op |
| 255 | `fiery-wrath` | FAIL | damage | meta | damage power=90; target=all-opponents; category=damage; flinch chance=20 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 256 | `fillet-away` | FAIL | no-meta | flavor_text fallback | target=user; accuracy bypass/no accuracy check | no-meta text does not prove exact expressibility with current ops |
| 257 | `final-gambit` | FAIL | damage | meta | target=selected-pokemon; category=damage | user-HP-based damage plus self-faint is not implemented |
| 258 | `fire-blast` | PASS | damage-ailment | meta | damage power=110; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 259 | `fire-fang` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=burn; flinch chance=10 | ailment op; flinch op; damage plus ailment if listed as supported |
| 260 | `fire-lash` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |

### Batch 14 (moves 261-280)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 261 | `fire-pledge` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 262 | `fire-punch` | PASS | damage-ailment | meta | damage power=75; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 263 | `fire-spin` | PASS | damage-ailment | meta | damage power=35; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 264 | `first-impression` | FAIL | damage | meta | damage power=90; target=selected-pokemon; priority 2; category=damage | first-turn-only condition is not implemented |
| 265 | `fishious-rend` | FAIL | damage | meta | damage power=85; target=selected-pokemon; category=damage | pre-target-move power doubling is not implemented |
| 266 | `fissure` | PASS | ohko | meta | target=selected-pokemon; category=ohko | ohko op; known covered move family from current op palette |
| 267 | `flail` | FAIL | damage | meta | target=selected-pokemon; category=damage | HP-ratio power is not implemented |
| 268 | `flame-burst` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 269 | `flame-charge` | PASS | damage-raise | meta | damage power=50; target=selected-pokemon; category=damage-raise; stat Spe 1 | single statStage op |
| 270 | `flamethrower` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 271 | `flame-wheel` | PASS | damage-ailment | meta | damage power=60; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 272 | `flare-blitz` | PASS | damage-ailment | meta | damage power=120; target=selected-pokemon; category=damage-ailment; ailment=burn; recoil=33% | ailment op; recoil op; damage plus ailment if listed as supported |
| 273 | `flash` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 274 | `flash-cannon` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 275 | `flatter` | PASS | swagger | meta | target=selected-pokemon; category=swagger; ailment=confusion; stat Spa 1 | ailment op; single statStage op; single statStage plus confusion combination |
| 276 | `fleur-cannon` | PASS | damage-raise | meta | damage power=130; target=selected-pokemon; category=damage-raise; stat Spa -2 | single statStage op |
| 277 | `fling` | FAIL | damage | meta | target=selected-pokemon; category=damage | item-dependent power/effect and consumption are not implemented |
| 278 | `flip-turn` | FAIL | damage | meta | damage power=60; target=selected-pokemon; category=damage | after-hit user switch is not implemented |
| 279 | `floaty-fall` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 280 | `floral-healing` | FAIL | heal | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=heal; heal=50% | heal target 'selected-pokemon' is not supported as self-heal |

### Batch 15 (moves 281-300)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 281 | `flower-shield` | FAIL | unique | meta | target=all-pokemon; accuracy bypass/no accuracy check; category=unique; stat Def 1 | target 'all-pokemon' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 282 | `flower-trick` | PASS | no-meta | flavor_text fallback | damage power=70; target=selected-pokemon; accuracy bypass/no accuracy check; crit-stage interaction; target Attack drop | critStage; single statStage; no-meta audited from flavor text because effect_entries is empty |
| 283 | `fly` | FAIL | damage | meta | damage power=90; target=selected-pokemon; category=damage | semi-invulnerable charge turn is not implemented |
| 284 | `flying-press` | FAIL | damage | meta | damage power=100; target=selected-pokemon; category=damage | dual-type damage effectiveness is not implemented |
| 285 | `focus-blast` | PASS | damage-lower | meta | damage power=120; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 286 | `focus-energy` | PASS | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | covered by existing generic op palette; known covered move family from current op palette |
| 287 | `focus-punch` | FAIL | damage | meta | damage power=150; target=selected-pokemon; priority -3; category=damage | fail-if-hit-before-use timing is not implemented |
| 288 | `follow-me` | FAIL | unique | meta | target=user; priority 2; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 289 | `force-palm` | PASS | damage-ailment | meta | damage power=60; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 290 | `foresight` | FAIL | ailment | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=ailment; ailment=no-type-immunity | ailment 'no-type-immunity' is not implemented |
| 291 | `forests-curse` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 292 | `foul-play` | FAIL | damage | meta | damage power=95; target=selected-pokemon; category=damage | uses target Attack as the offensive stat, which is not implemented |
| 293 | `freeze-dry` | FAIL | damage-ailment | meta | damage power=70; target=selected-pokemon; category=damage-ailment; ailment=freeze | type-effectiveness exception is not implemented |
| 294 | `freeze-shock` | PASS | damage-ailment | meta | damage power=140; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 295 | `freezing-glare` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=freeze | ailment op; damage plus ailment if listed as supported |
| 296 | `freezy-frost` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 297 | `frenzy-plant` | FAIL | damage | meta | damage power=150; target=selected-pokemon; category=damage | recharge turn is not implemented |
| 298 | `frost-breath` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; crit rate=6 | crit-stage support; damage op / normal damage pipeline |
| 299 | `frustration` | FAIL | damage | meta | target=selected-pokemon; category=damage | friendship-dependent power is not implemented |
| 300 | `fury-attack` | PASS | damage | meta | damage power=15; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |

### Batch 16 (moves 301-320)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 301 | `fury-cutter` | FAIL | damage | meta | damage power=40; target=selected-pokemon; category=damage | consecutive-use power ramp is not implemented |
| 302 | `fury-swipes` | PASS | damage | meta | damage power=18; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline |
| 303 | `fusion-bolt` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 304 | `fusion-flare` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 305 | `future-sight` | FAIL | unique | meta | damage power=120; target=selected-pokemon; category=unique | delayed damage is not implemented; unique behavior is not proven expressible by current generic ops |
| 306 | `gastro-acid` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 307 | `gear-grind` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage; multiHit=2-2 | multiHit op; damage op / normal damage pipeline |
| 308 | `gear-up` | FAIL | net-good-stats | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Spa 1 | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 309 | `genesis-supernova` | PASS | damage | meta | damage power=185; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 310 | `geomancy` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spa 2; stat Spd 2; stat Spe 2 | multiple simultaneous stat changes cannot be represented exactly |
| 311 | `giga-drain` | PASS | damage-heal | meta | damage power=75; target=selected-pokemon; category=damage-heal; drain=50% | drain op; damage plus drain op; known covered move family from current op palette |
| 312 | `giga-impact` | FAIL | damage | meta | damage power=150; target=selected-pokemon; category=damage | recharge turn is not implemented |
| 313 | `gigaton-hammer` | FAIL | no-meta | flavor_text fallback | damage power=160; target=selected-pokemon; repeat-use lockout | repeat-use lockout is not implemented |
| 314 | `gigavolt-havoc--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 315 | `gigavolt-havoc--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 316 | `glacial-lance` | FAIL | damage | meta | damage power=120; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 317 | `glaciate` | FAIL | damage-lower | meta | damage power=65; target=all-opponents; category=damage-lower; stat Spe -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 318 | `glaive-rush` | FAIL | no-meta | flavor_text fallback | damage power=120; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 319 | `glare` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=paralysis | ailment op; status move with ailment op when supported |
| 320 | `glitzy-glow` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |

### Batch 17 (moves 321-340)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 321 | `grass-knot` | FAIL | damage | meta | target=selected-pokemon; category=damage | weight-based power is not implemented |
| 322 | `grass-pledge` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 323 | `grass-whistle` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=sleep | ailment op; status move with ailment op when supported |
| 324 | `grassy-glide` | PASS | damage | meta | damage power=55; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 325 | `grassy-terrain` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 326 | `grav-apple` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 327 | `gravity` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 328 | `growl` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Atk -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 329 | `growth` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Spa 1 | multi-stat/weather-scaled self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 330 | `grudge` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 331 | `guardian-of-alola` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 332 | `guard-split` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | stat averaging is not implemented; unique behavior is not proven expressible by current generic ops |
| 333 | `guard-swap` | PASS | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | `statStageSwap` with `group=defense` swaps defense stages generically |
| 334 | `guillotine` | PASS | ohko | meta | target=selected-pokemon; category=ohko | ohko op; known covered move family from current op palette |
| 335 | `gunk-shot` | PASS | damage-ailment | meta | damage power=120; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |
| 336 | `gust` | FAIL | damage | meta | damage power=40; target=selected-pokemon; category=damage | semi-invulnerable target exception/double damage is not implemented |
| 337 | `gyro-ball` | FAIL | damage | meta | target=selected-pokemon; category=damage | speed-ratio power is not implemented |
| 338 | `hail` | PASS | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | field-target scope plus weather op sets Hail through the shared field-condition path |
| 339 | `hammer-arm` | PASS | damage-raise | meta | damage power=100; target=selected-pokemon; category=damage-raise; stat Spe -1 | single statStage op |
| 340 | `happy-hour` | FAIL | unique | meta | target=users-field; accuracy bypass/no accuracy check; category=unique | target 'users-field' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |

### Batch 18 (moves 341-360)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 341 | `harden` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 1 | single statStage op |
| 342 | `hard-press` | FAIL | no-meta | flavor_text fallback | target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 343 | `haze` | PASS | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | `statStageReset` with `scope=both` clears both active sides in the current singles resolver |
| 344 | `headbutt` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 345 | `head-charge` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage; recoil=25% | recoil op; damage op / normal damage pipeline |
| 346 | `headlong-rush` | FAIL | no-meta | flavor_text fallback | damage power=120; target=selected-pokemon; multiple stat changes | multiple simultaneous stat changes cannot be represented exactly |
| 347 | `head-smash` | PASS | damage | meta | damage power=150; target=selected-pokemon; category=damage; recoil=50% | recoil op; damage op / normal damage pipeline |
| 348 | `heal-bell` | FAIL | unique | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=unique | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 349 | `heal-block` | FAIL | ailment | meta | target=all-opponents; category=ailment; ailment=heal-block | target 'all-opponents' is not implemented by the current singles battle UI/resolver; ailment 'heal-block' is not implemented |
| 350 | `healing-wish` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 351 | `heal-order` | PASS | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | heal op when self-only flat fraction |
| 352 | `heal-pulse` | FAIL | heal | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=heal; heal=50% | heal target 'selected-pokemon' is not supported as self-heal |
| 353 | `heart-stamp` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 354 | `heart-swap` | PASS | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | `statStageSwap` with `group=all` swaps all stages generically |
| 355 | `heat-crash` | FAIL | damage | meta | target=selected-pokemon; category=damage | Minimize-dependent damage/hit exception is not implemented |
| 356 | `heat-wave` | FAIL | damage-ailment | meta | damage power=95; target=all-opponents; category=damage-ailment; ailment=burn | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 357 | `heavy-slam` | FAIL | damage | meta | target=selected-pokemon; category=damage | Minimize-dependent damage/hit exception is not implemented |
| 358 | `helping-hand` | FAIL | unique | meta | target=ally; priority 5; accuracy bypass/no accuracy check; category=unique | target 'ally' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 359 | `hex` | FAIL | damage | meta | damage power=65; target=selected-pokemon; category=damage | target-status-dependent power is not implemented |
| 360 | `hidden-power` | FAIL | damage | meta | damage power=60; target=selected-pokemon; category=damage | IV-dependent type/power is not implemented |

### Batch 19 (moves 361-380)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 361 | `high-horsepower` | PASS | damage | meta | damage power=95; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 362 | `high-jump-kick` | PASS | damage | meta | damage power=130; target=selected-pokemon; category=damage; crash recoil on miss/protect/no-effect | damage op plus existing `recoil` with `onMiss: true` |
| 363 | `hold-back` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 364 | `hold-hands` | FAIL | unique | meta | target=ally; accuracy bypass/no accuracy check; category=unique | target 'ally' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 365 | `hone-claws` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat accuracy 1 | multi-stat self boosts including accuracy cannot be represented exactly; stat stage 'accuracy' is not implemented; multiple simultaneous stat changes cannot be represented exactly |
| 366 | `horn-attack` | PASS | damage | meta | damage power=65; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 367 | `horn-drill` | PASS | ohko | meta | target=selected-pokemon; category=ohko | ohko op; known covered move family from current op palette |
| 368 | `horn-leech` | PASS | damage-heal | meta | damage power=75; target=selected-pokemon; category=damage-heal; drain=50% | drain op; damage plus drain op; known covered move family from current op palette |
| 369 | `howl` | FAIL | net-good-stats | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1 | target 'user-and-allies' is not implemented by the current singles battle UI/resolver |
| 370 | `hurricane` | FAIL | damage-ailment | meta | damage power=110; target=selected-pokemon; category=damage-ailment; ailment=confusion | weather-dependent accuracy and semi-invulnerable targeting exceptions are not implemented |
| 371 | `hydro-cannon` | FAIL | damage | meta | damage power=150; target=selected-pokemon; category=damage | recharge turn is not implemented |
| 372 | `hydro-pump` | PASS | damage | meta | damage power=110; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 373 | `hydro-steam` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 374 | `hydro-vortex--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 375 | `hydro-vortex--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 376 | `hyper-beam` | FAIL | damage | meta | damage power=150; target=selected-pokemon; category=damage | recharge turn is not implemented |
| 377 | `hyper-drill` | FAIL | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 378 | `hyper-fang` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=10 | flinch op; damage op / normal damage pipeline |
| 379 | `hyperspace-fury` | PASS | damage-raise | meta | damage power=100; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage-raise; stat Def -1 | single statStage op |
| 380 | `hyperspace-hole` | PASS | damage | meta | damage power=80; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |

### Batch 20 (moves 381-400)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 381 | `hyper-voice` | FAIL | damage | meta | damage power=90; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 382 | `hypnosis` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=sleep | ailment op; status move with ailment op when supported |
| 383 | `ice-ball` | FAIL | damage | meta | damage power=30; target=selected-pokemon; category=damage | locked multi-turn power ramp is not implemented |
| 384 | `ice-beam` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=freeze | ailment op; damage plus ailment if listed as supported |
| 385 | `ice-burn` | PASS | damage-ailment | meta | damage power=140; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 386 | `ice-fang` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=freeze; flinch chance=10 | ailment op; flinch op; damage plus ailment if listed as supported |
| 387 | `ice-hammer` | PASS | damage-raise | meta | damage power=100; target=selected-pokemon; category=damage-raise; stat Spe -1 | single statStage op |
| 388 | `ice-punch` | PASS | damage-ailment | meta | damage power=75; target=selected-pokemon; category=damage-ailment; ailment=freeze | ailment op; damage plus ailment if listed as supported |
| 389 | `ice-shard` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 390 | `ice-spinner` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 391 | `icicle-crash` | PASS | damage | meta | damage power=85; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 392 | `icicle-spear` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 393 | `icy-wind` | FAIL | damage-lower | meta | damage power=55; target=all-opponents; category=damage-lower; stat Spe -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 394 | `imprison` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 395 | `incinerate` | FAIL | damage | meta | damage power=60; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 396 | `infernal-parade` | FAIL | no-meta | flavor_text fallback | damage power=60; target=selected-pokemon; status: burn; dynamic power/modifier; conditional secondary/effect | dynamic/conditional power or damage modifier is not implemented; conditional move effect is not implemented exactly by current move ops |
| 397 | `inferno` | PASS | damage-ailment | meta | damage power=100; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 398 | `inferno-overdrive--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 399 | `inferno-overdrive--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 400 | `infestation` | PASS | damage-ailment | meta | damage power=20; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |

### Batch 21 (moves 401-420)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 401 | `ingrain` | FAIL | ailment | meta | target=user; accuracy bypass/no accuracy check; category=ailment; ailment=ingrain | ailment 'ingrain' is not implemented |
| 402 | `instruct` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 403 | `ion-deluge` | FAIL | whole-field-effect | meta | target=entire-field; priority 1; accuracy bypass/no accuracy check; category=whole-field-effect | normal-move type-changing field effect is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 404 | `iron-defense` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 2 | single statStage op |
| 405 | `iron-head` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 406 | `iron-tail` | PASS | damage-lower | meta | damage power=100; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 407 | `ivy-cudgel` | PASS | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon; crit-stage interaction | critStage; no-meta audited from flavor text because effect_entries is empty |
| 408 | `jaw-lock` | FAIL | damage | meta | damage power=80; target=selected-pokemon; category=damage | mutual no-switch trapping volatile is not implemented |
| 409 | `jet-punch` | FAIL | no-meta | flavor_text fallback | damage power=60; target=selected-pokemon; priority 1 | no-meta text does not prove exact expressibility with current ops |
| 410 | `judgment` | FAIL | damage | meta | damage power=100; target=selected-pokemon; category=damage | item-dependent type is not implemented |
| 411 | `jump-kick` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage; crash recoil on miss/protect/no-effect | damage op plus existing `recoil` with `onMiss: true` |
| 412 | `jungle-healing` | FAIL | unique | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=unique; heal=25% | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 413 | `karate-chop` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 414 | `kinesis` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 415 | `kings-shield` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | protect variant with contact stat drop is not implemented; unique behavior is not proven expressible by current generic ops |
| 416 | `knock-off` | FAIL | damage | meta | damage power=65; target=selected-pokemon; category=damage | held item removal and conditional power are not implemented |
| 417 | `kowtow-cleave` | FAIL | no-meta | flavor_text fallback | damage power=85; target=selected-pokemon; accuracy bypass/no accuracy check | no-meta text does not prove exact expressibility with current ops |
| 418 | `lands-wrath` | FAIL | damage | meta | damage power=90; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 419 | `laser-focus` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 420 | `lash-out` | FAIL | damage | meta | damage power=75; target=selected-pokemon; category=damage | same-turn stat-drop power modifier is not implemented |

### Batch 22 (moves 421-440)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 421 | `last-resort` | PASS | damage | meta | damage power=140; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 422 | `last-respects` | FAIL | no-meta | flavor_text fallback | damage power=50; target=selected-pokemon; multi-target or ally targeting | fainted-party-count power modifier is not implemented; multi-target/ally targeting is not implemented |
| 423 | `lava-plume` | FAIL | damage-ailment | meta | damage power=80; target=all-other-pokemon; category=damage-ailment; ailment=burn | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 424 | `leafage` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 425 | `leaf-blade` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 426 | `leaf-storm` | PASS | damage-raise | meta | damage power=130; target=selected-pokemon; category=damage-raise; stat Spa -2 | single statStage op |
| 427 | `leaf-tornado` | FAIL | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 428 | `leech-life` | PASS | damage-heal | meta | damage power=80; target=selected-pokemon; category=damage-heal; drain=50% | drain op; damage plus drain op; known covered move family from current op palette |
| 429 | `leech-seed` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=leech-seed | ailment 'leech-seed' is not implemented |
| 430 | `leer` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Def -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 431 | `lets-snuggle-forever` | PASS | damage | meta | damage power=190; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 432 | `lick` | PASS | damage-ailment | meta | damage power=30; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 433 | `life-dew` | FAIL | heal | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=heal; heal=25% | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; heal target 'user-and-allies' is not supported as self-heal |
| 434 | `light-of-ruin` | PASS | damage | meta | damage power=140; target=selected-pokemon; category=damage; recoil=50% | recoil op; damage op / normal damage pipeline |
| 435 | `light-screen` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | screen side condition is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 436 | `light-that-burns-the-sky` | FAIL | damage | meta | damage power=200; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | dynamic offensive stat/category choice is not implemented |
| 437 | `liquidation` | PASS | damage-lower | meta | damage power=85; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 438 | `lock-on` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 439 | `lovely-kiss` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=sleep | ailment op; status move with ailment op when supported |
| 440 | `low-kick` | FAIL | damage | meta | target=selected-pokemon; category=damage | weight-based power is not implemented |

### Batch 23 (moves 441-460)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 441 | `low-sweep` | PASS | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat Spe -1 | single statStage op |
| 442 | `lucky-chant` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 443 | `lumina-crash` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 444 | `lunar-blessing` | FAIL | no-meta | flavor_text fallback | target=all-allies; accuracy bypass/no accuracy check | target 'all-allies' is not implemented by the current singles battle UI/resolver |
| 445 | `lunar-dance` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 446 | `lunge` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Atk -1 | single statStage op |
| 447 | `luster-purge` | PASS | damage-lower | meta | damage power=95; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 448 | `mach-punch` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 449 | `magical-leaf` | PASS | damage | meta | damage power=60; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 450 | `magical-torque` | FAIL | no-meta | no English text | damage power=100; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | local JSON has no English effect text to audit exactly |
| 451 | `magic-coat` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 452 | `magic-powder` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 453 | `magic-room` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 454 | `magma-storm` | PASS | damage-ailment | meta | damage power=100; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 455 | `magnet-bomb` | PASS | damage | meta | damage power=60; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 456 | `magnetic-flux` | FAIL | net-good-stats | meta | target=user-and-allies; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 1; stat Spd 1 | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 457 | `magnet-rise` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 458 | `magnitude` | FAIL | damage | meta | target=all-other-pokemon; category=damage | random power table is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 459 | `make-it-rain` | FAIL | no-meta | flavor_text fallback | damage power=120; target=all-opponents | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 460 | `malicious-moonsault` | PASS | damage | meta | damage power=180; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |

### Batch 24 (moves 461-480)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 461 | `malignant-chain` | PASS | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon; status: poison | ailment poison if unconditional; no-meta audited from flavor text because effect_entries is empty |
| 462 | `mat-block` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 463 | `matcha-gotcha` | PASS | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; status: burn; drain heal from damage | ailment burn if unconditional; drain; no-meta audited from flavor text because effect_entries is empty |
| 464 | `max-airstream` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 465 | `max-darkness` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 466 | `max-flare` | FAIL | damage | meta | damage power=100; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 467 | `max-flutterby` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 468 | `max-geyser` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 469 | `max-guard` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 470 | `max-hailstorm` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 471 | `max-knuckle` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 472 | `max-lightning` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 473 | `max-mindstorm` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 474 | `max-ooze` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 475 | `max-overgrowth` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 476 | `max-phantasm` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 477 | `max-quake` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 478 | `max-rockfall` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 479 | `max-starfall` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 480 | `max-steelspike` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |

### Batch 25 (moves 481-500)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 481 | `max-strike` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 482 | `max-wyrmwind` | FAIL | damage | meta | damage power=10; target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 483 | `mean-look` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | no-switch trapping volatile is not implemented; unique behavior is not proven expressible by current generic ops |
| 484 | `meditate` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1 | single statStage op |
| 485 | `me-first` | FAIL | damage | meta | target=selected-pokemon-me-first; accuracy bypass/no accuracy check; category=damage | preemptive copied move is not implemented; target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 486 | `mega-drain` | PASS | damage-heal | meta | damage power=40; target=selected-pokemon; category=damage-heal; drain=50% | drain op; damage plus drain op; known covered move family from current op palette |
| 487 | `megahorn` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 488 | `mega-kick` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 489 | `mega-punch` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 490 | `memento` | FAIL | unique | meta | target=selected-pokemon; category=unique; stat Atk -2; stat Spa -2 | multiple simultaneous stat changes cannot be represented exactly; unique behavior is not proven expressible by current generic ops |
| 491 | `menacing-moonraze-maelstrom` | PASS | damage | meta | damage power=200; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 492 | `metal-burst` | FAIL | damage | meta | target=specific-move; category=damage | counter-any-category timing is not implemented; target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 493 | `metal-claw` | PASS | damage-raise | meta | damage power=50; target=selected-pokemon; category=damage-raise; stat Atk 1 | single statStage op |
| 494 | `metal-sound` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Spd -2 | single statStage op |
| 495 | `meteor-assault` | PASS | damage | meta | damage power=150; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 496 | `meteor-beam` | FAIL | damage | meta | damage power=120; target=selected-pokemon; category=damage | charge-turn stat boost is not implemented |
| 497 | `meteor-mash` | PASS | damage-raise | meta | damage power=90; target=selected-pokemon; category=damage-raise; stat Atk 1 | single statStage op |
| 498 | `metronome` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | move call/random move selection is not implemented; unique behavior is not proven expressible by current generic ops |
| 499 | `mighty-cleave` | FAIL | no-meta | flavor_text fallback | damage power=95; target=selected-pokemon | protect contact side effects are not implemented as move effects |
| 500 | `milk-drink` | PASS | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | heal op when self-only flat fraction |

### Batch 26 (moves 501-520)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 501 | `mimic` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | move copy/replace is not implemented; unique behavior is not proven expressible by current generic ops |
| 502 | `mind-blown` | FAIL | damage | meta | damage power=150; target=all-other-pokemon; category=damage | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 503 | `mind-reader` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 504 | `minimize` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat evasion 2 | stat stage 'evasion' is not implemented |
| 505 | `miracle-eye` | FAIL | ailment | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=ailment; ailment=no-type-immunity | ailment 'no-type-immunity' is not implemented |
| 506 | `mirror-coat` | FAIL | damage | meta | target=specific-move; priority -5; category=damage | target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 507 | `mirror-move` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | copy previous move is not implemented; unique behavior is not proven expressible by current generic ops |
| 508 | `mirror-shot` | FAIL | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 509 | `mist` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | team stat-drop shield is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 510 | `mist-ball` | PASS | damage-lower | meta | damage power=95; target=selected-pokemon; category=damage-lower; stat Spa -1 | single statStage op |
| 511 | `misty-explosion` | FAIL | damage | meta | damage power=100; target=all-other-pokemon; category=damage | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 512 | `misty-terrain` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 513 | `moonblast` | PASS | damage-lower | meta | damage power=95; target=selected-pokemon; category=damage-lower; stat Spa -1 | single statStage op |
| 514 | `moongeist-beam` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 515 | `moonlight` | FAIL | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | weather-scaled healing is not implemented |
| 516 | `morning-sun` | FAIL | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | weather-scaled healing is not implemented |
| 517 | `mortal-spin` | FAIL | no-meta | flavor_text fallback | damage power=30; target=all-opponents; status: poison | hazard/bind removal plus poison is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 518 | `mountain-gale` | FAIL | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 519 | `mud-bomb` | FAIL | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 520 | `muddy-water` | FAIL | damage-lower | meta | damage power=90; target=all-opponents; category=damage-lower; stat accuracy -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver; stat stage 'accuracy' is not implemented |

### Batch 27 (moves 521-540)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 521 | `mud-shot` | PASS | damage-lower | meta | damage power=55; target=selected-pokemon; category=damage-lower; stat Spe -1 | single statStage op |
| 522 | `mud-slap` | FAIL | damage-lower | meta | damage power=20; target=selected-pokemon; category=damage-lower; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 523 | `mud-sport` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | field damage modifier is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 524 | `multi-attack` | FAIL | damage | meta | damage power=120; target=selected-pokemon; category=damage | item-dependent type is not implemented |
| 525 | `mystical-fire` | PASS | damage-lower | meta | damage power=75; target=selected-pokemon; category=damage-lower; stat Spa -1 | single statStage op |
| 526 | `mystical-power` | FAIL | no-meta | flavor_text fallback | damage power=70; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 527 | `nasty-plot` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spa 2 | single statStage op |
| 528 | `natural-gift` | FAIL | damage | meta | target=selected-pokemon; category=damage | berry-dependent type/power and consumption are not implemented |
| 529 | `nature-power` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | field/terrain-dependent called move is not implemented; unique behavior is not proven expressible by current generic ops |
| 530 | `natures-madness` | FAIL | damage | meta | target=selected-pokemon; category=damage | fractional current-HP damage is not implemented |
| 531 | `needle-arm` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 532 | `never-ending-nightmare--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 533 | `never-ending-nightmare--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 534 | `night-daze` | FAIL | damage-lower | meta | damage power=85; target=selected-pokemon; category=damage-lower; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 535 | `nightmare` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=nightmare | ailment 'nightmare' is not implemented |
| 536 | `night-shade` | PASS | damage | meta | target=selected-pokemon; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 537 | `night-slash` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 538 | `noble-roar` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Atk -1; stat Spa -1 | multiple simultaneous stat changes cannot be represented exactly |
| 539 | `no-retreat` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Def 1; stat Spa 1; stat Spd 1; stat Spe 1 | multiple simultaneous stat changes cannot be represented exactly |
| 540 | `noxious-torque` | FAIL | no-meta | no English text | damage power=100; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | local JSON has no English effect text to audit exactly |

### Batch 28 (moves 541-560)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 541 | `nuzzle` | PASS | damage-ailment | meta | damage power=20; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 542 | `oblivion-wing` | PASS | damage-heal | meta | damage power=80; target=selected-pokemon; category=damage-heal; drain=75% | drain op; damage plus drain op; known covered move family from current op palette |
| 543 | `obstruct` | FAIL | unique | meta | target=user; priority 4; category=unique | protect variant with contact defense drop is not implemented; unique behavior is not proven expressible by current generic ops |
| 544 | `oceanic-operetta` | PASS | damage | meta | damage power=195; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 545 | `octazooka` | FAIL | damage-lower | meta | damage power=65; target=selected-pokemon; category=damage-lower; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 546 | `octolock` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 547 | `odor-sleuth` | FAIL | ailment | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=ailment; ailment=no-type-immunity | ailment 'no-type-immunity' is not implemented |
| 548 | `ominous-wind` | PASS | damage-raise | meta | damage power=60; target=selected-pokemon; category=damage-raise; stat Atk 1; stat Def 1; stat Spa 1; stat Spd 1; stat Spe 1 | `statStageAll` op gates and applies Atk/Def/Spa/Spd/Spe in order |
| 549 | `order-up` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; conditional secondary/effect | conditional move effect is not implemented exactly by current move ops |
| 550 | `origin-pulse` | FAIL | damage | meta | damage power=110; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 551 | `outrage` | PASS | damage | meta | damage power=120; target=random-opponent; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 552 | `overdrive` | FAIL | damage | meta | damage power=80; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 553 | `overheat` | PASS | damage-raise | meta | damage power=130; target=selected-pokemon; category=damage-raise; stat Spa -2 | single statStage op |
| 554 | `pain-split` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 555 | `parabolic-charge` | FAIL | damage-heal | meta | damage power=65; target=all-other-pokemon; category=damage-heal; drain=50% | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 556 | `parting-shot` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Atk -1; stat Spa -1 | stat drop plus user switch is not implemented; multiple simultaneous stat changes cannot be represented exactly |
| 557 | `payback` | FAIL | damage | meta | damage power=50; target=selected-pokemon; category=damage | move-after-target power modifier is not implemented |
| 558 | `pay-day` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 559 | `peck` | PASS | damage | meta | damage power=35; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 560 | `perish-song` | FAIL | ailment | meta | target=all-pokemon; accuracy bypass/no accuracy check; category=ailment; ailment=perish-song | perish count/faint timer is not implemented; target 'all-pokemon' is not implemented by the current singles battle UI/resolver; ailment 'perish-song' is not implemented |

### Batch 29 (moves 561-580)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 561 | `petal-blizzard` | FAIL | damage | meta | damage power=90; target=all-other-pokemon; category=damage | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 562 | `petal-dance` | PASS | damage | meta | damage power=120; target=random-opponent; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 563 | `phantom-force` | FAIL | damage | meta | damage power=90; target=selected-pokemon; category=damage | semi-invulnerable charge/protect break is not implemented |
| 564 | `photon-geyser` | FAIL | damage | meta | damage power=100; target=selected-pokemon; category=damage | dynamic offensive stat/category choice is not implemented |
| 565 | `pika-papow` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 566 | `pin-missile` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 567 | `plasma-fists` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 568 | `play-nice` | PASS | net-good-stats | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk -1 | single statStage op |
| 569 | `play-rough` | PASS | damage-lower | meta | damage power=90; target=selected-pokemon; category=damage-lower; stat Atk -1 | single statStage op |
| 570 | `pluck` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 571 | `poison-fang` | PASS | damage-ailment | meta | damage power=50; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |
| 572 | `poison-gas` | FAIL | ailment | meta | target=all-opponents; category=ailment; ailment=poison | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 573 | `poison-jab` | PASS | damage-ailment | meta | damage power=80; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |
| 574 | `poison-powder` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=poison | powder/grass immunities are not fully implemented |
| 575 | `poison-sting` | PASS | damage-ailment | meta | damage power=15; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |
| 576 | `poison-tail` | PASS | damage-ailment | meta | damage power=50; target=selected-pokemon; category=damage-ailment; ailment=poison; crit rate=1 | ailment op; crit-stage support; damage plus ailment if listed as supported |
| 577 | `pollen-puff` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 578 | `poltergeist` | PASS | damage | meta | damage power=110; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 579 | `population-bomb` | FAIL | no-meta | flavor_text fallback | damage power=20; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 580 | `pounce` | FAIL | no-meta | flavor_text fallback | damage power=50; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |

### Batch 30 (moves 581-600)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 581 | `pound` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 582 | `powder` | FAIL | unique | meta | target=selected-pokemon; priority 1; category=unique | powder volatile/fire trigger is not implemented; unique behavior is not proven expressible by current generic ops |
| 583 | `powder-snow` | FAIL | damage-ailment | meta | damage power=40; target=all-opponents; category=damage-ailment; ailment=freeze | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 584 | `power-gem` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 585 | `power-shift` | FAIL | no-meta | flavor_text fallback | target=user; accuracy bypass/no accuracy check | no-meta text does not prove exact expressibility with current ops |
| 586 | `power-split` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | stat averaging is not implemented; unique behavior is not proven expressible by current generic ops |
| 587 | `power-swap` | PASS | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | `statStageSwap` with `group=offense` swaps attacking stages generically |
| 588 | `power-trick` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 589 | `power-trip` | FAIL | damage | meta | damage power=20; target=selected-pokemon; category=damage | stat-stage-dependent power is not implemented |
| 590 | `power-up-punch` | PASS | damage-raise | meta | damage power=40; target=selected-pokemon; category=damage-raise; stat Atk 1 | single statStage op |
| 591 | `power-whip` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 592 | `precipice-blades` | FAIL | damage | meta | damage power=120; target=all-opponents; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 593 | `present` | FAIL | damage | meta | target=selected-pokemon; category=damage | random damage/heal is not implemented |
| 594 | `prismatic-laser` | PASS | damage | meta | damage power=160; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 595 | `protect` | PASS | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | covered by existing generic op palette; known covered move family from current op palette |
| 596 | `psybeam` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 597 | `psyblade` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; dynamic power/modifier; conditional secondary/effect | dynamic/conditional power or damage modifier is not implemented; conditional move effect is not implemented exactly by current move ops |
| 598 | `psychic` | PASS | damage-lower | meta | damage power=90; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 599 | `psychic-fangs` | PASS | damage | meta | damage power=85; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 600 | `psychic-noise` | FAIL | no-meta | flavor_text fallback | damage power=75; target=selected-pokemon; ability mutation/copy | ability mutation/copy is not implemented |

### Batch 31 (moves 601-620)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 601 | `psychic-terrain` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 602 | `psycho-boost` | PASS | damage-raise | meta | damage power=140; target=selected-pokemon; category=damage-raise; stat Spa -2 | single statStage op |
| 603 | `psycho-cut` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 604 | `psycho-shift` | FAIL | unique | meta | target=selected-pokemon; category=unique | status transfer is not implemented; unique behavior is not proven expressible by current generic ops |
| 605 | `psych-up` | PASS | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | `statStageCopy` copies target stages to the user |
| 606 | `psyshield-bash` | FAIL | no-meta | flavor_text fallback | damage power=70; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 607 | `psyshock` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | `damageStatOverride` uses target Defense while preserving the special damage category |
| 608 | `psystrike` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | `damageStatOverride` uses target Defense while preserving the special damage category |
| 609 | `psywave` | FAIL | damage | meta | target=selected-pokemon; category=damage | random level-scaled fixed damage is not implemented |
| 610 | `pulverizing-pancake` | PASS | damage | meta | damage power=210; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 611 | `punishment` | FAIL | damage | meta | target=selected-pokemon; category=damage | target-stat-stage-dependent power is not implemented |
| 612 | `purify` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique; heal=50% | unique behavior is not proven expressible by current generic ops |
| 613 | `pursuit` | FAIL | damage | meta | damage power=40; target=selected-pokemon; category=damage | switch-intercept damage is not implemented |
| 614 | `pyro-ball` | PASS | damage-ailment | meta | damage power=120; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 615 | `quash` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 616 | `quick-attack` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 617 | `quick-guard` | FAIL | field-effect | meta | target=users-field; priority 3; accuracy bypass/no accuracy check; category=field-effect | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 618 | `quiver-dance` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spa 1; stat Spd 1; stat Spe 1 | multi-stat self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 619 | `rage` | FAIL | damage | meta | damage power=20; target=selected-pokemon; category=damage | rage attack-boost-on-hit volatile is not implemented |
| 620 | `rage-fist` | FAIL | no-meta | flavor_text fallback | damage power=50; target=selected-pokemon | damage-taken count power modifier is not implemented |

### Batch 32 (moves 621-640)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 621 | `rage-powder` | FAIL | unique | meta | target=user; priority 2; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 622 | `raging-bull` | FAIL | no-meta | flavor_text fallback | damage power=90; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 623 | `raging-fury` | PASS | no-meta | flavor_text fallback | damage power=120; target=random-opponent; status: confusion | ailment/confusion if unconditional; no-meta audited from flavor text because effect_entries is empty |
| 624 | `rain-dance` | PASS | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | field-target scope plus weather op sets Rain through the shared field-condition path |
| 625 | `rapid-spin` | FAIL | damage-raise | meta | damage power=50; target=selected-pokemon; category=damage-raise; stat Spe 1 | hazard/bind removal plus speed boost is not implemented |
| 626 | `razor-leaf` | FAIL | damage | meta | damage power=55; target=all-opponents; category=damage; crit rate=1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 627 | `razor-shell` | PASS | damage-lower | meta | damage power=75; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 628 | `razor-wind` | FAIL | damage | meta | damage power=80; target=all-opponents; category=damage; crit rate=1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 629 | `recover` | PASS | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | heal op when self-only flat fraction |
| 630 | `recycle` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 631 | `reflect` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | screen side condition is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 632 | `reflect-type` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | copy target type is not implemented; unique behavior is not proven expressible by current generic ops |
| 633 | `refresh` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | self status cure is not implemented as a move effect; unique behavior is not proven expressible by current generic ops |
| 634 | `relic-song` | FAIL | damage-ailment | meta | damage power=75; target=all-opponents; category=damage-ailment; ailment=sleep | damage plus sleep plus species/form-specific transform is not implemented exactly; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 635 | `rest` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | self sleep plus full heal is not implemented as one exact move; unique behavior is not proven expressible by current generic ops |
| 636 | `retaliate` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | previous-turn ally faint power modifier is not implemented |
| 637 | `return` | FAIL | damage | meta | target=selected-pokemon; category=damage | friendship-dependent power is not implemented |
| 638 | `revelation-dance` | FAIL | damage | meta | damage power=90; target=selected-pokemon; category=damage | user-primary-type move typing is not implemented |
| 639 | `revenge` | FAIL | damage | meta | damage power=60; target=selected-pokemon; priority -4; category=damage | move-after-hit power modifier is not implemented |
| 640 | `reversal` | FAIL | damage | meta | target=selected-pokemon; category=damage | HP-ratio power is not implemented |

### Batch 33 (moves 641-660)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 641 | `revival-blessing` | FAIL | no-meta | flavor_text fallback | target=fainting-pokemon; accuracy bypass/no accuracy check | target 'fainting-pokemon' is not implemented by the current singles battle UI/resolver |
| 642 | `rising-voltage` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 643 | `roar` | PASS | force-switch | meta | target=selected-pokemon; priority -6; accuracy bypass/no accuracy check; category=force-switch | forceSwitch op; known covered move family from current op palette |
| 644 | `roar-of-time` | PASS | damage | meta | damage power=150; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 645 | `rock-blast` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 646 | `rock-climb` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 647 | `rock-polish` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spe 2 | single statStage op |
| 648 | `rock-slide` | FAIL | damage | meta | damage power=75; target=all-opponents; category=damage; flinch chance=30 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 649 | `rock-smash` | PASS | damage-lower | meta | damage power=40; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 650 | `rock-throw` | PASS | damage | meta | damage power=50; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 651 | `rock-tomb` | PASS | damage-lower | meta | damage power=60; target=selected-pokemon; category=damage-lower; stat Spe -1 | single statStage op |
| 652 | `rock-wrecker` | PASS | damage | meta | damage power=150; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 653 | `role-play` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 654 | `rolling-kick` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 655 | `rollout` | FAIL | damage | meta | damage power=30; target=selected-pokemon; category=damage | locked multi-turn power ramp is not implemented |
| 656 | `roost` | FAIL | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | temporary type removal plus heal is not implemented |
| 657 | `rototiller` | FAIL | net-good-stats | meta | target=all-pokemon; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Spa 1 | target 'all-pokemon' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 658 | `round` | FAIL | damage | meta | damage power=60; target=selected-pokemon; category=damage | same-turn ally move power/order behavior is not implemented |
| 659 | `ruination` | FAIL | no-meta | flavor_text fallback | damage power=1; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 660 | `sacred-fire` | PASS | damage-ailment | meta | damage power=100; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |

### Batch 34 (moves 661-680)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 661 | `sacred-sword` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 662 | `safeguard` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | team status shield is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 663 | `salt-cure` | FAIL | no-meta | flavor_text fallback | damage power=40; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 664 | `sand-attack` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 665 | `sandsear-storm` | FAIL | no-meta | flavor_text fallback | damage power=100; target=all-opponents; status: burn | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 666 | `sandstorm` | PASS | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | field-target scope plus weather op sets Sandstorm through the shared field-condition path |
| 667 | `sand-tomb` | PASS | damage-ailment | meta | damage power=35; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 668 | `sappy-seed` | FAIL | damage-ailment | meta | damage power=100; target=selected-pokemon; category=damage-ailment; ailment=leech-seed | ailment 'leech-seed' is not implemented |
| 669 | `savage-spin-out--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 670 | `savage-spin-out--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 671 | `scald` | PASS | damage-ailment | meta | damage power=80; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 672 | `scale-shot` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline |
| 673 | `scary-face` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Spe -2 | single statStage op |
| 674 | `scorching-sands` | PASS | damage-ailment | meta | damage power=70; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 675 | `scratch` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 676 | `screech` | PASS | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Def -2 | single statStage op |
| 677 | `searing-shot` | FAIL | damage-ailment | meta | damage power=100; target=all-other-pokemon; category=damage-ailment; ailment=burn | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 678 | `searing-sunraze-smash` | PASS | damage | meta | damage power=200; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 679 | `secret-power` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | field-dependent secondary effect is not implemented |
| 680 | `secret-sword` | PASS | damage | meta | damage power=85; target=selected-pokemon; category=damage | `damageStatOverride` uses target Defense while preserving the special damage category |

### Batch 35 (moves 681-700)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 681 | `seed-bomb` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 682 | `seed-flare` | PASS | damage-lower | meta | damage power=120; target=selected-pokemon; category=damage-lower; stat Spd -2 | single statStage op |
| 683 | `seismic-toss` | PASS | damage | meta | target=selected-pokemon; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 684 | `self-destruct` | FAIL | damage | meta | damage power=200; target=all-other-pokemon; category=damage | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 685 | `shadow-ball` | PASS | damage-lower | meta | damage power=80; target=selected-pokemon; category=damage-lower; stat Spd -1 | single statStage op |
| 686 | `shadow-blast` | PASS | no-meta | effect_entries | damage power=80; target=selected-pokemon; crit-stage interaction | critStage |
| 687 | `shadow-blitz` | FAIL | no-meta | effect_entries | damage power=40; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 688 | `shadow-bolt` | PASS | no-meta | effect_entries | damage power=75; target=selected-pokemon; status: paralysis | ailment paralysis if unconditional |
| 689 | `shadow-bone` | PASS | damage-lower | meta | damage power=85; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 690 | `shadow-break` | FAIL | no-meta | effect_entries | damage power=75; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 691 | `shadow-chill` | PASS | no-meta | effect_entries | damage power=75; target=selected-pokemon; status: freeze | ailment freeze if unconditional |
| 692 | `shadow-claw` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 693 | `shadow-down` | FAIL | no-meta | effect_entries | target=opponents-field | no-meta text does not prove exact expressibility with current ops |
| 694 | `shadow-end` | FAIL | no-meta | effect_entries | damage power=120; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 695 | `shadow-fire` | PASS | no-meta | effect_entries | damage power=75; target=selected-pokemon; status: burn | ailment burn if unconditional |
| 696 | `shadow-force` | FAIL | damage | meta | damage power=120; target=selected-pokemon; category=damage | semi-invulnerable charge/protect break is not implemented |
| 697 | `shadow-half` | FAIL | no-meta | effect_entries | target=entire-field | target 'entire-field' is not implemented by the current singles battle UI/resolver |
| 698 | `shadow-hold` | FAIL | no-meta | effect_entries | target=opponents-field; accuracy bypass/no accuracy check | no-meta text does not prove exact expressibility with current ops |
| 699 | `shadow-mist` | FAIL | no-meta | effect_entries | target=opponents-field | no-meta text does not prove exact expressibility with current ops |
| 700 | `shadow-panic` | PASS | no-meta | effect_entries | target=opponents-field; status: confusion | ailment/confusion if unconditional |

### Batch 36 (moves 701-720)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 701 | `shadow-punch` | PASS | damage | meta | damage power=60; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 702 | `shadow-rave` | FAIL | no-meta | effect_entries | damage power=70; target=opponents-field | no-meta text does not prove exact expressibility with current ops |
| 703 | `shadow-rush` | PASS | no-meta | effect_entries | damage power=55; target=selected-pokemon; crit-stage interaction | critStage |
| 704 | `shadow-shed` | FAIL | no-meta | effect_entries | target=entire-field; accuracy bypass/no accuracy check | target 'entire-field' is not implemented by the current singles battle UI/resolver |
| 705 | `shadow-sky` | FAIL | no-meta | effect_entries | target=entire-field; accuracy bypass/no accuracy check | target 'entire-field' is not implemented by the current singles battle UI/resolver |
| 706 | `shadow-sneak` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 707 | `shadow-storm` | FAIL | no-meta | effect_entries | damage power=95; target=opponents-field | no-meta text does not prove exact expressibility with current ops |
| 708 | `shadow-wave` | FAIL | no-meta | effect_entries | damage power=50; target=opponents-field | no-meta text does not prove exact expressibility with current ops |
| 709 | `sharpen` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1 | single statStage op |
| 710 | `shattered-psyche--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 711 | `shattered-psyche--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 712 | `shed-tail` | FAIL | no-meta | flavor_text fallback | target=user; accuracy bypass/no accuracy check; user switch effect | after-move user switch is not implemented |
| 713 | `sheer-cold` | PASS | ohko | meta | target=selected-pokemon; category=ohko | ohko op; known covered move family from current op palette |
| 714 | `shell-side-arm` | FAIL | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=poison | dynamic physical/special category choice is not implemented |
| 715 | `shell-smash` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique; stat Def -1; stat Spd -1; stat Atk 2; stat Spa 2; stat Spe 2 | multi-stat self changes cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly; unique behavior is not proven expressible by current generic ops |
| 716 | `shell-trap` | FAIL | damage | meta | damage power=150; target=all-opponents; priority -3; category=damage | conditional trap-on-contact timing is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 717 | `shelter` | FAIL | no-meta | flavor_text fallback | target=user; accuracy bypass/no accuracy check | no-meta text does not prove exact expressibility with current ops |
| 718 | `shift-gear` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Spe 2 | multi-stat self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 719 | `shock-wave` | PASS | damage | meta | damage power=60; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 720 | `shore-up` | FAIL | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | weather-scaled healing is not implemented |

### Batch 37 (moves 721-740)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 721 | `signal-beam` | PASS | damage-ailment | meta | damage power=75; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 722 | `silk-trap` | FAIL | no-meta | flavor_text fallback | target=user; priority 4; accuracy bypass/no accuracy check | protect variant with contact speed drop is not implemented |
| 723 | `silver-wind` | PASS | damage-raise | meta | damage power=60; target=selected-pokemon; category=damage-raise; stat Atk 1; stat Def 1; stat Spa 1; stat Spd 1; stat Spe 1 | `statStageAll` op gates and applies Atk/Def/Spa/Spd/Spe in order |
| 724 | `simple-beam` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 725 | `sing` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=sleep | ailment op; status move with ailment op when supported |
| 726 | `sinister-arrow-raid` | PASS | damage | meta | damage power=180; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 727 | `sizzly-slide` | PASS | damage-ailment | meta | damage power=60; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 728 | `sketch` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | permanent move copy is not implemented; unique behavior is not proven expressible by current generic ops |
| 729 | `skill-swap` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 730 | `skitter-smack` | PASS | damage-lower | meta | damage power=70; target=selected-pokemon; category=damage-lower; stat Spa -1 | single statStage op |
| 731 | `skull-bash` | FAIL | damage | meta | damage power=130; target=selected-pokemon; category=damage | charge-turn defense boost is not implemented |
| 732 | `sky-attack` | PASS | damage | meta | damage power=140; target=selected-pokemon; category=damage; flinch chance=30; crit rate=1 | flinch op; crit-stage support; damage op / normal damage pipeline |
| 733 | `sky-drop` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 734 | `sky-uppercut` | PASS | damage | meta | damage power=85; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 735 | `slack-off` | PASS | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | heal op when self-only flat fraction |
| 736 | `slam` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 737 | `slash` | PASS | damage | meta | damage power=70; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 738 | `sleep-powder` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=sleep | powder/grass immunities are not fully implemented |
| 739 | `sleep-talk` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | move call while asleep is not implemented; unique behavior is not proven expressible by current generic ops |
| 740 | `sludge` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |

### Batch 38 (moves 741-760)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 741 | `sludge-bomb` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |
| 742 | `sludge-wave` | FAIL | damage-ailment | meta | damage power=95; target=all-other-pokemon; category=damage-ailment; ailment=poison | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 743 | `smack-down` | FAIL | damage | meta | damage power=50; target=selected-pokemon; category=damage; ailment=unknown | ailment 'unknown' is not implemented |
| 744 | `smart-strike` | PASS | damage | meta | damage power=70; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 745 | `smelling-salts` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | paralysis-dependent power and cure are not implemented |
| 746 | `smog` | PASS | damage-ailment | meta | damage power=30; target=selected-pokemon; category=damage-ailment; ailment=poison | ailment op; damage plus ailment if listed as supported |
| 747 | `smokescreen` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat accuracy -1 | stat stage 'accuracy' is not implemented |
| 748 | `snap-trap` | PASS | damage-ailment | meta | damage power=35; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 749 | `snarl` | FAIL | damage-lower | meta | damage power=55; target=all-opponents; category=damage-lower; stat Spa -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 750 | `snatch` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 751 | `snipe-shot` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 752 | `snore` | FAIL | damage | meta | damage power=50; target=selected-pokemon; category=damage; flinch chance=30 | user-must-be-asleep gate is not implemented |
| 753 | `snowscape` | FAIL | no-meta | flavor_text fallback | target=entire-field; accuracy bypass/no accuracy check; snow weather | target 'entire-field' is not implemented by the current singles battle UI/resolver; snow weather is not implemented; engine has rain/sun/sandstorm/hail |
| 754 | `soak` | FAIL | unique | meta | target=selected-pokemon; category=unique | target type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 755 | `soft-boiled` | PASS | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | heal op when self-only flat fraction |
| 756 | `solar-beam` | FAIL | damage | meta | damage power=120; target=selected-pokemon; category=damage | weather-sensitive charge skip/power interaction is not implemented |
| 757 | `solar-blade` | FAIL | damage | meta | damage power=125; target=selected-pokemon; category=damage | weather-sensitive charge skip/power interaction is not implemented |
| 758 | `sonic-boom` | PASS | damage | meta | target=selected-pokemon; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 759 | `soul-stealing-7-star-strike` | PASS | damage | meta | damage power=195; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 760 | `spacial-rend` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |

### Batch 39 (moves 761-780)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 761 | `spark` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 762 | `sparkling-aria` | FAIL | damage | meta | damage power=90; target=all-other-pokemon; category=damage | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 763 | `sparkly-swirl` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 764 | `spectral-thief` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 765 | `speed-swap` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | speed stat swap is not implemented; unique behavior is not proven expressible by current generic ops |
| 766 | `spicy-extract` | FAIL | no-meta | flavor_text fallback | target=selected-pokemon; accuracy bypass/no accuracy check | no-meta text does not prove exact expressibility with current ops |
| 767 | `spider-web` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | no-switch trapping volatile is not implemented; unique behavior is not proven expressible by current generic ops |
| 768 | `spike-cannon` | PASS | damage | meta | damage power=20; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 769 | `spikes` | PASS | field-effect | meta | target=opponents-field; accuracy bypass/no accuracy check; category=field-effect | entry hazard op; known covered move family from current op palette |
| 770 | `spiky-shield` | FAIL | unique | meta | target=user; priority 4; accuracy bypass/no accuracy check; category=unique | protect variant with contact damage is not implemented; unique behavior is not proven expressible by current generic ops |
| 771 | `spin-out` | FAIL | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 772 | `spirit-break` | PASS | damage-lower | meta | damage power=75; target=selected-pokemon; category=damage-lower; stat Spa -1 | single statStage op |
| 773 | `spirit-shackle` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 774 | `spite` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 775 | `spit-up` | FAIL | damage | meta | target=selected-pokemon; category=damage | stockpile-dependent power is not implemented |
| 776 | `splash` | PASS | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | covered by existing generic op palette; known covered move family from current op palette |
| 777 | `splintered-stormshards` | PASS | damage | meta | damage power=190; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 778 | `splishy-splash` | FAIL | damage-ailment | meta | damage power=90; target=all-opponents; category=damage-ailment; ailment=paralysis | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 779 | `spore` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=sleep | powder/grass immunities are not fully implemented |
| 780 | `spotlight` | FAIL | unique | meta | target=selected-pokemon; priority 3; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |

### Batch 40 (moves 781-800)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 781 | `springtide-storm` | FAIL | no-meta | flavor_text fallback | damage power=100; target=all-opponents | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 782 | `stealth-rock` | PASS | field-effect | meta | target=opponents-field; accuracy bypass/no accuracy check; category=field-effect | entry hazard op; known covered move family from current op palette |
| 783 | `steam-eruption` | PASS | damage-ailment | meta | damage power=110; target=selected-pokemon; category=damage-ailment; ailment=burn | ailment op; damage plus ailment if listed as supported |
| 784 | `steamroller` | FAIL | damage | meta | damage power=65; target=selected-pokemon; category=damage; flinch chance=30 | Minimize-dependent damage/hit exception is not implemented |
| 785 | `steel-beam` | PASS | damage | meta | damage power=140; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 786 | `steel-roller` | FAIL | damage | meta | damage power=130; target=selected-pokemon; category=damage | terrain requirement/removal is not implemented |
| 787 | `steel-wing` | PASS | damage-raise | meta | damage power=70; target=selected-pokemon; category=damage-raise; stat Def 1 | single statStage op |
| 788 | `sticky-web` | FAIL | field-effect | meta | target=opponents-field; accuracy bypass/no accuracy check; category=field-effect | sticky web hazard is not implemented; field/side condition is not implemented by current move ops |
| 789 | `stockpile` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique; stat Def 1; stat Spd 1 | multiple simultaneous stat changes cannot be represented exactly; unique behavior is not proven expressible by current generic ops |
| 790 | `stoked-sparksurfer` | PASS | damage-ailment | meta | damage power=175; target=selected-pokemon; accuracy bypass/no accuracy check; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 791 | `stomp` | FAIL | damage | meta | damage power=65; target=selected-pokemon; category=damage; flinch chance=30 | Minimize-dependent damage/hit exception is not implemented |
| 792 | `stomping-tantrum` | FAIL | damage | meta | damage power=75; target=selected-pokemon; category=damage | previous-move-failed power modifier is not implemented |
| 793 | `stone-axe` | FAIL | no-meta | flavor_text fallback | damage power=65; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 794 | `stone-edge` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage; crit rate=1 | crit-stage support; damage op / normal damage pipeline |
| 795 | `stored-power` | FAIL | damage | meta | damage power=20; target=selected-pokemon; category=damage | stat-stage-dependent power is not implemented |
| 796 | `storm-throw` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage; crit rate=6 | crit-stage support; damage op / normal damage pipeline |
| 797 | `strange-steam` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 798 | `strength` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 799 | `strength-sap` | FAIL | unique | meta | target=selected-pokemon; category=unique; stat Atk -1 | unique behavior is not proven expressible by current generic ops |
| 800 | `string-shot` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Spe -2 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |

### Batch 41 (moves 801-820)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 801 | `struggle` | PASS | damage | meta | damage power=50; target=random-opponent; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 802 | `struggle-bug` | FAIL | damage-lower | meta | damage power=50; target=all-opponents; category=damage-lower; stat Spa -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 803 | `stuff-cheeks` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 2 | single statStage op |
| 804 | `stun-spore` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=paralysis | powder/grass immunities are not fully implemented |
| 805 | `submission` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; recoil=25% | recoil op; damage op / normal damage pipeline |
| 806 | `substitute` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | substitute decoy state is not implemented; unique behavior is not proven expressible by current generic ops |
| 807 | `subzero-slammer--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 808 | `subzero-slammer--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 809 | `sucker-punch` | FAIL | damage | meta | damage power=70; target=selected-pokemon; priority 1; category=damage | target-selected-damaging-move condition is not implemented |
| 810 | `sunny-day` | PASS | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | field-target scope plus weather op sets Sun through the shared field-condition path |
| 811 | `sunsteel-strike` | PASS | damage | meta | damage power=100; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 812 | `supercell-slam` | FAIL | no-meta | flavor_text fallback | damage power=100; target=selected-pokemon; recoil/crash damage | crash recoil maps to existing `recoil` with `onMiss: true`; local no-meta text lacks exact crash amount needed to author exactly |
| 813 | `super-fang` | FAIL | damage | meta | target=selected-pokemon; category=damage | fractional current-HP damage is not implemented |
| 814 | `superpower` | FAIL | damage-raise | meta | damage power=120; target=selected-pokemon; category=damage-raise; stat Atk -1; stat Def -1 | multiple simultaneous stat changes cannot be represented exactly |
| 815 | `supersonic` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=confusion | ailment op; status move with ailment op when supported |
| 816 | `supersonic-skystrike--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 817 | `supersonic-skystrike--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 818 | `surf` | FAIL | damage | meta | damage power=90; target=all-other-pokemon; category=damage | semi-invulnerable target exception/double damage is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 819 | `surging-strikes` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; crit rate=6; multiHit=3-3 | crit-stage support; multiHit op; damage op / normal damage pipeline |
| 820 | `swagger` | PASS | swagger | meta | target=selected-pokemon; category=swagger; ailment=confusion; stat Atk 2 | ailment op; single statStage op; single statStage plus confusion combination |

### Batch 42 (moves 821-840)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 821 | `swallow` | PASS | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=25% | heal op when self-only flat fraction |
| 822 | `sweet-kiss` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=confusion | ailment op; status move with ailment op when supported |
| 823 | `sweet-scent` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat evasion -2 | target 'all-opponents' is not implemented by the current singles battle UI/resolver; stat stage 'evasion' is not implemented |
| 824 | `swift` | FAIL | damage | meta | damage power=60; target=all-opponents; accuracy bypass/no accuracy check; category=damage | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 825 | `switcheroo` | FAIL | unique | meta | target=selected-pokemon; category=unique | held item swapping is not implemented; unique behavior is not proven expressible by current generic ops |
| 826 | `swords-dance` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 2 | single statStage op |
| 827 | `synchronoise` | FAIL | damage | meta | damage power=120; target=all-other-pokemon; category=damage | shared-type target gate is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 828 | `synthesis` | FAIL | heal | meta | target=user; accuracy bypass/no accuracy check; category=heal; heal=50% | weather-scaled healing is not implemented |
| 829 | `syrup-bomb` | FAIL | unique | meta | damage power=60; target=selected-pokemon; category=unique; stat Spe -1 | unique behavior is not proven expressible by current generic ops |
| 830 | `tachyon-cutter` | FAIL | no-meta | flavor_text fallback | damage power=50; target=selected-pokemon | no-meta text does not prove exact expressibility with current ops |
| 831 | `tackle` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 832 | `tail-glow` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Spa 3 | single statStage op |
| 833 | `tail-slap` | PASS | damage | meta | damage power=25; target=selected-pokemon; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 834 | `tail-whip` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Def -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 835 | `tailwind` | FAIL | field-effect | meta | target=users-field; accuracy bypass/no accuracy check; category=field-effect | side speed field is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 836 | `take-down` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage; recoil=25% | recoil op; damage op / normal damage pipeline |
| 837 | `take-heart` | FAIL | no-meta | flavor_text fallback | target=all-allies; accuracy bypass/no accuracy check | target 'all-allies' is not implemented by the current singles battle UI/resolver |
| 838 | `tar-shot` | FAIL | swagger | meta | target=selected-pokemon; category=swagger; ailment=tar-shot; stat Spe -1 | ailment 'tar-shot' is not implemented |
| 839 | `taunt` | FAIL | unique | meta | target=selected-pokemon; category=unique | status-move lockout is not implemented; unique behavior is not proven expressible by current generic ops |
| 840 | `tearful-look` | FAIL | net-good-stats | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk -1; stat Spa -1 | multiple simultaneous stat changes cannot be represented exactly |

### Batch 43 (moves 841-860)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 841 | `teatime` | FAIL | unique | meta | target=all-pokemon; accuracy bypass/no accuracy check; category=unique | target 'all-pokemon' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 842 | `techno-blast` | FAIL | damage | meta | damage power=120; target=selected-pokemon; category=damage | item-dependent type is not implemented |
| 843 | `tectonic-rage--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 844 | `tectonic-rage--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 845 | `teeter-dance` | FAIL | ailment | meta | target=all-other-pokemon; category=ailment; ailment=confusion | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 846 | `telekinesis` | FAIL | ailment | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=ailment; ailment=unknown | ailment 'unknown' is not implemented |
| 847 | `teleport` | FAIL | unique | meta | target=user; priority -6; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 848 | `temper-flare` | FAIL | no-meta | flavor_text fallback | damage power=75; target=selected-pokemon; dynamic power/modifier | dynamic/conditional power or damage modifier is not implemented |
| 849 | `tera-blast` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon; conditional secondary/effect | terastallization-dependent type/category is not implemented; conditional move effect is not implemented exactly by current move ops |
| 850 | `tera-starstorm` | FAIL | no-meta | flavor_text fallback | damage power=120; target=all-opponents; multi-target or ally targeting | target 'all-opponents' is not implemented by the current singles battle UI/resolver; multi-target/ally targeting is not implemented |
| 851 | `terrain-pulse` | FAIL | damage | meta | damage power=50; target=selected-pokemon; category=damage | terrain-dependent type/power is not implemented |
| 852 | `thief` | FAIL | damage | meta | damage power=60; target=selected-pokemon; category=damage | held item stealing is not implemented |
| 853 | `thousand-arrows` | FAIL | damage | meta | damage power=90; target=all-opponents; category=damage; ailment=unknown | target 'all-opponents' is not implemented by the current singles battle UI/resolver; ailment 'unknown' is not implemented |
| 854 | `thousand-waves` | FAIL | damage | meta | damage power=90; target=all-opponents; category=damage | damage plus no-switch trapping volatile is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 855 | `thrash` | PASS | damage | meta | damage power=120; target=random-opponent; category=damage | damage op / normal damage pipeline; known covered move family from current op palette |
| 856 | `throat-chop` | FAIL | damage-ailment | meta | damage power=80; target=selected-pokemon; category=damage-ailment; ailment=silence | ailment 'silence' is not implemented |
| 857 | `thunder` | FAIL | damage-ailment | meta | damage power=110; target=selected-pokemon; category=damage-ailment; ailment=paralysis | weather-dependent accuracy and semi-invulnerable targeting exceptions are not implemented |
| 858 | `thunderbolt` | PASS | damage-ailment | meta | damage power=90; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 859 | `thunder-cage` | PASS | damage-ailment | meta | damage power=80; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 860 | `thunderclap` | FAIL | no-meta | flavor_text fallback | damage power=70; target=selected-pokemon; priority 1 | target-selected-attack condition is not implemented |

### Batch 44 (moves 861-880)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 861 | `thunder-fang` | PASS | damage-ailment | meta | damage power=65; target=selected-pokemon; category=damage-ailment; ailment=paralysis; flinch chance=10 | ailment op; flinch op; damage plus ailment if listed as supported |
| 862 | `thunderous-kick` | PASS | damage-lower | meta | damage power=90; target=selected-pokemon; category=damage-lower; stat Def -1 | single statStage op |
| 863 | `thunder-punch` | PASS | damage-ailment | meta | damage power=75; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 864 | `thunder-shock` | PASS | damage-ailment | meta | damage power=40; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 865 | `thunder-wave` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=paralysis | ailment op; status move with ailment op when supported |
| 866 | `tickle` | FAIL | net-good-stats | meta | target=selected-pokemon; category=net-good-stats; stat Atk -1; stat Def -1 | multiple simultaneous stat changes cannot be represented exactly |
| 867 | `tidy-up` | FAIL | no-meta | flavor_text fallback | target=user; accuracy bypass/no accuracy check; sets spikes hazard | substitute/hazard cleanup plus stat boosts is not implemented |
| 868 | `topsy-turvy` | PASS | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | `statStageInvert` inverts target stages generically |
| 869 | `torch-song` | FAIL | no-meta | flavor_text fallback | damage power=80; target=selected-pokemon | damage plus self Sp. Atk boost must be authored from no-meta text and was not proven by metadata |
| 870 | `torment` | FAIL | ailment | meta | target=selected-pokemon; category=ailment; ailment=torment | repeat-move lockout is not implemented; ailment 'torment' is not implemented |
| 871 | `toxic` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=poison | ailment op; status move with ailment op when supported |
| 872 | `toxic-spikes` | FAIL | field-effect | meta | target=opponents-field; accuracy bypass/no accuracy check; category=field-effect | toxic spikes hazard is not implemented; field/side condition is not implemented by current move ops |
| 873 | `toxic-thread` | PASS | swagger | meta | target=selected-pokemon; category=swagger; ailment=poison; stat Spe -1 | ailment op; single statStage op; single statStage plus confusion combination |
| 874 | `trailblaze` | PASS | no-meta | flavor_text fallback | damage power=50; target=selected-pokemon; self Speed boost | single statStage; no-meta audited from flavor text because effect_entries is empty |
| 875 | `transform` | FAIL | unique | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 876 | `tri-attack` | FAIL | damage-ailment | meta | damage power=80; target=selected-pokemon; category=damage-ailment; ailment=unknown | ailment 'unknown' is not implemented |
| 877 | `trick` | FAIL | unique | meta | target=selected-pokemon; category=unique | held item swapping is not implemented; unique behavior is not proven expressible by current generic ops |
| 878 | `trick-or-treat` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 879 | `trick-room` | FAIL | whole-field-effect | meta | target=entire-field; priority -7; accuracy bypass/no accuracy check; category=whole-field-effect | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 880 | `triple-arrows` | PASS | no-meta | flavor_text fallback | damage power=90; target=selected-pokemon; crit-stage interaction | critStage; no-meta audited from flavor text because effect_entries is empty |

### Batch 45 (moves 881-900)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 881 | `triple-axel` | PASS | damage | meta | damage power=20; target=selected-pokemon; category=damage; multiHit=3-3 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 882 | `triple-dive` | FAIL | no-meta | flavor_text fallback | damage power=30; target=selected-pokemon | fixed three-hit no-meta behavior is not proven by metadata/effect text |
| 883 | `triple-kick` | PASS | damage | meta | damage power=10; target=selected-pokemon; category=damage; multiHit=3-3 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 884 | `trop-kick` | PASS | damage-lower | meta | damage power=70; target=selected-pokemon; category=damage-lower; stat Atk -1 | single statStage op |
| 885 | `trump-card` | FAIL | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | PP-dependent power is not implemented |
| 886 | `twin-beam` | FAIL | no-meta | flavor_text fallback | damage power=40; target=selected-pokemon | fixed two-hit no-meta behavior is not proven by metadata/effect text |
| 887 | `twineedle` | PASS | damage-ailment | meta | damage power=25; target=selected-pokemon; category=damage-ailment; ailment=poison; multiHit=2-2 | ailment op; multiHit op; damage plus ailment if listed as supported |
| 888 | `twinkle-tackle--physical` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 889 | `twinkle-tackle--special` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 890 | `twister` | FAIL | damage | meta | damage power=40; target=all-opponents; category=damage; flinch chance=20 | semi-invulnerable target exception/double damage is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 891 | `upper-hand` | FAIL | no-meta | flavor_text fallback | damage power=65; target=selected-pokemon; priority 3 | target-priority-move condition is not implemented |
| 892 | `uproar` | PASS | damage | meta | damage power=90; target=random-opponent; category=damage | damage op / normal damage pipeline |
| 893 | `u-turn` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | after-hit user switch is not implemented |
| 894 | `vacuum-wave` | PASS | damage | meta | damage power=40; target=selected-pokemon; priority 1; category=damage | damage op / normal damage pipeline |
| 895 | `v-create` | FAIL | damage-raise | meta | damage power=180; target=selected-pokemon; category=damage-raise; stat Def -1; stat Spd -1; stat Spe -1 | multiple simultaneous stat changes cannot be represented exactly |
| 896 | `veevee-volley` | PASS | damage | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 897 | `venom-drench` | FAIL | net-good-stats | meta | target=all-opponents; category=net-good-stats; stat Atk -1; stat Spa -1; stat Spe -1 | target 'all-opponents' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 898 | `venoshock` | FAIL | damage | meta | damage power=65; target=selected-pokemon; category=damage | target-status-dependent power is not implemented |
| 899 | `vice-grip` | PASS | damage | meta | damage power=55; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 900 | `victory-dance` | FAIL | no-meta | flavor_text fallback | target=user; accuracy bypass/no accuracy check | multi-stat self boosts cannot be represented exactly |

### Batch 46 (moves 901-920)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 901 | `vine-whip` | PASS | damage | meta | damage power=45; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 902 | `vital-throw` | PASS | damage | meta | damage power=70; target=selected-pokemon; priority -1; accuracy bypass/no accuracy check; category=damage | damage op / normal damage pipeline |
| 903 | `volt-switch` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | after-hit user switch is not implemented |
| 904 | `volt-tackle` | PASS | damage-ailment | meta | damage power=120; target=selected-pokemon; category=damage-ailment; ailment=paralysis; recoil=33% | ailment op; recoil op; damage plus ailment if listed as supported |
| 905 | `wake-up-slap` | FAIL | damage | meta | damage power=70; target=selected-pokemon; category=damage | sleep-dependent power and wake-up are not implemented |
| 906 | `waterfall` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=20 | flinch op; damage op / normal damage pipeline |
| 907 | `water-gun` | PASS | damage | meta | damage power=40; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 908 | `water-pledge` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 909 | `water-pulse` | PASS | damage-ailment | meta | damage power=60; target=selected-pokemon; category=damage-ailment; ailment=confusion | ailment op; damage plus ailment if listed as supported |
| 910 | `water-shuriken` | PASS | damage | meta | damage power=15; target=selected-pokemon; priority 1; category=damage; multiHit=2-5 | multiHit op; damage op / normal damage pipeline; known covered move family from current op palette |
| 911 | `water-sport` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | field damage modifier is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 912 | `water-spout` | PASS | damage | meta | damage power=150; target=all-opponents; category=damage | `hpRatioPower` with user source scales base power by the user's current HP ratio; singles active-target resolver maps all-opponents to the active opponent |
| 913 | `wave-crash` | FAIL | no-meta | flavor_text fallback | damage power=120; target=selected-pokemon; recoil/crash damage | local no-meta text lacks exact recoil/crash amount needed to author exactly |
| 914 | `weather-ball` | FAIL | damage | meta | damage power=50; target=selected-pokemon; category=damage | weather-dependent type/power is not implemented |
| 915 | `whirlpool` | FAIL | damage-ailment | meta | damage power=35; target=selected-pokemon; category=damage-ailment; ailment=trap | semi-invulnerable target exception/double damage is not implemented |
| 916 | `whirlwind` | PASS | force-switch | meta | target=selected-pokemon; priority -6; accuracy bypass/no accuracy check; category=force-switch | forceSwitch op; known covered move family from current op palette |
| 917 | `wicked-blow` | PASS | damage | meta | damage power=75; target=selected-pokemon; category=damage; crit rate=6 | crit-stage support; damage op / normal damage pipeline |
| 918 | `wicked-torque` | FAIL | no-meta | no English text | damage power=80; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | local JSON has no English effect text to audit exactly |
| 919 | `wide-guard` | FAIL | field-effect | meta | target=users-field; priority 3; accuracy bypass/no accuracy check; category=field-effect | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 920 | `wildbolt-storm` | FAIL | no-meta | flavor_text fallback | damage power=100; target=all-opponents; status: paralysis | target 'all-opponents' is not implemented by the current singles battle UI/resolver |

### Batch 47 (moves 921-937)

| # | Move | Status | Category | Source | Derived functionality | Engine support / missing support |
|---:|---|---|---|---|---|---|
| 921 | `wild-charge` | PASS | damage | meta | damage power=90; target=selected-pokemon; category=damage; recoil=25% | recoil op; damage op / normal damage pipeline |
| 922 | `will-o-wisp` | PASS | ailment | meta | target=selected-pokemon; category=ailment; ailment=burn | ailment op; status move with ailment op when supported |
| 923 | `wing-attack` | PASS | damage | meta | damage power=60; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 924 | `wish` | FAIL | unique | meta | target=user; accuracy bypass/no accuracy check; category=unique | unique behavior is not proven expressible by current generic ops |
| 925 | `withdraw` | PASS | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Def 1 | single statStage op |
| 926 | `wonder-room` | FAIL | whole-field-effect | meta | target=entire-field; accuracy bypass/no accuracy check; category=whole-field-effect | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 927 | `wood-hammer` | PASS | damage | meta | damage power=120; target=selected-pokemon; category=damage; recoil=33% | recoil op; damage op / normal damage pipeline |
| 928 | `work-up` | FAIL | net-good-stats | meta | target=user; accuracy bypass/no accuracy check; category=net-good-stats; stat Atk 1; stat Spa 1 | multiple simultaneous stat changes cannot be represented exactly |
| 929 | `worry-seed` | FAIL | unique | meta | target=selected-pokemon; category=unique | unique behavior is not proven expressible by current generic ops |
| 930 | `wrap` | PASS | damage-ailment | meta | damage power=15; target=selected-pokemon; category=damage-ailment; ailment=trap | bind/partial trap op; damage plus ailment if listed as supported; known covered move family from current op palette |
| 931 | `wring-out` | PASS | damage | meta | target=selected-pokemon; category=damage | `hpRatioPower` with target source scales base power by the active target's current HP ratio |
| 932 | `x-scissor` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage | damage op / normal damage pipeline |
| 933 | `yawn` | FAIL | ailment | meta | target=selected-pokemon; accuracy bypass/no accuracy check; category=ailment; ailment=yawn | delayed sleep is not implemented; ailment 'yawn' is not implemented |
| 934 | `zap-cannon` | PASS | damage-ailment | meta | damage power=120; target=selected-pokemon; category=damage-ailment; ailment=paralysis | ailment op; damage plus ailment if listed as supported |
| 935 | `zen-headbutt` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=20 | flinch op; damage op / normal damage pipeline |
| 936 | `zing-zap` | PASS | damage | meta | damage power=80; target=selected-pokemon; category=damage; flinch chance=30 | flinch op; damage op / normal damage pipeline |
| 937 | `zippy-zap` | FAIL | damage-raise | meta | damage power=80; target=selected-pokemon; priority 2; category=damage-raise; stat evasion 1 | stat stage 'evasion' is not implemented |

## Failed Moves Only

| # | Move | Category | Source | Missing support |
|---:|---|---|---|---|
| 9 | `acrobatics` | damage | meta | held-item absence power modifier is not implemented |
| 10 | `acupressure` | unique | meta | target 'user-or-ally' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 13 | `after-you` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 19 | `alluring-voice` | no-meta | flavor_text fallback | conditional move effect is not implemented exactly by current move ops |
| 20 | `ally-switch` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 22 | `anchor-shot` | damage | meta | damage plus no-switch trapping volatile is not implemented |
| 27 | `aqua-ring` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 30 | `armor-cannon` | no-meta | flavor_text fallback | multiple simultaneous stat changes cannot be represented exactly |
| 32 | `aromatherapy` | unique | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 33 | `aromatic-mist` | net-good-stats | meta | target 'ally' is not implemented by the current singles battle UI/resolver |
| 34 | `assist` | unique | meta | party move call is not implemented; unique behavior is not proven expressible by current generic ops |
| 35 | `assurance` | damage | meta | same-turn damage-taken power modifier is not implemented |
| 39 | `attract` | ailment | meta | ailment 'infatuation' is not implemented |
| 43 | `aurora-veil` | field-effect | meta | screen side condition is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 45 | `avalanche` | damage | meta | move-after-hit power modifier is not implemented |
| 46 | `axe-kick` | no-meta | flavor_text fallback | crash recoil maps to existing `recoil` with `onMiss: true`; local no-meta text lacks exact confusion chance needed to author exactly |
| 49 | `baneful-bunker` | unique | meta | protect variant with contact poison is not implemented; unique behavior is not proven expressible by current generic ops |
| 50 | `barb-barrage` | no-meta | flavor_text fallback | dynamic/conditional power or damage modifier is not implemented; conditional move effect is not implemented exactly by current move ops |
| 53 | `baton-pass` | unique | meta | stat/volatile passing on switch is not implemented; unique behavior is not proven expressible by current generic ops |
| 54 | `beak-blast` | damage | meta | charge/contact-burn timing is not implemented |
| 55 | `beat-up` | damage | meta | party-based multi-hit damage is not implemented |
| 58 | `belch` | damage | meta | requires prior berry consumption, which is not implemented as a move condition |
| 60 | `bestow` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 61 | `bide` | damage | meta | delayed stored-damage release is not implemented |
| 68 | `blast-burn` | damage | meta | recharge turn is not implemented |
| 70 | `blazing-torque` | no-meta | no English text | local JSON has no English effect text to audit exactly |
| 71 | `bleakwind-storm` | no-meta | flavor_text fallback | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 72 | `blizzard` | damage-ailment | meta | weather-dependent accuracy exception is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 73 | `block` | unique | meta | no-switch trapping volatile is not implemented; unique behavior is not proven expressible by current generic ops |
| 74 | `blood-moon` | no-meta | flavor_text fallback | repeat-use lockout is not implemented |
| 79 | `body-slam` | damage-ailment | meta | Minimize-dependent accuracy exception is not implemented |
| 80 | `bolt-beak` | damage | meta | pre-target-move power doubling is not implemented |
| 86 | `bounce` | damage-ailment | meta | semi-invulnerable charge turn is not implemented |
| 100 | `bulk-up` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 104 | `burning-bulwark` | no-meta | flavor_text fallback | protect variant with contact burn is not implemented; protect contact side effects are not implemented as move effects |
| 105 | `burning-jealousy` | damage-ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 106 | `burn-up` | damage | meta | post-use type removal is not implemented |
| 108 | `calm-mind` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 109 | `camouflage` | unique | meta | terrain-dependent type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 110 | `captivate` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 113 | `celebrate` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 114 | `charge` | net-good-stats | meta | charge volatile plus later Electric damage boost is not implemented |
| 119 | `chilly-reception` | no-meta | flavor_text fallback | weather plus user switch is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; after-move user switch is not implemented; snow weather is not implemented; engine has rain/sun/sandstorm/hail |
| 121 | `chloroblast` | no-meta | flavor_text fallback | local no-meta text lacks exact recoil/crash amount needed to author exactly |
| 124 | `clanging-scales` | damage-raise | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 126 | `clangorous-soulblaze` | damage-raise | meta | full all-opponents topology is not implemented beyond the current singles active opponent |
| 128 | `close-combat` | damage-raise | meta | multiple simultaneous stat changes cannot be represented exactly |
| 129 | `coaching` | net-good-stats | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 130 | `coil` | net-good-stats | meta | multi-stat self boosts including accuracy cannot be represented exactly; stat stage 'accuracy' is not implemented; multiple simultaneous stat changes cannot be represented exactly |
| 131 | `collision-course` | no-meta | flavor_text fallback | dynamic/conditional power or damage modifier is not implemented |
| 132 | `combat-torque` | no-meta | no English text | local JSON has no English effect text to audit exactly |
| 134 | `comeuppance` | no-meta | flavor_text fallback | retaliation against last damage source is not implemented; target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 141 | `conversion` | unique | meta | type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 142 | `conversion-2` | unique | meta | type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 143 | `copycat` | unique | meta | copy last move is not implemented; unique behavior is not proven expressible by current generic ops |
| 144 | `core-enforcer` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 147 | `corrosive-gas` | unique | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 148 | `cosmic-power` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 150 | `cotton-spore` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 151 | `counter` | damage | meta | target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 152 | `court-change` | unique | meta | target 'entire-field' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 153 | `covet` | damage | meta | held item stealing is not implemented |
| 155 | `crafty-shield` | field-effect | meta | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 161 | `curse` | unique | meta | target 'specific-move' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 165 | `dark-void` | ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 167 | `decorate` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 168 | `defend-order` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 169 | `defense-curl` | net-good-stats | meta | Rollout/Ice Ball power flag is not implemented |
| 170 | `defog` | unique | meta | hazard/screen removal and evasion drop are not implemented; stat stage 'evasion' is not implemented; unique behavior is not proven expressible by current generic ops |
| 171 | `destiny-bond` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 175 | `diamond-storm` | damage-raise | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 176 | `dig` | damage | meta | semi-invulnerable charge turn is not implemented |
| 177 | `dire-claw` | no-meta | flavor_text fallback | random one-of-many ailment selection is not implemented |
| 178 | `disable` | unique | meta | disable move lockout is not implemented; ailment 'disable' is not implemented; unique behavior is not proven expressible by current generic ops |
| 180 | `discharge` | damage-ailment | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 181 | `dive` | damage | meta | semi-invulnerable charge turn is not implemented |
| 183 | `doodle` | no-meta | flavor_text fallback | ability mutation/copy is not implemented |
| 184 | `doom-desire` | unique | meta | delayed damage is not implemented; unique behavior is not proven expressible by current generic ops |
| 189 | `double-shock` | no-meta | flavor_text fallback | post-use type removal is not implemented; type mutation is not implemented |
| 191 | `double-team` | net-good-stats | meta | stat stage 'evasion' is not implemented |
| 193 | `dragon-ascent` | damage-raise | meta | multiple simultaneous stat changes cannot be represented exactly |
| 195 | `dragon-cheer` | no-meta | flavor_text fallback | target 'all-allies' is not implemented by the current singles battle UI/resolver; multi-target/ally targeting is not implemented |
| 197 | `dragon-dance` | net-good-stats | meta | multi-stat self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 199 | `dragon-energy` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 203 | `dragon-rush` | damage | meta | Minimize-dependent damage/hit exception is not implemented |
| 207 | `dream-eater` | damage-heal | meta | target-must-be-asleep gate plus drain is not implemented |
| 216 | `earthquake` | damage | meta | semi-invulnerable target exception/double damage is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 217 | `echoed-voice` | damage | meta | consecutive-use field power ramp is not implemented |
| 221 | `electric-terrain` | whole-field-effect | meta | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 222 | `electrify` | unique | meta | target move type-changing effect is not implemented; unique behavior is not proven expressible by current generic ops |
| 223 | `electro-ball` | damage | meta | speed-ratio power is not implemented |
| 224 | `electro-drift` | no-meta | flavor_text fallback | dynamic/conditional power or damage modifier is not implemented |
| 225 | `electro-shot` | no-meta | flavor_text fallback | charge-turn stat boost and rain skip are not implemented |
| 226 | `electroweb` | damage-lower | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 227 | `embargo` | ailment | meta | ailment 'embargo' is not implemented |
| 229 | `encore` | unique | meta | encore move lock is not implemented; unique behavior is not proven expressible by current generic ops |
| 230 | `endeavor` | damage | meta | target-current-HP matching damage is not implemented |
| 231 | `endure` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 233 | `entrainment` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 237 | `expanding-force` | damage | meta | terrain-dependent target/power is not implemented |
| 238 | `explosion` | damage | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 242 | `facade` | damage | meta | status-dependent power is not implemented |
| 243 | `fairy-lock` | whole-field-effect | meta | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 245 | `fake-out` | damage | meta | first-turn-only condition is not implemented |
| 253 | `fickle-beam` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 255 | `fiery-wrath` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 256 | `fillet-away` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 257 | `final-gambit` | damage | meta | user-HP-based damage plus self-faint is not implemented |
| 264 | `first-impression` | damage | meta | first-turn-only condition is not implemented |
| 265 | `fishious-rend` | damage | meta | pre-target-move power doubling is not implemented |
| 267 | `flail` | damage | meta | HP-ratio power is not implemented |
| 273 | `flash` | net-good-stats | meta | stat stage 'accuracy' is not implemented |
| 277 | `fling` | damage | meta | item-dependent power/effect and consumption are not implemented |
| 278 | `flip-turn` | damage | meta | after-hit user switch is not implemented |
| 280 | `floral-healing` | heal | meta | heal target 'selected-pokemon' is not supported as self-heal |
| 281 | `flower-shield` | unique | meta | target 'all-pokemon' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 283 | `fly` | damage | meta | semi-invulnerable charge turn is not implemented |
| 284 | `flying-press` | damage | meta | dual-type damage effectiveness is not implemented |
| 287 | `focus-punch` | damage | meta | fail-if-hit-before-use timing is not implemented |
| 288 | `follow-me` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 290 | `foresight` | ailment | meta | ailment 'no-type-immunity' is not implemented |
| 291 | `forests-curse` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 292 | `foul-play` | damage | meta | uses target Attack as the offensive stat, which is not implemented |
| 293 | `freeze-dry` | damage-ailment | meta | type-effectiveness exception is not implemented |
| 297 | `frenzy-plant` | damage | meta | recharge turn is not implemented |
| 299 | `frustration` | damage | meta | friendship-dependent power is not implemented |
| 301 | `fury-cutter` | damage | meta | consecutive-use power ramp is not implemented |
| 305 | `future-sight` | unique | meta | delayed damage is not implemented; unique behavior is not proven expressible by current generic ops |
| 306 | `gastro-acid` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 308 | `gear-up` | net-good-stats | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 310 | `geomancy` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 312 | `giga-impact` | damage | meta | recharge turn is not implemented |
| 313 | `gigaton-hammer` | no-meta | flavor_text fallback | repeat-use lockout is not implemented |
| 316 | `glacial-lance` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 317 | `glaciate` | damage-lower | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 318 | `glaive-rush` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 321 | `grass-knot` | damage | meta | weight-based power is not implemented |
| 325 | `grassy-terrain` | whole-field-effect | meta | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 327 | `gravity` | whole-field-effect | meta | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 328 | `growl` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 329 | `growth` | net-good-stats | meta | multi-stat/weather-scaled self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 330 | `grudge` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 332 | `guard-split` | unique | meta | stat averaging is not implemented; unique behavior is not proven expressible by current generic ops |
| 336 | `gust` | damage | meta | semi-invulnerable target exception/double damage is not implemented |
| 337 | `gyro-ball` | damage | meta | speed-ratio power is not implemented |
| 340 | `happy-hour` | unique | meta | target 'users-field' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 342 | `hard-press` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 346 | `headlong-rush` | no-meta | flavor_text fallback | multiple simultaneous stat changes cannot be represented exactly |
| 348 | `heal-bell` | unique | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 349 | `heal-block` | ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver; ailment 'heal-block' is not implemented |
| 350 | `healing-wish` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 352 | `heal-pulse` | heal | meta | heal target 'selected-pokemon' is not supported as self-heal |
| 355 | `heat-crash` | damage | meta | Minimize-dependent damage/hit exception is not implemented |
| 356 | `heat-wave` | damage-ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 357 | `heavy-slam` | damage | meta | Minimize-dependent damage/hit exception is not implemented |
| 358 | `helping-hand` | unique | meta | target 'ally' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 359 | `hex` | damage | meta | target-status-dependent power is not implemented |
| 360 | `hidden-power` | damage | meta | IV-dependent type/power is not implemented |
| 364 | `hold-hands` | unique | meta | target 'ally' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 365 | `hone-claws` | net-good-stats | meta | multi-stat self boosts including accuracy cannot be represented exactly; stat stage 'accuracy' is not implemented; multiple simultaneous stat changes cannot be represented exactly |
| 369 | `howl` | net-good-stats | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver |
| 370 | `hurricane` | damage-ailment | meta | weather-dependent accuracy and semi-invulnerable targeting exceptions are not implemented |
| 371 | `hydro-cannon` | damage | meta | recharge turn is not implemented |
| 373 | `hydro-steam` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 376 | `hyper-beam` | damage | meta | recharge turn is not implemented |
| 377 | `hyper-drill` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 381 | `hyper-voice` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 383 | `ice-ball` | damage | meta | locked multi-turn power ramp is not implemented |
| 390 | `ice-spinner` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 393 | `icy-wind` | damage-lower | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 394 | `imprison` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 395 | `incinerate` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 396 | `infernal-parade` | no-meta | flavor_text fallback | dynamic/conditional power or damage modifier is not implemented; conditional move effect is not implemented exactly by current move ops |
| 401 | `ingrain` | ailment | meta | ailment 'ingrain' is not implemented |
| 402 | `instruct` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 403 | `ion-deluge` | whole-field-effect | meta | normal-move type-changing field effect is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 408 | `jaw-lock` | damage | meta | mutual no-switch trapping volatile is not implemented |
| 409 | `jet-punch` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 410 | `judgment` | damage | meta | item-dependent type is not implemented |
| 412 | `jungle-healing` | unique | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 414 | `kinesis` | net-good-stats | meta | stat stage 'accuracy' is not implemented |
| 415 | `kings-shield` | unique | meta | protect variant with contact stat drop is not implemented; unique behavior is not proven expressible by current generic ops |
| 416 | `knock-off` | damage | meta | held item removal and conditional power are not implemented |
| 417 | `kowtow-cleave` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 418 | `lands-wrath` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 419 | `laser-focus` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 420 | `lash-out` | damage | meta | same-turn stat-drop power modifier is not implemented |
| 422 | `last-respects` | no-meta | flavor_text fallback | fainted-party-count power modifier is not implemented; multi-target/ally targeting is not implemented |
| 423 | `lava-plume` | damage-ailment | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 427 | `leaf-tornado` | damage-lower | meta | stat stage 'accuracy' is not implemented |
| 429 | `leech-seed` | ailment | meta | ailment 'leech-seed' is not implemented |
| 430 | `leer` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 433 | `life-dew` | heal | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; heal target 'user-and-allies' is not supported as self-heal |
| 435 | `light-screen` | field-effect | meta | screen side condition is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 436 | `light-that-burns-the-sky` | damage | meta | dynamic offensive stat/category choice is not implemented |
| 438 | `lock-on` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 440 | `low-kick` | damage | meta | weight-based power is not implemented |
| 442 | `lucky-chant` | field-effect | meta | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 443 | `lumina-crash` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 444 | `lunar-blessing` | no-meta | flavor_text fallback | target 'all-allies' is not implemented by the current singles battle UI/resolver |
| 445 | `lunar-dance` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 450 | `magical-torque` | no-meta | no English text | local JSON has no English effect text to audit exactly |
| 451 | `magic-coat` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 452 | `magic-powder` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 453 | `magic-room` | whole-field-effect | meta | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 456 | `magnetic-flux` | net-good-stats | meta | target 'user-and-allies' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 457 | `magnet-rise` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 458 | `magnitude` | damage | meta | random power table is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 459 | `make-it-rain` | no-meta | flavor_text fallback | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 462 | `mat-block` | field-effect | meta | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 464 | `max-airstream` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 465 | `max-darkness` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 466 | `max-flare` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 467 | `max-flutterby` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 468 | `max-geyser` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 469 | `max-guard` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 470 | `max-hailstorm` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 471 | `max-knuckle` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 472 | `max-lightning` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 473 | `max-mindstorm` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 474 | `max-ooze` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 475 | `max-overgrowth` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 476 | `max-phantasm` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 477 | `max-quake` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 478 | `max-rockfall` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 479 | `max-starfall` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 480 | `max-steelspike` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 481 | `max-strike` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 482 | `max-wyrmwind` | damage | meta | target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 483 | `mean-look` | unique | meta | no-switch trapping volatile is not implemented; unique behavior is not proven expressible by current generic ops |
| 485 | `me-first` | damage | meta | preemptive copied move is not implemented; target 'selected-pokemon-me-first' is not implemented by the current singles battle UI/resolver |
| 490 | `memento` | unique | meta | multiple simultaneous stat changes cannot be represented exactly; unique behavior is not proven expressible by current generic ops |
| 492 | `metal-burst` | damage | meta | counter-any-category timing is not implemented; target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 496 | `meteor-beam` | damage | meta | charge-turn stat boost is not implemented |
| 498 | `metronome` | unique | meta | move call/random move selection is not implemented; unique behavior is not proven expressible by current generic ops |
| 499 | `mighty-cleave` | no-meta | flavor_text fallback | protect contact side effects are not implemented as move effects |
| 501 | `mimic` | unique | meta | move copy/replace is not implemented; unique behavior is not proven expressible by current generic ops |
| 502 | `mind-blown` | damage | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 503 | `mind-reader` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 504 | `minimize` | net-good-stats | meta | stat stage 'evasion' is not implemented |
| 505 | `miracle-eye` | ailment | meta | ailment 'no-type-immunity' is not implemented |
| 506 | `mirror-coat` | damage | meta | target 'specific-move' is not implemented by the current singles battle UI/resolver |
| 507 | `mirror-move` | unique | meta | copy previous move is not implemented; unique behavior is not proven expressible by current generic ops |
| 508 | `mirror-shot` | damage-lower | meta | stat stage 'accuracy' is not implemented |
| 509 | `mist` | field-effect | meta | team stat-drop shield is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 511 | `misty-explosion` | damage | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 512 | `misty-terrain` | whole-field-effect | meta | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 515 | `moonlight` | heal | meta | weather-scaled healing is not implemented |
| 516 | `morning-sun` | heal | meta | weather-scaled healing is not implemented |
| 517 | `mortal-spin` | no-meta | flavor_text fallback | hazard/bind removal plus poison is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 518 | `mountain-gale` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 519 | `mud-bomb` | damage-lower | meta | stat stage 'accuracy' is not implemented |
| 520 | `muddy-water` | damage-lower | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver; stat stage 'accuracy' is not implemented |
| 522 | `mud-slap` | damage-lower | meta | stat stage 'accuracy' is not implemented |
| 523 | `mud-sport` | whole-field-effect | meta | field damage modifier is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 524 | `multi-attack` | damage | meta | item-dependent type is not implemented |
| 526 | `mystical-power` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 528 | `natural-gift` | damage | meta | berry-dependent type/power and consumption are not implemented |
| 529 | `nature-power` | unique | meta | field/terrain-dependent called move is not implemented; unique behavior is not proven expressible by current generic ops |
| 530 | `natures-madness` | damage | meta | fractional current-HP damage is not implemented |
| 534 | `night-daze` | damage-lower | meta | stat stage 'accuracy' is not implemented |
| 535 | `nightmare` | ailment | meta | ailment 'nightmare' is not implemented |
| 538 | `noble-roar` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 539 | `no-retreat` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 540 | `noxious-torque` | no-meta | no English text | local JSON has no English effect text to audit exactly |
| 543 | `obstruct` | unique | meta | protect variant with contact defense drop is not implemented; unique behavior is not proven expressible by current generic ops |
| 545 | `octazooka` | damage-lower | meta | stat stage 'accuracy' is not implemented |
| 546 | `octolock` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 547 | `odor-sleuth` | ailment | meta | ailment 'no-type-immunity' is not implemented |
| 549 | `order-up` | no-meta | flavor_text fallback | conditional move effect is not implemented exactly by current move ops |
| 550 | `origin-pulse` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 552 | `overdrive` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 554 | `pain-split` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 555 | `parabolic-charge` | damage-heal | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 556 | `parting-shot` | net-good-stats | meta | stat drop plus user switch is not implemented; multiple simultaneous stat changes cannot be represented exactly |
| 557 | `payback` | damage | meta | move-after-target power modifier is not implemented |
| 560 | `perish-song` | ailment | meta | perish count/faint timer is not implemented; target 'all-pokemon' is not implemented by the current singles battle UI/resolver; ailment 'perish-song' is not implemented |
| 561 | `petal-blizzard` | damage | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 563 | `phantom-force` | damage | meta | semi-invulnerable charge/protect break is not implemented |
| 564 | `photon-geyser` | damage | meta | dynamic offensive stat/category choice is not implemented |
| 572 | `poison-gas` | ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 574 | `poison-powder` | ailment | meta | powder/grass immunities are not fully implemented |
| 579 | `population-bomb` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 580 | `pounce` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 582 | `powder` | unique | meta | powder volatile/fire trigger is not implemented; unique behavior is not proven expressible by current generic ops |
| 583 | `powder-snow` | damage-ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 585 | `power-shift` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 586 | `power-split` | unique | meta | stat averaging is not implemented; unique behavior is not proven expressible by current generic ops |
| 588 | `power-trick` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 589 | `power-trip` | damage | meta | stat-stage-dependent power is not implemented |
| 592 | `precipice-blades` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 593 | `present` | damage | meta | random damage/heal is not implemented |
| 597 | `psyblade` | no-meta | flavor_text fallback | dynamic/conditional power or damage modifier is not implemented; conditional move effect is not implemented exactly by current move ops |
| 600 | `psychic-noise` | no-meta | flavor_text fallback | ability mutation/copy is not implemented |
| 601 | `psychic-terrain` | whole-field-effect | meta | terrain is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 604 | `psycho-shift` | unique | meta | status transfer is not implemented; unique behavior is not proven expressible by current generic ops |
| 606 | `psyshield-bash` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 609 | `psywave` | damage | meta | random level-scaled fixed damage is not implemented |
| 611 | `punishment` | damage | meta | target-stat-stage-dependent power is not implemented |
| 612 | `purify` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 613 | `pursuit` | damage | meta | switch-intercept damage is not implemented |
| 615 | `quash` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 617 | `quick-guard` | field-effect | meta | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 618 | `quiver-dance` | net-good-stats | meta | multi-stat self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 619 | `rage` | damage | meta | rage attack-boost-on-hit volatile is not implemented |
| 620 | `rage-fist` | no-meta | flavor_text fallback | damage-taken count power modifier is not implemented |
| 621 | `rage-powder` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 622 | `raging-bull` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 625 | `rapid-spin` | damage-raise | meta | hazard/bind removal plus speed boost is not implemented |
| 626 | `razor-leaf` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 628 | `razor-wind` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 630 | `recycle` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 631 | `reflect` | field-effect | meta | screen side condition is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 632 | `reflect-type` | unique | meta | copy target type is not implemented; unique behavior is not proven expressible by current generic ops |
| 633 | `refresh` | unique | meta | self status cure is not implemented as a move effect; unique behavior is not proven expressible by current generic ops |
| 634 | `relic-song` | damage-ailment | meta | damage plus sleep plus species/form-specific transform is not implemented exactly; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 635 | `rest` | unique | meta | self sleep plus full heal is not implemented as one exact move; unique behavior is not proven expressible by current generic ops |
| 636 | `retaliate` | damage | meta | previous-turn ally faint power modifier is not implemented |
| 637 | `return` | damage | meta | friendship-dependent power is not implemented |
| 638 | `revelation-dance` | damage | meta | user-primary-type move typing is not implemented |
| 639 | `revenge` | damage | meta | move-after-hit power modifier is not implemented |
| 640 | `reversal` | damage | meta | HP-ratio power is not implemented |
| 641 | `revival-blessing` | no-meta | flavor_text fallback | target 'fainting-pokemon' is not implemented by the current singles battle UI/resolver |
| 648 | `rock-slide` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 653 | `role-play` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 655 | `rollout` | damage | meta | locked multi-turn power ramp is not implemented |
| 656 | `roost` | heal | meta | temporary type removal plus heal is not implemented |
| 657 | `rototiller` | net-good-stats | meta | target 'all-pokemon' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 658 | `round` | damage | meta | same-turn ally move power/order behavior is not implemented |
| 659 | `ruination` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 662 | `safeguard` | field-effect | meta | team status shield is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 663 | `salt-cure` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 664 | `sand-attack` | net-good-stats | meta | stat stage 'accuracy' is not implemented |
| 665 | `sandsear-storm` | no-meta | flavor_text fallback | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 668 | `sappy-seed` | damage-ailment | meta | ailment 'leech-seed' is not implemented |
| 677 | `searing-shot` | damage-ailment | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 679 | `secret-power` | damage | meta | field-dependent secondary effect is not implemented |
| 684 | `self-destruct` | damage | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 687 | `shadow-blitz` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 690 | `shadow-break` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 693 | `shadow-down` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 694 | `shadow-end` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 696 | `shadow-force` | damage | meta | semi-invulnerable charge/protect break is not implemented |
| 697 | `shadow-half` | no-meta | effect_entries | target 'entire-field' is not implemented by the current singles battle UI/resolver |
| 698 | `shadow-hold` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 699 | `shadow-mist` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 702 | `shadow-rave` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 704 | `shadow-shed` | no-meta | effect_entries | target 'entire-field' is not implemented by the current singles battle UI/resolver |
| 705 | `shadow-sky` | no-meta | effect_entries | target 'entire-field' is not implemented by the current singles battle UI/resolver |
| 707 | `shadow-storm` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 708 | `shadow-wave` | no-meta | effect_entries | no-meta text does not prove exact expressibility with current ops |
| 712 | `shed-tail` | no-meta | flavor_text fallback | after-move user switch is not implemented |
| 714 | `shell-side-arm` | damage-ailment | meta | dynamic physical/special category choice is not implemented |
| 715 | `shell-smash` | unique | meta | multi-stat self changes cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly; unique behavior is not proven expressible by current generic ops |
| 716 | `shell-trap` | damage | meta | conditional trap-on-contact timing is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 717 | `shelter` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 718 | `shift-gear` | net-good-stats | meta | multi-stat self boosts cannot be represented exactly; multiple simultaneous stat changes cannot be represented exactly |
| 720 | `shore-up` | heal | meta | weather-scaled healing is not implemented |
| 722 | `silk-trap` | no-meta | flavor_text fallback | protect variant with contact speed drop is not implemented |
| 724 | `simple-beam` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 728 | `sketch` | unique | meta | permanent move copy is not implemented; unique behavior is not proven expressible by current generic ops |
| 729 | `skill-swap` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 731 | `skull-bash` | damage | meta | charge-turn defense boost is not implemented |
| 738 | `sleep-powder` | ailment | meta | powder/grass immunities are not fully implemented |
| 739 | `sleep-talk` | unique | meta | move call while asleep is not implemented; unique behavior is not proven expressible by current generic ops |
| 742 | `sludge-wave` | damage-ailment | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 743 | `smack-down` | damage | meta | ailment 'unknown' is not implemented |
| 745 | `smelling-salts` | damage | meta | paralysis-dependent power and cure are not implemented |
| 747 | `smokescreen` | net-good-stats | meta | stat stage 'accuracy' is not implemented |
| 749 | `snarl` | damage-lower | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 750 | `snatch` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 752 | `snore` | damage | meta | user-must-be-asleep gate is not implemented |
| 753 | `snowscape` | no-meta | flavor_text fallback | target 'entire-field' is not implemented by the current singles battle UI/resolver; snow weather is not implemented; engine has rain/sun/sandstorm/hail |
| 754 | `soak` | unique | meta | target type changing is not implemented; unique behavior is not proven expressible by current generic ops |
| 756 | `solar-beam` | damage | meta | weather-sensitive charge skip/power interaction is not implemented |
| 757 | `solar-blade` | damage | meta | weather-sensitive charge skip/power interaction is not implemented |
| 762 | `sparkling-aria` | damage | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 765 | `speed-swap` | unique | meta | speed stat swap is not implemented; unique behavior is not proven expressible by current generic ops |
| 766 | `spicy-extract` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 767 | `spider-web` | unique | meta | no-switch trapping volatile is not implemented; unique behavior is not proven expressible by current generic ops |
| 770 | `spiky-shield` | unique | meta | protect variant with contact damage is not implemented; unique behavior is not proven expressible by current generic ops |
| 771 | `spin-out` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 774 | `spite` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 775 | `spit-up` | damage | meta | stockpile-dependent power is not implemented |
| 778 | `splishy-splash` | damage-ailment | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 779 | `spore` | ailment | meta | powder/grass immunities are not fully implemented |
| 780 | `spotlight` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 781 | `springtide-storm` | no-meta | flavor_text fallback | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 784 | `steamroller` | damage | meta | Minimize-dependent damage/hit exception is not implemented |
| 786 | `steel-roller` | damage | meta | terrain requirement/removal is not implemented |
| 788 | `sticky-web` | field-effect | meta | sticky web hazard is not implemented; field/side condition is not implemented by current move ops |
| 789 | `stockpile` | unique | meta | multiple simultaneous stat changes cannot be represented exactly; unique behavior is not proven expressible by current generic ops |
| 791 | `stomp` | damage | meta | Minimize-dependent damage/hit exception is not implemented |
| 792 | `stomping-tantrum` | damage | meta | previous-move-failed power modifier is not implemented |
| 793 | `stone-axe` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 795 | `stored-power` | damage | meta | stat-stage-dependent power is not implemented |
| 799 | `strength-sap` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 800 | `string-shot` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 802 | `struggle-bug` | damage-lower | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 804 | `stun-spore` | ailment | meta | powder/grass immunities are not fully implemented |
| 806 | `substitute` | unique | meta | substitute decoy state is not implemented; unique behavior is not proven expressible by current generic ops |
| 809 | `sucker-punch` | damage | meta | target-selected-damaging-move condition is not implemented |
| 812 | `supercell-slam` | no-meta | flavor_text fallback | crash recoil maps to existing `recoil` with `onMiss: true`; local no-meta text lacks exact crash amount needed to author exactly |
| 813 | `super-fang` | damage | meta | fractional current-HP damage is not implemented |
| 814 | `superpower` | damage-raise | meta | multiple simultaneous stat changes cannot be represented exactly |
| 818 | `surf` | damage | meta | semi-invulnerable target exception/double damage is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 823 | `sweet-scent` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver; stat stage 'evasion' is not implemented |
| 824 | `swift` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 825 | `switcheroo` | unique | meta | held item swapping is not implemented; unique behavior is not proven expressible by current generic ops |
| 827 | `synchronoise` | damage | meta | shared-type target gate is not implemented; target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 828 | `synthesis` | heal | meta | weather-scaled healing is not implemented |
| 829 | `syrup-bomb` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 830 | `tachyon-cutter` | no-meta | flavor_text fallback | no-meta text does not prove exact expressibility with current ops |
| 834 | `tail-whip` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 835 | `tailwind` | field-effect | meta | side speed field is not implemented; target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 837 | `take-heart` | no-meta | flavor_text fallback | target 'all-allies' is not implemented by the current singles battle UI/resolver |
| 838 | `tar-shot` | swagger | meta | ailment 'tar-shot' is not implemented |
| 839 | `taunt` | unique | meta | status-move lockout is not implemented; unique behavior is not proven expressible by current generic ops |
| 840 | `tearful-look` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 841 | `teatime` | unique | meta | target 'all-pokemon' is not implemented by the current singles battle UI/resolver; unique behavior is not proven expressible by current generic ops |
| 842 | `techno-blast` | damage | meta | item-dependent type is not implemented |
| 845 | `teeter-dance` | ailment | meta | target 'all-other-pokemon' is not implemented by the current singles battle UI/resolver |
| 846 | `telekinesis` | ailment | meta | ailment 'unknown' is not implemented |
| 847 | `teleport` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 848 | `temper-flare` | no-meta | flavor_text fallback | dynamic/conditional power or damage modifier is not implemented |
| 849 | `tera-blast` | no-meta | flavor_text fallback | terastallization-dependent type/category is not implemented; conditional move effect is not implemented exactly by current move ops |
| 850 | `tera-starstorm` | no-meta | flavor_text fallback | target 'all-opponents' is not implemented by the current singles battle UI/resolver; multi-target/ally targeting is not implemented |
| 851 | `terrain-pulse` | damage | meta | terrain-dependent type/power is not implemented |
| 852 | `thief` | damage | meta | held item stealing is not implemented |
| 853 | `thousand-arrows` | damage | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver; ailment 'unknown' is not implemented |
| 854 | `thousand-waves` | damage | meta | damage plus no-switch trapping volatile is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 856 | `throat-chop` | damage-ailment | meta | ailment 'silence' is not implemented |
| 857 | `thunder` | damage-ailment | meta | weather-dependent accuracy and semi-invulnerable targeting exceptions are not implemented |
| 860 | `thunderclap` | no-meta | flavor_text fallback | target-selected-attack condition is not implemented |
| 866 | `tickle` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 867 | `tidy-up` | no-meta | flavor_text fallback | substitute/hazard cleanup plus stat boosts is not implemented |
| 869 | `torch-song` | no-meta | flavor_text fallback | damage plus self Sp. Atk boost must be authored from no-meta text and was not proven by metadata |
| 870 | `torment` | ailment | meta | repeat-move lockout is not implemented; ailment 'torment' is not implemented |
| 872 | `toxic-spikes` | field-effect | meta | toxic spikes hazard is not implemented; field/side condition is not implemented by current move ops |
| 875 | `transform` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 876 | `tri-attack` | damage-ailment | meta | ailment 'unknown' is not implemented |
| 877 | `trick` | unique | meta | held item swapping is not implemented; unique behavior is not proven expressible by current generic ops |
| 878 | `trick-or-treat` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 879 | `trick-room` | whole-field-effect | meta | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 882 | `triple-dive` | no-meta | flavor_text fallback | fixed three-hit no-meta behavior is not proven by metadata/effect text |
| 885 | `trump-card` | damage | meta | PP-dependent power is not implemented |
| 886 | `twin-beam` | no-meta | flavor_text fallback | fixed two-hit no-meta behavior is not proven by metadata/effect text |
| 890 | `twister` | damage | meta | semi-invulnerable target exception/double damage is not implemented; target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 891 | `upper-hand` | no-meta | flavor_text fallback | target-priority-move condition is not implemented |
| 893 | `u-turn` | damage | meta | after-hit user switch is not implemented |
| 895 | `v-create` | damage-raise | meta | multiple simultaneous stat changes cannot be represented exactly |
| 897 | `venom-drench` | net-good-stats | meta | target 'all-opponents' is not implemented by the current singles battle UI/resolver; multiple simultaneous stat changes cannot be represented exactly |
| 898 | `venoshock` | damage | meta | target-status-dependent power is not implemented |
| 900 | `victory-dance` | no-meta | flavor_text fallback | multi-stat self boosts cannot be represented exactly |
| 903 | `volt-switch` | damage | meta | after-hit user switch is not implemented |
| 905 | `wake-up-slap` | damage | meta | sleep-dependent power and wake-up are not implemented |
| 911 | `water-sport` | whole-field-effect | meta | field damage modifier is not implemented; target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 913 | `wave-crash` | no-meta | flavor_text fallback | local no-meta text lacks exact recoil/crash amount needed to author exactly |
| 914 | `weather-ball` | damage | meta | weather-dependent type/power is not implemented |
| 915 | `whirlpool` | damage-ailment | meta | semi-invulnerable target exception/double damage is not implemented |
| 918 | `wicked-torque` | no-meta | no English text | local JSON has no English effect text to audit exactly |
| 919 | `wide-guard` | field-effect | meta | target 'users-field' is not implemented by the current singles battle UI/resolver; field/side condition is not implemented by current move ops |
| 920 | `wildbolt-storm` | no-meta | flavor_text fallback | target 'all-opponents' is not implemented by the current singles battle UI/resolver |
| 924 | `wish` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 926 | `wonder-room` | whole-field-effect | meta | target 'entire-field' is not implemented by the current singles battle UI/resolver; whole-field effect is not implemented by current move ops |
| 928 | `work-up` | net-good-stats | meta | multiple simultaneous stat changes cannot be represented exactly |
| 929 | `worry-seed` | unique | meta | unique behavior is not proven expressible by current generic ops |
| 933 | `yawn` | ailment | meta | delayed sleep is not implemented; ailment 'yawn' is not implemented |
| 937 | `zippy-zap` | damage-raise | meta | stat stage 'evasion' is not implemented |

## Historical Failure Groups By Required Engine Functionality

This section preserves the reusable-group inventory generated from an earlier 505-failure baseline.
The row table above is newer and currently contains 469 FAIL entries. A move can appear in multiple
groups, and moves that later became legacy PASS remain useful evidence that the generic capability is
required. These grouped counts are historical planning input only; they are not current FAIL counts
and never change strict manifest certification status.

| Metric | Count |
|---|---:|
| Historical failed moves represented | 505 |
| Historical total failed moves | 505 |
| Engine work groups | 20 |
| Category memberships | 692 |
| Unrepresented failed moves | 0 |

### Polish Pass: Minimal Reusable Primitive Inventory

This pass keeps the failed move list exact, but narrows the implementation target. The engine should not grow one handler per move, one class per move, or special-case branches named after specific moves. Every failed move should become expressible by composing a small set of reusable effect ops, query hooks, scoped conditions, and state mutation helpers.

| Minimal primitive | Covers these groups | Implementation guardrail |
|---|---|---|
| `targetSelector` / legal target resolver | Target Selection And Battle Topology; Redirection And Turn Order Manipulation; Healing, Cures, HP Costs, And Fractional HP Effects; Field And Side Conditions | Add target resolution data and validation before building doubles UI or presentation. Do not make move-specific target code. |
| Scoped `ConditionDef` model | Volatile Conditions, Statuses, And Move Lockouts; Field And Side Conditions; Protect Variants With Contact Punish; Turn Timing, Queued Effects, And Move Gates; Accuracy Locks And Special Accuracy Exceptions | Reuse one condition/timing framework for creature, side, field, and move-lock state. Do not create one condition class per named move. |
| Battle query hooks | Damage Query Modifiers; Accuracy Locks And Special Accuracy Exceptions; Creature And Move Type Mutation; Field And Side Conditions | Extend damage, accuracy, type, priority, and effectiveness queries with composable modifiers. Do not fork damage calculation by move. |
| Ordered effect list | Stat Stage Model Expansion; Protect Variants With Contact Punish; Healing, Cures, HP Costs, And Fractional HP Effects; Hazard, Screen, Substitute, And Side Cleanup | Replace single-purpose effect payloads with ordered generic ops only where the move data needs multiple effects. Do not build a scripting language. |
| Battle state mutation helpers | Held Item And Berry Mutation; Ability Mutation; Creature And Move Type Mutation; Hazard, Screen, Substitute, And Side Cleanup; Healing, Cures, HP Costs, And Fractional HP Effects | Keep mutations in Cgm.Core resolvers/controllers. Runtime renders snapshots and submits actions only. |
| Queued intent / timer system | Turn Timing, Queued Effects, And Move Gates; Switch Flow And State Passing; Counter, Revenge, And Stored Damage; Move Copy, Call, Replace, And Forced Execution | Use one queue/timer path for delayed attacks, recharge, forced execution, revenge counters, and switch-linked state. Do not add ad-hoc per-move flags. |
| Snapshot / overlay layer | Substitute, Transform, And Creature Snapshot Effects; Creature And Move Type Mutation; Move Copy, Call, Replace, And Forced Execution | Model temporary copied forms, types, moves, stats, and shields as overlays. Do not permanently rewrite species or move definitions during battle. |
| Reference data enrichment | Reference Data Gaps | Fill missing structured metadata from the `effect` text before code work. Do not guess exact behavior from flavor text or import official data into samples/exports. |

### Build Order / YAGNI Notes

| Order | Work | Reason |
|---:|---|---|
| 1 | Reference data enrichment for no-meta failures | Prevents coding from vague text and may move some failures into existing ops. |
| 2 | `targetSelector` plus ordered effect list | These are small, generic, and unblock many simple multi-target/multi-effect moves. |
| 3 | Scoped `ConditionDef` model plus battle query hooks | This is the main reusable engine core for volatile, field, damage, accuracy, and timing behavior. |
| 4 | State mutation helpers and queued intent/timers | Add only as required by the grouped failed move list; keep mutations deterministic in Core. |
| 5 | Snapshot/overlay layer | Needed for transform/substitute-style mechanics, but should wait until simpler primitives are proven. |

Per-move implementation rule: every failed move row must be implemented by composing primitives from this section and the group below. If a failed move cannot be expressed that way, update `docs/BATTLE_SYSTEM_SPEC.md` with a new generic effect op first; do not add bespoke move code.

### Engine Work Summary

| Engine work group | Failed moves needing it |
|---|---:|
| Target Selection And Battle Topology | 144 |
| Damage Query Modifiers | 64 |
| Turn Timing, Queued Effects, And Move Gates | 64 |
| Volatile Conditions, Statuses, And Move Lockouts | 49 |
| Stat Stage Model Expansion | 63 |
| Field And Side Conditions | 53 |
| Held Item And Berry Mutation | 19 |
| Ability Mutation | 8 |
| Creature And Move Type Mutation | 16 |
| Move Copy, Call, Replace, And Forced Execution | 13 |
| Switch Flow And State Passing | 8 |
| Hazard, Screen, Substitute, And Side Cleanup | 5 |
| Protect Variants With Contact Punish | 8 |
| Healing, Cures, HP Costs, And Fractional HP Effects | 29 |
| Redirection And Turn Order Manipulation | 11 |
| Accuracy Locks And Special Accuracy Exceptions | 36 |
| Substitute, Transform, And Creature Snapshot Effects | 6 |
| Counter, Revenge, And Stored Damage | 8 |
| Non-Battle Or Post-Battle Effects | 4 |
| Reference Data Gaps | 46 |

### Target Selection And Battle Topology (144 moves)

- Add: A reusable target resolver for all-opponents, all-other-creatures, ally, user-or-ally, side fields, entire field, and specific previous-move targets.
- Reusable primitive/helper: targetSelector + resolved target set passed into each effect op.
- Moves: `acupressure`, `aromatherapy`, `aromatic-mist`, `aurora-veil`, `bleakwind-storm`, `blizzard`, `burning-jealousy`, `captivate`, `chilly-reception`, `clanging-scales`, `clangorous-soulblaze`, `coaching`, `comeuppance`, `core-enforcer`, `corrosive-gas`, `cotton-spore`, `counter`, `court-change`, `crafty-shield`, `curse`, `dark-void`, `diamond-storm`, `discharge`, `dragon-cheer`, `dragon-energy`, `earthquake`, `electric-terrain`, `electroweb`, `explosion`, `fairy-lock`, `fiery-wrath`, `flower-shield`, `gear-up`, `glacial-lance`, `glaciate`, `grassy-terrain`, `gravity`, `growl`, `happy-hour`, `haze`, `heal-bell`, `heal-block`, `heat-wave`, `helping-hand`, `hold-hands`, `howl`, `hyper-voice`, `icy-wind`, `incinerate`, `ion-deluge`, `jungle-healing`, `lands-wrath`, `last-respects`, `lava-plume`, `leer`, `life-dew`, `light-screen`, `lucky-chant`, `lunar-blessing`, `magic-room`, `magnetic-flux`, `magnitude`, `make-it-rain`, `mat-block`, `max-airstream`, `max-darkness`, `max-flare`, `max-flutterby`, `max-geyser`, `max-hailstorm`, `max-knuckle`, `max-lightning`, `max-mindstorm`, `max-ooze`, `max-overgrowth`, `max-phantasm`, `max-quake`, `max-rockfall`, `max-starfall`, `max-steelspike`, `max-strike`, `max-wyrmwind`, `me-first`, `metal-burst`, `mind-blown`, `mirror-coat`, `mist`, `misty-explosion`, `misty-terrain`, `mortal-spin`, `muddy-water`, `mud-sport`, `origin-pulse`, `overdrive`, `parabolic-charge`, `perish-song`, `petal-blizzard`, `poison-gas`, `powder-snow`, `precipice-blades`, `psychic-terrain`, `quick-guard`, `razor-leaf`, `razor-wind`, `reflect`, `relic-song`, `revival-blessing`, `rock-slide`, `rototiller`, `safeguard`, `sandsear-storm`, `searing-shot`, `self-destruct`, `shadow-half`, `shadow-shed`, `shadow-sky`, `shell-trap`, `sludge-wave`, `snarl`, `snowscape`, `sparkling-aria`, `splishy-splash`, `springtide-storm`, `string-shot`, `struggle-bug`, `surf`, `sweet-scent`, `swift`, `synchronoise`, `tail-whip`, `tailwind`, `take-heart`, `teatime`, `teeter-dance`, `tera-starstorm`, `thousand-arrows`, `thousand-waves`, `trick-room`, `twister`, `venom-drench`, `water-sport`, `wide-guard`, `wildbolt-storm`, `wonder-room`

### Damage Query Modifiers (64 moves)

- Add: Base-power, damage-stat, move-type, category, effectiveness, and final-damage modifier hooks for conditional damage formulas.
- Reusable primitive/helper: basePowerQuery, damageStatQuery, moveTypeQuery, effectivenessQuery, damageModifierQuery.
- Moves: `acrobatics`, `assurance`, `avalanche`, `barb-barrage`, `beat-up`, `bolt-beak`, `camouflage`, `collision-course`, `defense-curl`, `echoed-voice`, `electro-ball`, `electro-drift`, `expanding-force`, `facade`, `fishious-rend`, `flail`, `fling`, `flying-press`, `foul-play`, `freeze-dry`, `frustration`, `fury-cutter`, `grass-knot`, `gyro-ball`, `hex`, `hidden-power`, `ice-ball`, `infernal-parade`, `knock-off`, `lash-out`, `last-respects`, `light-that-burns-the-sky`, `low-kick`, `magnitude`, `mud-sport`, `natural-gift`, `payback`, `photon-geyser`, `power-trip`, `present`, `psyblade`, `psywave`, `punishment`, `rage-fist`, `retaliate`, `return`, `revenge`, `reversal`, `rollout`, `round`, `shell-side-arm`, `smelling-salts`, `solar-beam`, `solar-blade`, `spit-up`, `stomping-tantrum`, `stored-power`, `temper-flare`, `terrain-pulse`, `trump-card`, `venoshock`, `wake-up-slap`, `water-sport`, `weather-ball`

### Turn Timing, Queued Effects, And Move Gates (64 moves)

- Add: Recharge, delayed damage, charge-turn variants, first-turn checks, previous-turn checks, target-action checks, cannot-repeat locks, and miss-conditional effects.
- Reusable primitive/helper: timed/queued conditions with onBeforeMove, onAfterMove, onTurnEnd, and future-hit resolution.
- Moves: `alluring-voice`, `aurora-veil`, `barb-barrage`, `beak-blast`, `bide`, `blast-burn`, `blood-moon`, `bolt-beak`, `bounce`, `crafty-shield`, `destiny-bond`, `dig`, `dive`, `doom-desire`, `earthquake`, `electro-shot`, `endure`, `fake-out`, `first-impression`, `fishious-rend`, `fly`, `focus-punch`, `frenzy-plant`, `future-sight`, `giga-impact`, `gigaton-hammer`, `gust`, `hurricane`, `hydro-cannon`, `hyper-beam`, `infernal-parade`, `light-screen`, `lucky-chant`, `magic-coat`, `mat-block`, `metal-burst`, `meteor-beam`, `mirror-move`, `mist`, `order-up`, `phantom-force`, `psyblade`, `quick-guard`, `reflect`, `retaliate`, `safeguard`, `shadow-force`, `shell-trap`, `skull-bash`, `snore`, `sticky-web`, `stomping-tantrum`, `sucker-punch`, `surf`, `tailwind`, `tera-blast`, `thunder`, `thunderclap`, `toxic-spikes`, `twister`, `upper-hand`, `whirlpool`, `wide-guard`, `yawn`

### Volatile Conditions, Statuses, And Move Lockouts (49 moves)

- Add: Infatuation, disable, encore, taunt, torment, yawn, perish, nightmare, curse, no-switch traps, throat/silence, powder, and similar battle conditions.
- Reusable primitive/helper: ConditionDef records with duration, switch-clear behavior, before-move hooks, end-turn hooks, and status-attempt hooks.
- Moves: `anchor-shot`, `aqua-ring`, `attract`, `baton-pass`, `block`, `blood-moon`, `charge`, `destiny-bond`, `dire-claw`, `disable`, `embargo`, `encore`, `foresight`, `gigaton-hammer`, `grudge`, `heal-block`, `imprison`, `ingrain`, `jaw-lock`, `leech-seed`, `magic-coat`, `mean-look`, `miracle-eye`, `nightmare`, `octolock`, `odor-sleuth`, `perish-song`, `poison-powder`, `powder`, `rage`, `sappy-seed`, `sleep-powder`, `smack-down`, `snore`, `spider-web`, `spite`, `spore`, `stun-spore`, `substitute`, `tar-shot`, `taunt`, `telekinesis`, `thousand-arrows`, `thousand-waves`, `throat-chop`, `topsy-turvy`, `torment`, `tri-attack`, `yawn`

### Stat Stage Model Expansion (63 moves)

- Add: stage averaging, stat passing, stockpile-like counters, and remaining move-specific topology or side-condition gaps.
- Reusable primitive/helper: stat stage helper operations for average and pass.
- Implementation note: ordered multiple `statStage` ops, accuracy/evasion stage changes, `statStageAll`, reset/copy/swap/invert helpers, and HP-cost setup moves now compile and resolve through the existing `SecondaryEffects` dispatcher; remaining blockers in this group are stage averaging, Baton Pass-style stage transfer, stockpile-like counters, and move-specific topology or side-condition gaps.
- Moves: `armor-cannon`, `baton-pass`, `bulk-up`, `calm-mind`, `clangorous-soulblaze`, `close-combat`, `coaching`, `coil`, `cosmic-power`, `decorate`, `defend-order`, `defog`, `double-team`, `dragon-ascent`, `dragon-dance`, `flash`, `gear-up`, `geomancy`, `growth`, `guard-split`, `headlong-rush`, `hone-claws`, `kinesis`, `leaf-tornado`, `magnetic-flux`, `memento`, `minimize`, `mirror-shot`, `mud-bomb`, `muddy-water`, `mud-slap`, `night-daze`, `noble-roar`, `no-retreat`, `octazooka`, `octolock`, `parting-shot`, `power-split`, `power-trip`, `punishment`, `quiver-dance`, `rototiller`, `sand-attack`, `shell-smash`, `shift-gear`, `smokescreen`, `speed-swap`, `stockpile`, `stored-power`, `superpower`, `sweet-scent`, `tearful-look`, `tickle`, `tidy-up`, `v-create`, `venom-drench`, `victory-dance`, `work-up`, `zippy-zap`

### Field And Side Conditions (53 moves)

- Add: Screens, safeguard/mist, terrains, rooms, gravity, tailwind, snow, field damage modifiers, weather accuracy changes, and weather-scaled healing.
- Reusable primitive/helper: field/side scoped ConditionDef records with hooks on damage, accuracy, status, healing, and turn order.
- Moves: `aurora-veil`, `blizzard`, `body-slam`, `camouflage`, `chilly-reception`, `court-change`, `crafty-shield`, `defog`, `echoed-voice`, `electric-terrain`, `expanding-force`, `fairy-lock`, `grassy-terrain`, `gravity`, `growth`, `happy-hour`, `haze`, `hurricane`, `ion-deluge`, `light-screen`, `lucky-chant`, `magic-room`, `mat-block`, `mist`, `misty-terrain`, `moonlight`, `morning-sun`, `mud-sport`, `nature-power`, `psychic-terrain`, `quick-guard`, `reflect`, `safeguard`, `secret-power`, `shadow-half`, `shadow-shed`, `shadow-sky`, `shore-up`, `snowscape`, `solar-beam`, `solar-blade`, `steel-roller`, `sticky-web`, `synthesis`, `tailwind`, `terrain-pulse`, `thunder`, `toxic-spikes`, `trick-room`, `water-sport`, `weather-ball`, `wide-guard`, `wonder-room`

### Held Item And Berry Mutation (19 moves)

- Add: Inspect, consume, steal, swap, remove, destroy, restore, or depend on held items/berries.
- Reusable primitive/helper: heldItem query/mutation helpers on battle state.
- Moves: `acrobatics`, `belch`, `bestow`, `corrosive-gas`, `covet`, `embargo`, `fling`, `incinerate`, `judgment`, `knock-off`, `magic-room`, `multi-attack`, `natural-gift`, `recycle`, `switcheroo`, `teatime`, `techno-blast`, `thief`, `trick`

### Ability Mutation (8 moves)

- Add: Copy, swap, replace, suppress, or query abilities during battle.
- Reusable primitive/helper: ability override/suppression state layered on the Phase 15 hook dispatcher.
- Moves: `doodle`, `entrainment`, `gastro-acid`, `psychic-noise`, `role-play`, `simple-beam`, `skill-swap`, `worry-seed`

### Creature And Move Type Mutation (16 moves)

- Add: Change, add, remove, copy, or temporarily override creature types or a move type.
- Reusable primitive/helper: battle type override layer for creatures plus moveTypeQuery for moves.
- Moves: `burn-up`, `camouflage`, `conversion`, `conversion-2`, `double-shock`, `electrify`, `forests-curse`, `ion-deluge`, `magic-powder`, `reflect-type`, `revelation-dance`, `roost`, `soak`, `tera-blast`, `tera-starstorm`, `trick-or-treat`

### Move Copy, Call, Replace, And Forced Execution (13 moves)

- Add: Random move calls, last/previous/target/party move copy, permanent move replacement, and forced repeat execution.
- Reusable primitive/helper: moveReference selector + executeResolvedMove path that preserves PP/log ownership.
- Moves: `assist`, `copycat`, `instruct`, `me-first`, `metronome`, `mimic`, `mirror-move`, `nature-power`, `psych-up`, `reflect-type`, `sketch`, `sleep-talk`, `snatch`

### Switch Flow And State Passing (8 moves)

- Add: After-hit user switch, weather-plus-switch, escape/switch variants, and Baton Pass-style state transfer.
- Reusable primitive/helper: postMoveSwitch intent plus explicit passable-state list.
- Moves: `baton-pass`, `chilly-reception`, `flip-turn`, `parting-shot`, `shed-tail`, `teleport`, `u-turn`, `volt-switch`

### Hazard, Screen, Substitute, And Side Cleanup (5 moves)

- Add: Remove hazards, screens, bind/trap state, substitute state, and swap side conditions.
- Reusable primitive/helper: sideCondition operations: removeByTag, clearHazards, clearScreens, clearVolatilesByTag, swapSides.
- Moves: `court-change`, `defog`, `mortal-spin`, `rapid-spin`, `tidy-up`

### Protect Variants With Contact Punish (8 moves)

- Add: Protect-family moves that block and then punish contact with damage, poison, burn, or stat drops.
- Reusable primitive/helper: protect condition with onContactBlocked effect list.
- Moves: `baneful-bunker`, `burning-bulwark`, `kings-shield`, `max-guard`, `mighty-cleave`, `obstruct`, `silk-trap`, `spiky-shield`

### Healing, Cures, HP Costs, And Fractional HP Effects (29 moves)

- Add: Target/ally/team healing, party-wide status cure, self sleep plus full heal, status transfer/cure, HP costs, pain-split/fractional HP effects, and revival.
- Reusable primitive/helper: heal/cure/hpCost/fractionalHp ops using the shared target selector.
- Moves: `aromatherapy`, `dream-eater`, `endeavor`, `floral-healing`, `heal-bell`, `heal-block`, `healing-wish`, `heal-pulse`, `jungle-healing`, `life-dew`, `lunar-blessing`, `lunar-dance`, `moonlight`, `morning-sun`, `natures-madness`, `pain-split`, `present`, `psycho-shift`, `purify`, `refresh`, `rest`, `revival-blessing`, `roost`, `shore-up`, `smelling-salts`, `strength-sap`, `super-fang`, `synthesis`, `wish`

### Redirection And Turn Order Manipulation (11 moves)

- Add: Follow Me/Rage Powder-style redirection, Helping Hand-style ally boost, After You/Quash/Instruct ordering, and ally position swaps.
- Reusable primitive/helper: turn-order and target-redirection volatiles resolved before action execution.
- Moves: `after-you`, `ally-switch`, `coaching`, `decorate`, `follow-me`, `helping-hand`, `hold-hands`, `instruct`, `quash`, `rage-powder`, `spotlight`

### Accuracy Locks And Special Accuracy Exceptions (36 moves)

- Add: Lock-On/Mind Reader next-hit guarantees, Laser Focus crit guarantee, identify/ignore-evasion effects, Minimize exceptions, weather accuracy exceptions, and semi-invulnerable hit exceptions.
- Reusable primitive/helper: accuracyQuery + critQuery hooks and target-state exception checks.
- Moves: `blizzard`, `body-slam`, `coil`, `dragon-rush`, `earthquake`, `flash`, `foresight`, `gust`, `heat-crash`, `heavy-slam`, `hone-claws`, `hurricane`, `kinesis`, `laser-focus`, `leaf-tornado`, `lock-on`, `magnet-rise`, `mind-reader`, `miracle-eye`, `mirror-shot`, `mud-bomb`, `muddy-water`, `mud-slap`, `night-daze`, `octazooka`, `odor-sleuth`, `sand-attack`, `smack-down`, `smokescreen`, `steamroller`, `stomp`, `surf`, `telekinesis`, `thunder`, `twister`, `whirlpool`

### Substitute, Transform, And Creature Snapshot Effects (6 moves)

- Add: Substitute decoys, Shed Tail transfer, Transform copying, Power Trick stat swapping, and form/species-like battle snapshots.
- Reusable primitive/helper: battle snapshot overlays for HP decoys, copied stats/types/moves, and temporary stat swaps.
- Moves: `power-trick`, `relic-song`, `shed-tail`, `substitute`, `tidy-up`, `transform`

### Counter, Revenge, And Stored Damage (8 moves)

- Add: Counter-any-category, retaliation against last damage source, stored/released damage, switch interception, and damage based on taken hits.
- Reusable primitive/helper: damage memory by side/category/source plus reaction damage ops.
- Moves: `bide`, `comeuppance`, `counter`, `final-gambit`, `metal-burst`, `mirror-coat`, `pursuit`, `rage-fist`

### Non-Battle Or Post-Battle Effects (4 moves)

- Add: Moves whose visible effect is reward, celebration, money, or no battle-state change.
- Reusable primitive/helper: explicit noBattleEffect/postBattleReward ops so these are intentional data, not missing mechanics.
- Implementation note: `noBattleEffect` and `postBattleReward` now compile as explicit no-op/reward markers. These moves still need authoring to choose the correct marker, but they do not require bespoke battle resolver code.
- Moves: `celebrate`, `happy-hour`, `hold-hands`, `make-it-rain`

### Reference Data Gaps (46 moves)

- Add: Local no-meta JSON lacks enough English effect text or exact numbers to author behavior safely.
- Reusable primitive/helper: reference-data enrichment before engine work; do not guess mechanics from vague flavor text.
- Implementation note: moves in this group should first be checked against existing primitives. For example, `axe-kick` maps its crash damage to the existing `recoil` op with `onMiss: true`; its remaining blocker is exact authored confusion chance from a mechanics source, not new crash-damage code.
- Moves: `axe-kick`, `blazing-torque`, `chloroblast`, `combat-torque`, `fickle-beam`, `fillet-away`, `glaive-rush`, `hard-press`, `hydro-steam`, `hyper-drill`, `ice-spinner`, `jet-punch`, `kowtow-cleave`, `lumina-crash`, `magical-torque`, `mountain-gale`, `mystical-power`, `noxious-torque`, `population-bomb`, `pounce`, `power-shift`, `psyshield-bash`, `raging-bull`, `ruination`, `salt-cure`, `shadow-blitz`, `shadow-break`, `shadow-down`, `shadow-end`, `shadow-hold`, `shadow-mist`, `shadow-rave`, `shadow-storm`, `shadow-wave`, `shelter`, `spicy-extract`, `spin-out`, `stone-axe`, `supercell-slam`, `syrup-bomb`, `tachyon-cutter`, `torch-song`, `triple-dive`, `twin-beam`, `wave-crash`, `wicked-torque`

## Verification

- Source JSON files counted: 937.
- Rows emitted in Move-by-Move Audit: 937.
- Batches emitted: 47 batches of 20, with the final batch containing 17 moves.
- Build/test not required for this doc-only audit; no engine behavior was changed.
