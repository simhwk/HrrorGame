using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CaveGame
{
    // 플레이어 설정의 단일 출처. PlayerPrefs에 저장하고, 값이 바뀌면 곧바로 게임 전체에 적용한다.
    // 밝기는 씬마다 있는 볼륨을 건드리지 않고, 씬을 넘어 살아남는 우선순위 높은 전역 볼륨 하나에서 노출값만 덮어쓴다
    // (볼륨 블렌딩은 override된 항목만 섞으므로 각 씬의 다른 후처리 값은 그대로 남는다).
    public static class GameSettings
    {
        public const int VolumeMax = 10;
        public const int SensitivityMin = 1, SensitivityMax = 10;
        public const int BrightnessMin = -5, BrightnessMax = 5;

        const string VolumeKey = "settings.volume";
        const string SensitivityKey = "settings.sensitivity";
        const string BrightnessKey = "settings.brightness";
        const string FullscreenKey = "settings.fullscreen";
        const float ExposurePerStep = 0.2f; // 밝기 한 칸 = 0.2 EV

        static ColorAdjustments exposure;

        public static int Volume { get; private set; }
        public static int Sensitivity { get; private set; }
        public static int Brightness { get; private set; }
        public static bool Fullscreen { get; private set; }

        // 5가 기본(1배) — PlayerController의 감도에 곱한다
        public static float SensitivityMultiplier => Sensitivity / 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Volume = PlayerPrefs.GetInt(VolumeKey, 8);
            Sensitivity = PlayerPrefs.GetInt(SensitivityKey, 5);
            Brightness = PlayerPrefs.GetInt(BrightnessKey, 0);
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            CreateExposureVolume();
            ApplyAll();
        }

        public static void SetVolume(int value)
        {
            Volume = Mathf.Clamp(value, 0, VolumeMax);
            PlayerPrefs.SetInt(VolumeKey, Volume);
            AudioListener.volume = Volume / (float)VolumeMax;
        }

        public static void SetSensitivity(int value)
        {
            Sensitivity = Mathf.Clamp(value, SensitivityMin, SensitivityMax);
            PlayerPrefs.SetInt(SensitivityKey, Sensitivity);
        }

        public static void SetBrightness(int value)
        {
            Brightness = Mathf.Clamp(value, BrightnessMin, BrightnessMax);
            PlayerPrefs.SetInt(BrightnessKey, Brightness);
            if (exposure != null) exposure.postExposure.Override(Brightness * ExposurePerStep);
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            Screen.fullScreen = value;
        }

        // 설정 화면을 나갈 때 한 번 — 값 바꿀 때마다 디스크에 쓰지 않게
        public static void Save() => PlayerPrefs.Save();

        static void ApplyAll()
        {
            SetVolume(Volume);
            SetSensitivity(Sensitivity);
            SetBrightness(Brightness);
#if !UNITY_EDITOR
            Screen.fullScreen = Fullscreen; // 에디터 게임 뷰에선 의미 없음
#endif
        }

        static void CreateExposureVolume()
        {
            var go = new GameObject("GameSettingsExposure");
            Object.DontDestroyOnLoad(go);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            exposure = profile.Add<ColorAdjustments>();
            exposure.postExposure.Override(0f);
            volume.profile = profile;
        }
    }
}
