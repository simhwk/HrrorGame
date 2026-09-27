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
        [SerializeField] float cameraLagSpeed = 8f;
        [SerializeField] float cameraYawLagSpeed = 5f;
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
        [SerializeField] float loopCrouchBlendSpeed = 1.5f; // 루프 강제 웅크림은 몸이 서서히 굽듯 천천히
        [SerializeField] float lookUpLimit = 80f;
        [SerializeField] float lookDownLimit = 80f;
        [SerializeField] float crouchLookUpLimit = 20f;
        [SerializeField] Vector3 crouchHandOffset = new Vector3(0f, -0.07f, -0.06f);
        [SerializeField] float crouchHandPitch = 8f;

        CharacterController controller;
        InputAction moveAction;
        InputAction lookAction;
        InputAction crouchAction;
        InputAction interactAction;

        float pitch;
        float currentPitch;
        float cameraYawOffset;
        float verticalVelocity;
        bool loopForcedCrouch;
        bool heldCrouch; // 루프 시작 시 웅크린 채로 시작 — 웅크리기 키를 눌렀다 떼면 풀린다
        Vector2 lastLookDelta;
        public Transform CameraPivot => cameraPivot;
        public bool IsCrouched => loopForcedCrouch || heldCrouch || (crouchAction != null && crouchAction.IsPressed());
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
            HandleHeight();
            HandleMove();
        }

        void HandleLook()
        {
            Vector2 delta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            lastLookDelta = delta;

            transform.Rotate(Vector3.up * delta.x);

            cameraYawOffset -= delta.x;
            cameraYawOffset = Mathf.Lerp(cameraYawOffset, 0f, Time.deltaTime * cameraYawLagSpeed);

            float upLimit = Mathf.Lerp(lookUpLimit, Mathf.Min(lookUpLimit, crouchLookUpLimit), CrouchBlend());
            pitch = Mathf.Clamp(pitch - delta.y, -upLimit, lookDownLimit);
            currentPitch = Mathf.LerpAngle(currentPitch, pitch, Time.deltaTime * cameraLagSpeed);

            cameraPivot.localRotation = Quaternion.Euler(currentPitch, cameraYawOffset, 0f);

            if (handPivot != null)
                handPivot.localRotation = Quaternion.Euler(pitch + crouchHandPitch * CrouchBlend(), 0f, 0f);
        }

        void HandleHeight()
        {
            float targetHeight = IsCrouched ? crouchedHeight : standingHeight;
            bool manual = crouchAction != null && crouchAction.IsPressed();
            float blendSpeed = (loopForcedCrouch || heldCrouch) && !manual ? loopCrouchBlendSpeed : heightBlendSpeed;
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * blendSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            float eyeHeight = ClampEyeBelowCeiling(controller.height - eyeHeadroom);
            cameraPivot.localPosition = new Vector3(0f, eyeHeight, 0f);

            if (handPivot != null)
                handPivot.localPosition = new Vector3(0f, eyeHeight, 0f) + handOffsetFromEye + crouchHandOffset * CrouchBlend();
        }

        // 캡슐 높이 변경(일어서기)은 충돌 판정 없이 늘어나므로, 위쪽을 직접 검사해 눈이 천장 밖으로 나가지 않게 막는다
        float ClampEyeBelowCeiling(float eyeHeight)
        {
            const float probeRadius = 0.1f;
            Vector3 origin = transform.position + Vector3.up * controller.radius;
            float wantedTop = eyeHeight + ceilingMargin - controller.radius;
            if (wantedTop <= 0f) return eyeHeight;

            if (Physics.SphereCast(origin, probeRadius, Vector3.up, out RaycastHit hit, wantedTop + probeRadius,
                    ceilingMask, QueryTriggerInteraction.Ignore))
            {
                float allowed = controller.radius + hit.distance + probeRadius - ceilingMargin;
                return Mathf.Min(eyeHeight, allowed);
            }
            return eyeHeight;
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

        public void SetLoopForcedCrouch(bool value) => loopForcedCrouch = value;

        public void HoldCrouchUntilRelease() => heldCrouch = true;
    }
}
