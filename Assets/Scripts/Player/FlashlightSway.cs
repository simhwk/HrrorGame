// =====================================================================
// [스크립트 역할] 손전등(FlashLight) 흔들림 연출
// 마우스 시점 이동에 반응하는 관성 스웨이, 이동 중 발걸음에 맞춘 바빙,
// 정지 상태에서도 손이 미세하게 떨리는 아이들 지터를 합성해
// 실제로 손에 들고 있는 손전등처럼 자연스럽게 흔들리게 만든다.
// Player/Hand/FlashLight (Light가 붙어있는 오브젝트)에 부착해서 사용한다.
// =====================================================================
using UnityEngine;

namespace CaveGame
{
    public class FlashlightSway : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] PlayerController playerController; // 이동/시점 입력을 읽어올 플레이어 컨트롤러(비워두면 부모에서 자동 탐색)

        [Header("시점 스웨이 (마우스로 돌아볼 때 관성으로 살짝 반대로 밀렸다가 되돌아옴)")]
        [SerializeField] float lookSwayAmount = 0.6f;   // 시점 이동량 대비 회전 스웨이 배율(각도)
        [SerializeField] float lookSwaySmooth = 6f;     // 스웨이가 목표치로 수렴하는 속도(클수록 빠르게 따라붙고 진동이 짧게 끝남)
        [SerializeField] float maxLookSway = 6f;        // 스웨이 각도 최대치(도) — 너무 심하게 꺾이지 않도록 제한

        [Header("이동 바빙 (걸을 때 손전등이 위아래/좌우로 흔들림)")]
        [SerializeField] float bobFrequency = 1.8f;       // 걸음 주기(초당 흔들림 횟수)
        [SerializeField] float bobPosAmount = 0.015f;     // 위치 흔들림 폭(미터)
        [SerializeField] float bobRotAmount = 2.2f;       // 회전 흔들림 폭(도)
        [SerializeField] float moveInputThreshold = 0.1f; // 이 값 이상 이동 입력이 있어야 "걷는 중"으로 판정

        [Header("정지 시 손떨림 (Perlin Noise 기반 미세 지터)")]
        [SerializeField] float idleJitterAmount = 0.06f; // 정지 중 흔들림 각도 폭(도)
        [SerializeField] float idleJitterSpeed = 0.6f;   // Perlin 노이즈를 샘플링하는 속도(클수록 빠르게 떨림)

        Vector3 initialLocalPos;   // 스웨이/바빙을 더해줄 기준 위치(원래 배치된 자리)
        Quaternion initialLocalRot; // 스웨이/바빙을 더해줄 기준 회전
        Vector2 currentLookSway;   // 현재 프레임의 스웨이 각도(목표치로 부드럽게 수렴시키기 위한 상태값)
        float bobTimer;            // 걸음 바빙에 쓰는 누적 시간(멈추면 진행하지 않아 뚝 끊기지 않게 함)
        float noiseSeedX, noiseSeedY; // 개체별로 다른 지터 패턴을 만들기 위한 랜덤 오프셋

        void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>(); // Hand -> Player 계층에서 자동으로 찾음

            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;

            noiseSeedX = Random.Range(0f, 100f); // 씨앗을 다르게 줘서 X/Y 지터가 서로 동기화되지 않게 함
            noiseSeedY = Random.Range(0f, 100f);
        }

        void Update()
        {
            Vector2 lookDelta = playerController != null ? playerController.LastLookDelta : Vector2.zero;
            Vector2 moveInput = playerController != null ? playerController.MoveInput : Vector2.zero;
            bool isMoving = moveInput.sqrMagnitude > moveInputThreshold * moveInputThreshold
                            && (playerController == null || playerController.IsGrounded);

            // 1) 시점 스웨이: 마우스를 움직인 반대 방향으로 살짝 회전이 밀렸다가 서서히 복귀(관성 느낌)
            Vector2 targetSway = new Vector2(-lookDelta.y, lookDelta.x) * lookSwayAmount;
            targetSway = Vector2.ClampMagnitude(targetSway, maxLookSway);
            currentLookSway = Vector2.Lerp(currentLookSway, targetSway, Time.deltaTime * lookSwaySmooth);

            // 2) 이동 바빙: 걷는 동안에만 타이머가 진행되어 사인파 기반 흔들림을 만듦
            if (isMoving)
                bobTimer += Time.deltaTime * bobFrequency * Mathf.PI * 2f;

            Vector3 bobPos = Vector3.zero;
            Vector3 bobRot = Vector3.zero;
            if (isMoving)
            {
                float bobSin = Mathf.Sin(bobTimer);
                float bobCos = Mathf.Cos(bobTimer * 0.5f); // 절반 주기로 좌우 성분을 더해 실제 걸음의 8자 궤적을 흉내냄
                bobPos = new Vector3(bobCos * bobPosAmount, Mathf.Abs(bobSin) * bobPosAmount, 0f);
                bobRot = new Vector3(bobSin * bobRotAmount * 0.5f, 0f, bobCos * bobRotAmount);
            }

            // 3) 정지 시 손떨림: Perlin Noise로 -1~1 사이를 매끄럽게 오가며 완전히 고정되지 않는 미세한 흔들림을 만듦
            float jitterX = (Mathf.PerlinNoise(noiseSeedX, Time.time * idleJitterSpeed) - 0.5f) * 2f;
            float jitterY = (Mathf.PerlinNoise(noiseSeedY, Time.time * idleJitterSpeed) - 0.5f) * 2f;
            Vector3 idleJitter = new Vector3(jitterY, jitterX, 0f) * idleJitterAmount;

            transform.localPosition = initialLocalPos + bobPos;
            transform.localRotation = initialLocalRot * Quaternion.Euler(
                currentLookSway.x + bobRot.x + idleJitter.x,
                currentLookSway.y + bobRot.y + idleJitter.y,
                bobRot.z);
        }
    }
}
