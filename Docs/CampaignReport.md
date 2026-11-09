# Campaign and Wall Mode Report

This report covers the [10-level campaign brief](LevelCampaign.md) and the extra Wall mode. All levels share one balance: `D0 = 1.6`, `Dcrit = 0.45`, `B = 1.15`, `cmin = 0.05`, charge rate `0.24/s`, `a = 0.5`, `b = 2.2`. The margin requirement is `1.2 × Cref ≤ 1.15`, so `Cref ≤ 0.958`.

## Modes

- **LEVELS** opens a 5×2 level grid. Completed levels are green, the next level is highlighted, and locked levels are unavailable. Picking a cell starts that level. A win unlocks the next level, and the unlocked level number is saved between launches (`PlayerPrefs`, key `campaign.unlocked`). RETRY replays the same layout at full size. After level 10 the game shows "CAMPAIGN COMPLETE!" and moves on to Wall mode. Levels have no explanatory text or names in game; the HUD shows only "LEVEL N".
- **WALL** builds a new wall every time. The whole camera frame is covered with crystals (about 250–310 of them), and the player has to break a path to the doors. RETRY and NEW WALL draw a new seed; after a defeat, SAME WALL is also available.
- The menu has WALL and LEVELS buttons to choose a mode. The result panel has a MENU button.

## Level table

The control sequence lists hold durations in seconds (0 means a short tap). A window is the range of hold times that hits the same set of crystals while the rest of the sequence still wins. The narrowest window is 0.15 s, which is about 9 frames at 60 FPS.

| # | Name | Intended idea | Control (holds, s) | Cref | Windows, s | Alternatives compared |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | First Taps | Two single crystals: a tap is enough, and extra charge wastes size | 0 / 0 | 0.100 | 0–3.57; 0–3.57 | Optimum 0.100 |
| 2 | The Cluster | One dense group: one bigger shot | 1.06 | 0.304 | 0.96–3.78 | A series of small shots (≤ 0.3 s) costs 0.694; taps alone cannot pass |
| 3 | Mixed Strength | Tap → big shot at the group → tap | 0 / 0.21 / 0.55 / 0 | 0.382 | 0–2.40; 0.11–0.40; 0.45–2.95; 0–2.40 | Taps alone cannot pass |
| 4 | Squeeze Through | Remove only the centre; the smaller slime fits between the side crystals | 0 / 0 | 0.100 | 0–0.31; 0–1.21 | Side crystals stay intact (tested) |
| 5 | Two Rows | Two wide groups with a gap: clearing them separately is cheaper | 0.66 / 0.65 | 0.414 | 0.56–1.49; 0.56–2.91 | One shot through both costs 0.532; separately, 0.369 |
| 6 | Tempting Crowd | The side crowds do not block the way: tap only the blocker | 0 / 0 | 0.100 | 0–0.84; 0–0.82 | Two 1 s shots that clear the crowds cost 0.580 |
| 7 | Tight Corridors | Three narrowing passages: shrink in advance, but do not overspend | 0.15 / 0.20 / 0 / 0 / 0 / 0.17 | 0.425 | ≥ 0.27 each | The passage walls stay intact |
| 8 | Two Layers | Small first shot → move closer → second shot at the back layer from close range | 0 / 0.66 | 0.258 | 0–1.92; 0.56–3.57 | A big shot from the start costs 0.633; a small first shot, 0.234 |
| 9 | Everything Together | Big shot at the group, enough shrinking to pass, taps | 0.21 / 0.55 / 0 / 0 / 0 | 0.432 | ≥ 0.29 each | Taps alone cannot pass |
| 10 | The Way Home | All situations in a row; the last passage is the narrowest | 0 / 0 / 0.26 / 0 / 0 / 0.34 / 0.21 | 0.544 | ≥ 0.19 each | Taps alone cannot pass; `B / Cref = 2.1` |

Difficulty grows with the mix of tasks and the cost: from 0.10 (levels 1, 4 and 6 each teach one idea) to 0.54 in the finale. The mechanics are the same on every level.

## How the numbers were obtained and checked

- The layouts are described in `Assets/_Game/Gameplay/Editor/CampaignDesigns.cs` and written to `Assets/_Game/Gameplay/Config/Campaign.asset` with the menu command `Tools/Ball Game/Gameplay/Campaign/Write Campaign Levels`. The game loads levels only from the asset.
- `CampaignSolver` (in the core) searches the distinct shot outcomes on a hold ladder with a 0.01 s step, pruning by cost, and finds the cheapest winning sequence. With constraints, it also gives the cost of the alternatives. The control sequence is the cheapest one whose windows are all at least 0.15 s.
- The analysis report is available from `Tools/Ball Game/Gameplay/Campaign/Analyse Designs`.
- `CampaignTests` (EditMode) check 10 levels on the shared balance: each level wins through the model with a 20% margin, each control shot has a window of at least 0.15 s, layouts repeat exactly, and the lessons of levels 1, 2, 4, 5, 6 and 8 and the passages of levels 7, 9 and 10 hold.
- The PlayMode test `Campaign_progresses_repeats_and_completes` checks that locked levels are unavailable, a win unlocks and saves the next level, a retry replays the same layout at full size, the player can move on, and the campaign completes.

## Wall mode

- `WallGenerator` uses a jittered hexagonal grid across the frame width (±4.6 m). Side crystals are always placed (fill 0.88) and never block the corridor. The central band (±1.45 m) blocks the path for 6.2 m of depth, and its fill comes in waves along the route. The average central density is tuned to the budget with a greedy control plan plus a replay through the model, at most 16 candidates, and then a simplified fallback. The route is 14 m long.
- Changing the density does not move any crystal. All random numbers are consumed in the same order; the density only decides whether a crystal is placed.
- Over 200 seeds, every wall is proven with a 20% margin, the fallback is never used, and generation takes at most 43 ms in the Editor. On average the central density is 0.78, a wall takes 4.2 shots, and the control plan uses 62–100% of the `B/1.2` limit.
- Settings live in `SlimeGameConfig → Wall`. The wall settings fingerprint is logged together with the seed.

## Not verified and known limitations

- A manual run through all 10 levels with the portrait camera was not completed, because the automated capture was interrupted. Screenshots were taken of the first stops, the menu and Wall mode. They show that on level 8 both layers are visible at decision time: the back layer is 2–3 m away and the camera looks 3.1 m ahead.
- The campaign solver runs in the Editor (seconds per level) and is not used in the game.
- On a phone, a wall is about 300 crystal objects. The profiler has not been run on a device.
