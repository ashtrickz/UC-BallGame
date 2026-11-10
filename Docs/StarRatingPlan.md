# Three-Star Rating for Levels and Random Walls

Implemented on November 9, 2026, against the refined criteria. The extra stars for shots and for mass are independent. The rating is calculated and saved on a confirmed win, before the animation plays; the presentation never changes the result.

## Criteria

| Condition | Stars |
| --- | --- |
| Defeat | 0 |
| Win | +1 |
| Win, and the shot count is at or under the level limit | +1 |
| Win, and the remaining mass is at or above the level threshold | +1 |

If only one extra criterion is met, the result is two stars, whichever criterion it is. Any win unlocks the next level.

Mass here is the linear spendable resource of the existing model: `100 × Diameter / D0`. For example, a starting resource of 1.6 with 0.8 left gives 50%. The critical size is not subtracted, and the game does not model a separate physical mass or sphere volume. The HUD budget bar keeps its existing scale up to the critical threshold.

## Level settings

[CampaignAsset.Level](../Assets/_Game/Gameplay/Config/CampaignAsset.cs) has two public fields:

- `maxShotsForStar` is the maximum shot count for the extra star.
- `minRemainingMassPercent` is the minimum percentage of the starting mass for the extra star.

Every level in `Assets/_Game/Gameplay/Config/Campaign.asset` exposes them. The initial values come from the saved control runs: the shot limit equals the control shot count, and the mass threshold is the control run's remaining mass, rounded down, minus five percentage points. Current values:

| Level | Shots at most | Mass at least |
| --- | --- | --- |
| 1 | 2 | 88% |
| 2 | 1 | 75% |
| 3 | 4 | 71% |
| 4 | 2 | 88% |
| 5 | 2 | 69% |
| 6 | 2 | 88% |
| 7 | 6 | 68% |
| 8 | 2 | 78% |
| 9 | 5 | 67% |
| 10 | 7 | 60% |

`CampaignDesigns.Write` writes the criteria when the campaign is rebuilt. A normal game launch does not recalculate them. Running the star installer again keeps the campaign criteria that are already set.

For WALL, the limits are fixed when a specific wall is built, from that wall's proven control plan. `SlimeGameConfig → Wall` exposes `StarExtraShots` and `StarMassAllowancePercent`: extra shots on top of the control count, and an allowance in percentage points on the remaining mass. The defaults are 0 and 5. SAME WALL keeps the seed, so it gets the same criteria under the same settings; after a win, both SAME WALL and NEW WALL are available.

## Architecture and saving

- [StarCriteria](../Assets/_Game/Gameplay/Core/Levels/StarCriteria.cs) is pure C#: independent comparisons and a result key, with no Unity dependency.
- `BuiltLevel` holds a snapshot of the criteria. The result key includes the mode, seed, generator version, rules, geometry and the criteria themselves, so a new layout or balance never reuses an old record.
- `AttemptContext.CaptureStars` takes the result only from the terminal model. `AttemptRunner` saves it through `IStarProgress` before the victory dive.
- [PlayerPrefsStarProgress](../Assets/_Game/Architecture/Application/Game/StarProgress.cs) stores the best result. A worse replay or a defeat never lowers the rating. The level select shows the saved stars.
- [ResultStarsPresenter](../Assets/_Game/ArchitectureIntegration/Game/Flow/ResultStarsPresenter.cs) handles cancellation and the existing AudioService. When the result is hidden, a new level starts or the scene unloads, the flight and its sound are cancelled.
- [WorldStarsView](../Assets/_Game/ArchitectureIntegration/Game/WorldStarsView.cs) animates exactly 1, 2 or 3 pre-created sprites. The stars pop out of the well, fly along an arc with a sine offset, and light up their slots on arrival. Each next sound plays at a higher pitch.
- The animation uses the shared `GameClock`, with no new `Update`, `LateUpdate` or hidden MonoBehaviour frame loop. Pause stops its time.
- The popup lives on its own Screen Space Camera canvas, and the stars on a World Space canvas with a higher sort order. A separate UI camera in the URP stack draws both after the world's post-processing, without tonemapping or colour grading. It uses a Forward renderer in the Mobile and PC pipeline assets; the world keeps its own renderer. The main camera projects the well position, while the UI camera handles the star path and size, so world zoom does not shift where the stars land in the slots.
- Reopening the result shows the existing rating without awarding it again or replaying the celebration. Changing the language only changes the text.

## Setup and checks

Filled stars share the material `Assets/_Game/Resources/UI/Star Shimmer.mat`: a slanted highlight moves from left to right. The defaults are a 12° angle from vertical, a 0.9 s pass, a 2.1 s pause and a strength of 0.7. Width, colour, angle, speed and pause are exposed on the material. Empty stars use the regular UI material. `ShimmerCoordinates` passes the icon's local coordinates in a separate UV channel, so the highlight keeps its look after atlas packing and at any star size. The effect adds no per-frame handlers.

The scenes and references are already updated. To reconfigure rendering, open Game and run `Tools/Ball Game/Gameplay/Configure Result Rendering`; this command keeps the existing layout. `Install Star Rating` is meant for the initial setup of the view and sets the starting geometry of the card and the star row; after a manual relayout there is no need to run it again. It also links the sprites and adds `StarLaunch` and `StarCollect` to the AudioLibrary, keeping existing entries.

Calculation, control-solution and key checks are in `StarCriteriaTests`; saving checks are in `StarProgressTests`; star count, pause and view cancellation checks are in `WorldStarsViewTests`. After the namespace migration and binding the relaid-out UI, the final full EditMode run on November 10 passed **231/231**. The Chinese atlas was rebuilt for the new result hints, and `LocalizationChineseFontBuilder.Validate()` passes. The result buttons now use icons, and the win explanation is localized with the missing criteria in mind. An earlier manual check of level 2 confirmed three shots against a limit of one, 62% mass against a 75% threshold, and exactly one filled star. The full PlayMode run was stopped and has not been run again; a final check of the star flight and the shader on Android is still pending.
