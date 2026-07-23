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

        // android.os.VibrationAttributes usages (frozen; verified against android.jar API 35).
        // Honest classification: Selection IS touch feedback; Impact is gameplay content; win/fail is
        // an event. This is what un-gates Impact/Notification from the OS touch-vibration setting.
        private const int UsageTouch = 18;
        private const int UsageMedia = 19;
        private const int UsageNotification = 49;

        // NON-PUBLIC flag value (AOSP FLAG_BYPASS_USER_VIBRATION_INTENSITY_OFF; the public API exposes
        // only FLAG_BYPASS_INTERRUPTION_POLICY = 1). OEM/version dependent by nature — if a platform
        // strips it, the vibration still carries its usage and behavior degrades to classification.
        private const int FlagBypassUserVibrationIntensityOff = 2;

        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaObject _resolver;                       // for the settings advisory
        private readonly AndroidJavaObject[] _attributes = new AndroidJavaObject[3]; // per-usage cache: touch/media/notification
        private readonly int _sdk;
        private bool _supported;   // not readonly: a denied vibrate() demotes it for the session
        private bool _bypass;
        private bool _warnedAttributesFailed;

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

                _resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                _supported = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TK.Haptics] Android vibrator init failed — haptics disabled: {exception.Message}");
                _supported = false;
            }
        }

        public bool IsSupported => _supported;

        /// <summary>
        /// Live read of the OS touch-vibration setting (Settings.System "haptic_feedback_enabled").
        /// True = the system will drop TOUCH-usage vibrations unless the bypass flag survives. Best
        /// effort: any failure reads as false.
        /// </summary>
        public bool SystemTouchVibrationDisabled
        {
            get
            {
                if (_resolver == null) return false;

                try
                {
                    using var settings = new AndroidJavaClass("android.provider.Settings$System");
                    return settings.CallStatic<int>("getInt", _resolver, "haptic_feedback_enabled", 1) == 0;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[TK.Haptics] Reading haptic_feedback_enabled failed: {exception.Message}");
                    return false;
                }
            }
        }

        public bool BypassSystemVibrationSetting
        {
            get => _bypass;
            set
            {
                if (_bypass == value) return;
                _bypass = value;

                // Cached attributes carry the flag bits, so they are stale now — drop them and let the
                // next vibrate rebuild with (or without) the bypass flag.
                for (var i = 0; i < _attributes.Length; i++)
                {
                    _attributes[i]?.Dispose();
                    _attributes[i] = null;
                }
            }
        }

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
                    }, UsageMedia);
                }
                else
                {
                    VibrateSimple(strength switch
                    {
                        HapticImpact.Light => 12L,
                        HapticImpact.Medium => 20L,
                        _ => 30L
                    }, UsageMedia);
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
                if (_sdk >= 29) VibratePredefined(EffectTick, UsageTouch);
                else VibrateSimple(8L, UsageTouch);
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
                    }, UsageNotification);
                }
                else
                {
                    VibrateSimple(type switch
                    {
                        HapticNotification.Success => 20L,
                        HapticNotification.Warning => 40L,
                        _ => 60L
                    }, UsageNotification);
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

        private void VibratePredefined(int effectId, int usage)
        {
            using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = effectClass.CallStatic<AndroidJavaObject>("createPredefined", effectId);
            Vibrate(effect, usage);
        }

        private void VibrateWaveform(long[] timings, int usage)
        {
            using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, -1);
            Vibrate(effect, usage);
        }

        // API 26+ amplitude one-shot when possible; plain timed vibrate below.
        private void VibrateSimple(long milliseconds, int usage)
        {
            if (_sdk >= 26)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, DefaultAmplitude);
                Vibrate(effect, usage);
            }
            else
            {
                // Pre-26 has no VibrationEffect (and pre-33 no attributes overload anyway).
                _vibrator.Call("vibrate", milliseconds);
            }
        }

        /// <summary>
        /// Single exit point for every effect: vibrate(effect, attributes) on API 33+ — the level the
        /// public VibrationAttributes overload exists at — and plain vibrate(effect) below, or whenever
        /// attribute construction failed (behavior then matches 0.1.x exactly).
        /// </summary>
        private void Vibrate(AndroidJavaObject effect, int usage)
        {
            if (_sdk >= 33)
            {
                var attributes = GetAttributes(usage);
                if (attributes != null)
                {
                    // Two-object overload resolution: Unity binds by the actual Java class of the
                    // arguments, so this should hit vibrate(VibrationEffect, VibrationAttributes), not
                    // the deprecated AudioAttributes one — checked on device via the dumpsys Usage=
                    // readout (a wrong binding would surface as Usage=TOUCH on everything).
                    _vibrator.Call("vibrate", effect, attributes);
                    return;
                }
            }

            _vibrator.Call("vibrate", effect);
        }

        private AndroidJavaObject GetAttributes(int usage)
        {
            var slot = usage switch { UsageTouch => 0, UsageMedia => 1, _ => 2 };
            if (_attributes[slot] != null) return _attributes[slot];

            try
            {
                using var builder = new AndroidJavaObject("android.os.VibrationAttributes$Builder");
                // The Java builder mutates in place; the returned wrappers are disposed, the original
                // builder reference stays valid for build().
                builder.Call<AndroidJavaObject>("setUsage", usage).Dispose();
                if (_bypass)
                {
                    builder.Call<AndroidJavaObject>("setFlags",
                        FlagBypassUserVibrationIntensityOff, FlagBypassUserVibrationIntensityOff).Dispose();
                }

                _attributes[slot] = builder.Call<AndroidJavaObject>("build");
                return _attributes[slot];
            }
            catch (Exception exception)
            {
                if (!_warnedAttributesFailed)
                {
                    _warnedAttributesFailed = true;
                    Debug.LogWarning($"[TK.Haptics] VibrationAttributes construction failed — falling back " +
                                     $"to unclassified vibrations: {exception.Message}");
                }

                return null;
            }
        }
    }
}
#endif
