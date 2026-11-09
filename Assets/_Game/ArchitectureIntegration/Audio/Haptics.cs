using System;
using UnityEngine;

namespace Game.Integration.Audio
{
    public enum HapticKind { Tap, Success }

    /// <summary>
    /// Main-thread mobile feedback. Android uses the platform Vibrator APIs; iOS uses the
    /// Taptic Engine plug-in in _Game/Plugins/iOS. Calls are harmless on desktop or unsupported devices.
    /// </summary>
    public static class Haptics
    {
        private static bool _enabled = true;
        public static bool Enabled
        {
            get => _enabled;
            set { _enabled = value; if (!value) Cancel(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _enabled = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            ReleaseAndroid();
            _initialized = _failed = false;
            UnityEngine.Application.quitting -= ReleaseAndroid;
            UnityEngine.Application.quitting += ReleaseAndroid;
#endif
        }

        public static void Play(HapticKind kind)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            UseAndroid(() =>
            {
                if (_sdk >= 29)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    // EFFECT_TICK = 2, EFFECT_HEAVY_CLICK = 5. Android supplies a generic fallback.
                    using var effect = effects.CallStatic<AndroidJavaObject>("createPredefined", kind == HapticKind.Success ? 5 : 2);
                    _vibrator.Call("vibrate", effect);
                }
                else PlayOneShot(kind == HapticKind.Success ? 35 : 12, kind == HapticKind.Success ? 200 : 90);
            });
#elif UNITY_IOS && !UNITY_EDITOR
            try { PTHaptic(kind == HapticKind.Success ? 3 : 0); }
            catch (Exception error) { _enabled = false; Debug.LogWarning("Haptics off: " + error.Message); }
#endif
        }

        /// <summary>Android one-shot vibration. Intensity is 0..1; devices without amplitude control use their default strength.</summary>
        public static void Vibrate(int milliseconds, float intensity = 1f)
        {
            if (!Enabled || milliseconds <= 0 || intensity <= 0 || float.IsNaN(intensity) || float.IsInfinity(intensity)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            UseAndroid(() => PlayOneShot(milliseconds, Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(intensity) * 255), 1, 255)));
#elif UNITY_IOS && !UNITY_EDITOR
            Play(intensity >= 0.5f ? HapticKind.Success : HapticKind.Tap);
#endif
        }

        /// <summary>Schedules the charge's continuously increasing amplitude; Cancel ends it at finger release.</summary>
        public static void StartCharge(float initialCharge, float chargePerSecond, float seconds)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            UseAndroid(() =>
            {
                var pattern = ChargeHapticPattern.Build(initialCharge, chargePerSecond, seconds, _hasAmplitudeControl);
                if (pattern.Timings.Length == 0) return;
                if (_sdk >= 26)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effects.CallStatic<AndroidJavaObject>("createWaveform", pattern.Timings, pattern.Amplitudes, -1);
                    _vibrator.Call("vibrate", effect);
                }
                else _vibrator.Call("vibrate", pattern.Timings, -1);
            });
#endif
        }

        /// <summary>Stops Android vibration, including when feedback is disabled or the app loses focus.</summary>
        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null || _failed) return;
            try { _vibrator.Call("cancel"); }
            catch (Exception error) { DisableAndroid(error); }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _sdk;
        private static bool _initialized, _available, _hasAmplitudeControl, _failed;

        private static void UseAndroid(Action play)
        {
            if (_failed) return;
            try { if (EnsureAndroid()) play(); }
            catch (Exception error) { DisableAndroid(error); }
        }

        private static bool EnsureAndroid()
        {
            if (_initialized) return _available;
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            // An early call before the Activity is available may try again later.
            if (activity == null) return false;
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            _sdk = version.GetStatic<int>("SDK_INT");
            if (_sdk >= 31)
            {
                using var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                if (manager != null) _vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
            }
            if (_vibrator == null) _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            _available = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            _hasAmplitudeControl = _available && _sdk >= 26 && _vibrator.Call<bool>("hasAmplitudeControl");
            _initialized = true;
            return _available;
        }

        private static void PlayOneShot(int milliseconds, int amplitude)
        {
            if (_sdk >= 26)
            {
                using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, _hasAmplitudeControl ? amplitude : -1);
                _vibrator.Call("vibrate", effect);
            }
            else _vibrator.Call("vibrate", (long)milliseconds);
        }

        private static void DisableAndroid(Exception error)
        {
            _failed = true;
            Debug.LogWarning("Haptics off: " + error.Message);
        }

        private static void ReleaseAndroid()
        {
            Cancel();
            _vibrator?.Dispose();
            _vibrator = null;
            _initialized = _available = _hasAmplitudeControl = false;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void PTHaptic(int style);
#endif
    }
}
