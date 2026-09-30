using UnityEngine;

namespace CaveGame
{
    // 5루프: 오른쪽 갈래(줄이 가리키는 정답 쪽) 끝에서 오른쪽 오르막 통로를 올려다보면, 괴물이 이미 거기 있다 —
    // 통로 안쪽을 향해(플레이어를 등지고) 네 발로 서 있다. 얼굴은 반대편이라 안 보인다 (7루프 점프스케어까지 아낀다).
    // 손전등 빛이 몸에 닿는 순간 곧바로 네 발로 통로 안쪽으로 걸어 들어가고, 벽 뒤로 사라지는 순간 한 번 운다.
    // 발소리("스윽… 스윽…")는 몸이 사라진 뒤에도 통로 저편으로 이어지며 점점 작아진다.
    // - 괴물은 루프가 시작될 때부터 그 자리에 있다 — 플레이어가 다가가서 생기는 게 아니라, 원래 있던 것을 "발견"한다.
    // - 괴물은 path(통로 중심선을 따라 놓은 점들) 위를 걷는다 — 몸을 굽히지 못하니 구불고 좁은 통로에선 중심선을 따라야 벽에 안 박힌다.
    //   몸 방향은 경로 위 앞뒤 두 점을 잇는 방향이라 커브에서 부드럽게 돈다. 높이는 바닥을 따라간다.
    // - 괴물은 플레이어를 쫓지 않는다 — 앞서 간 무언가를 따라간다 (1회차는 "친구를 쫓는 괴물"로 읽힌다).
    // - 네 발 걷기 애니메이션은 제자리 걸음이다 — 몸은 코드가 경로를 따라 민다.
    //   미는 속도 = 딛은 발이 땅 위에서 뒤로 흐르는 속도여야 발이 미끄러지지 않는다 (stridePerCycle / 주기 길이).
    public class MonsterReveal : LoopListener
    {
        enum State { Waiting, Walking, Fading, Done }

        [SerializeField, Min(1)] int loop = 5;
        [SerializeField] PlayerController player;

        [Header("발견 — 손전등 빛이 몸에 닿으면 (빛 원뿔 안 + 사거리 안 + 가려지지 않음)")]
        [SerializeField] FlashlightPower flashlight;
        [SerializeField] Light beam;
        [Tooltip("빛 원뿔 반각에 이만큼 곱한다 — 1보다 작으면 가장자리 흐린 빛은 안 친다")]
        [SerializeField, Range(0.3f, 1f)] float beamConeScale = 0.9f;
        [SerializeField] LayerMask sightBlockers = ~0;
        [Tooltip("하나라도 빛에 닿아야 '봤다'로 친다 — 엉덩이·뒷다리 (괴물 로컬, 크기 1 기준). 걷다가 플레이어 눈에서 전부 가려지면 몸을 끈다")]
        [SerializeField] Vector3[] visibleProbes = { new Vector3(0f, 0.87f, -0.3f), new Vector3(0f, 0.8f, -0.55f), new Vector3(0f, 0.5f, -0.6f) };

        [Header("경로 — 통로 중심선을 따라 놓은 점들")]
        [SerializeField] Transform[] path;
        [Tooltip("괴물이 서 있는 자리 = 경로 첫 점에서 이만큼 안쪽 (m). 키우면 더 멀리·더 높이 선다")]
        [SerializeField] float startDistance = 0f;
        [Tooltip("몸 방향 = 경로 위에서 앞뒤로 이만큼 떨어진 두 점을 잇는 방향 (m) — 클수록 커브에서 완만하게 돈다")]
        [SerializeField] float headingSpan = 0.8f;

        [Header("괴물")]
        [Tooltip("괴물 루트 (Animator 포함). 5루프에만 켠다")]
        [SerializeField] GameObject creature;
        [SerializeField] Animator animator;
        [SerializeField] string idleState = "Idle";
        [SerializeField] string crawlState = "Crawl";
        [Tooltip("걷기 애니메이션 재생 배속 — 이동 속도도 같이 빨라진다")]
        [SerializeField] float crawlSpeed = 1f;
        [Tooltip("걷기 한 주기 길이 (초, 배속 1 기준)")]
        [SerializeField] float crawlCycle = 1.3333f;
        [Tooltip("걷기 한 주기 동안 나아가는 거리 (크기 1 기준) — 보폭 ÷ 딛는 시간 비율")]
        [SerializeField] float stridePerCycle = 1.077f;
        [Tooltip("멈춤 → 걷기 전환 시간 (초)")]
        [SerializeField] float startBlend = 0.3f;
        [Tooltip("몸이 전부 벽 뒤로 사라지면 끈다. 그래도 이만큼 걸어가면(또는 경로 끝에 닿으면) 끈다 (m)")]
        [SerializeField] float hideDistance = 6f;

        [Header("소리")]
        [Tooltip("괴물 바깥의 3D AudioSource — 괴물이 꺼져도 잔향이 남게")]
        [SerializeField] AudioSource voice;
        [Tooltip("벽 뒤로 사라지는 순간 한 번")]
        [SerializeField] AudioClip scream;
        [SerializeField, Range(0f, 1f)] float screamVolume = 1f;
        [SerializeField] AudioClip gasp;
        [SerializeField, Range(0f, 1f)] float gaspVolume = 0.8f;
        [SerializeField] float gaspDelay = 0.25f;
        [Tooltip("걷는 동안 몸 위치에서 나는 발소리 (스윽… 스윽…)")]
        [SerializeField] AudioSource stepSource;
        [SerializeField] AudioClip[] steps;
        [SerializeField, Range(0f, 1f)] float stepVolume = 0.6f;
        [Tooltip("발소리 간격 (초, 배속 1 기준)")]
        [SerializeField] float stepInterval = 0.3333f;
        [Tooltip("걷기 시작해서 이만큼 멀어지면 발소리가 완전히 사라진다 (m) — 몸이 사라진 뒤에도 이 거리까지 통로 저편으로 이어진다")]
        [SerializeField] float stepFadeDistance = 12f;
        [Tooltip("괴물이 사라진 뒤에도 심장이 최고조로 뛰는 시간 — 그 뒤 천천히 가라앉는다")]
        [SerializeField] float heartAfterHide = 2.5f;

        State state = State.Done;
        float stateTime, nextStep, gaspAt = -1f, travelled, pathLength;
        AudioSource gaspSource;
        Renderer body;
        int lastStep = -1;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<FlashlightPower>();
            if (beam == null && flashlight != null) beam = flashlight.GetComponent<Light>();
            body = creature.GetComponentInChildren<Renderer>(true);
            for (int i = 1; i < path.Length; i++) pathLength += Flat(path[i].position - path[i - 1].position).magnitude;
            creature.SetActive(false);

            gaspSource = gameObject.AddComponent<AudioSource>(); // 내 숨 — 머릿속에서 들리는 2D
            gaspSource.playOnAwake = false;
            gaspSource.spatialBlend = 0f;
        }

        protected override void OnLoopChanged(int current)
        {
            bool mine = current == loop;
            state = mine ? State.Waiting : State.Done;
            creature.SetActive(mine);
            if (!mine) return;

            // 원래 거기 있던 것 — 통로 안쪽을 보고 숨을 고르며 서 있다
            Pose(startDistance, out var pos, out var rot);
            creature.transform.SetPositionAndRotation(pos, rot);
            animator.speed = 1f;
            animator.Play(idleState, 0, Random.value);
        }

        void Update()
        {
            if (gaspAt >= 0f && Time.time >= gaspAt)
            {
                gaspAt = -1f;
                if (gasp != null) gaspSource.PlayOneShot(gasp, gaspVolume);
            }

            // 괴물을 본 순간부터 발소리가 잦아든 조금 뒤까지 (세기는 Heartbeat의 5루프 값)
            if (state == State.Walking || state == State.Fading) Heartbeat.Instance?.Raise(heartAfterHide);

            switch (state)
            {
                case State.Waiting:
                    if (Lit()) StartWalking(); // 비춘 그 순간 바로 움직인다
                    break;

                case State.Walking:
                case State.Fading:
                    stateTime += Time.deltaTime;
                    Move();
                    if (Time.time >= nextStep) Step();
                    break;
            }
        }

        void Move()
        {
            // 멈춤→걷기로 섞이는 동안엔 속도도 0에서 서서히 — 발이 아직 멈춘 자세인데 몸만 미끄러지지 않게
            float ramp = startBlend > 0f ? Mathf.Clamp01(stateTime / startBlend) : 1f;
            var t = creature.transform;
            travelled += stridePerCycle * t.lossyScale.x / crawlCycle * crawlSpeed * ramp * Time.deltaTime;
            // 몸이 꺼진 뒤에도 보이지 않는 걸음은 경로(끝을 넘으면 끝 방향으로 연장)를 따라 계속 간다 — 발소리 자리
            Pose(startDistance + travelled, out var pos, out var rot);
            t.SetPositionAndRotation(pos, rot);

            if (state == State.Walking &&
                (travelled >= hideDistance || startDistance + travelled >= pathLength || !AnyVisible(player.CameraPivot.position, pos, rot, visibleProbes)))
            {
                Scream(); // 시야에서 사라지는 순간 — 벽 너머에서 운다
                creature.SetActive(false);
                state = State.Fading;
            }
            if (travelled >= stepFadeDistance) state = State.Done;
        }

        // 손전등 빛이 몸에 닿았나 — 원뿔 안, 사거리 안, 광원에서 막힘 없이
        bool Lit()
        {
            if (beam == null || !beam.enabled || (flashlight != null && !flashlight.IsOn)) return false;
            var t = creature.transform;
            Vector3 origin = beam.transform.position, fwd = beam.transform.forward;
            float halfAngle = beam.spotAngle * 0.5f * beamConeScale;
            foreach (var p in visibleProbes)
            {
                Vector3 w = ProbeWorld(t.position, t.rotation, p), d = w - origin;
                if (d.magnitude > beam.range || Vector3.Angle(fwd, d) > halfAngle) continue;
                if (!Physics.Linecast(origin, w, sightBlockers, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // 경로를 따라 dist만큼 간 자리와 몸 방향
        void Pose(float dist, out Vector3 pos, out Quaternion rot)
        {
            pos = Ground(PointAt(dist));
            Vector3 dir = Flat(PointAt(dist + headingSpan) - PointAt(dist - headingSpan));
            rot = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Flat(path[1].position - path[0].position));
        }

        // 경로 위 dist 지점 (높이는 Ground가 맞춘다). 양 끝을 넘으면 끝 구간 방향으로 늘인다
        Vector3 PointAt(float dist)
        {
            for (int i = 1; i < path.Length; i++)
            {
                Vector3 a = path[i - 1].position, b = path[i].position;
                float len = Flat(b - a).magnitude;
                if (dist <= len || i == path.Length - 1) return a + (b - a) * (dist / len);
                dist -= len;
            }
            return path[0].position;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // 바닥이 오르내리는 커브에서도 발이 바닥에 붙게 — 바로 아래 바닥 높이로 맞춘다
        Vector3 Ground(Vector3 pos)
        {
            // 통로 천장이 낮아(1.4m~) 너무 높이서 쏘면 천장 바위에 먼저 맞는다
            if (Physics.Raycast(pos + Vector3.up * 0.7f, Vector3.down, out var hit, 2f, sightBlockers, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            return pos;
        }

        Vector3 ProbeWorld(Vector3 rootPos, Quaternion rot, Vector3 local) => rootPos + rot * Vector3.Scale(local, creature.transform.lossyScale);

        bool AnyVisible(Vector3 eye, Vector3 rootPos, Quaternion rot, Vector3[] probes)
        {
            foreach (var p in probes)
                if (!Physics.Linecast(eye, ProbeWorld(rootPos, rot, p), sightBlockers, QueryTriggerInteraction.Ignore)) return true;
            return false;
        }

        void Scream()
        {
            if (body != null) voice.transform.position = body.bounds.center;
            if (scream != null) voice.PlayOneShot(scream, screamVolume);
            gaspAt = Time.time + gaspDelay;
        }

        void StartWalking()
        {
            state = State.Walking;
            stateTime = 0f;
            travelled = 0f;
            animator.speed = crawlSpeed;
            animator.CrossFadeInFixedTime(crawlState, startBlend);
            nextStep = Time.time + startBlend;
        }

        void Step()
        {
            nextStep = Time.time + stepInterval / crawlSpeed;
            if (steps == null || steps.Length == 0) return;

            // 3D 거리 감쇠에 더해, 걸어간 거리만큼 직접 줄인다 — 벽 너머로 사라진 뒤에도 통로 저편으로 쭉 잦아들게
            float fade = 1f - Mathf.Clamp01(travelled / stepFadeDistance);
            int i = Random.Range(0, steps.Length);
            if (steps.Length > 1 && i == lastStep) i = (i + 1) % steps.Length;
            lastStep = i;
            stepSource.transform.position = creature.transform.position + Vector3.up * 0.5f;
            stepSource.pitch = Random.Range(0.9f, 1.05f);
            stepSource.PlayOneShot(steps[i], stepVolume * fade);
        }


        void OnDrawGizmosSelected()
        {
            if (path == null || path.Length < 2 || creature == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < path.Length; i++) Gizmos.DrawLine(path[i - 1].position, path[i].position);
            Pose(startDistance, out var root, out var rot);
            foreach (var p in visibleProbes) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(ProbeWorld(root, rot, p), 0.08f); }
        }
    }
}
