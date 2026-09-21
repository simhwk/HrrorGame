// =====================================================================
// [스크립트 역할] 캠코더 화면 느낌의 URP 포스트프로세싱 강도 제어
// 루프 진행도(0~1)에 비례해 Film Grain / Chromatic Aberration / Vignette 강도를
// 점점 세지도록 보간하여, 진행할수록 화면이 더 불안정하고 공포스럽게 보이게 한다.
// =====================================================================
using UnityEngine;
using UnityEngine.Rendering;          // Volume, VolumeProfile 등 URP 공통 렌더링 타입
using UnityEngine.Rendering.Universal; // FilmGrain, ChromaticAberration, Vignette 등 URP 전용 이펙트 타입

namespace CaveGame
{
    /// <summary>
    /// 캠코더 저화질 필터 — URP Volume의 Film Grain / Chromatic Aberration / Vignette를
    /// 루프 진행도에 맞춰 점점 강하게 만든다(누적 공포). 3D 에셋 자체의 품질은 건드리지 않는다.
    /// Volume 프로필에 해당 오버라이드를 미리 추가해 두어야 한다.
    /// </summary>
    [RequireComponent(typeof(Volume))] // 이 스크립트가 조작할 URP Volume 컴포넌트가 반드시 같은 오브젝트에 있어야 함
    public class CamcorderPostFXController : MonoBehaviour
    {
        [SerializeField] float grainIntensityStart = 0.35f; // 필름 그레인(입자 노이즈) 시작(1루프) 강도
        [SerializeField] float grainIntensityEnd = 0.7f;    // 필름 그레인 종료(마지막 루프) 강도
        [SerializeField] float aberrationStart = 0.15f;     // 색수차(색 번짐) 시작 강도
        [SerializeField] float aberrationEnd = 0.5f;        // 색수차 종료 강도
        [SerializeField] float vignetteStart = 0.3f;        // 비네팅(화면 가장자리 어두워짐) 시작 강도
        [SerializeField] float vignetteEnd = 0.55f;         // 비네팅 종료 강도

        Volume volume; // Awake에서 한 번 캐싱 — GetComponent 반복 호출만 피하고, 이펙트 컴포넌트 자체는 캐싱하지 않음

        void Awake() // 최초 1회 초기화
        {
            volume = GetComponent<Volume>();
        }

        void OnEnable() // 컴포넌트 활성화 시
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoopAdvanced += UpdateDread; // 루프가 진행될 때마다 UpdateDread를 자동 호출하도록 구독
        }

        void OnDisable() // 컴포넌트 비활성화/파괴 시
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoopAdvanced -= UpdateDread; // 구독 해제(안 하면 파괴된 뒤에도 호출 시도되어 에러 위험)
        }

        // 루프 진행 이벤트가 올 때마다 현재 진행률에 맞춰 이펙트 강도 갱신
        void UpdateDread(int loop, MarkerStage stage) // 이벤트 시그니처(Action<int, MarkerStage>)와 일치해야 구독 가능
        {
            // 이펙트 오버라이드 참조를 Awake에서 미리 캐싱해두지 않고 매번 새로 조회한다 — Volume의 런타임 프로필 인스턴스가
            // 도메인 리로드/프로필 재할당 등으로 교체될 수 있어, 캐싱된 참조가 MissingReferenceException을 내는 경우가 있었다.
            VolumeProfile profile = volume.profile; // 같은 오브젝트의 Volume 컴포넌트가 사용 중인(런타임) 프로필 인스턴스를 매번 새로 가져옴

            float t = Mathf.InverseLerp(1, GameManager.Instance.TotalLoops, loop);
            // InverseLerp(최소, 최대, 현재값): "현재값이 최소~최대 구간에서 몇 % 지점인지"를 0~1로 반환.
            // 즉 1루프=0(시작), 마지막 루프(7)=1(최대)로 진행도를 정규화

            if (profile.TryGet(out FilmGrain filmGrain))
                filmGrain.intensity.value = Mathf.Lerp(grainIntensityStart, grainIntensityEnd, t);
            // Lerp(시작값, 끝값, t): 진행도 t에 비례해 시작~끝 사이의 값을 계산해 실제 이펙트 강도(.intensity.value)에 대입
            if (profile.TryGet(out ChromaticAberration chromaticAberration))
                chromaticAberration.intensity.value = Mathf.Lerp(aberrationStart, aberrationEnd, t);
            if (profile.TryGet(out Vignette vignette))
                vignette.intensity.value = Mathf.Lerp(vignetteStart, vignetteEnd, t);
        }
    }
}
