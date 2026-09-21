// =====================================================================
// [스크립트 역할] 1인칭 플레이어 이동/시점/자세(크라우치) 컨트롤러
// New Input System을 사용해 이동·시점·크라우치·상호작용 입력을 읽고,
// CharacterController를 통해 실제 이동과 중력을 처리하며 표식 단계나
// 저천장 구역에 따라 강제로 구부린 자세로 전환한다.
// =====================================================================
using UnityEngine;
using UnityEngine.InputSystem; // InputActionAsset, InputAction 등 New Input System 타입

namespace CaveGame
{
    [RequireComponent(typeof(CharacterController))] // 이동 처리를 CharacterController에 위임하므로 필수 컴포넌트로 강제
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions; // 입력 액션들이 정의된 에셋(InputSystem_Actions.inputactions) 참조
        [SerializeField] Transform cameraPivot;          // 카메라가 실제로 붙어서 상하로 회전하는 축(눈높이) 오브젝트
        [SerializeField] Transform handPivot;            // Hand 오브젝트 참조
        [SerializeField] float cameraLagSpeed = 8f;      // 낮은수록 카메라가 더 늣게 따라옴
        [SerializeField] float cameraYawLagSpeed = 5f;   // 좌우(yaw) 카메라 지연 속도 — 낮을수록 몸이 돈 뒤 카메라가 더 늦게 따라옴
        [SerializeField] float standingHeight = 1.6f;      // 서 있을 때 캐릭터 높이(미터)
        [SerializeField] float crouchedHeight = 1.0f;      // 구부렸을 때 캐릭터 높이(미터)
        [SerializeField] float moveSpeed = 1.8f;            // 기본 이동 속도(미터/초)
        [SerializeField] float crouchSpeedMultiplier = 0.55f; // 구부린 상태에서의 이동속도 배율(느려짐)
        [SerializeField] float mouseSensitivity = 0.12f;    // 마우스(시점) 감도
        [SerializeField] float gravity = -9.81f;            // 중력 가속도(아래 방향이라 음수)
        [SerializeField] float heightBlendSpeed = 8f;      // 서기/구부리기 높이 전환 보간 속도(클수록 빠르게 전환)

        CharacterController controller; // 유니티 기본 캡슐형 충돌/이동 처리 컴포넌트(직접 캐싱해서 반복 GetComponent 호출 방지)
        InputAction moveAction;    // "이동" 입력 액션(보통 WASD/스틱)
        InputAction lookAction;    // "시점 회전" 입력 액션(마우스/오른쪽 스틱)
        InputAction crouchAction;  // "크라우치(수동)" 입력 액션
        InputAction interactAction; // "상호작용(Interact)" 입력 액션

        float pitch;              // 카메라 상하 회전 각도(누적값, 도 단위)
        float currentPitch;        // 카메라에 실제로 적용되는, pitch를 서서히 따라가는 값(지연 연출용)
        float cameraYawOffset;     // 카메라가 아직 따라잡지 못한 좌우 각도 오차(0으로 서서히 수렴하면서 카메라만 뒤늦게 따라옴)
        float verticalVelocity;   // 중력에 의한 수직 속도(매 프레임 누적되는 낙하 속도)
        bool stageForcedCrouch;   // 표식 단계(GameManager/LoopManager)에 의해 강제된 크라우치 여부
        bool zoneForcedCrouch;    // 저천장 존(CeilingZone)에 의해 강제된 크라우치 여부
        Vector2 lastLookDelta;      // FlashlightSway 등 외부 스크립트가 참고할 수 있게 저장해두는 마지막 프레임의 시점 이동량

        public Transform CameraPivot => cameraPivot; // 외부(EndingSequenceController 등)가 카메라 축을 참조할 수 있게 읽기 전용으로 공개
        // 강제 크라우치(단계/존) 또는 사용자가 직접 크라우치 키를 누르고 있으면 true
        public bool IsCrouched => stageForcedCrouch || zoneForcedCrouch || (crouchAction != null && crouchAction.IsPressed());
        // 세 조건 중 하나라도 참이면 크라우치 상태 — OR 연산이라 "강제든 자발적이든 하나라도 있으면 구부림"
        public bool InteractHeld => interactAction != null && interactAction.IsPressed();
        public Vector2 LastLookDelta => lastLookDelta; // 이번 프레임에 마우스가 얼마나 움직였는지(감도 반영 후) — 손전등 스웨이 계산용
        public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero; // 이동 입력값 — 걸음 바빙 계산용
        public bool IsGrounded => controller != null && controller.isGrounded; // 착지 여부 — 걷는 중인지 판정용
        // MarkerPoint가 "지금 Interact 키를 누르고 있는지" 확인할 때 쓰는 읽기 전용 프로퍼티

        void Awake() // 씬 로드 시 가장 먼저 한 번 호출
        {
            controller = GetComponent<CharacterController>(); // 같은 GameObject에 붙어있는 CharacterController를 찾아 캐싱

            // Input Actions 에셋에서 "Player" 액션맵과 각 액션들을 캐싱
            var map = inputActions.FindActionMap("Player"); // 에셋 안에서 이름이 "Player"인 액션 맵(그룹)을 찾음
            moveAction = map.FindAction("Move");         // 그 맵 안에서 "Move"라는 이름의 액션을 찾아 저장
            lookAction = map.FindAction("Look");         // "Look" 액션(시점)
            crouchAction = map.FindAction("Crouch");     // "Crouch" 액션(수동 크라우치)
            interactAction = map.FindAction("Interact"); // "Interact" 액션(상호작용)
            map.Enable(); // 이 액션 맵 전체를 활성화해야 실제로 입력값을 읽을 수 있음(안 하면 항상 0)
        }

        void OnEnable() // 이 컴포넌트가 활성화될 때마다 호출
        {
            Cursor.lockState = CursorLockMode.Locked; // 마우스 커서를 화면 중앙에 고정(1인칭 시점에서 마우스가 화면 밖으로 안 나가게)
        }

        void Update() // 매 프레임 호출 — 입력 처리는 보통 Update에서 한다(물리가 아닌 일반 로직이라 FixedUpdate 불필요)
        {
            HandleLook();   // 1) 시점 회전 처리
            HandleHeight();  // 2) 서기/구부리기 높이 보간 처리
            HandleMove();    // 3) 실제 이동(+중력) 처리
        }

        // 마우스 입력으로 몸(좌우)과 카메라(상하) 회전 처리
        void HandleLook()
        {
            Vector2 delta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            // ReadValue<Vector2>(): 이번 프레임의 마우스/스틱 이동량(x,y)을 읽고 감도를 곱해 실제 회전량으로 변환
            lastLookDelta = delta; // 외부 스크립트(FlashlightSway 등)가 참고할 수 있도록 저장

            transform.Rotate(Vector3.up * delta.x); // 몸(+이동 방향)은 즉시 좌우 회전 — 조작감은 그대로 유지

            cameraYawOffset -= delta.x; // 몸이 방금 돈 만큼 카메라는 아직 안 돈 것으로 남겨둠(부호 반대로 누적)
            cameraYawOffset = Mathf.Lerp(cameraYawOffset, 0f, Time.deltaTime * cameraYawLagSpeed); // 서서히 0(=몸과 정렬)으로 수렴 → 카메라만 뒤늦게 따라옴

            pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f); // 위아래 각도를 누적하되 -80~80도로 제한(고개가 뒤로 안 꺾이게)
            currentPitch = Mathf.LerpAngle(currentPitch, pitch, Time.deltaTime * cameraLagSpeed); // 카메라의 상하 회전도 목표치(pitch)를 서서히 따라감

            cameraPivot.localRotation = Quaternion.Euler(currentPitch, cameraYawOffset, 0f); // 카메라만 상하(pitch)+좌우(yaw) 모두 지연 적용(몸은 즉시, 카메라는 뒤늦게 반응하는 분리 구조)

            if(handPivot  != null)
            {
                handPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 손(손전등)은 기존처럼 시점에 즉시 반응 유지
            }
        }

        // 서기/구부리기 상태에 따라 캐릭터 높이와 카메라 위치를 부드럽게 보간
        void HandleHeight()
        {
            float targetHeight = IsCrouched ? crouchedHeight : standingHeight; // 삼항 연산자: 크라우치 중이면 낮은 높이, 아니면 서 있는 높이를 목표값으로 설정
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * heightBlendSpeed);
            // Lerp(현재값, 목표값, 보간비율): 매 프레임 목표 높이 쪽으로 조금씩 이동시켜 "뚝뚝 끊기지 않고" 부드럽게 높이가 변함
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f); // 캡슐의 중심을 높이의 절반 위치로 재조정(높이가 바뀌면 발이 땅속에 박히거나 뜨는 걸 방지)
            cameraPivot.localPosition = new Vector3(0f, controller.height * 0.9f, 0f); // 카메라를 캐릭터 높이의 90% 지점(눈높이 근처)에 배치
        }

        // 이동 입력과 중력을 합산해 CharacterController를 실제로 이동시킴
        void HandleMove()
        {
            Vector2 input = moveAction.ReadValue<Vector2>(); // 이번 프레임의 이동 입력(x=좌우, y=앞뒤, 보통 -1~1 범위)
            float speed = moveSpeed * (IsCrouched ? crouchSpeedMultiplier : 1f); // 크라우치 중이면 속도에 배율(0.55)을 곱해 느리게, 아니면 그대로
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;
            // transform.right/forward: 캐릭터가 바라보는 방향 기준의 좌우/앞뒤 벡터. 입력값을 곱해 "월드 기준 이동 방향+속도" 벡터를 만듦

            if (controller.isGrounded && verticalVelocity < 0f) // 바닥에 붙어있고 낙하 중(음수)이라면
                verticalVelocity = -1f; // 아주 약한 하강력만 유지(0으로 하면 경사면에서 살짝 붕 뜨는 문제가 생기기 쉬워서 관용적으로 쓰는 값)
            verticalVelocity += gravity * Time.deltaTime; // 매 프레임 중력을 누적시켜 낙하 속도를 계속 증가시킴(자유낙하 시뮬레이션)
            move.y = verticalVelocity; // 최종 이동 벡터의 y축에 수직 속도(중력 결과)를 대입

            controller.Move(move * Time.deltaTime); // CharacterController에게 "이번 프레임에 이만큼 이동해라" 요청(내부적으로 충돌 처리까지 해줌). Time.deltaTime을 곱해 프레임레이트와 무관하게 초당 속도 기준으로 이동
        }

        /// <summary>표식 단계에 따라 강제되는 이동 모드 — 160cm(서서) / 100cm(구부려).</summary>
        public void SetStageForcedCrouch(bool value) => stageForcedCrouch = value; // LoopManager가 호출: 표식 단계에 의한 강제 크라우치 값을 설정

        /// <summary>루프 지오메트리상 좁아지는 특정 구간(<see cref="CeilingZone"/>)에서 오버라이드.</summary>
        public void SetZoneCrouch(bool value) => zoneForcedCrouch = value; // CeilingZone이 호출: 특정 구역 진입/이탈에 따른 강제 크라우치 값을 설정
    }
}
