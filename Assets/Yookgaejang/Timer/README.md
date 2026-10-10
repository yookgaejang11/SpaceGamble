# SpaceGamble Stopwatch Counter

A compact, stopwatch-like tabletop 3D prop based on the supplied sketch. The front has a single completely blank smoked-glass display; **no numbers, labels, graphics, or divider lines are modeled or baked in**. Add the left/right layout and all separators in the game's UI.

## Files

- `SpaceGamble_StopwatchCounter.glb` — preferred Unity import; preserves the named parts and `DisplayRoot` anchor.
- `SpaceGamble_StopwatchCounter.obj` + `.mtl` — geometry/material fallback; hierarchy is flattened.
- `SpaceGamble_StopwatchCounter_preview.png` — front view of the blank display model.
- `build_counter.py` and `render_preview.py` — reproducible model source and preview renderer.

## Model

- Unity/glTF convention: Y-up, X horizontal, front faces +Z; dimensions are in meters.
- Approximate overall size: 0.50 m wide × 0.306 m high (including pusher) × 0.081 m deep.
- The rounded graphite case has a layered metal bezel, one blank glass display, a central stopwatch pusher, two small top controls, and four bezel fasteners.
- `DisplayRoot` contains `DisplayGlass`; use it as the parent/placement anchor for a World Space Canvas or runtime text. The face is centered around the root and points toward +Z.

## Unity use

Import the GLB with glTFast (or another glTF importer). Add a World Space Canvas or TextMeshPro objects under `DisplayRoot`, then create the player/opponent layout and line divisions in Unity UI. The model itself deliberately contains none of those elements.

To rebuild, run `python3 build_counter.py`, then `python3 render_preview.py`.
