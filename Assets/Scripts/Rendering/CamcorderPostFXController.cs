using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CaveGame
{
    // 루프가 진행될수록 캠코더 화면 열화(그레인·색수차·비네트)를 키운다.
    [RequireComponent(typeof(Volume))]
    public class CamcorderPostFXController : LoopListener
    {
        [SerializeField] float grainIntensityStart = 0.35f;
        [SerializeField] float grainIntensityEnd = 0.7f;
        [SerializeField] float aberrationStart = 0.15f;
        [SerializeField] float aberrationEnd = 0.5f;
        [SerializeField] float vignetteStart = 0.3f;
        [SerializeField] float vignetteEnd = 0.55f;

        FilmGrain filmGrain;
        ChromaticAberration chromaticAberration;
        Vignette vignette;

        // volume.profile은 인스턴스 사본을 만든다 — 프로필 에셋 원본은 바뀌지 않음
        void Awake()
        {
            VolumeProfile profile = GetComponent<Volume>().profile;
            profile.TryGet(out filmGrain);
            profile.TryGet(out chromaticAberration);
            profile.TryGet(out vignette);
        }

        protected override void OnLoopChanged(int loop)
        {
            float t = Mathf.InverseLerp(1, GameManager.Instance.TotalLoops, loop);

            if (filmGrain != null) filmGrain.intensity.value = Mathf.Lerp(grainIntensityStart, grainIntensityEnd, t);
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(aberrationStart, aberrationEnd, t);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(vignetteStart, vignetteEnd, t);
        }
    }
}
