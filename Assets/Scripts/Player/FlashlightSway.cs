using UnityEngine;

namespace CaveGame
{
    public class FlashlightSway : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] PlayerController playerController;

        [Header("시점 스웨이 (마우스로 돌아볼 때 관성으로 살짝 반대로 밀렸다가 되돌아옴)")]
        [SerializeField] float lookSwayAmount = 0.6f;
        [SerializeField] float lookSwaySmooth = 6f;
        [SerializeField] float maxLookSway = 6f;

        [Header("이동 바빙 (걸을 때 손전등이 위아래/좌우로 흔들림)")]
        [SerializeField] float bobFrequency = 1.8f;
        [SerializeField] float bobPosAmount = 0.015f;
        [SerializeField] float bobRotAmount = 2.2f;
        [SerializeField] float moveInputThreshold = 0.1f;

        [Header("정지 시 손떨림 (Perlin Noise 기반 미세 지터)")]
        [SerializeField] float idleJitterAmount = 0.06f;
        [SerializeField] float idleJitterSpeed = 0.6f;

        Vector3 initialLocalPos;
        Quaternion initialLocalRot;
        Vector2 currentLookSway;
        float bobTimer;
        float noiseSeedX, noiseSeedY;

        void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();

            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;

            noiseSeedX = Random.Range(0f, 100f);
            noiseSeedY = Random.Range(0f, 100f);
        }

        void Update()
        {
            Vector2 lookDelta = playerController != null ? playerController.LastLookDelta : Vector2.zero;
            Vector2 moveInput = playerController != null ? playerController.MoveInput : Vector2.zero;
            bool isMoving = moveInput.sqrMagnitude > moveInputThreshold * moveInputThreshold
                            && (playerController == null || playerController.IsGrounded);

            Vector2 targetSway = new Vector2(-lookDelta.y, lookDelta.x) * lookSwayAmount;
            targetSway = Vector2.ClampMagnitude(targetSway, maxLookSway);
            currentLookSway = Vector2.Lerp(currentLookSway, targetSway, Time.deltaTime * lookSwaySmooth);

            if (isMoving)
                bobTimer += Time.deltaTime * bobFrequency * Mathf.PI * 2f;

            Vector3 bobPos = Vector3.zero;
            Vector3 bobRot = Vector3.zero;
            if (isMoving)
            {
                float bobSin = Mathf.Sin(bobTimer);
                float bobCos = Mathf.Cos(bobTimer * 0.5f);
                bobPos = new Vector3(bobCos * bobPosAmount, Mathf.Abs(bobSin) * bobPosAmount, 0f);
                bobRot = new Vector3(bobSin * bobRotAmount * 0.5f, 0f, bobCos * bobRotAmount);
            }

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
