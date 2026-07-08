#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace TK.Haptics
{
    /// <summary>
    /// Android haptics via the platform Vibrator. Predefined effects on API 29+, amplitude
    /// one-shots on API 26+, plain timed vibration below. Every JNI call is guarded — a failure
    /// degrades to unsupported/no-op, never throws into game code. Device-verified.
    /// </summary>
    public sealed class AndroidHapticBackend : IHapticBackend
    {
        // Stable android.os.VibrationEffect constants (frozen since API 29 / 26).
        private const int EffectClick = 0;
        private const int EffectTick = 2;
        private const int EffectHeavyClick = 5;
        private const int DefaultAmplitude = -1;

        private readonly AndroidJavaObject _vibrator;
        private readonly int _sdk;
        private readonly bool _supported;

        public AndroidHapticBackend()
        {
            try
            {
                _sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                if (_sdk >= 31)
                {
                    using var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                    _vibrator = manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                }
                else
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                _supported = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TK.Haptics] Android vibrator init failed — haptics disabled: {exception.Message}");
                _supported = false;
            }
        }

        public bool IsSupported => _supported;

        public void Impact(HapticImpact strength)
        {
            if (!_supported) return;

            try
            {
                if (_sdk >= 29)
                {
                    VibratePredefined(strength switch
                    {
                        HapticImpact.Light => EffectTick,
                        HapticImpact.Medium => EffectClick,
                        _ => EffectHeavyClick
                    });
                }
                else
                {
                    VibrateSimple(strength switch
                    {
                        HapticImpact.Light => 12L,
                        HapticImpact.Medium => 20L,
                        _ => 30L
                    });
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TK.Haptics] Impact failed: {exception.Message}");
            }
        }

        public void Selection()
        {
            if (!_supported) return;

            try
            {
                if (_sdk >= 29) VibratePredefined(EffectTick);
                else VibrateSimple(8L);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TK.Haptics] Selection failed: {exception.Message}");
            }
        }

        public void Notification(HapticNotification type)
        {
            if (!_supported) return;

            try
            {
                if (_sdk >= 26)
                {
                    VibrateWaveform(type switch
                    {
                        HapticNotification.Success => new[] { 0L, 15L, 40L, 15L },
                        HapticNotification.Warning => new[] { 0L, 25L, 50L, 25L },
                        _ => new[] { 0L, 35L, 40L, 35L, 40L, 35L }
                    });
                }
                else
                {
                    VibrateSimple(type switch
                    {
                        HapticNotification.Success => 20L,
                        HapticNotification.Warning => 40L,
                        _ => 60L
                    });
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TK.Haptics] Notification failed: {exception.Message}");
            }
        }

        private void VibratePredefined(int effectId)
        {
            using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = effectClass.CallStatic<AndroidJavaObject>("createPredefined", effectId);
            _vibrator.Call("vibrate", effect);
        }

        private void VibrateWaveform(long[] timings)
        {
            using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, -1);
            _vibrator.Call("vibrate", effect);
        }

        // API 26+ amplitude one-shot when possible; plain timed vibrate below.
        private void VibrateSimple(long milliseconds)
        {
            if (_sdk >= 26)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, DefaultAmplitude);
                _vibrator.Call("vibrate", effect);
            }
            else
            {
                _vibrator.Call("vibrate", milliseconds);
            }
        }
    }
}
#endif
