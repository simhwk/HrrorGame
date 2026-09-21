// =====================================================================
// [스크립트 역할] 루프(공간 반복) 진행 흐름 총괄
// 플레이어가 루프 끝에 도달했을 때 시작 지점으로 되돌리고, 표식 단계 갱신과
// 공포 사운드 재생을 트리거하며, 마지막 루프에서는 엔딩 시퀀스를 시작시킨다.
// 반대로(뒤로) 걸어 나가려 하면 루프 진행 없이 반대쪽 끝으로만 순간이동시켜
// "공간을 벗어날 수 없다"는 연출을 유지한다. 시작/끝 지점의 트리거와 순간이동
// 목적지가 같은 오브젝트를 재사용하므로, 도착 직후 그 자리의 트리거가 곧바로
// 다시 걸리는 핑퐁을 막기 위한 짧은 재트리거 무시 시간을 둔다.
// =====================================================================
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// 공간 1개를 7회 재사용하는 루프 전환 처리. 루프 끝에 도달하면 플레이어를 시작점으로
    /// 되돌리고(같은 지오메트리라 인지되지 않는다), 표식 단계와 사운드를 갱신한다.
    /// </summary>
    public class LoopManager : MonoBehaviour
    {
        [SerializeField] PlayerController player;             // 위치를 되돌릴 대상 플레이어
        [SerializeField] Transform loopStartAnchor;    // 루프 시작 시 플레이어가 되돌아갈 위치/방향(빈 GameObject의 Transform)
        [SerializeField] Transform loopEndAnchor;      // 뒤로 나가려 할 때 되돌아갈 반대쪽 끝 위치(기존 LoopEndTrigger 오브젝트를 그대로 재사용해도 됨)
        [SerializeField] MarkerSpawner markerSpawner;         // 표식들을 한꺼번에 갱신시킬 대상
        [SerializeField] DreadAudioSequencer audioSequencer;  // 루프별 공포 사운드를 재생시킬 대상
        [SerializeField] EndingSequenceController ending;      // 마지막 루프에 도달했을 때 시작시킬 엔딩 컨트롤러
        [SerializeField] float reTriggerCooldown = 0.5f; // 순간이동 직후 이 시간(초) 동안은 어떤 루프 트리거가 걸려도 무시(핑퐁 방지)

        bool endingTriggered; // 엔딩이 이미 시작됐는지 여부 — 한 번 true가 되면 다시 실행되지 않도록 막는 역할(중복 트리거 방지)
        float lastTeleportTime = -999f; // 마지막으로 순간이동을 실행한 시각(Time.time 기준) — 재트리거 무시 판단에 사용

        // GameManager의 루프 진행 이벤트를 구독
        void OnEnable() // 이 컴포넌트가 활성화될 때(씬 로드/오브젝트 켜짐 등) 호출되는 생명주기 함수
        {
            if (GameManager.Instance != null) // 아직 GameManager가 초기화되기 전일 수도 있으니 null 체크
                GameManager.Instance.LoopAdvanced += OnLoopAdvanced;
                // "+="는 이벤트 구독: GameManager가 LoopAdvanced를 Invoke할 때마다 내 OnLoopAdvanced 함수가 자동으로 같이 호출됨
        }

        // 구독 해제(메모리 누수/오류 방지)
        void OnDisable() // 이 컴포넌트가 비활성화/파괴될 때 호출
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoopAdvanced -= OnLoopAdvanced;
                // "-="는 구독 해제: 파괴된 이후에도 이벤트가 호출되면 에러가 나므로 반드시 짝을 맞춰 해제해야 함
        }

        // 시작 시 현재 상태(단계/표식/사운드)를 한 번 적용
        void Start() // 첫 프레임 직전에 한 번 호출
        {
            ApplyStage(GameManager.Instance.CurrentStage); // 현재 저장된 단계(기본 Tape)를 플레이어에 적용
            markerSpawner.SetStage(GameManager.Instance.CurrentStage); // 표식들도 같은 단계로 맞춤
            audioSequencer.PlayCueForLoop(GameManager.Instance.CurrentLoop); // 1루프에 맞는 공포 사운드 시작
        }

        // LoopEndTrigger로부터 호출되는, 루프 끝 지점 도달 처리
        public void OnLoopEndReached() // 플레이어가 루프 끝 콜라이더를 밟았을 때 LoopEndTrigger가 호출
        {
            if (endingTriggered) return; // 이미 엔딩 중이면 아무것도 하지 않고 즉시 종료(중복 방지)
            if (Time.time - lastTeleportTime < reTriggerCooldown) return; // 방금 순간이동한 직후라면 무시(같은 자리 트리거가 곧바로 재발화하는 핑퐁 방지)

            if (GameManager.Instance.CurrentLoop >= GameManager.Instance.TotalLoops) // 현재 루프가 마지막(7)이라면
            {
                // 마지막 루프를 마쳤다면 엔딩 시퀀스 시작
                endingTriggered = true; // 플래그를 세워 이후 재호출을 막음
                ending.BeginEnding();   // 엔딩 컨트롤러의 컷신 코루틴 시작
                return; // 아래의 "루프 재시작" 로직은 실행하지 않음
            }

            RepositionTo(loopStartAnchor);       // 아직 마지막이 아니면: 플레이어를 루프 시작 지점으로 순간 이동
            GameManager.Instance.AdvanceLoop(); // 루프 번호를 1 증가시키고 필요하면 단계 변경 이벤트를 방출
        }

        // LoopReverseTrigger로부터 호출되는, 뒤로(시작점 쪽으로) 나가려 할 때의 처리
        public void OnReverseLoopReached() // 플레이어가 시작점 뒤쪽 콜라이더를 밟았을 때 LoopReverseTrigger가 호출
        {
            if (Time.time - lastTeleportTime < reTriggerCooldown) return; // 방금 순간이동한 직후라면 무시(핑퐁 방지)
            // 루프 카운트/표식 단계/사운드는 건드리지 않는다 — 뒤로 걸어도 진행이 스킵되지 않도록 하기 위함(악용 방지)
            RepositionTo(loopEndAnchor); // 반대쪽 끝으로만 순간이동시켜 "공간을 벗어날 수 없다"는 연출 유지
        }

        // 루프가 진행될 때마다(GameManager 이벤트) 표식/사운드 상태 갱신
        void OnLoopAdvanced(int loop, MarkerStage stage) // GameManager.LoopAdvanced 이벤트에 구독된 콜백 함수(시그니처가 Action<int, MarkerStage>와 일치해야 함)
        {
            ApplyStage(stage);                  // 새 단계에 맞춰 플레이어 강제 자세 갱신
            markerSpawner.SetStage(stage);       // 새 단계에 맞춰 모든 표식 비주얼 갱신
            audioSequencer.PlayCueForLoop(loop); // 새 루프 번호에 맞는 공포 사운드 추가 재생
        }

        // Tape 단계가 아니면(Guideline/Bloodstain) 강제 크라우치 적용
        void ApplyStage(MarkerStage stage)
        {
            player.SetStageForcedCrouch(stage != MarkerStage.Tape);
            // 조건식 (stage != MarkerStage.Tape) 자체가 bool 값 — Tape가 아니면 true(강제 크라우치 On), Tape면 false(Off)
        }

        // CharacterController를 잠시 끄고 플레이어를 지정된 앵커로 순간 이동(충돌 오류 방지)
        void RepositionTo(Transform anchor)
        {
            var controller = player.GetComponent<CharacterController>(); // 플레이어 오브젝트에 붙은 CharacterController 컴포넌트를 가져옴
            controller.enabled = false; // 컨트롤러를 끔 — 켜진 상태에서 강제로 위치를 바꾸면 물리 충돌 오류가 날 수 있어서 잠시 비활성화
            Vector3 current = player.transform.position; // 현재 위치 — y(높이)는 그대로 유지하기 위해 필요
            Vector3 target = new Vector3(anchor.position.x, current.y, anchor.position.z);
            // x/z만 앵커 값으로 텔레포트하고 y는 플레이어의 현재 높이를 그대로 사용
            player.transform.SetPositionAndRotation(target, player.transform.rotation);
            controller.enabled = true; // 이동이 끝났으니 컨트롤러를 다시 켬(이후 다시 정상적으로 이동/충돌 처리)
            lastTeleportTime = Time.time; // 이 시각을 기록해두어, 도착 직후 그 자리의 트리거가 곧바로 재발화하는 것을 잠시 무시하게 함
        }
    }
}
