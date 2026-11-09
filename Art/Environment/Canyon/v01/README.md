# Canyon environment kit

Created in Blender 5.2.3 LTS through the installed Blender Lab MCP bridge and integrated into `Assets/_Game/Scenes/Game.unity`.

## Art direction

- Rounded, faceted sandstone canyon, following the existing menu background and candy crystals.
- Sand `#EDBD83`, sandstone `#CF916D`, light edges `#F2CC9A`, olive foliage `#7B9B54`, mint doors `#4C9991`, cream accents `#F6D69A`.
- Warm sunlight, restrained cool shadows, clear route center. Tall rocks stay on the far bank; the near bank stays low.

## Files

- `UC_Canyon_Kit.blend`: editable source and arranged asset studio.
- `tools/create_canyon_assets.py`: deterministic Blender modeling/export source.
- `asset-manifest.json`: source-space bounds and triangle counts for all 14 models.
- `unity-validation.json`: actual Unity import comparison against the manifest and signed axis probe.
- `wall-smoke.json`, `normal-route-smoke.json`: successful control-plan playthroughs through the real presentation.
- `kit-preview.png`, `gameplay.png`, `charging.png`, `closeup.png`, `finish.png`, `mobile-profile.png`: reviewed images. The closeup and finish use temporary review cameras; gameplay and charging use the actual gameplay camera.

Exported models, Unity materials and prefab assets are in `Assets/_Game/Art/Environment/Canyon`. No `.blend` file is placed under Assets, so importing/building the Unity project does not invoke a local Blender installation.

## Models

| Model | Purpose |
| --- | --- |
| GroundTile | Flat Y=0 surface, 6 m repeat along the route |
| TrailBank | Low sandy edge |
| RockWide / RockSmall / RockTall / CliffChunk | Four reusable rock silhouettes |
| SucculentA / SucculentB / GrassTuft | Opaque low-poly foliage |
| ColonyGateFrame | Beveled stone arch and footings |
| ColonyDoorLeft / ColonyDoorRight | Separate hinged leaves with outward-edge pivots |
| ColonyBasin | Stone rim around the existing jelly pool |
| StartMedallion | Flat starting marker |

Total: **14 unique models, 5,736 triangles**, plus the separate diagnostic AxisProbe model. All use vertex colors and a shared opaque canyon shader; they require no texture downloads or third-party packages. Instance count and total visible triangles depend on the level's existing crystal field and camera.

## Axis, scale and normals verification

The modeling script uses semantic Unity coordinates: X right, Y up, +Z along the route. It stores those in Blender as `(x,z,y)`: Blender +Y is route-forward and +Z is up. Face winding is corrected for that reflection, normals recalculated, and geometry triangulated before export.

FBX settings: `axis_forward='-Z'`, `axis_up='Y'`, global scale 1, metric units, `FBX_SCALE_UNITS`, `bake_space_transform=True`. The scoped Unity AssetPostprocessor sets `bakeAxisConversion=true`, scale factor 1, unit conversion on, imported normals, no animation/cameras/lights/colliders, and no duplicate embedded materials.

The first export test exposed reversed Z; it was corrected before exporting the kit. The final AxisProbe has cubes at semantic origin, +X=1, +Y=3, +Z=2. Unity must report bounds min `(-0.05,-0.05,-0.05)`, max `(1.05,3.05,2.05)`. Every model's imported bounds match the Blender manifest within 2 mm, every imported transform has zero translation/rotation and unit scale, and all GroundTile normals point +Y. Degenerate leaf tips found during validation were remodeled before final export.

Reference: [Unity Model Import Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/FBXImporter-Model.html) and [ModelImporter.bakeAxisConversion](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ModelImporter-bakeAxisConversion.html).

## Integration and maintenance

- `Tools > Ball Game > Environment > Install Blender Canyon` rebuilds only this art dressing, materials and prefabs, then saves Game.
- `Tools > Ball Game > Environment > Validate Blender Axis And Models` checks the imported kit outside Play mode.
- The existing Gameplay scene installer calls the canyon installer when the model assets are present, so reinstalling gameplay also restores the environment.
- Existing GameplayStage references, lane width behavior, crystal geometry/contours, door hinge transforms, Living pool target, victory dive and splash systems remain connected. Decorative models have no colliders.
- The background cube top is below the route at Y=-0.1. The canyon shader selects the shadow cascade per pixel, avoiding incorrect interpolation across the large floor.
- Art installation is idempotent: the named canyon dressing and gate/colony art are replaced on repeat, rather than accumulated.

To regenerate the source/export with Blender open and its MCP bridge listening:

```powershell
python Art/Environment/Canyon/v01/tools/blender_bridge.py --file Art/Environment/Canyon/v01/tools/create_canyon_assets.py
```

Then let Unity import, run Validate, and run Install. The generator owns only its `Canyon Asset Studio` scene in Blender; it does not reset user preferences.

## Review

- WALL, seed 424242: Won, 4 shots, cost 0.848 matching the control plan, both doors open.
- Normal generated route, seed 424242: Won, 5 shots, cost 0.898 matching the control plan, final dive reaches the preserved pool target.
- Actual `Mobile_RPAsset` visually checked in the Editor (render scale 0.8, hard shadows); original PC pipeline restored afterward. This is shader/import compatibility evidence, not a device frame-rate benchmark.
- No missing/default/error materials on enabled stage renderers; no decorative colliders; zero current Console errors and zero canyon shader compilation messages.
- Scoped visual score: palette 4/5, style consistency 4/5, lighting 3/5, gameplay silhouette 3/5, existing UI 3/5, charge feedback 3/5. The strongest remaining visual follow-up is the existing HUD and a slightly more frontal gameplay view of the slime's face.

Craft references used: `art-direction`, `scene-lighting`, `toon-shading`, `placeholder-assets`, `camera-rigs`, `visual-scorecard`; the existing camera rig and UI were retained. No packages were installed.

The pre-integration tracked working tree was retained at Git ref `refs/codex/backups/pre-canyon-environment`; exact scene/installer copies are in the ignored `Temp/CanyonEnvironmentBackup` directory. Earlier unrelated user edits were not reverted.
