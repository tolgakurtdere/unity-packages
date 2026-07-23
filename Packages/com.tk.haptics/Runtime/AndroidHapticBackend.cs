#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace TK.Haptics
{
    /// <summary>
    /// Android haptics via the platform Vibrator. Predefined effects on API 29+, amplitude
    /// one-shots on API 26+, plain timed vibration below. Every JNI call is guarded — a failure
    /// degrades to unsupported/no-op, never throws into game code.
    ///
    /// The package declares android.permission.VIBRATE itself via
    /// Plugins/Android/TKHaptics.androidlib, because this backend reaches the Vibrator through raw
    /// JNI and Unity only auto-injects that permission when it sees Handheld.Vibrate(). If a build
    /// still lacks the permission, the first denied call demotes IsSupported for the session instead
    /// of failing silently — see OnCallFailed.
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
        private bool _supported;   // not readonly: a denied vibrate() demotes it for the session

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
                OnCallFailed(exception, nameof(Impact));
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
                OnCallFailed(exception, nameof(Selection));
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
                OnCallFailed(exception, nameof(Notification));
            }
        }

        /// <summary>
        /// A denied vibrate() means the app was built without android.permission.VIBRATE — a permanent
        /// misconfiguration, not a transient glitch — so stop claiming the device can vibrate. The game
        /// can then hide its Vibration toggle instead of offering a switch that does nothing.
        /// SecurityException cannot be caught by type here: Unity surfaces Java throwables as
        /// AndroidJavaException and the Java type survives only in the message.
        /// </summary>
        private void OnCallFailed(Exception exception, string operation)
        {
            if (exception is AndroidJavaException && exception.Message.Contains("SecurityException"))
            {
                _supported = false;
                Debug.LogError($"[TK.Haptics] {operation} denied: android.permission.VIBRATE is missing from " +
                               "the built manifest. Haptics are disabled for this session.");
                return;
            }

            Debug.LogWarning($"[TK.Haptics] {operation} failed: {exception.Message}");
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
