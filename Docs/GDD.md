# Game Design: Slime and Crystals

Version 1.0. Date: November 8, 2026.

The agreed specification of the first playable version of UC_BallGame. The game is made in Unity for Android in portrait orientation. The player spends the slime's size on shots, clears a path between crystals and reaches the finish.

Related documents: [implementation plan](ImplementationPlan.md), [10-level campaign brief](LevelCampaign.md).

## Scope of the first version

One procedural mode with a shared set of difficulty settings. Every new attempt gets a new layout from a seed. The player's starting size is constant; generation adapts to the player's spendable budget.

The visual reference is the SlimeLab scene: a jelly slime, the plasticity of its jumps and of the projectile separating from it, and faceted crystals. The lab stays a standalone reference. The gameplay rules are implemented in the Game level.

A chain-reaction booster is kept as an expansion idea and is not part of the first version. Budget and passability are calculated for regular shots. A sequence of levels with rising difficulty is also outside the first version.

## Gameplay loop

1. The slime stops in front of a blocked section. The player looks at the crystal layout.
2. A press starts the charge: the slime shrinks and a projectile grows in front of it.
3. While the player holds, the expected blast area and the affected crystals are shown.
4. Releasing locks in the shot. The projectile separates and flies to the first obstacle in the direction of the finish.
5. When it reaches the hit point, the crystals are hit and play their destruction animation.
6. After clearing finishes, the slime automatically jumps along the free part of the centre line.
7. In front of the next blocked section the slime stops, and charging becomes available again.
8. Reaching the finish ends the attempt with a win.

The format is close to turn-based. There is no time pressure until the player starts holding. Holding too long is dangerous because it spends size. Only one shot happens at a time; a new charge is available after the movement finishes.

## Controls and charging

A short tap and release fires the minimum shot. The minimum shot has a fixed positive cost and a small blast radius. Holding grows the projectile and the cost.

The shot direction is the direction to the finish. Manual aiming is not part of the core loop.

Pausing or sending the app to the background during an unfinished charge cancels the charge and returns exactly the size invested since the hold began. After release, the shot counts as fired: a pause keeps its state, and the cost is no longer refunded.

Game charging needs its own press-and-release handler. The existing UI component `HoldButton` solves a different task (an action after a held duration) and does not define the shot rules.

## Size and shot cost

The game resource is the slime's linear size. Calculations use the diameter. Physical volume or mass conservation is not applied.

| Symbol | Meaning |
| --- | --- |
| `D0` | The player's constant starting diameter |
| `Dcrit` | The positive critical diameter of defeat |
| `D` | The player's current logical diameter |
| `S` | The projectile diameter |
| `a` | The coefficient transferring size into the projectile |
| `cmin` | The minimum shot cost, in units of player diameter |
| `b` | The blast radius coefficient |

The charge cost is `c = a × S`. The player's diameter while charging is `DchargeStart − c`. The minimum cost is part of `c` and is not charged again on release.

The minimum shot is available if, after paying its cost, the diameter stays strictly above `Dcrit`, with one shared precision tolerance. A free path to the finish does not require the ability to fire another shot.

The blast radius is `R = b × S`. Doubling the projectile diameter doubles the radius and quadruples the area of the circle. That makes a big shot worthwhile against a dense group, and a minimum shot useful against a single crystal.

An initial implementation of charge growth can use `c(t) = cmin + chargeRate × t`. The rate and coefficients are balance settings. The main requirement is continuous projectile growth while holding and proportional shrinking of the player.

Reaching `Dcrit` while charging is an immediate defeat. A safety cap that lets the player hold forever without losing does not meet this rule.

### Resource margin

The spendable budget is `B = D0 − Dcrit`. For the cost `Cref` of the control winning run:

`B ≥ 1.2 × Cref`, that is, `Cref ≤ B / 1.2`.

For example, with a spendable budget of 120, the control solution spends at most 100. The critical remainder is not part of those 120.

The control plan proves that a winning solution exists within the budget. A mathematically optimal shot sequence is not required. The player may find a cheaper solution; the player's mistakes may spend the margin and lead to defeat.

## Geometry and crystals

The rules work on the XZ plane; the scene is shown in 3D. Jumps and the projectile arc animate height. Jelly stretching and squashing do not change the logical size or passability.

Crystals are static. A regular shot destroys every crystal it hits in full. Touching the slime is dangerous. Moving forward requires a free corridor for the player's current diameter, not just a free centre line.

### Crystal contour

SlimeLab uses a faceted mesh with a hexagonal base, a taper and an offset tip. Instance tilts give a convex XZ projection of about 8–9 vertices.

For each gameplay variant, the convex XZ projection of its visible meshes is baked in advance, in the local space of the pivot. It includes the scale, height and tilts of the variant's child meshes. After baking, generation applies a consistent rotation around the vertical axis and a scale to this contour. Any change to the visual model's tilt requires a matching contour.

The contour data is stored with the variant definition. The generator and the game model work with this data without creating scene objects. Reading meshes every frame is not needed.

### Hit and blast area

While charging, a shared shot function finds the first intersection on the way to the finish, the blast centre, the radius and the list of crystals. The preview uses this list for highlighting.

On release, the calculation is frozen in an immutable shot plan. Later animation playback does not change its outcome. The hit is applied when reaching the hit point is confirmed.

The plan separates the projectile centre at first contact from the contact point on the crystal contour. The projectile centre's flight ends at the first position; the blast circle is centred on the contact point. This keeps the visible contact, the preview and the geometry consistent.

A crystal is hit if the blast circle touches or crosses its convex contour. The crystal's centre may be outside the circle. Touching the boundary counts as a hit, with one shared precision tolerance.

Destruction can be shown as a sequential wave of colouring and explosions. The hit list stays limited to the frozen area: the visual wave never spreads a chain reaction to other objects.

## Movement and track

The slime automatically moves along the centre line to the nearest safe point in front of the remaining obstacle. Safety accounts for the current logical radius and a small adjustable clearance.

Jumps are driven, with procedural deformation based on the SlimeLab reference. In the lab, the wind-up takes 0.14 s, the flight 0.78 s and the height is 1.15 units; these values are the starting point for the feel. The length of each move in the game is set by the free route. The lab's X-axis limit and its fixed test step are not level rules.

The logical movement pivot is separate from the body's vertical animation. The camera follows this pivot, keeping the frame calm during jumps.

The track narrows together with the player's diameter. It must show the corridor needed for the current size. The exact width coefficient and clearance are tuned on the prototype.

## Camera

The portrait camera smoothly follows the slime with a set offset. The player is in the lower part of the frame; the nearest obstacles, the projectile and the blast area are easy to read on a phone.

The full route does not have to fit on screen. The follow camera accepted by the user refines the original brief's setup, which had the player at the bottom left and the goal at the top right. The diagonal direction is kept as a composition guide when the goal is in frame.

Zoom partly compensates for the slime shrinking. A big slime and a small slime still look different. The adaptation range is limited.

While charging, the camera pulls back a little, smoothly. After release, it eases into the position for the player's remaining size. The transition has its own duration and easing curve; a new transition is not restarted on every `scale` change. During a hold, one continuous camera reaction animation runs.

Following and zoom animation are separate responsibilities. The camera and lingering effects never block the gameplay loop.

## Victory and defeat

The finish doors open when the slime is within 5 metres. The win counts when the slime reaches the finish threshold.

| Cause | When it is recorded |
| --- | --- |
| Charging too long | The diameter reached `Dcrit` while holding |
| Running out of shots | After a shot resolves and any available movement, the path is still blocked and there is not enough size for a minimum shot |

Early detection that the rest of the level can no longer be passed is not part of the first version. The out-of-resource check uses the actual ability to fire a minimum shot.

A free path lets the slime keep moving even with a small margin, as long as the diameter is above critical. The finish does not require another shot to be available.

Once defeat is recorded, input is blocked. The slime makes a last weak jump into the nearest blocking crystal, shrinks and pops. Then a restart becomes available. The animation itself does not decide whether defeat happened.

## Seed-based generation

Every attempt gets a seed. Reproducibility depends on the seed, the algorithm version, the settings snapshot and the crystal definitions version together.

The generator uses its own random number stream with a fixed algorithm. Effects, sound and animation variations use separate streams. Stable identifiers and the order in which objects are processed are part of the reproducibility contract.

Generation order:

1. Validate the settings and the available budget.
2. Create a random control plan with single obstacles and dense groups.
3. Choose the control shot sizes so that their total cost fits in `B / 1.2`.
4. Place the specific crystal variants with their contours.
5. Replay the plan with the shared shot and movement rules on the final layout.
6. Accept the level if the control plan reaches the finish, never reaches the critical size and stays within the budget.

Any background filling with crystals happens before the final check: extra objects may change the first hit and the free passage.

The seed and the configuration fingerprint go into the diagnostics. For debugging, a specific seed can be replayed without changing the main rule of a new seed on a regular restart.

## Architecture

The game model is plain C# classes with no dependency on scene objects, cameras, animators or Unity waits. The shared geometry calculations are used by the generator, the preview and the actual shot resolution.

The names below define responsibilities; the final class names can be aligned with the project.

| Part | Responsibility |
| --- | --- |
| Configuration | ScriptableObjects for balance and generation; separate view settings |
| Configuration snapshot | Immutable game values of a specific attempt |
| Attempt context | Attempt ID, seed, snapshot, model and cancellation of running actions |
| Game model | Size, charge, route position, live crystals, phases and outcome |
| Shot calculation | First hit, area and IDs of the crystals hit |
| Movement calculation | Available corridor and safe end point |
| Generator and validator | Level data, control plan and proof of passability |
| Gameplay loop controller | Input, model commands, starting the views, waiting for completions |
| Views | Slime, projectile, crystals, track, doors, camera, UI and effects |

The configuration is passed to `GameScope` through `RegisterInstance`. The generator and the attempt factory get it through their constructors. A snapshot is taken at the start of an attempt; editing the source game config does not change an attempt that already exists.

A new attempt is created inside the same Game scene. The scene container and shared services keep running. The previous attempt context is cancelled and released.

### Animation completion

The model owns states and rules. Completion of a critical animation allows the matching transition. The model does not keep a second, independent timer for finishing these phases.

| View signal | Model action |
| --- | --- |
| The projectile reached the hit point | Apply the frozen shot result |
| Clearing finished | Calculate and allow movement |
| Movement finished | Allow the next charge or end the attempt |
| The defeat animation finished | Allow a restart |

The controller chains actions through UniTask and the attempt context's cancellation. For SlimeLab's procedural animations, the action driver sends the signal when the action actually finishes. Confirmations are checked against the attempt and action IDs and accepted only once.

The generation validator sends the same confirmations to the model immediately, with no scene objects and no waiting for real animations. The camera, small particles and lingering wobble play in parallel.

### Pause and restart

Opening the settings or sending the app to the background pauses the gameplay loop. An unfinished charge is cancelled with an exact refund of size. Other started actions are kept during the pause and continue after all pause reasons are lifted.

Restoring input must not create a shot from an old finger release. A restart cancels the old attempt's waits and effects; its finished callbacks cannot affect the new attempt.

## Initial implementation assumptions

These details fill gaps that did not need a separate design decision. They can be refined after the first playable prototype, as long as the agreed rules above are kept.

| Detail | Initial choice |
| --- | --- |
| Route | A straight centre line on a flat surface, placed diagonally in the frame |
| Track | A visual strip on the surface; its width is the slime diameter plus an adjustable clearance |
| Input | One active pointer; presses on the UI do not start a charge; mouse in the Editor and touch on Android |
| Projectile in calculations | A moving circle in XZ with radius `S / 2`; the first contact with a crystal contour is chosen |
| Blast centre | The calculated contact point with the first crystal; the preview uses the same centre |
| Shot that hits nothing | The resource is spent, the projectile vanishes at the end point, and the hit list is empty |
| Shrinking and passage | After every resolved shot, the available corridor is checked again, including a passage that opened because the player shrank |
| Logical destruction | When the hit is confirmed, the crystals are marked destroyed; movement waits for the visual clearing to finish |
| Doors | The opening threshold is measured between the slime's pivot and the finish threshold in XZ; one world unit equals one metre |
| Missing target for the final jump | In this edge case, the defeat ends with a pop in place |
| Unsuitable generation candidate | A limited deterministic number of retries, then a verified simplified layout for a valid config |
| Initial camera projection | An orthographic setup based on the SlimeLab reference, adapted to portrait |

## Tuning parameters

The prototype tunes `D0`, `Dcrit`, `cmin`, the charge rate, `a`, `b`, the passage clearance, the route length, group variety and density, jump parameters, destruction durations, and zoom limits and curves.

Tuning these numbers is part of the implementation. The agreed rules for size, budget, generation, hits and animation waits stay.

## Acceptance checks for the finished game

| Code | Criterion |
| --- | --- |
| G01 | The portrait gameplay loop can be played from start to doors |
| G02 | A short tap fires the minimum shot; holding grows the projectile and shrinks the player linearly |
| G03 | The preview and the actual hit give the same set of crystals |
| G04 | The blast hits a whole crystal when it crosses the crystal's contour |
| G05 | The slime moves automatically along the available corridor and stops in front of blocking crystals |
| G06 | The track width and the available passage match the current size |
| G07 | Both defeat conditions work; running out of size ends with a last jump and a pop |
| G08 | The doors open at 5 metres; the finish ends the attempt |
| G09 | For an accepted layout, a control plan exists that costs no more than `B / 1.2` |
| G10 | One seed and one settings version reproduce the same level data and control plan |
| G11 | A restart creates a new attempt inside Game and isolates it from the old attempt's completions |
| G12 | Pause refunds an unfinished charge and correctly keeps the other actions |
| G13 | Critical transitions wait for animations to finish; secondary effects do not delay the loop |
| G14 | The camera follows the slime, partly adapts its scale and reacts to charge and release with smooth transitions |
