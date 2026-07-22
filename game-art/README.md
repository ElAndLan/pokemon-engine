# Game Art

This root-level directory stores original source sprite sheets for the project's playable demo.
These files are authoring sources, not the Runtime's imported asset directory.

## Local development placeholders

User-supplied third-party sheets that are not project-owned must live only under
`game-art/dev-placeholders/`. That subtree is gitignored and may be used solely for private local
manual playtesting under `AGENTS.md` §2. It is not an original-source folder: its contents are never
committed, approved, hashed into goldens, used by automated fixtures, packed, exported, released, or
published. Phase 16H's live local sandbox may use them for private manual visual playtesting; every
automated, packed, distributable, and release gate remains neutral and project-owned.

## Layout

```text
game-art/
├── creatures/
│   └── <creature_slug>/
│       └── sprite_sheet.png
├── terrain/
│   └── <terrain_slug>/
│       └── sprite_sheet.png
└── npcs/
    └── <npc_slug>/
        └── sprite_sheet.png
```

Use lowercase `snake_case` slugs, such as `moss_fox`, `autumn_route`, or `field_researcher`.
Use only the slug portion of an entity ID: `species:moss_fox` belongs in
`creatures/moss_fox/`, because `:` is not valid in a Windows directory name.

Keep each supplied source sheet unchanged after import. Runtime-ready copies and project metadata
belong under the demo project's `assets/` and `data/` folders; generated atlases and exports do not
belong here.

## Sprite-sheet intake

Alongside `sprite_sheet.png`, an asset folder may contain a short `README.md` recording:

- entity ID and display name;
- frame width and height;
- row/column order;
- animation names and timing;
- anchor or ground-contact point;
- source and revision notes.

Reusable sprites should be PNG files with real alpha transparency. Exact dimensions, alpha,
palette, frame registration, and slicing are verified from the supplied file before import.
