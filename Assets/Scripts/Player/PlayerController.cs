using UnityEngine;
using UnityEngine.InputSystem;

namespace CaveGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] Transform cameraPivot;
        [SerializeField] Transform handPivot;
        [Header("카메라 지연 — 손전등은 마우스를 즉시, 카메라는 살짝 늦게 따라간다")]
        [SerializeField] float cameraLagSpeed = 11f;     // 상하 (클수록 빨리 따라붙음)
        [SerializeField] float cameraYawLagSpeed = 9f;  // 좌우
        [Tooltip("카메라가 손전등보다 뒤처질 수 있는 최대 각도 — 확 돌아도 이 이상 벌어지지 않아 어지럽지 않다")]
        [SerializeField] float maxCameraPitchLag = 6f;
        [SerializeField] float maxCameraYawLag = 20f;
        [SerializeField] float standingHeight = 2.38f;
        [SerializeField] float crouchedHeight = 1.25f;

        [Header("눈 높이 — 카메라는 항상 캡슐 안쪽에 둔다 (벽/천장 관통 방지)")]
        [SerializeField] float eyeHeadroom = 0.12f; // 캡슐 꼭대기에서 눈까지의 거리
        [SerializeField] Vector3 handOffsetFromEye = new Vector3(0.25f, -0.24f, 0.2f); // 손은 눈을 따라가 화면 속 위치가 고정된다
        [SerializeField] float ceilingMargin = 0.08f; // 천장과 눈 사이 최소 간격 (낮은 천장 아래서 일어서는 중 캡슐이 천장에 파묻히는 경우 대비)
        [SerializeField] LayerMask ceilingMask = ~0;

        [SerializeField] float moveSpeed = 1.8f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float heightBlendSpeed = 8f;
        [SerializeField] float loopCrouchBlendSpeed = 1.5f; // 루프 시작 웅크림은 몸이 서서히 굽듯 천천히
        [SerializeField] float lookUpLimit = 80f;
        [SerializeField] float lookDownLimit = 80f;
        [SerializeField] float crouchLookUpLimit = 20f;

        [Header("천장 자동 웅크림 — 선 채로 머리 위 여유가 이 값 이하면 웅크리고, 일어설 공간이 생기면 편다")]
        [SerializeField] float autoCrouchClearance = 0.1f;
        [Tooltip("일어설 땐 이만큼 더 여유가 있어야 편다 — 경계에서 앉았다 일어섰다 떨리지 않게")]
        [SerializeField] float autoStandExtra = 0.15f;
        [Tooltip("이동 방향으로 이만큼 앞의 천장도 검사한다 — 낮아지는 천장 턱에 머리가 먼저 걸리지 않게")]
        [SerializeField] float ceilingLookAhead = 0.35f;

        CharacterController controller;
        InputAction moveAction;
        InputAction lookAction;
        InputAction crouchAction;
        InputAction interactAction;

        float pitch;
        float currentPitch;
        float cameraYawOffset;
        float verticalVelocity;
        bool ceilingCrouch;
        bool ceilingAutoCrouch; // 루프가 켜 준다 (4루프부터)
        readonly RaycastHit[] ceilingHits = new RaycastHit[8];
        const float EyeProbeRadius = 0.1f;
        bool heldCrouch; // 루프 시작 시 웅크린 채로 시작 — 웅크리기 키를 눌렀다 떼면 풀린다
        Vector2 lastLookDelta;
        public Transform CameraPivot => cameraPivot;
        public bool IsCrouched => ceilingCrouch || heldCrouch || (crouchAction != null && crouchAction.IsPressed());
        public bool InteractPressed => interactAction != null && interactAction.WasPressedThisFrame();
        public Vector2 LastLookDelta => lastLookDelta;
        public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        public bool IsGrounded => controller != null && controller.isGrounded;

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            var map = inputActions.FindActionMap("Player");
            moveAction = map.FindAction("Move");
            lookAction = map.FindAction("Look");
            crouchAction = map.FindAction("Crouch");
            interactAction = map.FindAction("Interact");
            map.Enable();
        }

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            if (heldCrouch && crouchAction != null && crouchAction.WasReleasedThisFrame()) heldCrouch = false;
            HandleLook();
            UpdateCeilingCrouch();
            HandleHeight();
            HandleMove();
        }

        void HandleLook()
        {
            Vector2 delta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            lastLookDelta = delta;

            transform.Rotate(Vector3.up * delta.x);

            // 지연은 상한으로 자르고, 프레임레이트와 무관하게 지수적으로 줄인다
            cameraYawOffset = Mathf.Clamp(cameraYawOffset - delta.x, -maxCameraYawLag, maxCameraYawLag);
            cameraYawOffset *= Mathf.Exp(-cameraYawLagSpeed * Time.deltaTime);

            float upLimit = Mathf.Lerp(lookUpLimit, Mathf.Min(lookUpLimit, crouchLookUpLimit), CrouchBlend());
            pitch = Mathf.Clamp(pitch - delta.y, -upLimit, lookDownLimit);
            float pitchLag = Mathf.Clamp(currentPitch - pitch, -maxCameraPitchLag, maxCameraPitchLag);
            currentPitch = pitch + pitchLag * Mathf.Exp(-cameraLagSpeed * Time.deltaTime);

            cameraPivot.localRotation = Quaternion.Euler(currentPitch, cameraYawOffset, 0f);

            if (handPivot != null)
                handPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void HandleHeight()
        {
            float targetHeight = IsCrouched ? crouchedHeight : standingHeight;
            bool manual = crouchAction != null && crouchAction.IsPressed();
            float blendSpeed = heldCrouch && !manual ? loopCrouchBlendSpeed : heightBlendSpeed;
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * blendSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            float eyeHeight = ClampEyeBelowCeiling(controller.height - eyeHeadroom);
            cameraPivot.localPosition = new Vector3(0f, eyeHeight, 0f);

            if (handPivot != null)
                handPivot.localPosition = new Vector3(0f, eyeHeight, 0f) + handOffsetFromEye;
        }

        // 캡슐 높이 변경(일어서기)은 충돌 판정 없이 늘어나므로, 위쪽을 직접 검사해 눈이 천장 밖으로 나가지 않게 막는다
        float ClampEyeBelowCeiling(float eyeHeight) =>
            Mathf.Min(eyeHeight, CeilingHeightAt(transform.position, EyeProbeRadius) - ceilingMargin);

        void UpdateCeilingCrouch()
        {
            if (!ceilingAutoCrouch)
            {
                ceilingCrouch = false;
                return;
            }

            float room = CeilingHeightAt(transform.position, controller.radius * 0.9f);
            Vector2 input = MoveInput;
            if (input.sqrMagnitude > 0.01f)
            {
                Vector3 dir = (transform.right * input.x + transform.forward * input.y).normalized;
                room = Mathf.Min(room, CeilingHeightAt(transform.position + dir * ceilingLookAhead, controller.radius * 0.9f));
            }

            float spare = room - standingHeight; // 선 키 기준 머리 위 여유
            if (!ceilingCrouch && spare <= autoCrouchClearance) ceilingCrouch = true;
            else if (ceilingCrouch && spare >= autoCrouchClearance + autoStandExtra) ceilingCrouch = false;
        }

        // 발바닥에서 바로 위 천장까지의 높이. 천장이 없으면 무한대.
        // 선 키로 쏘면 이미 천장에 파묻힌 머리에서 레이가 시작돼 못 맞히므로, 발 쪽에서 위로 쏜다.
        // 웅크림 판정은 몸통 폭(굵은 구), 눈 높이 제한은 머리 한가운데(가는 구)로 검사한다
        float CeilingHeightAt(Vector3 feet, float probe)
        {
            Vector3 origin = feet + Vector3.up * controller.radius;
            int count = Physics.SphereCastNonAlloc(origin, probe, Vector3.up, ceilingHits, standingHeight + 1f,
                ceilingMask, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (ceilingHits[i].collider != controller && ceilingHits[i].distance > 0f) // 자기 캡슐, 시작부터 겹친 것 제외
                    nearest = Mathf.Min(nearest, ceilingHits[i].distance);
            return controller.radius + nearest + probe;
        }

        float CrouchBlend() => Mathf.Clamp01(Mathf.InverseLerp(standingHeight, crouchedHeight, controller.height));

        void HandleMove()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * moveSpeed;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }

        public void SetCeilingAutoCrouch(bool enabled) => ceilingAutoCrouch = enabled;

        public void HoldCrouchUntilRelease() => heldCrouch = true;
    }
}
