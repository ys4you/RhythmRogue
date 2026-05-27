using UnityEngine;

namespace RhythmRogue.Core.Display
{
    /// <summary>
    /// Centralized load/save for display settings.
    /// Backed by PlayerPrefs. Single source of truth, callable from anywhere.
    ///
    /// Settings exposed:
    ///   - Resolution: width x height
    ///   - FullscreenMode: 0 = Windowed, 1 = Borderless Window, 2 = Exclusive Fullscreen
    ///   - VSync: 0 = off, 1 = on
    ///   - CRTEffect: bool, controls the CRT overlay shader
    ///
    /// Setters apply to live Screen/QualitySettings immediately.
    /// Call ApplyAll() once on game start to push persisted values to Unity.
    /// </summary>
    public static class DisplaySettings
    {
        private const string KeyResWidth = "display.resolution.width";
        private const string KeyResHeight = "display.resolution.height";
        private const string KeyFullscreenMode = "display.fullscreenMode";
        private const string KeyVSync = "display.vsync";
        private const string KeyCRT = "display.crt";

        // Cache for lazy load
        private static int _resWidth = -1;
        private static int _resHeight = -1;
        private static int _fullscreenMode = -1;
        private static int _vsync = -1;
        private static int _crt = -1;

        public static int ResolutionWidth
        {
            get
            {
                if (_resWidth < 0) _resWidth = PlayerPrefs.GetInt(KeyResWidth, Screen.currentResolution.width);
                return _resWidth;
            }
        }

        public static int ResolutionHeight
        {
            get
            {
                if (_resHeight < 0) _resHeight = PlayerPrefs.GetInt(KeyResHeight, Screen.currentResolution.height);
                return _resHeight;
            }
        }

        /// <summary>0 = Windowed, 1 = Borderless Window, 2 = Exclusive Fullscreen.</summary>
        public static int FullscreenMode
        {
            get
            {
                if (_fullscreenMode < 0) _fullscreenMode = PlayerPrefs.GetInt(KeyFullscreenMode, 1);
                return _fullscreenMode;
            }
            set
            {
                _fullscreenMode = Mathf.Clamp(value, 0, 2);
                PlayerPrefs.SetInt(KeyFullscreenMode, _fullscreenMode);
                PlayerPrefs.Save();
                ApplyResolutionAndFullscreen();
            }
        }

        public static bool VSync
        {
            get
            {
                if (_vsync < 0) _vsync = PlayerPrefs.GetInt(KeyVSync, 1);
                return _vsync != 0;
            }
            set
            {
                _vsync = value ? 1 : 0;
                PlayerPrefs.SetInt(KeyVSync, _vsync);
                PlayerPrefs.Save();
                QualitySettings.vSyncCount = _vsync;
            }
        }

        public static bool CRTEffect
        {
            get
            {
                if (_crt < 0) _crt = PlayerPrefs.GetInt(KeyCRT, 1);
                return _crt != 0;
            }
            set
            {
                _crt = value ? 1 : 0;
                PlayerPrefs.SetInt(KeyCRT, _crt);
                PlayerPrefs.Save();
                // CRTOverlay reads this on its own each frame.
            }
        }

        /// <summary>
        /// Set both resolution and fullscreen mode together.
        /// </summary>
        public static void SetResolution(int width, int height)
        {
            _resWidth = Mathf.Max(640, width);
            _resHeight = Mathf.Max(360, height);
            PlayerPrefs.SetInt(KeyResWidth, _resWidth);
            PlayerPrefs.SetInt(KeyResHeight, _resHeight);
            PlayerPrefs.Save();
            ApplyResolutionAndFullscreen();
        }

        /// <summary>
        /// Push all persisted settings to Unity. Call once on game start.
        /// </summary>
        public static void ApplyAll()
        {
            ApplyResolutionAndFullscreen();
            QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        private static void ApplyResolutionAndFullscreen()
        {
            var mode = FullscreenMode switch
            {
                0 => FullScreenMode.Windowed,
                1 => FullScreenMode.FullScreenWindow,
                2 => FullScreenMode.ExclusiveFullScreen,
                _ => FullScreenMode.FullScreenWindow,
            };
            Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
        }
    }
}
