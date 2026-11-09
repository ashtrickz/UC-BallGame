using Game.App.Loading;
using Game.Foundation;
using Game.App.Scenes;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Game.App.Bootstrap
{
    public sealed class BootstrapEntryPoint : IStartable, IDisposable
    {
        private const string GameLoadingLabel = "loading.game";
        
        private readonly IReadOnlyList<IInitializationStep> _steps;
        private readonly IInitializationModel _model;
        private readonly ILoadingScreen _loading;
        private readonly GameNavigation _navigation;
        private readonly IIntroScreen _intro;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private int _nextStep;
        private bool _running;
        private bool _disposed;

        public BootstrapEntryPoint(IReadOnlyList<IInitializationStep> steps,
            IInitializationModel model, ILoadingScreen loading, GameNavigation navigation, IIntroScreen intro)
        {
            _steps = steps;
            _model = model;
            _loading = loading;
            _navigation = navigation;
            _intro = intro;
            loading.RetryRequested += Retry;
        }

        public void Start() => RunAsync().Forget(Debug.LogException);

        private void Retry()
        {
            if (!_disposed && _model.HasError && _model.CanRetry && !_running)
                RunAsync().Forget(Debug.LogException);
        }

        private async UniTask RunAsync()
        {
            if (_running || _disposed || _model.IsComplete) 
                return;
            
            _running = true;
            var token = _cts.Token;
            try
            {
                for (; _nextStep < _steps.Count; _nextStep++)
                {
                    token.ThrowIfCancellationRequested();
                    _model.SetStage(GetLoadingPercentage(), _steps[_nextStep].Name);
                    await _steps[_nextStep].InitializeAsync(token);
                }

                _model.SetStage(35, GameLoadingLabel);

                await IntroGate.PassAsync(_intro, t => UniTask.Delay(IntroGate.SettleMs, ignoreTimeScale: true, cancellationToken: t), token);
                await _navigation.OpenAsync(GameNavigation.MenuScene);
                token.ThrowIfCancellationRequested();
                _model.Complete();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _model.Fail(exception.Message, _nextStep >= _steps.Count || _steps[_nextStep].CanRetry);
                Debug.LogException(exception);
            }
            finally
            {
                _running = false;
            }
        }

        private float GetLoadingPercentage() => _steps.Count == 0 ? 35f : (float)_nextStep * 35 / _steps.Count;

        public void Dispose()
        {
            if (_disposed) 
                return;
            
            _disposed = true;
            _loading.RetryRequested -= Retry;
            
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
