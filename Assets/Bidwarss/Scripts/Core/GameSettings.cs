using UnityEngine;

namespace Bidwarss
{
    // Local, per-player preferences. Never networked and never part of the rules hash.
    public static class GameSettings
    {
        public const float MinSensitivity = .03f, MaxSensitivity = .4f, MinFov = 60f, MaxFov = 110f;
        const string SensitivityKey = "Bidwarss.Sensitivity", VolumeKey = "Bidwarss.Volume", MusicKey = "Bidwarss.Music", FovKey = "Bidwarss.Fov";
        static bool loaded;
        static float sensitivity = .12f, volume = 1f, music = .5f, fov = 78f;

        static float Read(string key, float fallback, float min, float max)
        {
            float value = PlayerPrefs.GetFloat(key, fallback);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Clamp(value, min, max);
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivity = Read(SensitivityKey, .12f, MinSensitivity, MaxSensitivity);
            volume = Read(VolumeKey, 1f, 0f, 1f);
            music = Read(MusicKey, .5f, 0f, 1f);
            fov = Read(FovKey, 78f, MinFov, MaxFov);
        }

        public static float MouseSensitivity { get { Load(); return sensitivity; } set { Load(); sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); } }
        public static float Volume { get { Load(); return volume; } set { Load(); volume = Mathf.Clamp01(value); AudioListener.volume = volume; } }
        public static float MusicVolume { get { Load(); return music; } set { Load(); music = Mathf.Clamp01(value); } }
        public static float Fov { get { Load(); return fov; } set { Load(); fov = Mathf.Clamp(value, MinFov, MaxFov); } }

        public static void Apply() { Load(); AudioListener.volume = volume; }

        public static void Save()
        {
            Load();
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetFloat(MusicKey, music);
            PlayerPrefs.SetFloat(FovKey, fov);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            Load();
            sensitivity = .12f; volume = 1f; music = .5f; fov = 78f;
            AudioListener.volume = volume;
            Save();
        }
    }
}
