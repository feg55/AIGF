# Mint runtime avatar

`Model/Mint_Optimized.fbx` is generated from the private raw source under
`ForAI/` by `Tools/AvatarPipeline/prepare_mint.py`.

The raw downloads are intentionally ignored. Regenerate the runtime asset with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.0\blender.exe' `
  --background `
  --python Tools/AvatarPipeline/prepare_mint.py `
  -- `
  --source 'ForAI/_fbx_source/source/Mint Swimsuit1.fbx' `
  --textures 'ForAI/_fbx_source/textures' `
  --output 'Assets/Avatars/Mint/Model/Mint_Optimized.fbx'
```

The generated FBX contains three skinned LOD meshes and one pruned facial/body
rig. See `THIRD_PARTY_NOTICES.md` before redistribution.

Body motion is retargeted through Unity Humanoid from the CC0 runtime animation
asset at `Assets/Resources/CompanionAnimations/Mint_Motion_CC0.fbx`. The old
direct bone-rotation fallback is intentionally disabled because it overwrote
the rig pose and caused severe arm and leg deformation.
