# ADR-010 — Project data schema derived from the local PokeAPI corpus (trimmed)

Status: Accepted (2026-07-06)

## Decision
Our project data schemas **mirror the field structure and semantics of the PokeAPI data**
(`docs/pokeapi-results/`) for the useful subset of every entity, adapted three ways:
1. **Flatten references.** PokeAPI `{name, url}` links become our `category:slug` EntityIds
   (e.g. `type: {name:"fire", url:...}` → `type:fire`). The URL is never fetched — the target
   is resolved from the **local sibling folder** (`type/fire.json`, `evolution-chain/1.json`).
   **The project makes no network calls, ever** — the corpus is a local seed only.
2. **Trim baggage.** Drop fields the engine never uses: `flavor_text_entries`, localized
   `names`, `game_indices`, `past_values`, `contest_*`, `learned_by_pokemon`, `held_by_pokemon`,
   `pal_park_encounters`, `machines` (unless TMs are built), `varieties`/`forms` link lists, etc.
3. **Replace prose effects with executable ops.** PokeAPI `effect_entries` are natural-language
   prose (and partly non-English in this dump) — not executable. We keep the **structured**
   effect data PokeAPI already provides (`meta`: ailment/ailment_chance/drain/crit_rate/
   flinch_chance/healing/min-max hits/turns/stat_chance, plus `stat_changes`) and express moves
   as our closed effect-op palette seeded from it. Prose is discarded.

**Exception — sprite URLs are kept for now.** Per product direction, imported records retain the
relevant PokeAPI sprite URLs (front/back default + shiny + official artwork for species; the
default sprite for items) in an import-staging `spriteUrls` block, so images can be downloaded
later. These are NOT the runtime sprite fields; once art is downloaded and sliced, species point
at our own `sprite:*` IDs and the staging block is dropped. Per-generation/version sprite
duplicates ARE trimmed (bloat).

## Context
The local corpus is comprehensive and already well-structured for the useful subset (stats,
EV yields via `effort`, capture_rate, base_happiness, gender_rate, growth_rate with a full
level→exp table, egg_groups, evolution details, type `damage_relations`, item `attributes`).
Reusing that structure saves large design time and makes the Phase 17 **local** import an almost
mechanical field-map (~85% of moves are fully described by `meta`+`stat_changes`). But the raw
files are ~90% baggage (a Pokémon file is ~4,669 lines / 150 KB) and their effect text is not
executable, so storing them as-is is a non-starter.

## Alternatives considered
- **Mirror PokeAPI 1:1 (store raw files).** Rejected: massive bloat, un-editable by hand,
  URLs/prose the engine can't use, and it bakes official-content structure into original-content
  authoring. Violates the project's own Ponytail/YAGNI rule.
- **Design a schema from scratch, ignore PokeAPI shape.** Rejected: throws away a proven
  structure and makes the local import a bespoke translation for every field.

## Consequences
- DATA_SCHEMA.md adopts PokeAPI field names/semantics where clean; a local importer maps
  corpus → our schema (offline, deterministic).
- Our data files stay small, git-diffable, hand-authorable, and executable.
- Official content is dev-seed only; original creators author the same clean schema. No official
  assets/names ship in exports (unchanged legal boundary).

## Risks
- **Effect-mapping gaps:** the ~15% of moves with bespoke effects need hand-authored ops
  (Battle v5). Mitigation: the op palette is closed and versioned; unmapped moves are flagged.
- **Sprite-staging cruft lingering:** mitigation: staging `spriteUrls` is explicitly import-only
  and stripped once art lands; a validation rule can flag species still carrying it at export.
