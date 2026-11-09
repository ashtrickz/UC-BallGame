#if UNITY_ANDROID && !UNITY_EDITOR
using Game.Foundation;
using System;
using Game.Audio;
using UnityEngine;

namespace Game.Integration.Audio
{
    /// <summary>Owns Android vibrator handles and submits finite native charge envelopes.</summary>
    public sealed class AndroidHaptics : IHaptics
    {
        private const string PlayerClass = "com.unity3d.player.UnityPlayer";
        private const string CurrentActivityField = "currentActivity";
        private const string VersionClass = "android.os.Build$VERSION";
        private const string SdkField = "SDK_INT";
        private const string ServiceMethod = "getSystemService";
        private const string ManagerService = "vibrator_manager";
        private const string VibratorService = "vibrator";
        private const string DefaultVibratorMethod = "getDefaultVibrator";
        private const string AvailableMethod = "hasVibrator";
        private const string AmplitudeMethod = "hasAmplitudeControl";
        private const string EffectClass = "android.os.VibrationEffect";
        private const string PredefinedMethod = "createPredefined";
        private const string OneShotMethod = "createOneShot";
        private const string WaveformMethod = "createWaveform";
        private const string VibrateMethod = "vibrate";
        private const string CancelMethod = "cancel";
        private const string WarningPrefix = "Haptics off: ";
        private const int ManagerSdk = 31;
        private const int PredefinedSdk = 29;
        private const int EffectSdk = 26;
        private const int SuccessEffect = 5;
        private const int TapEffect = 2;
        private const int SuccessMilliseconds = 35;
        private const int TapMilliseconds = 12;
        private const int SuccessAmplitude = 200;
        private const int TapAmplitude = 90;
        private const int MaximumAmplitude = 255;
        private AndroidJavaObject _vibrator;
        private int _sdk;
        private bool _enabled = true;
        private bool _initialized;
        private bool _available;
        private bool _hasAmplitudeControl;
        private bool _failed;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value)
                    Cancel();
            }
        }

        public void Play(global::Game.Audio.HapticKind kind)
        {
            if (!Enabled)
                return;

            UseAndroid(() =>
            {
                if (_sdk >= PredefinedSdk)
                {
                    using var effects = new AndroidJavaClass(EffectClass);
                    var preset = kind == global::Game.Audio.HapticKind.Success ? SuccessEffect : TapEffect;
                    using var effect = effects.CallStatic<AndroidJavaObject>(PredefinedMethod, preset);
                    _vibrator.Call(VibrateMethod, effect);
                }
                else
                {
                    var duration = kind == global::Game.Audio.HapticKind.Success ? SuccessMilliseconds : TapMilliseconds;
                    var amplitude = kind == global::Game.Audio.HapticKind.Success ? SuccessAmplitude : TapAmplitude;
                    PlayOneShot(duration, amplitude);
                }
            });
        }

        public void Vibrate(int milliseconds, float intensity)
        {
            if (!Enabled || milliseconds <= 0 || intensity <= 0 || float.IsNaN(intensity) || float.IsInfinity(intensity))
                return;

            var amplitude = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(intensity) * MaximumAmplitude), 1, MaximumAmplitude);
            UseAndroid(() => PlayOneShot(milliseconds, amplitude));
        }

        public void StartCharge(float initialCharge, float chargePerSecond, float seconds)
        {
            if (!Enabled)
                return;

            UseAndroid(() =>
            {
                var pattern = ChargeHapticPattern.Build(initialCharge, chargePerSecond, seconds, _hasAmplitudeControl);
                if (pattern.Timings.Length == 0)
                    return;

                if (_sdk >= EffectSdk)
                {
                    using var effects = new AndroidJavaClass(EffectClass);
                    using var effect = effects.CallStatic<AndroidJavaObject>(WaveformMethod, pattern.Timings, pattern.Amplitudes, -1);
                    _vibrator.Call(VibrateMethod, effect);
                }
                else
                    _vibrator.Call(VibrateMethod, pattern.Timings, -1);
            });
        }

        public void Cancel()
        {
            if (_vibrator == null || _failed)
                return;

            try
            {
                _vibrator.Call(CancelMethod);
            }
            catch (Exception error)
            {
                DisableAndroid(error);
            }
        }

        public void Dispose()
        {
            Cancel();
            _vibrator?.Dispose();
            _vibrator = null;
            _initialized = false;
            _available = false;
            _hasAmplitudeControl = false;
        }

        private void UseAndroid(Action play)
        {
            if (_failed)
                return;

            try
            {
                if (EnsureAndroid())
                    play();
            }
            catch (Exception error)
            {
                DisableAndroid(error);
            }
        }

        private bool EnsureAndroid()
        {
            if (_initialized)
                return _available;

            using var player = new AndroidJavaClass(PlayerClass);
            using var activity = player.GetStatic<AndroidJavaObject>(CurrentActivityField);
            // The activity can be absent early in startup, so a later call may retry.
            if (activity == null)
                return false;

            using var version = new AndroidJavaClass(VersionClass);
            _sdk = version.GetStatic<int>(SdkField);
            if (_sdk >= ManagerSdk)
            {
                using var manager = activity.Call<AndroidJavaObject>(ServiceMethod, ManagerService);
                if (manager != null)
                    _vibrator = manager.Call<AndroidJavaObject>(DefaultVibratorMethod);
            }

            if (_vibrator == null)
                _vibrator = activity.Call<AndroidJavaObject>(ServiceMethod, VibratorService);
            _available = _vibrator != null && _vibrator.Call<bool>(AvailableMethod);
            _hasAmplitudeControl = _available && _sdk >= EffectSdk && _vibrator.Call<bool>(AmplitudeMethod);
            _initialized = true;
            return _available;
        }

        private void PlayOneShot(int milliseconds, int amplitude)
        {
            if (_sdk >= EffectSdk)
            {
                using var effects = new AndroidJavaClass(EffectClass);
                using var effect = effects.CallStatic<AndroidJavaObject>(OneShotMethod, (long)milliseconds, _hasAmplitudeControl ? amplitude : -1);
                _vibrator.Call(VibrateMethod, effect);
            }
            else
                _vibrator.Call(VibrateMethod, (long)milliseconds);
        }

        private void DisableAndroid(Exception error)
        {
            _failed = true;
            GameLog.Warning(WarningPrefix + error.Message);
        }
    }
}
#endif
