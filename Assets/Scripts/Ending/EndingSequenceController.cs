// =====================================================================
// [스크립트 역할] 게임 마지막 엔딩 컷신 연출 제어
// 플레이어 조작을 끄고 카메라를 컷신 위치로 이동시킨 뒤 낙하시키고,
// 화면 없이 공격 사운드만 재생한 후 일정 시간 뒤 빛이 드는 연출로 마무리한다.
// =====================================================================
using System.Collections; // IEnumerator(코루틴 반환 타입)를 쓰기 위해 필요
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// 예외 규정 구간 — 컷신 진입 → 카메라 낙하(시점 분리) → 사운드 습격(화면 없음) → 빛 드는 틈.
    /// 크리처는 끝까지 화면에 등장하지 않는다.
    /// </summary>
    public class EndingSequenceController : MonoBehaviour
    {
        [SerializeField] PlayerController player; // 조작을 끄고 카메라를 가져올 대상 플레이어
        [SerializeField] Transform cutsceneCameraTarget;    // 컷신 진입 시 카메라가 이동할 목표 지점(위치+회전)
        [SerializeField] Transform droppedCameraRestPose;   // 카메라 낙하 후 최종 정지 위치(바닥에 떨어진 각도)
        [SerializeField] Light endingLight;                 // 엔딩 마지막에 켜지는 빛(탈출구에서 비치는 빛)
        [SerializeField] DreadAudioSequencer audioSequencer; // 공격 사운드를 재생시킬 대상
        [SerializeField] float cutsceneDuration = 3f;       // 컷신 카메라 진입에 걸리는 시간(초)
        [SerializeField] float dropDuration = 1.2f;         // 카메라 낙하 연출 시간(초)
        [SerializeField] float attackToLightDelay = 4f;     // 공격 사운드 이후 빛이 켜지기까지 대기 시간(초)

        // 외부(LoopManager 등)에서 엔딩 시퀀스를 시작시키는 진입점
        public void BeginEnding() => StartCoroutine(EndingRoutine());
        // StartCoroutine: EndingRoutine을 "여러 프레임에 걸쳐 순차 실행"되도록 시작시킴(끝날 때까지 기다리지 않고 즉시 반환)

        IEnumerator EndingRoutine() // 코루틴: yield return을 만날 때마다 실행을 잠시 멈추고 다음 프레임(또는 지정 시간 후)에 이어서 진행
        {
            player.enabled = false; // 플레이어 조작 스크립트를 비활성화(Update가 멈춤) — 이제부터 카메라는 이 스크립트가 직접 제어

            yield return MoveCamera(player.CameraPivot, cutsceneCameraTarget, cutsceneDuration);
            // MoveCamera 코루틴이 "끝날 때까지" 여기서 대기(컷신 위치로 카메라 이동 완료 후 다음 줄 진행)
            yield return MoveCamera(player.CameraPivot, droppedCameraRestPose, dropDuration);
            // 이어서 카메라를 "낙하한 것처럼" 정지 위치까지 이동(완료될 때까지 대기)

            audioSequencer.PlayAttack(player.CameraPivot.position); // 화면에는 안 보이는 공격 사운드를 카메라 위치에서 재생(즉시 실행, 대기 없음)

            yield return new WaitForSeconds(attackToLightDelay);
            // WaitForSeconds: 지정한 시간(4초)만큼 아무것도 안 하고 대기(공격 후의 정적을 표현)

            if (endingLight != null)
                endingLight.enabled = true; // 대기가 끝나면 빛을 켜서 "빛 드는 틈" 연출로 마무리
        }

        // 카메라를 시작 위치/회전에서 목표 위치/회전까지 부드럽게 보간 이동시키는 코루틴
        IEnumerator MoveCamera(Transform cam, Transform target, float duration)
        {
            Vector3 startPos = cam.position;       // 이동 시작 시점의 현재 위치를 기록(보간 기준점)
            Quaternion startRot = cam.rotation;    // 이동 시작 시점의 현재 회전을 기록
            float t = 0f; // 경과 시간 누적용 변수

            while (t < duration) // 목표 시간에 도달하기 전까지 매 프레임 반복
            {
                t += Time.deltaTime; // 이번 프레임 동안 지난 시간을 누적
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                // t/duration: 0~1로 정규화된 진행률. SmoothStep은 Lerp와 달리 시작/끝에서 부드럽게 가속·감속되는 곡선을 만들어줌
                cam.position = Vector3.Lerp(startPos, target.position, k); // 시작 위치에서 목표 위치까지 k 비율만큼 보간해 대입
                cam.rotation = Quaternion.Slerp(startRot, target.rotation, k); // 회전은 Slerp(구면 선형보간)를 사용해 자연스럽게 회전 보간
                yield return null; // 한 프레임 쉬고 다음 프레임에 while문 계속(=매 프레임 조금씩 이동하는 애니메이션 효과)
            }

            cam.SetPositionAndRotation(target.position, target.rotation);
            // 반복이 끝난 뒤 부동소수점 오차 등으로 목표값에 딱 안 맞을 수 있으니, 마지막에 정확한 목표값으로 스냅(보정)
        }
    }
}
