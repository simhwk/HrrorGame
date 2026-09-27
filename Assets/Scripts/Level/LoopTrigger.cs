using UnityEngine;

namespace CaveGame
{
    // 플레이어가 선(BoxCollider의 가운데 면)을 가로지르면 LoopManager에 알린다 (끝선 / 시작선 / 중간 체크포인트).
    // 물리 트리거 이벤트는 쓰지 않는다 — 물리는 0.02초마다 "겹침"만 찍기 때문에 빠르게 움직이거나 프레임이 튀면 선을 건너뛴다.
    // 대신 매 프레임 "지난 위치 → 지금 위치" 선분이 선을 통과했는지 계산하므로 속도·프레임과 무관하게 정확하다.
    // 트리거의 파란 화살표(forward)가 플레이어의 정주행 방향을 향해야 한다. 박스의 폭·높이가 선의 유효 범위다.
    [RequireComponent(typeof(BoxCollider))]
    public class LoopTrigger : MonoBehaviour
    {
        enum Kind { LoopEnd, Reverse, Checkpoint }

        // 한 프레임에 이보다 멀리 움직였다면 걸어서 온 게 아니라 순간이동이다 (10m/s × 최대 프레임 0.33초 ≈ 3.3m)
        const float MaxWalkStep = 5f;

        [SerializeField] Kind kind;
        [SerializeField] LoopManager loopManager;

        BoxCollider box;
        Vector3 lastLocal;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void OnEnable() => loopManager.Teleported += Resync;
        void OnDisable() => loopManager.Teleported -= Resync;
        void Start() => Resync();

        void Resync() => lastLocal = PlayerLocal();

        Vector3 PlayerLocal() => transform.InverseTransformPoint(loopManager.PlayerPosition) - box.center;

        // 플레이어 이동(Update)이 끝난 뒤 판정한다
        void LateUpdate()
        {
            Vector3 from = lastLocal;
            Vector3 to = PlayerLocal();
            lastLocal = to; // Notify 안에서 순간이동하면 Resync가 이 값을 새 위치로 덮어쓴다

            bool forward = from.z < 0f && to.z >= 0f;
            bool backward = from.z >= 0f && to.z < 0f;
            if (!forward && !backward) return;
            if ((to - from).sqrMagnitude > MaxWalkStep * MaxWalkStep) return;
            if (!CrossedInsideLine(from, to)) return;

            Notify(forward);
        }

        // 선분이 선의 면(z=0)을 지나는 지점이 박스 폭·높이 안인가
        bool CrossedInsideLine(Vector3 from, Vector3 to)
        {
            Vector3 hit = Vector3.Lerp(from, to, from.z / (from.z - to.z));
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(hit.x) <= half.x && Mathf.Abs(hit.y) <= half.y;
        }

        void Notify(bool forward)
        {
            if (kind == Kind.LoopEnd && forward) loopManager.OnLoopEndReached(transform);
            else if (kind == Kind.Reverse && !forward) loopManager.OnReverseLoopReached(transform);
            else if (kind == Kind.Checkpoint) loopManager.OnCheckpointCrossed(forward);
        }
    }
}
