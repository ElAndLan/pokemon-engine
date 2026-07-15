# BATTLE_AI_SPEC

Status: **Phase 14 verified Core baseline.** Core has `random`, `basic`, and `smart` trainer AI
dispatch. Smart AI chooses legal move or switch actions, exposes named score components for debug
visibility, tracks seen/repeated player moves, and covers the v5-supported scoring categories.
Measured baseline: Smart-vs-Basic 69.0% @400, Smart-vs-Smart side balance 49.2%. Full tuning is
deferred until Phase 15+ battle mechanics exist; Expert/open-team-sheet logic remains a later
design decision.

## Purpose

Define three player-facing AI difficulty tiers for exported games:

1. **Beginner AI** - mainline Pokemon-style trainer AI. Somewhat competent, especially for
   boss trainers, but not a hard-mode experience.
2. **Advanced AI** - the intended challenge mode. Beatable, but it should force players to
   build coherent teams, think about switching, and respect setup/status/hazards.
3. **Expert AI** - proposed third tier because the product goal calls for three tiers but only
   two were named. This is an optional hard/postgame-style mode with stronger prediction and
   team-preview assumptions. Rename or remove by user decision before schema/UI work.

The AI must remain deterministic for a given seed, explainable in debug output, and unable to
mutate battle state directly. It chooses legal `BattleAction`s; `BattleController` remains the
only resolver.

## Research takeaways

Mainline Pokemon trainer AI is not a perfect player simulator. Public documentation of the newest
mainline games' internal AI is sparse, so this project uses well-documented Gen IV-style mainline
AI as the Beginner benchmark:

- Move choice is score-based. Each move starts with a baseline score, modules add/subtract
  score, the highest score wins, and ties are random.
- The documented Gen IV modules are Basic, Strong, Expert, and Doubles. Basic avoids no-effect
  moves, Strong favors damage/KOs, and Expert adds more move-specific strategy.
- Battle Frontier-style difficulty mainly comes from stacking more modules on stronger teams,
  not from reading the player's chosen action.
- Gen IV AI knowledge is asymmetric but bounded: it knows exact HP, held item, turn order, and
  its own team; it does not know player moves until they are seen, and it forgets revealed move
  and ability knowledge when that Pokemon switches out.
- Gen IV switching is rule-based: send-in prefers good type/move matchups, while voluntary
  withdrawal is limited to specific bad states such as hard walls, imminent fainting, or useful
  absorption/immunity pivots.
- Pokemon Essentials is a useful fangame reference: higher skill enables switch consideration,
  item choice, move failure prediction, and move scoring as separate capabilities.

References:
- https://pokemow.com/Gen4/TrainerAI/
- https://pokemow.com/Gen4/TrainerAI/switching.html
- https://essentialsdocs.fandom.com/wiki/Battle_AI

## Design pillars

- **Fair before clever.** The AI can infer from visible information and memory; it must not read
  the player's selected action.
- **Strong teams still matter.** Difficulty should come from better teams, movesets, items, and
  trainer role tuning as much as from smarter logic.
- **Prediction is probabilistic.** Advanced/Expert can weigh likely player choices, but never
  commit as if it knows the future.
- **Mistakes are intentional, not bugs.** Lower tiers use higher score noise, narrower knowledge,
  and smaller action sets.
- **No stall traps.** Healing, switching, protect loops, and PP stall all need caps/cooldowns.
- **Debuggable.** Every non-random AI decision should be able to emit a score table explaining
  why it chose a move, switch, or item.

## Difficulty model

There are two independent knobs:

- **Game AI tier**: player/export setting: `beginner`, `advanced`, `expert`.
- **Trainer importance**: creator-authored trainer role: `regular`, `ace`, `gymLeader`,
  `rival`, `elite`, `postgame`.

The game AI tier controls which capabilities are available. Trainer importance adjusts weights,
noise, item access, and team quality. Example: on Beginner, a gym leader still uses Beginner
logic, but with less noise, stronger teams, and limited healing items.

Current schema has `AiProfile { Random, Basic, Smart }`. Do not force a schema change during
brainstorming. Implementation can initially map:

- `Random` -> wild/very weak trainers.
- `Basic` -> Beginner.
- `Smart` -> Advanced core.

The eventual player-facing three-tier option can be added later through project/game options,
with a migration note if serialized shape changes.

## Knowledge model

The AI's knowledge is explicit data, not implicit access to everything in memory.

| Knowledge bucket | Beginner | Advanced | Expert |
|---|---|---|---|
| Own active/team/moves/items | Full | Full | Full |
| Player active species/types/HP/status/stages | Full | Full | Full |
| Player active moves | Seen moves only | Seen moves + likely-move estimates | Open team sheet or strong estimates, by setting |
| Player held item | Revealed only by default | Revealed or inferred | Open team sheet if enabled |
| Player reserve party | Seen only | Seen, or full party if party preview is enabled | Full party if expert/open-sheet setting enabled |
| Player selected action this turn | Never | Never | Never |
| Future RNG | Never | Never | Never |

Recommendation: **do not let Advanced see unrevealed moves/items by default.** It should predict
from observed behavior, team context, and public party preview if the game mode exposes preview.
Expert can use open-team-sheet knowledge, but only if the game clearly labels that setting.

## Shared decision flow

Each AI turn:

1. Build an `AiDecisionContext`: battle snapshot, legal actions, tier, trainer importance,
   battle memory, known/revealed player info, and scoring weights.
2. If forced to choose a replacement after fainting, score legal switch-ins.
3. Score voluntary switches if the tier can switch.
4. Score trainer item use if trainer item actions are available. Current battle action model does
   not have enemy item use, so this is not implemented today.
5. Score moves.
6. Apply tier noise and anti-loop cooldowns.
7. Pick the highest legal action; tie-break with injected `IRng`.
8. In debug builds, expose the candidate score table.

The candidate score should be a sum of named components, not one opaque number:

- Expected damage.
- KO chance / survival swing.
- Accuracy and miss risk.
- Type effectiveness and immunity.
- Status value.
- Stat-stage/setup value.
- Hazard value.
- Trap/force-switch value.
- Recovery value.
- Recoil/crash/self-KO risk.
- Switch tempo cost.
- Predicted player response value, if the tier supports prediction.
- Repetition penalty and anti-stall penalty.

## Beginner AI

Target feel: similar to normal mainline Pokemon campaigns. It can punish obvious mistakes, but a
casual player using reasonable levels and type matchups should win most story fights.

Capabilities:

- Chooses moves by score, mostly Basic + Strong style.
- Avoids moves that obviously fail: no PP, type immunity, status on an already-statused target,
  stat boosts at cap, healing at full HP.
- Favors the highest expected damage and likely KOs.
- Values super-effective damage, STAB, and priority KOs.
- Uses simple status/setup only when it is obviously useful:
  - status on a healthy unstated target;
  - setup when the AI is not in immediate KO danger;
  - recovery below a threshold if the move is available.
- Boss trainers can use limited healing items once trainer item actions exist.
- Replacement after fainting prefers a party member with a good type matchup or strongest damage.
- Voluntary switching is rare:
  - no damaging move can affect the target;
  - trapped/locked rules allow it;
  - boss trainer has a very obvious immune/resist switch.

Limitations:

- No deep prediction.
- Does not inspect unrevealed player moves.
- Does not double-switch.
- Does not switch only to waste the player's turn.
- Does not play long hazard/stall lines.
- Regular trainers have enough score noise to make suboptimal moves plausible.

Suggested tuning:

- Regular trainer noise: 15-25%.
- Important trainer noise: 5-12%.
- Voluntary switch cooldown: at least 3 turns.
- Healing item use: important trainers only; one or two items max; never heal if it only delays a
  guaranteed loss by one turn.

## Advanced AI

Target feel: challenging but completable. Players should need coherent teams, sane movesets,
switching, status management, and some planning. It should not feel like the game is reading
their controller.

Capabilities beyond Beginner:

- Uses true expected damage with stat stages, burn, type chart, STAB, weather field effects, and
  accuracy.
- Weather scoring consumes the controller's immutable condition snapshot and collects the same
  typed `DamageQuery` registrations as the resolver. A missing snapshot means clear weather; AI
  code never reconstructs weather from presentation events or a parallel timer.
- Weather-sensitive move accuracy consumes the same typed `AccuracyQuery` registration and bypass
  filter as the resolver. It changes only the existing `damage`/`ko` expected-value components,
  consumes no additional AI RNG, and preserves the visible-field-only fairness boundary.
- Weather-sensitive status scoring consumes the same typed `StatusAttempt` filter as the resolver.
  A denied status contributes no `status` component; unlisted statuses and a missing condition
  snapshot retain their ordinary value. Preview does not mutate or complete the hook snapshot.
- Weather-sensitive recovery consumes the same typed `HealingQuery` replacement as the resolver.
  The named `recovery` component uses the direct recipient-max-HP amount capped at missing HP; an
  absent condition snapshot or unlisted weather retains the authored healing fraction and consumes
  no additional AI RNG.
- Values hazards based on remaining opposing party and expected future switches.
- Values setup based on expected survival and sweep potential.
- Values status by matchup:
  - paralysis against faster attackers;
  - burn against physical attackers;
  - poison/toxic against bulky targets;
  - sleep/freeze-like control as high tempo.
- Values Protect/Detect as scouting, poison/burn/hazard chip, or avoiding a predicted KO, but with
  chain penalties.
- Values force-switch against boosted or bad-matchup targets.
- Switches when hard-countered, when a reserve has a clear tempo advantage, or when preserving the
  current Pokemon matters.
- Uses limited trainer items below thresholds when they change the expected outcome, not simply
  whenever HP is low.
- Maintains battle memory:
  - player moves seen this battle;
  - last move used;
  - repeated player patterns;
  - revealed item/ability when future systems expose them;
  - whether the player tends to switch out of bad matchups.

Prediction model:

Advanced predicts categories, not exact button presses. Candidate player responses:

- stay and use strongest damaging move;
- stay and use setup/status/recovery if already shown;
- switch to the best known resist/immunity;
- switch to preserve a threatened Pokemon.

The AI scores its own action against a blended expectation:

`score = current-board-score * 0.65 + predicted-response-score * 0.35`

The exact split is tunable. The key rule: prediction never dominates the visible board unless
the player has repeated the same pattern enough times.

Fairness limits:

- No reading selected action.
- No exact knowledge of unrevealed moves/items unless party preview/open-sheet says so.
- Prediction confidence starts low and increases only from observed behavior.
- Voluntary switches need a threshold improvement and a cooldown.
- Recovery and Protect loops are capped.
- If the top two actions are close, use seeded noise to keep it from feeling mechanical.

Suggested tuning:

- Noise: 5-10%.
- Voluntary switch cooldown: 2-3 turns.
- Prediction weight: 20-40%.
- Hard-counter switch threshold: the switch-in must improve expected value by a meaningful margin,
  not merely by 1 point.
- Difficulty smoke target: a competent scripted player should win roughly 45-65% of evenly leveled
  advanced fights across seeded sims. Tune teams and weights before adding complexity.

## Expert AI

Proposed third tier. Target feel: optional hard mode/postgame. It can be sharp enough that
players are expected to lose and adjust, but still must not cheat by reading the chosen action.

Capabilities beyond Advanced:

- Uses open-team-sheet knowledge if the game mode exposes it: known party, moves, and items.
- Evaluates one-turn and limited two-turn lines:
  - "If I set up, can I survive and sweep?"
  - "If I attack, can the player safely switch to a resist?"
  - "If I switch, do I lose too much hazard/tempo?"
- Stronger switch-in selection, including preserving win conditions.
- Stronger item timing, including choosing not to heal when a switch is better.
- More aggressive hazard/force-switch planning.
- Recognizes repeated player habits faster.

Limits:

- Still no selected-action reading.
- Still no future RNG reading.
- No full minimax search over whole battles.
- No infinite switching loops.
- No perfect prediction against unrevealed information unless open-team-sheet mode is enabled.

Suggested tuning:

- Noise: 2-6%.
- Prediction weight: 35-55%, capped by confidence.
- Voluntary switch cooldown: 1-2 turns, but repeated switches get a tempo penalty.
- Difficulty smoke target: a competent scripted player wins roughly 25-45% of evenly leveled
  expert fights. This is not the default story experience.

## Making challenge fun instead of oppressive

Use these rules even if they make the AI slightly weaker:

- The AI should make visible, explainable decisions. Debug score tables are mandatory for tuning.
- The AI should sometimes respect risk instead of always making the theoretically highest-EV play.
- The AI should not counterteam the player unless the creator authored that trainer as a counter.
- The AI should not heal forever. Item stock is visible/finite.
- The AI should not switch every time it is at a disadvantage. Switching has a score threshold,
  cooldown, and hazard/tempo cost.
- The AI should not always punish setup instantly. It should respond better at higher tiers, but
  players should be able to create openings.
- The AI should be stronger for important trainers primarily because their teams and items are
  better, not because regular trainers are intentionally stupid.

## Phase 14 implementation target

Before code, confirm this document's tier names and whether Expert is wanted.

Minimum Phase 14 AI work should be:

1. Keep `Random` and `Basic` behavior intact.
2. Integrate the smart/Advanced chooser through trainer AI profile dispatch.
3. Score moves with named score components and expose a debug score table.
4. Add battle memory for seen player moves and repeated patterns.
5. Add bounded voluntary switch scoring.
6. Add hazard/status/setup/protect/force-switch/recovery scoring where current battle actions
   support it.
7. Decide whether trainer item use belongs in Phase 14 now. Current battle actions do not expose
   enemy item use, so adding it is a separate scope decision.
8. Add unit tests for each decision category.
9. Add seeded integration smoke tests with the target win-rate bands.

Do not implement Expert-specific open-team-sheet logic until the player-facing difficulty option
and party-preview/open-sheet rules are designed.

## Open questions

- Should the third tier be named `Expert`, `Champion`, `Competitive`, or something else?
- Should Advanced know the player's full party by default, or only when the game uses party preview?
  Recommendation: party preview only.
- Should Beginner copy Gen IV's hidden held-item knowledge? Recommendation: no by default; use
  revealed items only, unless the creator toggles "classic AI knowledge".
- Should important trainers override global tier upward? Recommendation: no. They should get better
  teams/items and lower noise, but the player-selected tier remains the cap.
- Are healing items part of Phase 14, or deferred until battle item actions are exposed cleanly?
