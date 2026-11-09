# Addressables: Content Inventory and Packing Plan

Audit date: November 9, 2026. Unity 6000.3.25f1, Addressables 2.10.3, Android.

After this audit, a texture, atlas and audio import optimization was applied. The original audio settings described below are from before that optimization; the Player/GameContent split plan does not change because of it.

**For the current size of the game, one `GameContent` group with `Pack Together` is enough.** There is no reason yet to split content by level, individual sound, slime or environment: the levels are data of one campaign, and the main resources are shared. `Loading` and the minimal startup resources stay in the Player.

**Moving content into local Addressables does not by itself make the APK smaller.** The bundles still ship with the app; only the packing and load control change. Savings must be measured after duplicates are removed. If size is the only goal, the migration is optional: checking fonts and audio import first is more useful. Moving content out of the initial download is possible through remote distribution, at the cost of a download on first launch. See [dependencies and delivery options](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/AssetDependencies.html) and [remote content](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/RemoteContentDistribution.html).

The per-file breakdown is in [AddressablesAssetPlan.csv](AddressablesAssetPlan.csv). It is a **migration plan**, not applied Unity settings. The audit did not change scenes, references, importers or existing Addressables groups.

## Project findings

- The only existing group, `Default Local Group`, is empty (`m_SerializeEntries: []`).
- The Player includes `Loading.unity`, `Menu.unity` and `Game.unity`.
- `BootstrapEntryPoint` runs initialization and opens Menu. `AssetService.InitializeAsync` initializes Addressables but does not preload game content.
- `UnitySceneLoader` uses `SceneManager.LoadSceneAsync`, `Application.CanStreamedLevelBeLoaded` and regular scene unloading. Removing Menu and Game from the Scene List without changing the loader would break navigation.
- `ProjectScope.Configure` synchronously reads `Localization/Fonts`, `Localization/LocalizationConfig` and `Audio/AudioLibrary` from Resources. `SettingsPopup.prefab` is assigned by direct reference in Loading. The `audioLibrary`, `audioMixer` and `introVideoPrefab` fields in the saved Loading scene are empty; the audio library comes from Resources, and the mixer comes through it. No intro video is connected.
- `AudioLibrary` holds direct references to **68 WAV files and one MP3**. The hidden legacy AssetReference fields are empty. The menu and gameplay both use `Curious Pattern.mp3`, so two music groups are not needed right now.
- `SlimeGameConfig` references `Campaign` and `CrystalCatalog`; Menu and Game use the same config. Geometry, materials and the slime are also shared. Campaign levels are not separate scenes or downloadable file sets.
- The production game depends on `Prototypes/SlimeLab/Shaders/SlimeJelly.shader` and `CartoonCrystal.shader`. The folder is called Prototypes, but these two shaders must not be excluded.

## One group and the actual bundles

| Setting | Proposal |
| --- | --- |
| Group | `GameContent` |
| Bundle Mode | `Pack Together` |
| Build / Load Path | `LocalBuildPath` / `LocalLoadPath` |
| Compression | LZ4 |
| Include in Build | On |
| Asset Load Mode | Requested Asset And Dependencies |
| Label | `startup` only for explicit entries of regular assets; scenes do not get this label |
| Player Scene List after migration | `Loading` only |

Unity packs scenes separately from regular assets even within one group. So Menu and Game produce a scene bundle, the regular assets produce a data bundle, and shader bundles may add more files. **One group here does not mean one physical `.bundle`.** See [how groups are packed into bundles](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/PackingGroupsAsBundles.html).

`Default Local Group` can be renamed to `GameContent` during implementation. Separate Shared/Audio/Environment/Level01–10 groups are not needed for the current scenario. When all content is kept in memory, one group has no real lifetime drawback; if languages or large independent sets ever need unloading, the group boundaries will need another look.

## What goes where

All paths below are relative to the project root. The full list of files, addresses and conditions is in the CSV.

| Files | Group / role | How to use |
| --- | --- | --- |
| `Assets/_Game/Scenes/Loading.unity` | Player, not Addressable | The only startup scene |
| `Assets/_Game/Architecture/Application/Loading/CrystalTransition.mat`, `.shader`, `CrystalTransitionAtlas.png` | Player | The transition screen must work before the catalog loads and on errors |
| `Assets/_Game/Art/UI/bar_empty.png`, `bar_filled.png` | Player | The loading bars' own sprites, outside the game atlas |
| `Assets/_Game/Resources/Localization/LocalizationConfig.asset`, `fallback.json` | Player / Resources | The small config and the loading/error texts are available at once |
| `Assets/Resources/DOTweenSettings.asset`, TMP Settings, line-breaking rules and the Default Style Sheet | Player / Resources | Service settings; clear the TMP Settings references to the game fonts being moved |
| `Assets/_Game/Scenes/Menu.unity` | GameContent, `scenes/menu` | Load through Addressables when entering the menu |
| `Assets/_Game/Scenes/Game.unity` | GameContent, `scenes/game` | Load through Addressables when starting the game |
| `Assets/_Game/Gameplay/Config/SlimeGameConfig.asset` | GameContent, `config/game` | Shared rules, colours and references to the campaign and crystals |
| `Assets/_Game/Gameplay/Config/Campaign.asset`, `CrystalCatalog.asset`, `SlimeViewConfig.asset` | GameContent, `config/campaign`, `config/crystals`, `config/view` | All levels and view settings together |
| `Assets/_Game/Prefabs/Gameplay/Slime.prefab` | GameContent, `prefabs/slime` | The slime shared by the menu and the game |
| `Assets/_Game/Prefabs/UI/SettingsPopup.prefab` | GameContent, `ui/settings` | Remove the direct reference from Loading; instantiate after preloading |
| `Assets/_Game/Gameplay/Art/*`, the Canyon prefabs/models/materials in use and the two SlimeLab shaders | GameContent | Shared dependencies are listed in the CSV; do not add whole folders |
| `Assets/_Game/Art/UI/CartoonContour/CartoonContour.spriteatlasv2` | GameContent, `ui/cartoon-atlas` | Include the atlas explicitly; do not mark its 20 source sprites separately |
| `Assets/_Game/Art/UI/MenuBackground.png`, `SlimeLogo.png`, `popup_panel.png`, `Assets/_Game/Textures/SlimePreviewTexture.renderTexture` | GameContent | Background, logo, panel and the preview RenderTexture |
| `Assets/_Game/Resources/Audio/AudioLibrary.asset` | GameContent, `audio/library`, after moving | Proposed new path: `Assets/_Game/Content/Audio/AudioLibrary.asset` |
| `Assets/_Game/Audio/Selected/**` and `RootMixer.mixer` | Implicit GameContent dependencies | Included through the AudioLibrary; clips do not need their own addresses |
| `Assets/_Game/Resources/Localization/Fonts.asset` | GameContent, `localization/fonts`, after moving | Proposed new path: `Assets/_Game/Content/Localization/Fonts.asset` |
| The DynaPuff/LiberationSans SDF assets in use, their materials and source TTFs; `Assets/_Game/Art/Fonts/Localization/*` | GameContent | The full current font set, including Chinese; move the files out of Resources first |
| AppIcon, URP/input/project settings | Player / platform | App build settings and resources, not game preloading |
| Code, asmdefs, plugins | Player compilation | Not Addressables content |
| Unused prototypes, samples and other assets | No group entry | A file being in Assets does not by itself put it in the Player |

It is better to include the atlas explicitly: otherwise its texture may be duplicated across prefab bundles. See the [SpriteAtlas documentation](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/AddressablesAndSpriteAtlases.html). The result must be confirmed with the Build Layout, including implicit sprite objects.

The CSV lists 323 content files from Assets, excluding `.meta` files, code and assemblies:

| Action | Files | Meaning |
| --- | ---: | --- |
| `MARK_AFTER_MIGRATION` | 82 | Proposed explicit GameContent entries; meet the migration conditions first |
| `IMPLICIT_DEPENDENCY` | 70 | AudioLibrary dependencies: 68 WAV files, one MP3 and the mixer; no separate entry needed |
| `ATLAS_MEMBER` | 20 | CartoonContour sprites, included with the atlas |
| `KEEP_BUILTIN` | 13 | The minimal bootstrap and service Resources |
| `KEEP_PLATFORM_OR_ENGINE` | 68 | Platform and system resources, not game content |
| `REVIEW_UNUSED_RESOURCE` | 5 | Extra TMP Resources for a separate check |
| `NO_CONTENT_ENTRY` | 65 | No serialized game dependency found; do not add to Addressables |

The 82 explicit entries are a conservative starting point. Besides the loadable roots, they include scene dependencies that could otherwise end up in both the scene bundle and the data bundle. After a real Build Layout, some entries can be removed. This is no reason to add all of Assets or all Art folders to the group.

In the CSV, `CurrentAndroidBuildInput` means the file was among the inputs of the cached build, not proof that every object in it is in the APK. `SourceBytes_NotPackedSize` is the source size, not the bundle size or RAM use.

## Changes needed before applying the plan

1. **Free the bootstrap from game content.** Remove the direct SettingsPopup reference and the references to game fonts and materials from Loading. Keep the transition and the loading bars built in. Text shown before preloading needs a small standalone bootstrap font without the Noto/DynaPuff fallback chains; assign the game fonts after loading. That bootstrap font does not exist yet and is not in the CSV.
2. **Break the chains through Resources.** Move AudioLibrary, Fonts and the game TMP assets out of Resources together with their `.meta` files, keeping the GUIDs. Update `ProjectScope` and `LocalizationFonts`: the current `Resources.Load` / `Resources.LoadAll` calls will no longer find them. Before GameContent loads, `TMP Settings.defaultFontAsset` and the global fallback references must point only to the bootstrap set. Font materials must not keep the game set in the built-in Player either.
3. **Add explicit content preloading.** After AssetService initializes, run a `ContentInitializationStep` that loads the regular assets labelled `startup`, keeps their leases/handles and fills a shared container of loaded references. Then set up the fonts and initialize AudioService. Today the services get the library and fonts synchronously in `ProjectScope.Configure`, so the order of construction and access has to change; a new label alone is not enough.
4. **Switch the scene loader.** `UnitySceneLoader` must use `Addressables.LoadSceneAsync` and the matching unload, keep the scene handles, and check the address in the catalog instead of `CanStreamedLevelBeLoaded`. Keep additive loading, `LifetimeScope.EnqueueParent`, the Transit scene and the wait for ReadySceneScope. Only then remove Menu and Game from the Player Scene List. Update the `SceneFlowInstaller` (`Assets/_Game/Gameplay/Editor/SceneFlowInstaller.cs`) so running it again does not put the scenes back into the built-in list.
5. **Apply the CSV entries through the Editor API**, set the group, schema and addresses, build Addressables for Android, then build the Player. Do not edit the settings YAML by hand. Do not give scenes the `startup` label used by `LoadAssetsAsync<Object>`.

Marking an asset Addressable does not replace regular serialized references. If a built-in scene still references it directly, a copy stays in both the Player and the bundle. The same applies to Resources and dependency chains. See [loading scenes and duplicates](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/LoadingScenes.html).

The TMP Resources contain five candidates for a separate cleanup: `LiberationSans SDF - Drop Shadow.mat`, `LiberationSans SDF - Outline.mat`, `EmojiOne.asset`, its `EmojiOne.png` and `TMP_Sprite.shader`. They enter the graph through Resources/TMP defaults, not through the game scenes. Before excluding EmojiOne, remove the `TMP Settings.defaultSpriteAsset` reference and check the real texts. The audit does not delete these files.

## What "load everything in the bootstrap first" means

For the current requirement, the shared set of regular assets can be loaded in the bootstrap and kept until the app closes. Direct references inside the already loaded AudioLibrary can stay as they are: there is no need to turn every SoundDefinition into an AssetReference.

Initializing Addressables, downloading bundles and loading objects are different operations. `InitializeAsync` prepares the system and the catalog. For remote content, `DownloadDependenciesAsync` puts the files in the cache but does not create game objects or replace loading the assets into memory. Local bundles need no download. See [pre-downloading](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/remote-content-predownload.html).

On the next app launch, resources are loaded into memory again. Remote content may not need a new download while the right version is cached; clearing the cache or a new version requires downloading the files again.

Open Menu and Game through navigation. Do not activate both scenes at once just to preload them: that starts their scopes and entry points and breaks the current lifecycle. Do not keep Game loaded with `activateOnLoad=false` waiting for a button press: such an operation blocks the async load queue. This limitation is described in [LoadingScenes](https://docs.unity3d.com/Packages/com.unity.addressables@2.10/manual/LoadingScenes.html).

Regular content handles belong to the persistent ProjectScope and are released on its Dispose; scene handles belong to the scene loader. On errors and retries, do not keep piling up new holds. Preloading does not replace pool creation, building dynamic TMP atlases or shader warm-up: the first attempt is still ready only when ReadySceneScope says so.

## What can really affect size

The examined existing `SlimeHomecoming.apk` is **70,076,900 bytes (66.83 MiB)**. The file was modified on November 9 at 16:56:32 Kyiv time; SHA-256: `7968c5054694980bb67f38ad08de97abe8c8ce0ac16a0c936366841aeb036b4b`.

| APK entry | Size inside the ZIP, MiB |
| --- | ---: |
| `assets/bin/Data/data.unity3d` | 23.27 |
| `lib/arm64-v8a/libil2cpp.so` | 21.19 |
| `lib/arm64-v8a/libunity.so` | 10.07 |
| `assets/bin/Data/resources.resource` | 4.59 |
| `assets/bin/Data/Managed/Metadata/global-metadata.dat` | 2.96 |
| `classes.dex` | 2.25 |

Addressables does not move IL2CPP, the engine, metadata or dex into content. That alone means moving assets cannot shrink the whole APK in proportion to the size of the source Art folder. `data.unity3d` is mixed packed data; the table above is not a per-asset size report.

The largest source resources:

- `NotoSansCJKsc-Regular.otf`: 16,437,364 bytes, **15.68 MiB**. The Chinese TMP asset is Dynamic (`m_AtlasPopulationMode: 1`), and the source OTF importer has `includeFontData: 1`. Simply deleting the source would break generation of new glyphs. Possible savings: prepare a sufficient static character set validated against the localization, or move Chinese into a separate loadable group and stop keeping it in the shared fallback chain. Neither option is applied here.
- `NotoSans Multilingual SDF.asset`: 3,365,267 bytes of source YAML. That is not its final size in the Player: dynamic atlas data may be cleared at build time, and the serialization differs.
- `Curious Pattern.mp3`: 4,030,090 bytes of source. The final size depends on the AudioImporter; an MP3 extension does not mean the source format is kept in the Player. The `SelectedAudioLibraryInstaller` sets the WAV effects to PCM / DecompressOnLoad / preload. Import and compression are worth checking separately, and running the installer again must keep the chosen policy.
- `MenuBackground.png` and `SlimeLogo.png`: 1,825,851 and 1,354,838 bytes of source. To save space, check the Android texture format and the actual texture sizes, not just the PNG on disk.

In the base plan, all active languages stay in GameContent, as does all the shared content. A separate `Localization.zh-Hans` group only makes sense if the language is actually loaded on demand; if it is loaded for everyone at startup anyway, it is just one more packing boundary.

## Verification and audit limits

The audit covered the loading and composition code, the saved scenes, the Addressables settings, the GUID references of assets and their importers, the packable atlas folder, the inputs of the cached Android build and the ZIP of the existing APK. The CSV lists real files; the source sizes are not summed as a build size forecast. The GUID graph does not fully account for importer transforms, stripping and object dependencies inside FBX files and atlases, so the number of explicit entries is a starting plan.

`unity status` found the Editor but reported `STATUS_ALL_UNREACHABLE`; the Pipeline command search responded, but a read-only `script eval` did not finish within 30 seconds. Running outside the sandbox did not fix this. The CLI version check failed with `ERR_NETWORK`. So `AssetDatabase.GetDependencies`, Analyze, a fresh Build Layout, a new Android build and post-migration behaviour checks were not run in this audit. Existing user edits were not overwritten.

After implementation, check on Android: the bootstrap offline with local content, the Menu → Game → Menu transitions and a second Game, settings, music and effects, all languages and handling of a failed load. Compare clean builds with identical Player settings; in Analyze/Build Layout, check duplicates between scenes, Resources and bundles, especially TMP, the UI atlas and shared materials. Only such a comparison gives the real savings in bytes.
