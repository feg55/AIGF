"""Prepare the private Mint FBX source for the Unity/PICO runtime.

Run with Blender in background mode. The raw source stays under the ignored
ForAI directory; only the processed FBX, textures and a deterministic report
are written into Assets.
"""

from __future__ import annotations

import argparse
import json
import math
import shutil
from pathlib import Path

import bpy


TARGET_HEIGHT_METERS = 1.63
LOD_RATIOS = (0.78, 0.42, 0.20)


def parse_args() -> argparse.Namespace:
    argv = []
    if "--" in __import__("sys").argv:
        argv = __import__("sys").argv[__import__("sys").argv.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--textures", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def character_mesh_for(armature: bpy.types.Object) -> bpy.types.Object:
    candidates = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        if any(mod.type == "ARMATURE" and mod.object == armature for mod in obj.modifiers):
            candidates.append(obj)
    if not candidates:
        raise RuntimeError("The main armature has no skinned mesh.")
    return max(candidates, key=lambda obj: len(obj.data.polygons))


def rebind_texture_paths(texture_dir: Path) -> list[str]:
    available = {path.name.lower().replace(" ", "_"): path for path in texture_dir.iterdir() if path.is_file()}
    rebound = []
    for image in bpy.data.images:
        if image.source != "FILE" or image.name == "Render Result":
            continue
        candidates = {
            image.name.lower().replace(" ", "_"),
            Path(image.filepath).name.lower().replace(" ", "_"),
        }
        match = next((available[name] for name in candidates if name in available), None)
        if match is not None:
            image.filepath = str(match)
            image.reload()
            rebound.append(match.name)
    return sorted(set(rebound))


def weighted_bones(mesh: bpy.types.Object, threshold: float = 0.0001) -> set[str]:
    group_names = {group.index: group.name for group in mesh.vertex_groups}
    result: set[str] = set()
    for vertex in mesh.data.vertices:
        for weight in vertex.groups:
            if weight.weight > threshold and weight.group in group_names:
                result.add(group_names[weight.group])
    return result


def prune_unused_bones(armature: bpy.types.Object, mesh: bpy.types.Object) -> tuple[int, int]:
    used = weighted_bones(mesh)
    keep = set(used)
    for bone in armature.data.bones:
        if bone.name in used:
            keep.update(parent.name for parent in bone.parent_recursive)

    before = len(armature.data.bones)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in list(armature.data.edit_bones):
        if bone.name not in keep:
            armature.data.edit_bones.remove(bone)
    bpy.ops.object.mode_set(mode="OBJECT")

    for group in list(mesh.vertex_groups):
        if group.name not in keep:
            mesh.vertex_groups.remove(group)
    return before, len(armature.data.bones)


def activate_only(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_uniform_scale(objects: list[bpy.types.Object], scale: float) -> None:
    for obj in objects:
        obj.scale = tuple(component * scale for component in obj.scale)
    for obj in objects:
        activate_only(obj)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def decimate(mesh: bpy.types.Object, ratio: float) -> None:
    activate_only(mesh)
    modifier = mesh.modifiers.new(name="PICO_LOD", type="DECIMATE")
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    while mesh.modifiers.find(modifier.name) > 0:
        bpy.ops.object.modifier_move_up(modifier=modifier.name)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def main() -> None:
    args = parse_args()
    source = Path(args.source).resolve()
    texture_dir = Path(args.textures).resolve()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    runtime_texture_dir = output.parent.parent / "Textures"
    runtime_texture_dir.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False, use_image_search=False)

    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("No armature was imported from the Mint source.")
    rig = max(armatures, key=lambda obj: len(obj.data.bones))
    source_mesh = character_mesh_for(rig)

    for obj in list(bpy.data.objects):
        if obj not in {rig, source_mesh}:
            bpy.data.objects.remove(obj, do_unlink=True)

    rig.name = "Mint_Rig"
    rig.data.name = "Mint_Rig"
    source_mesh.name = "Mint_LOD0"
    source_mesh.data.name = "Mint_LOD0"
    source_mesh.parent = rig

    rebound_textures = rebind_texture_paths(texture_dir)
    for filename in rebound_textures:
        shutil.copy2(texture_dir / filename, runtime_texture_dir / filename)
    bone_count_before, bone_count_after = prune_unused_bones(rig, source_mesh)

    height = source_mesh.dimensions.z
    if not math.isfinite(height) or height <= 0.1:
        raise RuntimeError(f"Unexpected Mint height: {height}")
    scale = TARGET_HEIGHT_METERS / height
    apply_uniform_scale([rig, source_mesh], scale)

    lod_meshes = []
    pristine_mesh = source_mesh.data.copy()
    for index, ratio in enumerate(LOD_RATIOS):
        if index == 0:
            mesh = source_mesh
        else:
            mesh = source_mesh.copy()
            mesh.data = pristine_mesh.copy()
            bpy.context.collection.objects.link(mesh)
            mesh.parent = rig
            for modifier in mesh.modifiers:
                if modifier.type == "ARMATURE":
                    modifier.object = rig
        mesh.name = f"Mint_LOD{index}"
        mesh.data.name = mesh.name
        decimate(mesh, ratio)
        lod_meshes.append(mesh)
    bpy.data.meshes.remove(pristine_mesh)

    rig["source_title"] = "Mint Swimsuit Animated- Neverness To Everness"
    rig["source_author"] = "Jun Hungry"
    rig["source_license"] = "CC-BY-4.0"
    rig["source_url"] = "https://sketchfab.com/3d-models/6dbe3e3dc6704a36bb3acef80b0bc5e4"

    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for mesh in lod_meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
    )

    report = {
        "source": source.name,
        "targetHeightMeters": TARGET_HEIGHT_METERS,
        "materials": len(source_mesh.material_slots),
        "bonesBefore": bone_count_before,
        "bonesAfter": bone_count_after,
        "texturesRebound": rebound_textures,
        "lods": [
            {
                "name": mesh.name,
                "vertices": len(mesh.data.vertices),
                "triangles": sum(len(poly.vertices) - 2 for poly in mesh.data.polygons),
            }
            for mesh in lod_meshes
        ],
    }
    output.with_suffix(".report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("AIGF_MINT_REPORT=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
