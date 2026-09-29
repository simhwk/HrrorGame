using UnityEngine;

namespace CaveGame
{
    // 지정 루프에서 이 지점에 들어서면 심장이 뛰기 시작해, 루프가 끝날 때까지 멎지 않는다 (세기는 Heartbeat의 루프별 값).
    // 7루프: 끝선을 넘는 순간 씬이 엔딩으로 뚝 바뀌며 심장도 함께 끊긴다 — 그 끊김이 점프스케어 직전의 정적이 된다.
    public class HeartbeatZone : LoopListener
    {
        [SerializeField, Min(1)] int loop = 7;
        [SerializeField] Transform triggerCenter;
        [SerializeField] float triggerRadius = 2f;

        Transform player;
        bool armed, beating;

        void Awake()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        protected override void OnLoopChanged(int current)
        {
            armed = current == loop;
            beating = false;
        }

        void Update()
        {
            if (!armed || player == null) return;

            if (!beating)
            {
                Vector3 d = player.position - triggerCenter.position;
                d.y = 0f;
                beating = d.sqrMagnitude <= triggerRadius * triggerRadius;
            }
            if (beating) Heartbeat.Instance?.Raise(1f);
        }

        void OnDrawGizmosSelected()
        {
            if (triggerCenter == null) return;
            Gizmos.color = new Color(0.9f, 0.1f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(triggerCenter.position, triggerRadius);
        }
    }
}
