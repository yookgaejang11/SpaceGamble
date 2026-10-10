# SpaceGamble Pull Lever

Reference-inspired, low-poly console lever with a saturated signal-red horizontal grip, dark metal stem, and compact charcoal hinge base. The grip was updated from the previous muted red to a cleaner, more vivid red with a satin finish. The lever is a standalone prop and is split into named pieces so it can be animated independently from the mount.

## Included files

- `SpaceGamble_Lever.glb` — preferred delivery; named parts and a `LeverPivot` node are preserved.
- `SpaceGamble_Lever.obj` + `SpaceGamble_Lever.mtl` — grouped OBJ fallback. This format does not preserve the animated pivot hierarchy as reliably as GLB.
- `SpaceGambleLeverAnimator.cs` — small Unity helper with `Pull()` and `ReturnToRest()` methods for the pivot.
- `build_lever.py` — procedural source used to make the meshes and exports.
- `SpaceGamble_Lever_preview.png` — rendered preview.

## Approximate dimensions

- Units: meters
- Overall: 0.306 m wide × 0.469 m high × 0.220 m deep
- Up axis: Y
- Grip axis: X
- Pull direction: +Z

## Scene hierarchy

```text
BaseRoot
├── BaseLower / BaseHousing / TopInset / InsetStripe
├── HingeCheek_L / HingeCheek_R
├── PivotAxle / PivotCap_L / PivotCap_R
└── LeverPivot  ← animate this node
    ├── LeverStem
    ├── StemLowerCollar / StemUpperCollar
    ├── RedGripCore / RedGripEnd_L / RedGripEnd_R
    └── GripEndMark_L / GripEndMark_R
```

`LeverPivot` is located at the hinge and rotates around its local X axis. Its modeled rest angle is −4°. A pulled pose around +52° swings the handle toward +Z. Tune this range to the console and interaction animation.

## Unity usage

Import the GLB with a glTF importer such as glTFast. Attach `SpaceGambleLeverAnimator.cs` to the lever root, assign the imported child transform named `LeverPivot`, then call `Pull()` from the interaction system and `ReturnToRest()` when the lever resets. The base remains stationary.

The core animation can also be implemented directly:

```csharp
[SerializeField] private Transform leverPivot;

public void SetPull(float t)
{
    t = Mathf.Clamp01(t);
    var rest = Quaternion.Euler(-4f, 0f, 0f);
    var pulled = Quaternion.Euler(52f, 0f, 0f);
    leverPivot.localRotation = Quaternion.Slerp(rest, pulled, t);
}
```

The OBJ/MTL alternative is useful for inspection or pipelines that prefer OBJ, but use GLB when you need the pivot hierarchy. The model is a visual starting point; adjust collider placement, pivot limits, and proportions to fit your in-game console.
