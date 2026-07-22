# Water/Steel Sturgeon Developmental Line

Status: production candidates for direction approval. These are original internal-demo assets and are not canon until approved.

## Family concept

A sturgeon line whose natural dermal scutes mature into flexible blue-steel armor. Water identity comes from the unmistakable fish anatomy, swept hydrofoil fins, and powerful asymmetric tail; Steel identity comes from the biological scute rows rather than manufactured equipment.

Inherited anchors: wedge-shaped sturgeon snout, small barbels, deep teal-blue back, cream underside, five pale steel scute rows, broad pectoral fins, and a tall asymmetric tail.

## Stage progression

1. Scutlet (juvenile): compact fry, large expressive eye, rounded fins, sparse smooth scutes, eager swimming posture.
2. Sturgarde (intermediate): longer fast-swimmer body, reduced head ratio, swept fins, stronger tail base, denser interlocking scute rows.
3. Ferrurgeon (mature): deep current-breaking body, broad lowered head, stern eye, wide bracing fins, muscular tail, and fewer larger shield scutes.

## Sheet contract

- Canvas: 128 x 64 px RGBA PNG.
- Grid: one row by two 64 x 64 px cells; zero offset, spacing, and gutter.
- Column 0: front three-quarter battle view.
- Column 1: rear three-quarter battle view facing screen-right.
- Transparent padding: at least 4 px from every cell edge.
- Palette: 14 actual colors per sheet including transparency, below the 16-opaque-color creature target.
- Alpha: binary 0/255; no visible magenta key pixels.
- Scaling: nearest-neighbor normalization from preserved generated sources.

The `raw/` directory preserves the three generated source images. The user-facing production candidates are the three files in `sheets/` without the `_source` suffix.
