using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Config;
using Game.Gameplay.Generation;
using Game.Gameplay.Geometry;
using Game.Gameplay.Level;
using Game.Gameplay.Model;
using Game.Gameplay.View;
using UnityEngine;

namespace Game.Integration.Game
{
    [Flags]
    public enum PauseReason { None = 0, Menu = 1, Settings = 2, Background = 4, Tutorial = 8 }

    public enum AttemptMode { Campaign, Wall, Endless }

    /// <summary>Identity, seed, rule snapshot, model and cancellation of one attempt.</summary>
    public sealed class AttemptContext : IDisposable
    {
        private readonly CancellationTokenSource _cancellation;
        public int Id { get; }
        public int Seed { get; }
        /// <summary>Campaign level number (1-based), or 0 for an endless (generated) attempt.</summary>
        public int LevelNumber { get; }
        public AttemptMode Mode { get; }
        public bool IsCampaign => Mode == AttemptMode.Campaign;
        public GameRules Rules { get; }
        public LevelData Level { get; }
        public AttemptModel Model { get; }
        public CancellationToken Token { get; }
        public bool IsDisposed { get; private set; }

        public AttemptContext(int id, int seed, AttemptMode mode, int levelNumber, GameRules rules, LevelData level, CancellationToken parent)
        {
            Id = id; Seed = seed; Mode = mode; LevelNumber = levelNumber; Rules = rules; Level = level;
            Model = new AttemptModel(id, rules, level);
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(parent);
            Token = _cancellation.Token;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }

    /// <summary>
    /// Game loop of the slime level: input, model commands, view actions and their confirmations.
    /// Critical transitions wait for the real end of the view action (UniTask) and are confirmed to the model with
    /// the attempt and action ids. Restart cancels the attempt's own lifetime; stale completions cannot reach a new one.
    /// Owned by <see cref="GameController"/>, so the scene keeps a single IController.
    /// </summary>
    public sealed class GameplayController : IDisposable
    {
        private readonly SlimeGameConfig _config;
        private readonly GameplayStage _stage;
        private readonly LevelGenerator _generator;
        private readonly ICampaignProgress _progress;
        private readonly WallGenerator _walls = new WallGenerator();
        private Action _primaryAction, _secondaryAction;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly System.Random _seeds = new System.Random(Environment.TickCount);
        private AttemptContext _attempt;
        private PauseReason _pause;
        private bool _busy, _initialized, _disposed;
        private int _attemptCounter;
        private double? _scriptedHold;
        private TutorialDirector _tutorial;

        public AttemptContext Current => _attempt;
        public GameplayStage Stage => _stage;
        /// <summary>The level-1 tutorial of the current attempt, or null.</summary>
        public TutorialDirector Tutorial => _tutorial;
        /// <summary>When false, level 1 starts without the tutorial (tests, demos).</summary>
        public bool TutorialEnabled { get; set; } = true;

        public void SkipTutorial() => _tutorial?.Skip();
        public PauseReason Pause => _pause;
        public bool IsBusy => _busy;
        public event Action<AttemptContext> AttemptStarted;
        /// <summary>The player asked to go back to the mode selection.</summary>
        public event Action MenuRequested;

        public GameplayController(SlimeGameConfig config, GameplayStage stage, LevelGenerator generator, ICampaignProgress progress)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public bool HasCampaign => _config.Campaign != null && _config.Campaign.Count > 0;
        public int CampaignLength => HasCampaign ? _config.Campaign.Count : 0;
        public ICampaignProgress Progress => _progress;
        /// <summary>The level the campaign continues from (the highest unlocked, or the last one once complete).</summary>
        public int ContinueLevel => Math.Min(Math.Max(1, _progress.Unlocked), CampaignLength);

        /// <summary>Creates the first attempt (level built, view bound, initial state set) and starts the frame loop.</summary>
        public void Initialize(PauseReason initialPause)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GameplayController));
            if (_initialized) return;
            _initialized = true;
            _pause = initialPause;
            _stage.Clock.Paused = _pause != PauseReason.None;
            _stage.Hud.RestartRequested += OnRestartRequested;
            _stage.Hud.PrimaryRequested += OnPrimary;
            _stage.Hud.SecondaryRequested += OnSecondary;
            _stage.Hud.MenuRequested += OnMenu;
            _stage.BackgroundChanged += OnBackgroundChanged;
            if (HasCampaign) PlayCampaignLevel(ContinueLevel);
            else PlayEndless();
            RunFrameLoopAsync(_lifetime.Token).Forget(Debug.LogException);
        }

        /// <summary>Campaign: the same layout again at full size. Wall and endless: a new seed.</summary>
        public void Restart()
        {
            switch (_attempt?.Mode)
            {
                case AttemptMode.Campaign: PlayCampaignLevel(_attempt.LevelNumber); break;
                case AttemptMode.Wall: PlayWall(); break;
                default: StartEndless(null); break;
            }
        }

        /// <summary>A freshly generated crystal wall (a new seed unless one is given for debugging).</summary>
        public void PlayWall(int? seed = null)
        {
            if (_disposed) return;
            var value = seed ?? (_config.DebugFixedSeed != 0 ? _config.DebugFixedSeed : _seeds.Next(1, int.MaxValue));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var level = _walls.Generate(_config.CreateSnapshot(), _config.Wall, value, out var rules);
            Debug.Log($"[Gameplay] wall generated in {watch.ElapsedMilliseconds} ms · {level.Report.Note}");
            Begin(level, rules, value, AttemptMode.Wall, 0);
        }

        /// <summary>Debug: an endless attempt with a concrete seed. Normal endless restarts pick a new seed.</summary>
        public void RestartWithSeed(int seed) => StartEndless(seed);

        public void PlayEndless() => StartEndless(null);

        public void NextLevel()
        {
            var next = (_attempt?.LevelNumber ?? 0) + 1;
            if (next <= CampaignLength) PlayCampaignLevel(next);
            else PlayEndless();
        }

        /// <summary>Starts a campaign level (1-based); levels above the unlocked one are not reachable.</summary>
        public void PlayCampaignLevel(int number)
        {
            if (_disposed) return;
            if (!HasCampaign) throw new InvalidOperationException("No campaign configured.");
            number = Math.Max(1, Math.Min(number, Math.Min(CampaignLength, Math.Max(1, _progress.Unlocked))));
            var level = CampaignLevels.Build(_config.CreateSnapshot(), _config.Campaign.Spec(number - 1), out var rules);
            Begin(level, rules, level.Seed, AttemptMode.Campaign, number);
        }

        /// <summary>
        /// Autoplay/debug: the current hold is released exactly when it reaches this duration (null = player-driven).
        /// Lets a scripted run replay the generator's control plan bit-for-bit through the real presentation.
        /// </summary>
        public void SetScriptedHold(double? seconds) => _scriptedHold = seconds;

        public void SetPaused(PauseReason reason, bool paused)
        {
            if (_disposed) return;
            var was = _pause != PauseReason.None;
            _pause = paused ? _pause | reason : _pause & ~reason;
            var now = _pause != PauseReason.None;
            _stage.Clock.Paused = now;
            if (_attempt != null)
            {
                var model = _attempt.Model;
                var wasCharging = model.Phase == AttemptPhase.Charging;
                model.SetPaused(now);
                if (wasCharging && model.Phase == AttemptPhase.Waiting) OnChargeCancelled(model);
            }
            // A pause or resume never turns an old touch into a shot: a fresh press is required.
            if (was != now) _stage.Input.RequireFreshPress();
        }

        private void OnBackgroundChanged(bool background) => SetPaused(PauseReason.Background, background);
        private void OnRestartRequested() => Restart();

        // ------------------------------------------------------------------ attempts

        private void StartEndless(int? requestedSeed)
        {
            if (_disposed) return;
            var rules = _config.CreateSnapshot();
            var seed = requestedSeed ?? (_config.DebugFixedSeed != 0 ? _config.DebugFixedSeed : _seeds.Next(1, int.MaxValue));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var level = _generator.Generate(rules, seed);
            Debug.Log($"[Gameplay] generated in {watch.ElapsedMilliseconds} ms");
            Begin(level, rules, seed, AttemptMode.Endless, 0);
        }

        private void Begin(LevelData level, GameRules rules, int seed, AttemptMode mode, int levelNumber)
        {
            _tutorial?.Skip();
            _tutorial = null;
            if ((_pause & PauseReason.Tutorial) != 0) SetPaused(PauseReason.Tutorial, false);
            _attempt?.Dispose();
            _busy = false;
            _scriptedHold = null;
            var context = new AttemptContext(++_attemptCounter, seed, mode, levelNumber, rules, level, _lifetime.Token);
            _attempt = context;
            Debug.Log($"[Gameplay] attempt {context.Id}: {level.Describe()}");

            var anchor = new Vector3(0, 0, (float)level.StartZ);
            _stage.Crystals.Build(level, _config.Crystals);
            _stage.Track.Build((float)level.StartZ - 1.5f, (float)level.DoorZ, (float)(rules.CorridorHalfWidth(rules.D0) * 2));
            _stage.Doors.Place((float)level.DoorZ);
            if (_stage.FinishMarker != null) _stage.FinishMarker.position = new Vector3(0, 0, (float)level.FinishZ + 0.4f);
            _stage.Slime.ResetForAttempt(anchor, (float)rules.D0);
            var colorIndex = SlimeColorPreferences.SelectedIndex(_config);
            if (colorIndex >= 0) _stage.Slime.SetColors(_config.SlimeColors[colorIndex]);
            if (_stage.PoolSplash != null) _stage.PoolSplash.Clear();
            _stage.CameraRig.Snap(anchor, (float)rules.D0, (float)rules.D0);
            _stage.Preview.Hide();
            _stage.Hud.HideResult();
            if (context.IsCampaign)
            {
                _stage.Hud.ShowSeed($"LEVEL {levelNumber}");
                _stage.Hud.ShowHint(null);
            }
            else if (mode == AttemptMode.Wall)
            {
                _stage.Hud.ShowSeed($"WALL · SEED {seed}");
                _stage.Hud.ShowHint(null);
            }
            else
            {
                _stage.Hud.ShowSeed($"ENDLESS · SEED {seed}");
                _stage.Hud.ShowHint(null);
            }
            _stage.Input.RequireFreshPress();
            context.Model.DoorsOpened += () => { if (_attempt == context) _stage.Doors.Open(); };
            context.Model.SetPaused(_pause != PauseReason.None);
            AttemptStarted?.Invoke(context);
            RunChain(context, context.Model.Start());
            if (mode == AttemptMode.Campaign && levelNumber == 1 && TutorialEnabled && _stage.Tutorial != null)
            {
                _tutorial = new TutorialDirector(this, context);
                _tutorial.RunAsync().Forget(Debug.LogException);
            }
        }

        // ------------------------------------------------------------------ per frame

        private async UniTask RunFrameLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // The loop ends quietly with the scene: cancellation is the normal way out.
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
                var context = _attempt;
                if (context == null || context.IsDisposed) continue;
                var model = context.Model;
                _stage.Input.Enabled = _pause == PauseReason.None && !_busy && model.Phase == AttemptPhase.Waiting;
                _stage.Input.Poll();
                try { Tick(context); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        private void Tick(AttemptContext context)
        {
            var model = context.Model;
            var input = _stage.Input;
            if (_pause == PauseReason.None && !_busy)
            {
                if (model.Phase == AttemptPhase.Waiting && input.PressedThisFrame && model.BeginCharge())
                {
                    _stage.Slime.BeginCharge();
                    _stage.CameraRig.OnChargeStarted((float)model.ChargeStartDiameter);
                    _stage.Hud.ShowHint(null);
                }
                else if (model.Phase == AttemptPhase.Charging && input.IsHeld)
                {
                    double delta = _stage.Clock.Delta;
                    if (_scriptedHold.HasValue) delta = Math.Min(delta, Math.Max(0, _scriptedHold.Value - model.HoldSeconds));
                    model.TickCharge(delta);
                }
                var scriptedRelease = _scriptedHold.HasValue && model.HoldSeconds >= _scriptedHold.Value - 1e-9;

                if (model.Phase == AttemptPhase.DefeatAnimation && model.Pending?.Kind == ActionKind.Defeat)
                {
                    // Held too long: Dcrit reached during the hold. The outcome is fixed; the view only shows it.
                    ClearPreview();
                    _stage.CameraRig.SettleTo((float)model.Diameter);
                    RunChain(context, model.Pending);
                }
                else if (model.Phase == AttemptPhase.Charging && (!input.IsHeld || scriptedRelease))
                {
                    var shot = model.ReleaseCharge();
                    ClearPreview();
                    _stage.CameraRig.SettleTo((float)model.Diameter);
                    RunChain(context, shot);
                }
                else if (model.Phase == AttemptPhase.Charging) ShowPreview(model);
            }

            var rules = model.Rules;
            var diameter = (float)model.Diameter;
            _stage.Slime.SetDiameter(diameter);
            _stage.Track.SetCorridorWidth((float)(rules.CorridorHalfWidth(model.Diameter) * 2));
            _stage.CameraRig.Follow(_stage.Slime.Anchor);
            var budget = (float)rules.Budget;
            _stage.Hud.ShowSize((float)(model.Diameter - rules.Dcrit) / budget, (float)(model.ChargeStartDiameter - rules.Dcrit) / budget, diameter);
        }

        private void ShowPreview(AttemptModel model)
        {
            var plan = model.PreviewShot();
            if (plan == null) return;
            var budgetLeft = model.ChargeStartDiameter - model.Rules.Dcrit;
            _stage.Slime.UpdateCharge((float)plan.ProjectileDiameter, (float)(plan.Cost / Math.Max(1e-6, budgetLeft)));
            _stage.Preview.Show(World(plan.ImpactCenter), (float)plan.BlastRadius, _stage.ViewConfig.previewColor);
            _stage.Crystals.SetPreview(plan.HitIds);
        }

        private void ClearPreview()
        {
            _stage.Preview.Hide();
            _stage.Crystals.SetPreview(null);
        }

        private void OnChargeCancelled(AttemptModel model)
        {
            _stage.Slime.CancelCharge();
            ClearPreview();
            _stage.CameraRig.SettleTo((float)model.Diameter);
        }

        // ------------------------------------------------------------------ action chain

        private void RunChain(AttemptContext context, ActionRequest request)
        {
            if (request == null) return;
            _busy = true;
            ExecuteAsync(context, request).Forget(Debug.LogException);
        }

        private async UniTask ExecuteAsync(AttemptContext context, ActionRequest request)
        {
            var model = context.Model;
            var token = context.Token;
            try
            {
                while (request != null && !token.IsCancellationRequested)
                {
                    var actionId = request.ActionId;
                    switch (request.Kind)
                    {
                        case ActionKind.Advance:
                            await _stage.Slime.MoveAsync((float)request.Advance.ToZ,
                                z => model.ReportProgress(context.Id, actionId, z), DoorGateAsync, token);
                            if (!model.ConfirmMove(context.Id, actionId)) return;
                            if (model.Phase == AttemptPhase.Won)
                            {
                                // The outcome is fixed and saved at once; the dive and a short beat play before the popup.
                                RecordWin(context);
                                var pool = _stage.FinishPool;
                                if (pool != null) _stage.CameraRig.ShowVictory(pool.position);
                                await _stage.Slime.DiveIntoAsync(pool, _stage.PoolSplash, token);
                                await _stage.Clock.Wait(0.35f, token);
                                OnWon(context);
                                request = null;
                            }
                            else request = model.Phase == AttemptPhase.DefeatAnimation ? model.Pending : null;
                            break;
                        case ActionKind.Shot:
                        {
                            var plan = request.Shot;
                            await _stage.Slime.ShootAsync(World(plan.TouchCenter), World(plan.ImpactCenter), (float)plan.ProjectileDiameter, token);
                            if (!model.ConfirmImpact(context.Id, actionId)) return;
                            request = model.Pending;
                            break;
                        }
                        case ActionKind.Clearing:
                            await _stage.Crystals.ClearAsync(request.Shot.HitIds, World(request.Shot.ImpactCenter), token);
                            if (!model.ConfirmClearing(context.Id, actionId)) return;
                            request = model.Pending;
                            break;
                        case ActionKind.Defeat:
                        {
                            Vector3? target = request.DefeatCrystalId >= 0 ? World(request.DefeatTarget) : (Vector3?)null;
                            await _stage.Slime.DefeatAsync(target, token);
                            if (!model.ConfirmDefeat(context.Id, actionId)) return;
                            OnLost(context);
                            request = null;
                            break;
                        }
                        default:
                            request = null;
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (_attempt == context) _busy = false;
            }
        }

        private async UniTask DoorGateAsync(float targetZ, CancellationToken token)
        {
            var doors = _stage.Doors;
            if (doors.IsOpen || targetZ < doors.Z - _stage.Slime.Diameter * 0.5f) return;
            // The slime never jumps through a closed leaf: movement waits until the doors are open.
            if (_attempt != null && _attempt.Model.DoorsOpen) doors.Open();
            await doors.WaitOpenAsync(token);
        }

        private void OnWon(AttemptContext context)
        {
            var model = context.Model;
            var left = Mathf.RoundToInt((float)((model.Diameter - model.Rules.Dcrit) / model.Rules.Budget) * 100);
            var stats = $"{left}% size to spare · {model.ShotsFired} shots";
            if (context.Mode == AttemptMode.Wall) ShowResult("WALL BROKEN!", stats, "NEW WALL", () => PlayWall(), "MENU", RequestMenu);
            else if (!context.IsCampaign) ShowResult("HOME AT LAST!", stats, "NEW LEVEL", PlayEndless, null, null);
            else
            {
                if (context.LevelNumber < CampaignLength)
                    ShowResult($"LEVEL {context.LevelNumber} CLEAR!", stats, "NEXT LEVEL", NextLevel, "RETRY", Restart);
                else
                    ShowResult("CAMPAIGN COMPLETE!", "The colony is home. " + stats, "WALL MODE", () => PlayWall(), "REPLAY LEVEL", Restart);
            }
            Debug.Log($"[Gameplay] attempt {context.Id} won: level {context.LevelNumber}, seed {context.Seed}, shots {model.ShotsFired}, spent {model.TotalSpent:0.###}");
        }

        /// <summary>Saves the campaign progress the moment the win is fixed (before any animation).</summary>
        private void RecordWin(AttemptContext context)
        {
            if (context.IsCampaign) _progress.MarkCompleted(context.LevelNumber, CampaignLength);
        }

        private void OnLost(AttemptContext context)
        {
            var model = context.Model;
            var why = model.Defeat == DefeatReason.Overcharge ? "Held too long: the slime got too small to hold together."
                : "Not enough jelly left for another shot.";
            if (context.Mode == AttemptMode.Wall)
            {
                var seed = context.Seed;
                ShowResult("POP!", why, "NEW WALL", () => PlayWall(), "SAME WALL", () => PlayWall(seed));
            }
            else ShowResult("POP!", why, "RETRY", Restart, null, null);
            Debug.Log($"[Gameplay] attempt {context.Id} lost ({model.Defeat}): level {context.LevelNumber}, seed {context.Seed}, shots {model.ShotsFired}");
        }

        private void ShowResult(string title, string subtitle, string primary, Action primaryAction, string secondary, Action secondaryAction)
        {
            _primaryAction = primaryAction;
            _secondaryAction = secondaryAction;
            _stage.Hud.ShowResult(title, subtitle, primary, secondary);
        }

        private void OnMenu() { _primaryAction = _secondaryAction = null; RequestMenu(); }
        private void RequestMenu() => MenuRequested?.Invoke();

        private void OnPrimary() { var action = _primaryAction; _primaryAction = _secondaryAction = null; action?.Invoke(); }
        private void OnSecondary() { var action = _secondaryAction; _primaryAction = _secondaryAction = null; action?.Invoke(); }

        private static Vector3 World(Vec2 p) => new Vector3((float)p.X, 0, (float)p.Z);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stage.Hud.RestartRequested -= OnRestartRequested;
            _stage.Hud.PrimaryRequested -= OnPrimary;
            _stage.Hud.SecondaryRequested -= OnSecondary;
            _stage.Hud.MenuRequested -= OnMenu;
            _stage.BackgroundChanged -= OnBackgroundChanged;
            _attempt?.Dispose();
            _attempt = null;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
