# SpaceGamble control materials

Three tileable PBR material sets for the hand-strike button, the player's tabletop territory plate, and the lever grip. The material choices keep the game's modest industrial look: dark graphite for the plate and stronger, cleaner signal red for the two interaction points.

## Material sets

| Folder | Intended use | Suggested URP Lit values |
|---|---|---|
| `Territory_Graphite_SatinSteel` | The player's rectangular territory plate; subtle brushed graphite with enough contrast against the wooden table | Metallic 0.52; smoothness about 0.28 |
| `ImpactButton_SignalRed_Enamel` | Top/cap of the punch-down button; saturated signal red with a restrained satin sheen | Metallic 0.12; smoothness about 0.62 |
| `LeverGrip_SaturatedRed_Satin` | Replacement red for the lever handle; deeper and more saturated, not faded | Metallic 0.06; smoothness about 0.62 |

Each folder contains a BaseColor map, grayscale Roughness and Metallic maps, a tangent-space Normal map, and a Unity-ready `UnityMetallicSmoothness` map (metallic in RGB, smoothness in alpha). The textures are 1024×1024 PNGs and made to tile; they contain no text, border, button silhouette, or baked-in geometry, so UVs and shapes stay under your control.

## Unity import

For each BaseColor texture, enable **sRGB (Color Texture)**. For Roughness, Metallic, Normal, and Unity MetallicSmoothness, disable sRGB; set the Normal map's texture type to **Normal map**. In URP/Lit, assign BaseColor to **Base Map**, Normal to **Normal Map**, and the Unity MetallicSmoothness texture to **Metallic Map**; its alpha carries smoothness. If you prefer separate scalar controls, use the Metallic value in the table and convert smoothness to roughness as `roughness = 1 - smoothness`.

The plate map is intentionally texture-only: make the territory shape, bevels, and corner fasteners as geometry, then use this map for its surface material. The button map is likewise a material for an existing or future button mesh, not a button model.

## Lever model update

`../SpaceGambleLever/SpaceGamble_Lever.glb` and the OBJ/MTL fallback have been rebuilt with a cleaner signal-red handle (sRGB base color approximately `#DA231D`) and a less chalky satin response (roughness 0.38). The updated model preview is included in the parent package.

Run `python3 build_materials.py` to regenerate the PBR maps and swatch image. The lever model source can be rebuilt with `python3 ../SpaceGambleLever/build_lever.py`.
