#!/usr/bin/env python3
"""Build a modest low-poly pull lever for SpaceGamble.

Coordinate system: X = lever grip axis, Y = up, Z = depth/toward player.
Units: meters. The LeverPivot node is placed at the hinge and can rotate about local X.
"""
from pathlib import Path
import math
import numpy as np
import trimesh
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals

OUT = Path(__file__).resolve().parent

COLORS = {
    "base":       (36, 41, 48, 255),
    "base_edge":  (23, 27, 32, 255),
    "insert":     (67, 75, 84, 255),
    "steel":      (91, 101, 111, 255),
    "shaft":      (49, 56, 64, 255),
    "rubber":     (29, 33, 38, 255),
    "red":        (218, 35, 29, 255),
    "red_dark":   (151, 25, 22, 255),
}


def material(name, rgba, metallic=0.0, roughness=0.72):
    return PBRMaterial(
        name=name,
        baseColorFactor=list(rgba),
        metallicFactor=float(metallic),
        roughnessFactor=float(roughness),
    )

MATS = {
    "base": material("Charcoal Housing", COLORS["base"], 0.18, 0.76),
    "base_edge": material("Dark Base Edge", COLORS["base_edge"], 0.12, 0.82),
    "insert": material("Inset Panel", COLORS["insert"], 0.22, 0.68),
    "steel": material("Brushed Steel", COLORS["steel"], 0.62, 0.4),
    "shaft": material("Lever Stem", COLORS["shaft"], 0.48, 0.48),
    "rubber": material("Rubber Grip Ends", COLORS["rubber"], 0.0, 0.9),
    "red": material("Red Pull Handle | saturated signal red", COLORS["red"], 0.06, 0.38),
    "red_dark": material("Handle End Accent | deep red", COLORS["red_dark"], 0.04, 0.48),
}


def apply_mat(mesh, key):
    mesh.visual = TextureVisuals(material=MATS[key])
    return mesh


def box(name, extents, center, mat, subdivisions=1):
    mesh = trimesh.creation.box(extents=extents, transform=trimesh.transformations.translation_matrix(center))
    mesh.metadata["name"] = name
    return apply_mat(mesh, mat)


def cylinder_between(name, start, end, radius, mat, sections=20):
    start = np.asarray(start, dtype=float)
    end = np.asarray(end, dtype=float)
    direction = end - start
    length = float(np.linalg.norm(direction))
    mesh = trimesh.creation.cylinder(radius=radius, height=length, sections=sections)
    rot = trimesh.geometry.align_vectors([0, 0, 1], direction)
    if rot is None:
        rot = np.eye(4)
    rot[:3, 3] = (start + end) * 0.5
    mesh.apply_transform(rot)
    mesh.metadata["name"] = name
    return apply_mat(mesh, mat)


def sphere(name, center, radius, mat, subdivisions=2):
    mesh = trimesh.creation.icosphere(subdivisions=subdivisions, radius=radius)
    mesh.apply_translation(center)
    mesh.metadata["name"] = name
    return apply_mat(mesh, mat)


def add_part(scene, mesh, node, geom, parent):
    scene.add_geometry(
        mesh,
        node_name=node,
        geom_name=geom,
        parent_node_name=parent,
        transform=np.eye(4),
    )


def translation(v):
    return trimesh.transformations.translation_matrix(v)


def build_scene():
    scene = trimesh.Scene(base_frame="world")
    scene.graph.update(frame_to="BaseRoot", frame_from="world", matrix=np.eye(4), metadata={"role": "stationary mount"})

    # Stationary console-mount assembly; 30 cm wide, 22 cm deep.
    stationary = [
        ("BaseLower", "BaseLowerMesh", box("BaseLower", (0.30, 0.028, 0.22), (0, 0.014, 0), "base_edge")),
        ("BaseHousing", "BaseHousingMesh", box("BaseHousing", (0.276, 0.042, 0.198), (0, 0.046, 0), "base")),
        ("TopInset", "TopInsetMesh", box("TopInset", (0.222, 0.008, 0.142), (0, 0.071, 0), "insert")),
        # Two compact cheeks form an open yoke around the lever hinge.
        ("HingeCheek_L", "HingeCheek_L_Mesh", box("HingeCheek_L", (0.055, 0.102, 0.104), (-0.082, 0.108, 0), "base_edge")),
        ("HingeCheek_R", "HingeCheek_R_Mesh", box("HingeCheek_R", (0.055, 0.102, 0.104), (0.082, 0.108, 0), "base_edge")),
        ("InsetStripe", "InsetStripeMesh", box("InsetStripe", (0.17, 0.006, 0.016), (0, 0.078, 0.049), "steel")),
        ("PivotAxle", "PivotAxleMesh", cylinder_between("PivotAxle", (-0.134, 0.132, 0), (0.134, 0.132, 0), 0.022, "steel", 24)),
        ("PivotCap_L", "PivotCap_L_Mesh", cylinder_between("PivotCap_L", (-0.153, 0.132, 0), (-0.132, 0.132, 0), 0.031, "rubber", 24)),
        ("PivotCap_R", "PivotCap_R_Mesh", cylinder_between("PivotCap_R", (0.132, 0.132, 0), (0.153, 0.132, 0), 0.031, "rubber", 24)),
    ]
    for node, geom, mesh in stationary:
        add_part(scene, mesh, node, geom, "BaseRoot")

    # Hinge origin is centered on the visible axle. Children are local to this pivot.
    pivot = np.eye(4)
    pivot[:3, :3] = trimesh.transformations.rotation_matrix(math.radians(-4.0), [1, 0, 0])[:3, :3]
    pivot[:3, 3] = [0.0, 0.132, 0.0]
    scene.graph.update(
        frame_to="LeverPivot",
        frame_from="BaseRoot",
        matrix=pivot,
        metadata={
            "role": "animated lever pivot",
            "axis": "local X",
            "rest_angle_degrees": -4.0,
            "pulled_angle_degrees": 52.0,
        },
    )

    # Moving lever assembly. Meshes are modeled relative to the hinge origin.
    moving = [
        ("LeverStem", "LeverStemMesh", cylinder_between("LeverStem", (0, 0.0, 0), (0, 0.292, -0.018), 0.014, "shaft", 16)),
        ("StemLowerCollar", "StemLowerCollarMesh", cylinder_between("StemLowerCollar", (0, 0.035, 0), (0, 0.067, -0.002), 0.022, "steel", 20)),
        ("StemUpperCollar", "StemUpperCollarMesh", cylinder_between("StemUpperCollar", (0, 0.266, -0.016), (0, 0.291, -0.018), 0.021, "steel", 20)),
        # Horizontal red grip: central cylinder plus rounded end caps creates a pill silhouette.
        ("RedGripCore", "RedGripCoreMesh", cylinder_between("RedGripCore", (-0.083, 0.302, -0.019), (0.083, 0.302, -0.019), 0.037, "red", 20)),
        ("RedGripEnd_L", "RedGripEnd_L_Mesh", sphere("RedGripEnd_L", (-0.083, 0.302, -0.019), 0.037, "red", 2)),
        ("RedGripEnd_R", "RedGripEnd_R_Mesh", sphere("RedGripEnd_R", (0.083, 0.302, -0.019), 0.037, "red", 2)),
        # Small end discs imply capped rubber without making it glossy.
        ("GripEndMark_L", "GripEndMark_L_Mesh", cylinder_between("GripEndMark_L", (-0.122, 0.302, -0.019), (-0.117, 0.302, -0.019), 0.018, "red_dark", 16)),
        ("GripEndMark_R", "GripEndMark_R_Mesh", cylinder_between("GripEndMark_R", (0.117, 0.302, -0.019), (0.122, 0.302, -0.019), 0.018, "red_dark", 16)),
    ]
    for node, geom, mesh in moving:
        add_part(scene, mesh, node, geom, "LeverPivot")

    scene.metadata.update({
        "asset": "SpaceGamble Pull Lever",
        "units": "meters",
        "up_axis": "Y",
        "description": "Stylized low-poly console lever. Animate the LeverPivot node around local X from -4 degrees (rest) to +52 degrees (pulled toward +Z).",
    })
    return scene


def export_obj(scene):
    # A grouped OBJ/MTL fallback for import workflows that do not use glTF.
    obj_path = OUT / "SpaceGamble_Lever.obj"
    mtl_path = OUT / "SpaceGamble_Lever.mtl"
    lines = ["# SpaceGamble pull lever - grouped OBJ fallback", "mtllib SpaceGamble_Lever.mtl"]
    mtl = []
    offset = 1
    # OBJ has no portable pivot hierarchy, so bake each node's world transform
    # while retaining the per-part object/group names.
    for key, node_list in scene.graph.geometry_nodes.items():
        node = node_list[0]
        geom = scene.geometry[key].copy()
        if not isinstance(geom, trimesh.Trimesh):
            continue
        transform, _ = scene.graph.get(node)
        geom.apply_transform(transform)
        name = geom.metadata.get("name", key).replace(" ", "_")
        mat = geom.visual.material
        color = np.asarray(mat.baseColorFactor[:3], dtype=float) / 255.0
        mat_name = "MAT_" + name
        lines.extend([f"o {name}", f"g {name}", f"usemtl {mat_name}"])
        for v in geom.vertices:
            lines.append(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}")
        for face in geom.faces:
            idx = [str(int(i) + offset) for i in face]
            lines.append("f " + " ".join(idx))
        offset += len(geom.vertices)
        mtl.extend([
            f"newmtl {mat_name}",
            "Ka 0.05 0.05 0.05",
            f"Kd {color[0]:.4f} {color[1]:.4f} {color[2]:.4f}",
            "Ks 0.15 0.15 0.15",
            "Ns 80",
            "d 1.0",
            "illum 2",
            "",
        ])
    obj_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    mtl_path.write_text("\n".join(mtl), encoding="utf-8")


def main():
    scene = build_scene()
    glb_path = OUT / "SpaceGamble_Lever.glb"
    scene.export(glb_path, file_type="glb")
    export_obj(scene)
    print(f"Wrote {glb_path}")
    print(f"Wrote {OUT / 'SpaceGamble_Lever.obj'}")
    print(f"Wrote {OUT / 'SpaceGamble_Lever.mtl'}")
    print("nodes:", sorted(str(n) for n in scene.graph.nodes))
    print("bounds:", scene.bounds.tolist())

if __name__ == "__main__":
    main()
