#!/usr/bin/env python3
"""Render a lightweight shaded PNG preview from the exported GLB."""
from pathlib import Path
import numpy as np
import trimesh
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection

HERE = Path(__file__).resolve().parent
scene = trimesh.load(HERE / "SpaceGamble_Lever.glb", force="scene")
fig = plt.figure(figsize=(8, 8), dpi=160, facecolor="#e9edf2")
ax = fig.add_subplot(111, projection="3d", computed_zorder=False)
ax.set_facecolor("#e9edf2")
light = np.array([-0.4, 0.55, 0.8], dtype=float)
light /= np.linalg.norm(light)

for mesh in scene.dump(concatenate=False):
    if not isinstance(mesh, trimesh.Trimesh) or len(mesh.faces) == 0:
        continue
    base = np.array([0.35, 0.38, 0.42, 1.0])
    mat = getattr(mesh.visual, "material", None)
    factor = getattr(mat, "baseColorFactor", None)
    if factor is not None:
        base = np.asarray(factor, dtype=float) / 255.0
        if base.shape[0] == 3:
            base = np.r_[base, 1.0]
    normals = mesh.face_normals[:, [0, 2, 1]]
    shade = 0.43 + 0.57 * np.maximum(normals @ light, 0.0)
    colors = np.tile(base, (len(mesh.faces), 1))
    colors[:, :3] *= shade[:, None]
    vertices_y_up = mesh.vertices[:, [0, 2, 1]]
    collection = Poly3DCollection(vertices_y_up[mesh.faces], linewidths=0.12, edgecolors=(0.10, 0.12, 0.15, 0.35))
    collection.set_facecolors(colors)
    ax.add_collection3d(collection)

ax.set_xlim(-0.21, 0.21)
ax.set_ylim(-0.18, 0.18)
ax.set_zlim(0.0, 0.52)
ax.set_box_aspect((0.42, 0.36, 0.52))
ax.view_init(elev=17, azim=-54)
ax.set_proj_type("persp")
ax.set_axis_off()
plt.subplots_adjust(0, 0, 1, 1)
fig.savefig(HERE / "SpaceGamble_Lever_preview.png", dpi=160, facecolor=fig.get_facecolor(), bbox_inches="tight", pad_inches=0.1)
print(HERE / "SpaceGamble_Lever_preview.png")
