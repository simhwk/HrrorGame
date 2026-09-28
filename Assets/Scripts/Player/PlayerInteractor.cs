using UnityEngine;

namespace CaveGame
{
    // 카메라 정면으로 한 번 SphereCast를 쏴서 조준 중인 대상을 찾고, E 입력을 전달한다.
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float range = 2f;
        [Tooltip("얇은 줄도 잡히도록 레이 대신 이 반경의 SphereCast를 쓴다")]
        [SerializeField] float aimRadius = 0.08f;
        [SerializeField] LayerMask aimMask = ~0;

        PlayerController player;
        readonly RaycastHit[] hits = new RaycastHit[8];

        public IInteractable Target { get; private set; }

        void Awake() => player = GetComponent<PlayerController>();

        void Update()
        {
            Vector3 point = default;
            SetTarget(player.enabled ? FindTarget(out point) : null); // 엔딩 컷신 중엔 조작 불가

            if (Target != null && player.InteractPressed)
                Target.Interact(point);
        }

        void OnDisable() => SetTarget(null);

        IInteractable FindTarget(out Vector3 point)
        {
            point = default;
            Transform cam = player.CameraPivot;

            // 벽 뒤의 표식은 가려지도록, 처음 맞은 콜라이더가 표식일 때만 인정.
            // 카메라가 캡슐 안쪽 위에 있어 바닥을 내려다보면 자기 몸에 먼저 맞으므로 플레이어 콜라이더는 건너뛴다
            int count = Physics.SphereCastNonAlloc(cam.position, aimRadius, cam.forward, hits, range,
                                                   aimMask, QueryTriggerInteraction.Ignore);
            RaycastHit nearest = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= nearestDistance) continue;
                nearest = hits[i];
                nearestDistance = hits[i].distance;
            }
            if (nearest.collider == null) return null;

            point = nearest.point;
            return nearest.collider.GetComponentInParent<IInteractable>();
        }

        void SetTarget(IInteractable next)
        {
            if (next == Target) return;
            if (Target != null) Target.SetHover(false);
            Target = next;
            if (Target != null) Target.SetHover(true);
        }
    }
}
