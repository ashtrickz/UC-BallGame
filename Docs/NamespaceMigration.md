# Assembly and Namespace Migration

November 10, 2026. The root namespace is `Slime`. Assembly names, `.asmdef`/`.asmref` files, `rootNamespace`, code references and tests now follow one consistent scheme.

## Renames

| Before | After |
| --- | --- |
| Game.Application | Slime.App |
| Game.Assets | Slime.Assets |
| Game.Audio | Slime.Audio |
| Game.Composition | Slime.Composition |
| Game.Debug | Slime.Diagnostics |
| Game.Editor | Slime.Editor |
| Game.Features.Game | Slime.Gameplay.Presentation |
| Game.Features.Menu | Slime.Menu |
| Game.Foundation | Slime.Foundation |
| Game.Foundation.Tests | Slime.Tests.EditMode |
| Game.Foundation.PlayMode.Tests | Slime.Tests.PlayMode |
| Game.Gameplay | Slime.Gameplay.Core |
| Game.Gameplay.Config | Slime.Gameplay.Config |
| Game.Gameplay.View | Slime.Gameplay.View |
| Game.Gameplay.Editor | Slime.Gameplay.Editor |
| Game.Gameplay.Tests | Slime.Gameplay.Tests.EditMode |
| Game.Gameplay.PlayMode.Tests | Slime.Gameplay.Tests.PlayMode |
| Game.Localization | Slime.Localization |
| Game.Services | Slime.Infrastructure |
| Game.Settings | Slime.Settings |
| Game.SlimeLab | Slime.Prototypes.SlimeLab |
| Game.SlimeLab.Editor | Slime.Prototypes.SlimeLab.Editor |
| Game.UI | Slime.UI |
| Game.Users | Slime.Users |

Namespaces follow code ownership: the pure model is `Slime.Gameplay.Core.*`, the game flow and presentation are `Slime.Gameplay.Presentation`, the menu is `Slime.Menu`, Unity service implementations are `Slime.Infrastructure.*`, and the DI scopes and scene loader are `Slime.Composition.*`. Source folders were not moved. Internal assembly references were switched to `GUID:...`; third-party references were kept.

## Project integrity

- Assembly definition and reference files were renamed through `AssetDatabase.MoveAsset`, keeping their existing `.meta` files.
- All 669 GUIDs from the pre-migration snapshot, the assembly membership of 257 source files and all 138 internal dependencies were preserved. Platform flags, define constraints and `noEngineReferences` were kept.
- Unity resolves all 54 script types from the serialized assets of the original snapshot to the expected classes and assemblies. `m_EditorClassIdentifier` values were updated through `SerializedObject` and Unity's asset saving.
- Before the follow-up UI edit, the references of 239 components in 16 assets were checked separately. User changes to the scene and prefabs were preserved.
- Moved Unity types got `MovedFrom` attributes. The old `Game.*` names in these attributes describe the migration source and are needed for compatibility. Historical attributes from earlier migrations were kept.
- A final check of all 23 scenes and prefabs in `Assets/_Game` found no Missing Script. The active scene stayed `Game`.

## Follow-up UI edit

In the existing `Prefabs/UI/Popups/SettingsPopup.prefab`, the missing references to the three volume sliders, two FPS buttons and four language buttons were filled in. Copied translation keys and FPS arguments were fixed. The prefab GUID, hierarchy and LayoutGroup settings were kept.

In `ResultView`, the unused button text fields were removed. Whether the secondary action is available is decided by the action itself. The title and explanation stay dynamically localized; four star-criteria hints were added in four languages. The Chinese atlas was rebuilt, keeping the existing font, material and texture assets. Details are in the [localization guide](Localization.md).

## Checks

- The Editor compiles without errors. The conditional Android Player and Standalone Editor branches of `Slime.Infrastructure` were compiled separately with the installed Unity compiler; a full APK build is not part of this check.
- Final full EditMode run: **231/231**, with no skips or failures.
- The assembly graph, GUIDs, source membership, serialized types and the absence of Missing Script were checked.
- The localization checks for all three scenes and the settings, the language glyphs and the Chinese atlas pass. The tests account for the new prefab paths, the CloseButton, the icon result buttons and the GridLayoutGroup.
- The full PlayMode run was stopped and has not been run again. The final Android build check is still pending.
