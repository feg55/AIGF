#!/usr/bin/env python3
"""Build the small, runtime-only Mint animation FBX from Quaternius UAL1.

Run through Blender, for example:
  blender --background --python Tools/AnimationPipeline/extract_quaternius.py -- \
    --source "Temp/UniversalAnimationLibrary/.../Unity/UAL1_Standard.fbx" \
    --output "Assets/Resources/CompanionAnimations/Mint_Motion_CC0.fbx"
"""

from __future__ import annotations

import argparse
import pathlib
import sys

import bpy


ACTION_NAMES = {
    "Idle_Loop": "Mint_Idle",
    "Idle_Talking_Loop": "Mint_Talk",
    "Walk_Loop": "Mint_Walk",
    "Sitting_Enter": "Mint_SitEnter",
    "Sitting_Idle_Loop": "Mint_SitIdle",
    "Sitting_Talking_Loop": "Mint_SitTalk",
    "Sitting_Exit": "Mint_SitExit",
    "Interact": "Mint_Greet",
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def short_action_name(name: str) -> str:
    return name.rsplit("|", 1)[-1]


def main() -> None:
    args = parse_args()
    source = pathlib.Path(args.source).resolve()
    output = pathlib.Path(args.output).resolve()
    if not source.is_file():
        raise FileNotFoundError(source)

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True)

    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one armature, found {len(armatures)}")
    armature = armatures[0]

    selected = {}
    for action in list(bpy.data.actions):
        source_name = short_action_name(action.name)
        output_name = ACTION_NAMES.get(source_name)
        if output_name is None:
            bpy.data.actions.remove(action)
            continue
        action.name = output_name
        action.use_fake_user = True
        selected[output_name] = action

    missing = sorted(set(ACTION_NAMES.values()) - set(selected))
    if missing:
        raise RuntimeError(f"Missing required source actions: {', '.join(missing)}")

    for obj in list(bpy.data.objects):
        obj.select_set(False)
        if obj != armature:
            bpy.data.objects.remove(obj, do_unlink=True)

    armature.name = "MintMotionSource"
    armature.data.name = "MintMotionSource_Rig"
    armature.animation_data_create()
    armature.animation_data.action = None
    for track in list(armature.animation_data.nla_tracks):
        armature.animation_data.nla_tracks.remove(track)

    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX export failed: {result}")

    print(f"Exported {len(selected)} CC0 actions to {output}")
    for action_name in sorted(selected):
        action = selected[action_name]
        print(f"  {action_name}: {int(action.frame_range[0])}-{int(action.frame_range[1])}")


if __name__ == "__main__":
    main()
