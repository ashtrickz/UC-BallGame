# Implementation Plan: Slime and Crystals

Version 1.0. Date: November 8, 2026.

The goal is to build the first playable version from the [GDD](GDD.md), keeping the existing project framework and using SlimeLab as the visual reference.

The stages run in order. After each stage, the result must compile and have the listed proof of readiness. Specific numeric parameters are tuned during prototyping. The table below describes planned work, not features that already exist.

## Existing foundation

The project uses Unity 6000.3.25f1. At the start of work, check the current versions and the state of the files.

| Area | Entry point |
| --- | --- |
| Game scene | [Game.unity](../Assets/_Game/Scenes/Game.unity) |
| Game scene container | [GameScope.cs](../Assets/_Game/ArchitectureIntegration/Game/GameScope.cs) |
| Scene readiness | [GameEntryPoint.cs](../Assets/_Game/ArchitectureIntegration/Game/GameEntryPoint.cs), [ReadySceneScope.cs](../Assets/_Game/Architecture/Application/Scenes/ReadySceneScope.cs) |
| Current controller | [GameController.cs](../Assets/_Game/ArchitectureIntegration/Game/GameController.cs) |
| Shared container | [ProjectScope.cs](../Assets/_Game/ArchitectureIntegration/Composition/ProjectScope.cs) |
| Visual reference | [SlimeLab.unity](../Assets/_Game/Prototypes/SlimeLab/Scenes/SlimeLab.unity) |
| Reference behaviour | [SlimeLabDemo.cs](../Assets/_Game/Prototypes/SlimeLab/Runtime/SlimeLabDemo.cs) |
| Crystal creation | [SlimeLabSceneBuilder.cs](../Assets/_Game/Prototypes/SlimeLab/Editor/SlimeLabSceneBuilder.cs) |
| Assets | [IAssetService.cs](../Assets/_Game/Architecture/Assets/IAssetService.cs) and [AssetLease.cs](../Assets/_Game/Architecture/Assets/AssetLease.cs) |
| Settings and popups | The existing `ISettingsService`, `IPopupService` and `SettingsPopupController` |

The project already has VContainer, UniTask, DOTween, Cinemachine, the Input System and asset infrastructure. Use the existing tools and keep the assembly dependencies consistent. The current `GameController` serves the UI and settings; the full gameplay loop is added as a separate responsibility.

`GameScope` registers one `IController`, which starts through `GameEntryPoint`. When connecting gameplay, keep this interface resolving unambiguously: the current controller can coordinate the gameplay controller, or an explicit composition can be used. Do not add a second, ambiguous `IController` registration.

The working tree already contains user changes and untracked game files. Record the initial status before making changes. The existing untracked files are part of the project.

## Stage order

| Stage | Result | Depends on |
| --- | --- | --- |
| 0 | A verified integration point and starting state | None |
| 1 | Configuration, data and crystal geometry | 0 |
| 2 | The full game model and shared rules | 1 |
| 3 | The seed-based generator and the budget validator | 2 |
| 4 | Driven views and animation completions | 1–2 |
| 5 | The full loop of one attempt in Game | 3–4 |
| 6 | Camera, track, doors and readability | 5 |
| 7 | Restart, pause and attempt completion | 5–6 |
| 8 | Balance, generation checks and Android | 7 |
| 9 | Delivery materials and the final check | 8 |

## Stage 0: Project check and integration point

Actions:

- Read the GDD, the applicable project instructions and the current framework code.
- Check the current scenes, assemblies, dependencies and existing compile errors.
- Compare SlimeLab with the reference: jump, deformation, droplet separation, materials and crystals.
- Decide where the new gameplay classes go, with one dependency direction: from the model to shared data, and from integration to the model and the views.
- Record existing problems separately from new changes.

Ready when: it is clear how the first attempt connects before the scene readiness signal, and the current loading, settings and popups keep their working lifecycle. SlimeLab is available as the source reference.

## Stage 1: Configuration and geometry data

Implement:

- A ScriptableObject for balance and generation settings; keep view parameters separate from gameplay rules.
- Config registration through `builder.RegisterInstance(gameConfig)` in `GameScope`, and passing dependencies through constructors.
- An immutable snapshot of the attempt's parameters with a version or a fingerprint of the values.
- Level data: seed, generator version, crystal definitions and stable IDs, positions, contours, start and finish.
- Data for the control plan, the shot plan and the movement plan.
- Parameter validation: finite positive coefficients, `D0 > Dcrit > 0`, a positive minimum cost and enough budget for a control shot.
- Preparing the gameplay crystal variants and baking the convex XZ projection, accounting for the transforms of the visible meshes.
- Shared operations: circle versus convex contour, the first intersection of the projectile path with a contour, the movement corridor check and the safe stop point.

The baked contour and the visual variant share one coordinate system. Generation rotates and scales them consistently. Within an attempt, contours do not move while their crystal is alive.

Checks: touching an edge and a vertex, a hit with the crystal centre outside the circle, scale and rotation, choosing the nearest intersection, a narrow passage, and stable resolution of equal distances. In the Editor, it must be possible to see the logical contour over the gameplay variant.

Ready when: geometry calculations run without creating scene objects, and the verified contours match the visible crystals. Covers the basis of G03–G06.

## Stage 2: Game model and rules

Implement the attempt model and its explicit phases. The working set is: preparation, idle, charging, shot in progress, clearing, movement, defeat animation, victory and defeat. Pause suspends the current phase; a charge is cancelled under a separate rule.

The model owns the diameter, the current charge, the live crystals, the route position, the outcome, the attempt ID and the action ID. The views do not keep a second version of these rules.

Implement:

- The minimum shot, holding, linear size transfer and a linear blast radius.
- A shot function shared by the preview, the release and the validator.
- Freezing an immutable result on release and applying it when the hit is confirmed.
- A single defeat when the critical size is reached.
- Movement calculation after a shot with the current diameter.
- Defeat when the path is blocked and there is no size left for a minimum shot.
- The finish, the door opening threshold and commands to the views.
- Validation of action confirmations: current attempt, expected ID, allowed phase, no double application.

For movement control, the model accepts confirmed progress along the route plan; body height does not take part in the distance check to the doors. Finishing the movement phase requires the view's final signal.

Checks: the minimum tap, double charging, overcharging, the exact critical-size boundary, the refund on cancel, a free path with a small remainder, a shot that hits nothing, a stale or repeated confirmation, and both defeat conditions. The shot plan separates the projectile centre at contact from the contact point used as the blast centre.

Ready when: the full control loop can be played through model calls and synthetic completion signals. Covers G02–G08 and the logical part of G13.

## Stage 3: Seed-based generator and validator

The generator takes a config snapshot and a seed, and returns the final level data and the control plan.

Implement:

1. A local PRNG with a fixed algorithm, stable IDs and a stable traversal order.
2. Building a random plan with single obstacles and dense groups.
3. Choosing control charges with a total cost of `Cref ≤ (D0 − Dcrit) / 1.2`.
4. Placing crystal variants for the plan and dressing the rest of the layout.
5. A full replay of the plan through the model and the shared geometry on the final level data.
6. Checking that the finish is reached, a positive remainder above the critical threshold is left, and the budget holds.
7. A limited deterministic number of candidate attempts; for valid settings, a verified simplified layout when the attempts run out.

The retry limit is a number of candidates, so device speed cannot change a seed's result. An invalid config is detected separately; generation must never loop forever.

For diagnostics, store the seed, the config fingerprint, the generator version, the control plan's cost and whether a retry or the simplified layout was used. The plan replay confirms critical actions immediately instead of waiting for animations.

Checks: reproducibility, independence from random calls made by visual effects, bounded generation, the control run for a set of seeds, the budget boundaries and invalid settings. Before final delivery, check at least 1000 seeds on the final config, and print the exact inputs on any failure.

Ready when: every accepted candidate has a verified control solution. Covers G09–G10.

## Stage 4: Slime and crystal views

Move the needed SlimeLab behaviour into reusable game components. The game scene must not depend on the lab's `autoDemo` schedule, its fixed `aimPoint`, its X-axis movement limit, crystal regeneration or the cube root of mass.

Implement:

- A movement pivot and a separate visual deformation of the body.
- Wind-up, arc, squash/stretch, landing and damped wobble.
- Charging, a growing projectile, stretching of the bridge and its separation.
- Flight along a visual arc to the point from the shot plan.
- Preview highlighting, visual infection and destruction of crystals.
- A last weak jump and a pop on defeat.
- Driven pause, resume, cancel and completion of actions.

Every critical action provides one awaitable completion contract. Its source is the real end of the procedural animation, a tween or an animation event. On cancel, the wait is cancelled; a successful completion is never faked.

Crystals of the game level stay destroyed until the next attempt. Their shards and lingering effects have their own lifetime and never become new gameplay obstacles.

Ready when: the views play the given plan, keep the SlimeLab feel, report each completion once, and fully stop actions on cancel. Covers the visual part of G02, G04, G05, G07 and G13.

## Stage 5: A full attempt in the Game scene

Connect the attempt factory, the game model, the generator, input and the views through the existing scene container.

The loop controller runs this sequence:

`ReleaseCharge → separation and flight → ResolveImpact → clearing → Advance → idle or outcome`.

Completions of critical animations allow the transitions. The model does not run its own duplicate flight or landing timer. The preview and the shot plan use the same function, and resolution applies the stored plan.

Handle one active pointer. The UI is excluded from the gameplay input area. The mouse works in the Editor and touch on Android. Input is limited to the allowed phases.

The first attempt counts as ready for `GameEntryPoint.Ready` after the level is created, the views are bound and the initial state is set. Scene readiness must not wait for the whole attempt to finish. The attempt's animations and actions use its own lifetime, which outlives initialization.

Ready when: Game can be played from start to victory or defeat on a generated level, and the settings and the existing UI keep working. Covers G01–G07 and G13.

## Stage 6: Camera, track and finish

Implement:

- A portrait setup with a large view of the nearest part of the path.
- Smooth following of the slime's pivot.
- Limited partial adaptation of zoom to size.
- A small pull-back while charging and a smooth transition to the new base setup after release.
- Narrowing of the visual track together with the player's diameter.
- Doors that open at 5 metres, and the finish threshold.

Zoom has one owner of the parameter. Following and the lens or scale animation are coordinated; automatic framing and a separate tween must not overwrite the same parameter at the same time. A repeated transition starts from the current value. Camera events never block the game model.

If the doors are still visually closed when the slime reaches the passage threshold, movement waits for them to open. This keeps the slime from passing through a closed door model.

Checks: big and small slime, long and short charges, narrow passages, different portrait aspect ratios, the position of the blast area, no vertical shake from jumps, and the door threshold.

Ready when: the game screen is readable and shows the resource size, and the camera is good enough to choose a shot. Covers G06, G08 and G14.

## Stage 7: Restart and interruptions

Implement one attempt context with an ID and cancellation. A restart cancels the old context, releases its views and creates a new seed, model and settings snapshot inside Game. Shared services and the scene container stay.

Pause tracks its reasons: open settings and the app state. Lifting one reason does not resume the game while another remains. An unfinished charge is cancelled with an exact refund of size. Other actions stay paused; their waits finish only after the game actually resumes and the action ends.

When input is restored, reset any stale pointer state and require a new deliberate press. A repeated focus callback must not refund the resource twice.

Provide attempt completion through simple victory and defeat states and an available restart. To repeat a problem level while debugging, accept a specific seed; a normal restart picks a new one.

Checks: pause during charging, separation, flight, clearing, a jump and defeat; going to the background with the settings open; fast repeated restarts; late and repeated callbacks; leaving the scene. A pause after a recorded critical-size defeat must not cancel that outcome or refund the charge.

Ready when: completions of an old attempt never change the new one, resources are released correctly, and pause never charges size or creates an accidental shot. Covers G11–G12 and the lifecycle part of G13.

## Stage 8: Balance and Android check

Tune the numbers so that in regular generation both minimum and big shots make sense, the slime shrinks noticeably, and the remainder lets it reach the doors freely.

Check:

- All criteria G01–G14 on the final config.
- A set of seeds, exact agreement between the preview and the result, the control cost and the final outcome.
- Continuity of the camera and gameplay animations with different hold durations.
- Repeated attempts and resource release over a series of restarts.
- Portrait layout and touch in the simulator in use and in an Android build; then a smoke run on a Pixel 9 Pro if the device is available.
- The actual cost of generation, geometry queries, materials and effects in the profiler. Fix confirmed bottlenecks.

The minimum Android class and the target FPS are not set yet. This does not block the game prototype. Support for different portrait screens is required; checking one phone does not promise the same performance on every Android device.

Cover the maths rules, generation and the lifecycle with meaningful automated checks. Run the existing framework's tests to the extent the changes affect them; before delivery, run the applicable final project checks. Screenshots and a gameplay video complement the automated checks of visual behaviour.

Ready when: runs are reproducible, the budget is confirmed, the Android build starts and accepts touch, and significant defects are fixed or listed with exact reproduction steps.

## Stage 9: Delivery materials

Prepare locally:

- An installable Android build and launch instructions.
- The source Unity project with sources, scenes, configuration and the required `.meta` files.
- A video of a full winning run and a short defeat example, in a format suitable for YouTube.
- A short README with controls, architecture, launch steps, seed replay and check results.
- The final balance values, a few demo seeds and known limitations.

Publishing the video and sending the materials to third parties are outside this implementation task. The original brief's YouTube requirement stays a requirement of the final delivery.

Ready when: a new developer can open the project, run Game, install the build and replay a given seed by following the instructions. The GDD, the actual code and the instructions describe the same behaviour.

## Stage rules

- Do not reopen the rules agreed in the GDD to pick a technical pattern. Class names, file structure and specific parameter values are chosen on the spot.
- Keep SlimeLab as a lab. Reuse or extract the needed views while keeping the lab working; implement gameplay rules in Game.
- Limit changes outside gameplay to the necessary integration points. Account for existing GUIDs, asset loading, settings and scene readiness when moving things.
- When working with Unity, follow the applicable project instructions and the available Unity tools; manage the state of the open Editor and Play Mode safely.
- After each stage, record what was implemented, what was checked and which specific problems remain. Do not mark a stage done just because code was written.
- Do not add a booster, progression, monetization or other game systems to the first build without a separate scope change.
