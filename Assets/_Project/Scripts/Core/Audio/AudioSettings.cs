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

        /// <summary>
        /// Push current values to AudioManager. Call after AudioManager spawns
        /// to apply persisted settings. Safe to call when AudioManager is null
        /// (no-op until it exists).
        /// </summary>
        public static void ApplyToAudioManager()
        {
            var mgr = AudioManager.Instance;
            if (mgr == null) return;
            mgr.SetMasterVolume(MasterVolume);
            mgr.SetSfxVolume(SfxVolume);
            // MusicVolume is reserved for a future music system (Conductor's AudioSource).
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
