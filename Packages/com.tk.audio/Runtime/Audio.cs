using UnityEngine;

namespace TK.Audio
{
    /// <summary>
    /// Static façade over a bound <see cref="AudioService"/> (the Analytics pattern):
    /// <c>Audio.Bind(service)</c> at the composition root, then <c>Audio.PlaySfx("click")</c>
    /// anywhere. Unbound calls warn once and no-op.
    /// </summary>
    public static class Audio
    {
        private static AudioService s_service;
        private static bool s_warned;

        // With Enter Play Mode's domain reload disabled, statics survive between sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_service = null;
            s_warned = false;
        }

        /// <summary>The bound service; null when none. Prefer the mirrored members below.</summary>
        public static AudioService Service => s_service;

        public static void Bind(AudioService service)
        {
            s_service = service;
            s_warned = false;
        }

        /// <summary>Unbinds only when <paramref name="service"/> is the currently bound one.</summary>
        public static void Unbind(AudioService service)
        {
            if (ReferenceEquals(s_service, service)) s_service = null;
        }

        public static bool MusicEnabled
        {
            get => s_service?.MusicEnabled ?? true;
            set { var s = Resolve(); if (s != null) s.MusicEnabled = value; }
        }

        public static bool SfxEnabled
        {
            get => s_service?.SfxEnabled ?? true;
            set { var s = Resolve(); if (s != null) s.SfxEnabled = value; }
        }

        public static float MusicVolume
        {
            get => s_service?.MusicVolume ?? 1f;
            set { var s = Resolve(); if (s != null) s.MusicVolume = value; }
        }

        public static float SfxVolume
        {
            get => s_service?.SfxVolume ?? 1f;
            set { var s = Resolve(); if (s != null) s.SfxVolume = value; }
        }

        public static string ActiveMusicKey => s_service?.ActiveMusicKey;
        public static string ActivePlaylistKey => s_service?.ActivePlaylistKey;

        public static void PushMute() => Resolve()?.PushMute();
        public static void PopMute() => Resolve()?.PopMute();
        public static void PauseMusic() => Resolve()?.PauseMusic();
        public static void ResumeMusic() => Resolve()?.ResumeMusic();
        public static void FadeChannelVolume(AudioChannel channel, float target, float seconds) => Resolve()?.FadeChannelVolume(channel, target, seconds);

        public static void PlaySfx(string key, float volumeScale = 1f) => Resolve()?.PlaySfx(key, volumeScale);
        public static void PlaySfx(string key, float volumeScale, float delaySeconds) => Resolve()?.PlaySfx(key, volumeScale, delaySeconds);
        public static void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f) => Resolve()?.PlaySfx(clip, volumeScale, pitch);
        public static AudioHandle PlaySfxLoop(string key, float volumeScale = 1f) => Resolve()?.PlaySfxLoop(key, volumeScale) ?? default;
        public static AudioHandle PlaySfxLoop(AudioClip clip, float volumeScale = 1f, float pitch = 1f) => Resolve()?.PlaySfxLoop(clip, volumeScale, pitch) ?? default;
        public static void StopSfx(string key) => Resolve()?.StopSfx(key);
        public static void StopAllSfx() => Resolve()?.StopAllSfx();
        public static void PlayMusic(string key, bool loop = true) => Resolve()?.PlayMusic(key, loop);
        public static void PlayMusic(AudioClip clip, bool loop = true) => Resolve()?.PlayMusic(clip, loop);
        public static void PlayPlaylist(string key) => Resolve()?.PlayPlaylist(key);
        public static void StopMusic() => Resolve()?.StopMusic();

        public static Awaitable PreloadAsync(string musicKey)
        {
            var service = Resolve();
            if (service != null) return service.PreloadAsync(musicKey);

            var source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }

        private static AudioService Resolve()
        {
            if (s_service == null && !s_warned)
            {
                s_warned = true;
                Debug.LogWarning("[Audio] No AudioService bound — call Audio.Bind(service) at your composition root.");
            }

            return s_service;
        }
    }
}
