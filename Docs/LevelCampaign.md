# Brief: 10-Level Campaign

Build a short campaign of 10 hand-designed levels. Each level teaches the player to choose the shot strength, account for the slime shrinking, and save enough size to reach the doors. Variety comes from crystal layouts and the combination of tasks.

This brief replaces three limits from the [GDD](GDD.md): "one procedural mode", "a new seed on every attempt" and "progression is outside the first version". All other gameplay and architecture rules still apply.

## Campaign format

- Levels are played in order from 1 to 10. A win unlocks the next level; after the tenth, the game shows that the campaign is complete.
- A retry starts the same layout at full starting size. Size does not carry over between levels.
- The unlocked level number is saved between launches. For the first version, a level number, a retry button and a next-level button are enough; a campaign map is not required.
- Starting size, charge cost and rate, and the radius-to-projectile-size ratio are the same on every level. Difficulty comes from geometry.
- Layouts are stored as level data. The generator may be used for drafts, but the final geometry must be fixed and checked by hand.

## Rules to keep

Keep charging by holding, the player shrinking, the shot flying to the first obstacle in the direction of the finish, crystals breaking within the radius, and automatic jumps along the centre line. Passability depends on the current size: the track narrows together with the slime. The preview shows the real result of the shot. The doors open when the slime is within 5 metres.

Both defeats stay: reaching the critical size while holding, and running out of size to clear the path. On every level, the spendable budget must be at least 20% larger than the cost of the control run: `D0 − Dcrit ≥ 1.2 × Cref`.

## Level plan

| # | Layout | What the player should learn |
| --- | --- | --- |
| 1 | Two single obstacles with a large gap | A short tap is enough; extra charge wastes size. |
| 2 | One dense group | One bigger shot is better than a series of small ones. |
| 3 | A single crystal, a dense group, a single crystal | Charge strength has to change with the situation. |
| 4 | A central crystal and side crystals that form a narrow passage | After a small shot, the smaller slime fits between the remaining side crystals. There is no need to destroy everything. |
| 5 | Two groups with a small gap in depth | Compare one shot through both groups with clearing each separately. The geometry must make the cost difference noticeable. |
| 6 | A small blocking section next to a large side group | Breaking many crystals does not mean the shot was useful. The side group does not block the way. |
| 7 | Several narrow passages and single blockers | Save size for later sections and account for the changing width of the passage. |
| 8 | Obstacles in two layers | A small first shot lets the slime move closer and change where the next shot lands. This sequence must be cheaper than one big shot from the start. |
| 9 | A dense group, a narrow passage and sparse obstacles | Combine a big shot, enough clearing for the passage, and cheap taps. |
| 10 | A sequence of the learned situations with a narrow final passage | Plan the size budget to the doors alone. The final level adds no new rules. |

Choose the exact coordinates, sizes and distances in the scene using the current geometry rules. For levels 5 and 8, compare the cost of the alternative sequences. If the intended advantage does not appear, change the layout. Adding more of the same obstacles is not enough to raise the difficulty.

## Checks before delivery

- For each level, store the control charge sequence and check that it wins through the current model. The solution does not need to be optimal, but the 20% margin is required.
- Make sure the intended task really appears: the narrow passage lets the smaller slime through, the side group is optional, and the better sequence saves size.
- Give the player a noticeable range of suitable charge. A solution must never require releasing on one exact frame.
- Check all 10 levels by hand with the portrait camera: the relevant obstacles and the preview must be readable before the shot. If a task requires comparing two layers, both must be visible at decision time.
- Check retrying the same layout, size restoration, moving to the next level, saving progress and completing the campaign.

Deliver working levels, a short table of control-run costs, and a description of the intended idea for each level. Separate mechanics such as manual aiming, timers, coloured locks and boosters are outside this scope.
