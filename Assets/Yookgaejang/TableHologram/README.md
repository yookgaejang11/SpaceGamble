# SpaceGamble Table-Center Hologram

A standalone, low-poly inverted-pyramid projection prop intended for the center of the game table. The broad square opening floats above a small circular projector; the pyramid narrows to a point toward the projector ring. The model includes two opposed content planes and an embedded generic `HOLO LINK / SIGNAL LOCKED` sample texture so image/text processing can be previewed from either side. No probability number, accident label, or survival outcome is included.

## Files

- `SpaceGamble_TableHologram.glb` — preferred Unity import; keeps named mesh nodes.
- `SpaceGamble_TableHologram.obj` + `SpaceGamble_TableHologram.mtl` — geometry fallback; hierarchy/pivot grouping is flattened.
- `SpaceGambleSoftHologram.shader` — custom texture-aware transparent shader for Unity 6 URP.
- `SpaceGamble_HoloSampleContent.png` — transparent sample content texture (also embedded in the GLB).
- `SpaceGamble_TableHologram_preview.png` — shaded model preview with the sample mapped onto the inner card.
- `build_hologram.py`, `make_sample_content.py`, and `render_preview.py` — procedural source and preview tools.

## Approximate size

- Units: meters; Y-up
- Overall bounding box: about 0.605 m wide × 0.633 m high × 0.605 m deep
- Projector housing diameter: 0.34 m
- Pyramid top opening: 0.60 m square
- Pyramid tip is near the projector ring; the wide opening is above it.
- Content cards are approximately 0.23 m × 0.20 m.

Scale the complete `AssemblyRoot` to fit the table, or change the dimensions near `make_pyramid_shell()` / `make_content_plane()` in `build_hologram.py` and rebuild. The GLB already embeds the sample image; the separate PNG is included for easy assignment to a Unity material.

## GLB hierarchy

```text
AssemblyRoot
├── ProjectorHousing
├── OuterEmitterRing
├── InnerEmitterRing
├── EmitterDiffuser
└── HologramAnchor
    ├── InvertedPyramidShell
    ├── PyramidFrame
    ├── ContentPlane_Front
    └── ContentPlane_Back
```

The front content card faces +Z; the back card faces -Z and has mirrored UVs so the same image/text reads normally from opposite seats. They are slightly separated to avoid z-fighting. The shell, frame, and content cards are separate meshes. The projector housing/rings can use ordinary URP Lit materials.

## Apply the shader in Unity (URP)

1. Import `SpaceGamble_TableHologram.glb` with a glTF importer such as **glTFast**.
2. Create a material from `SpaceGambleSoftHologram.shader` (`SpaceGamble → SoftHologram`).
3. For the shell/frame material, leave `Use Content Texture` off, set `Cull Mode` to `Off (0)`, and assign it to `InvertedPyramidShell` and `PyramidFrame`.
4. Create a second material from the same shader for the content cards. Turn `Use Content Texture` on, set `Cull Mode` to `Back (2)`, assign it to both `ContentPlane_Front` and `ContentPlane_Back`, and set `_BaseMap` to `SpaceGamble_HoloSampleContent.png` (or another texture). The demo texture is also embedded in the GLB's default content material.
5. Place `AssemblyRoot` at the table center with the housing at table height. Resize the root to suit the camera and table proportions.

### Content image or text

- **Image:** assign a regular RGBA texture to `_BaseMap`. Keep alpha enabled if the image has transparent areas.
- **Static text:** use a transparent PNG of the text, or render it to a transparent `RenderTexture`.
- **Dynamic text:** render a TextMeshPro/Canvas display into an ARGB RenderTexture with a transparent camera clear, then assign that RenderTexture to `_BaseMap`. The shader will apply scanlines, small horizontal glitch shifts, tint, flicker, and pulse to the rendered text pixels just like an image.

The included shader samples the content texture itself, so the effect is not limited to the glass pyramid surface. The demo card says `HOLO LINK / SYSTEM STATUS / SIGNAL LOCKED / LINK STABLE` and includes a small signal trace and bars; it is only a style/sample texture, not a gameplay probability result. Use separate shell and content materials: a good starting point is shell opacity `0.10–0.16`, content opacity `0.35–0.60`, rim opacity `0.16–0.24`, scanline strength `0.04–0.08`, glitch amount `0.001–0.003`, flicker amount `0.01–0.03`, and pulse amount `0–0.025`. Reduce or disable glitch/flicker for a calmer read. The shader updates from Unity time; no animation script is required.

To rebuild from source, run `python3 make_sample_content.py`, then `python3 build_hologram.py`, then `python3 render_preview.py`.

The shader is written for **URP**. If the project uses the Built-in Render Pipeline or HDRP, it will need a pipeline-specific version. Transparent meshes can sort differently depending on camera angle and overlap; the shell material is two-sided, while the opposed content cards use back-face culling so each viewing side gets one readable copy.
