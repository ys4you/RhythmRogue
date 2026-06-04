using UnityEngine;

namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// Centralized load/save for audio volume settings.
    /// Single source of truth backed by PlayerPrefs.
    ///
    /// Reading values: AudioSettings.MasterVolume, etc. (live values)
    /// Writing values: AudioSettings.MasterVolume = 0.5f; (saves + applies to AudioManager)
    ///
    /// Defaults match the slider defaults in MainMenuScreen (1.0 = full volume).
    /// </summary>
    public static class AudioSettings
    {
        private const string KeyMaster = "audio.masterVolume";
        private const string KeyMusic = "audio.musicVolume";
        private const string KeySfx = "audio.sfxVolume";

        // Legacy keys (from old MainMenuScreen save path) - migrated on first read
        private const string LegacyKeyMaster = "masterVolume";
        private const string LegacyKeyMusic = "musicVolume";
        private const string LegacyKeySfx = "sfxVolume";

        private const float Default = 1f;

        private static float _master = -1f;
        private static float _music = -1f;
        private static float _sfx = -1f;

        public static float MasterVolume
        {
            get { if (_master < 0f) _master = LoadWithMigration(KeyMaster, LegacyKeyMaster); return _master; }
            set
            {
                _master = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyMaster, _master);
                PlayerPrefs.Save();
                ApplyToAudioManager();
            }
        }

        public static float MusicVolume
        {
            get { if (_music < 0f) _music = LoadWithMigration(KeyMusic, LegacyKeyMusic); return _music; }
            set
            {
                _music = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyMusic, _music);
                PlayerPrefs.Save();
                ApplyToAudioManager();
            }
        }

        public static float SfxVolume
        {
            get { if (_sfx < 0f) _sfx = LoadWithMigration(KeySfx, LegacyKeySfx); return _sfx; }
            set
            {
                _sfx = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeySfx, _sfx);
                PlayerPrefs.Save();
                ApplyToAudioManager();
            }
        }

        // Audio/input latency calibration in milliseconds, stored under the historical key.
        private const string KeyOffsetMs = "audioOffset";
        private static float _offsetMs = float.NaN;

        /// <summary>
        /// Audio/input latency calibration in milliseconds. The Conductor applies this to the
        /// beat clock at song start, so it shifts note visuals AND hit detection together.
        /// Positive shifts the notes later (use if you are being judged late, e.g. with speaker
        /// or Bluetooth latency); negative shifts them earlier (use if you hit ahead).
        /// </summary>
        public static float CalibrationOffsetMs
        {
            get { if (float.IsNaN(_offsetMs)) _offsetMs = PlayerPrefs.GetFloat(KeyOffsetMs, 0f); return _offsetMs; }
            set
            {
                _offsetMs = Mathf.Clamp(value, -200f, 200f);
                PlayerPrefs.SetFloat(KeyOffsetMs, _offsetMs);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Calibration offset in seconds. See <see cref="CalibrationOffsetMs"/>.</summary>
        public static float CalibrationOffsetSeconds => CalibrationOffsetMs / 1000f;

        /// <summary>
        /// Convert a perceptual 0-1 slider value into a linear audio gain.
        /// Human loudness perception is logarithmic, so a linear slider feels
        /// front-loaded: most of the perceived volume change happens in the first
        /// ~20% of travel. The standard fix is a square-law taper, which makes
        /// slider position roughly match perceived loudness across the full range.
        ///
        /// Edge cases: 0 maps to 0 (true silence at the floor), 1 maps to 1 (full gain at the top).
        /// </summary>
        public static float ToLinearGain(float perceptual)
        {
            float p = Mathf.Clamp01(perceptual);
            return p * p;
        }

        /// <summary>
        /// Push current values to AudioManager and MusicManager. Call after either spawns
        /// to apply persisted settings. Safe to call when either is null (no-op until
        /// it exists).
        /// </summary>
        public static void ApplyToAudioManager()
        {
            var mgr = AudioManager.Instance;
            if (mgr != null)
            {
                mgr.SetMasterVolume(MasterVolume);
                mgr.SetSfxVolume(SfxVolume);
            }

            var music = MusicManager.Instance;
            if (music != null) music.ApplyVolumeFromSettings();
        }

        /// <summary>
        /// Reads new key first, falls back to legacy key for one-time migration,
        /// then to default. Persists the migrated value under the new key.
        /// </summary>
        private static float LoadWithMigration(string newKey, string legacyKey)
        {
            if (PlayerPrefs.HasKey(newKey))
                return PlayerPrefs.GetFloat(newKey, Default);

            if (PlayerPrefs.HasKey(legacyKey))
            {
                float legacyValue = PlayerPrefs.GetFloat(legacyKey, Default);
                PlayerPrefs.SetFloat(newKey, legacyValue);
                PlayerPrefs.Save();
                return legacyValue;
            }

            return Default;
        }
    }
}
