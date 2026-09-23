// =====================================================================
// [스크립트 역할] 갈림길에 배치되는 개별 표식(Marker) 오브젝트
// 현재 표식 단계에 맞는 비주얼(테이프/가이드라인/핏자국)을 보여주고,
// Guideline 단계에서는 플레이어가 근처에서 Interact를 눌러야 확인된 것으로 처리한다.
// =====================================================================
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// 갈림길에 놓인 표식 하나. Tape/Guideline 구간은 항상 통과 가능한 가짜 분기이며,
    /// Guideline 구간만 Interact(Hold)로 손 확인을 요구한다. Bloodstain은 확인 수단이 없다.
    /// </summary>
    public class MarkerPoint : MonoBehaviour
    {
        [SerializeField] GameObject tapeVisual;      // 테이프 단계일 때 켜질 비주얼(모델/이펙트) 오브젝트
        [SerializeField] GameObject guidelineVisual;  // 가이드라인 단계일 때 켜질 비주얼 오브젝트
        [SerializeField] GameObject bloodstainVisual; // 핏자국 단계일 때 켜질 비주얼 오브젝트
        [SerializeField] PlayerController player;      // 거리 계산과 Interact 입력 확인용 플레이어 참조
        [SerializeField] float touchRange = 1.2f; // 손으로 표식을 확인할 수 있는 최대 거리(미터)

        MarkerStage stage = MarkerStage.Tape; // 이 표식이 현재 어떤 단계로 표시되고 있는지(기본값 Tape)
        bool touchConfirmed; // Guideline 단계에서 손으로 확인했는지 여부(한 번 true가 되면 유지)

        // Guideline 단계가 아니거나(=Tape/Bloodstain) 이미 확인했으면 true
        public bool IsConfirmed => stage != MarkerStage.Guideline || touchConfirmed;
        // "=>" 표현식 프로퍼티: 조건식 하나로 계산되는 읽기 전용 값.
        // 논리 흐름: Guideline이 "아니면" 무조건 true(확인 불필요) / Guideline이면 touchConfirmed 값을 그대로 반환

        // 표식 단계를 갱신하고 그에 맞는 비주얼만 활성화
        public void SetStage(MarkerStage newStage) // 외부(MarkerSpawner)에서 단계 전환 시 호출
        {
            Debug.Log(newStage);
            stage = newStage; // 내부 상태를 새 단계로 교체
            touchConfirmed = false; // 단계가 바뀌면 확인 상태 초기화(이전 단계에서 확인했던 기록은 무효)

            // null 체크 후 SetActive: 인스펙터에서 비주얼을 안 연결해놔도 에러 없이 넘어가도록 방어
            if (tapeVisual != null) tapeVisual.SetActive(stage == MarkerStage.Tape); // 현재 단계가 Tape일 때만 켜고, 아니면 끔
            if (guidelineVisual != null) guidelineVisual.SetActive(stage == MarkerStage.Guideline); // Guideline일 때만 켬
            if (bloodstainVisual != null) bloodstainVisual.SetActive(stage == MarkerStage.Bloodstain); // Bloodstain일 때만 켬
        }

        void Update() // 매 프레임 호출되는 유니티 생명주기 함수
        {
            // Guideline 단계가 아니거나 이미 확인됐거나 플레이어 참조가 없으면 검사할 필요 없음
            if (stage != MarkerStage.Guideline || touchConfirmed || player == null) return; // 조건 중 하나라도 참이면 이후 로직 스킵(성능/의미상 불필요한 계산 방지)

            float distance = Vector3.Distance(player.transform.position, transform.position); // 플레이어와 이 표식 사이의 3D 직선 거리 계산
            if (distance <= touchRange && player.InteractHeld) // 사거리 안에 있고 "동시에" Interact 키를 누르고 있는 중이면
                touchConfirmed = true; // 손으로 확인 완료 처리(이후 IsConfirmed가 true로 바뀜)
        }
    }
}
