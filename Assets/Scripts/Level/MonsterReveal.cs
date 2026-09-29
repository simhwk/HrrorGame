using UnityEngine;

namespace CaveGame
{
    // 5루프: 오른쪽 갈래(줄이 가리키는 정답 쪽)에 들어서서 갈래 끝 꺾이는 모퉁이를 비추면, 괴물이 꺾인 뒤 직진하는 통로를
    // 바라본 채 네 발로 웅크려 있다 — 플레이어에겐 옆·뒷모습만 보인다.
    // 잠시 뒤 그르렁거리다 포효하고, 기어서 꺾인 통로 안쪽으로 멀어져 벽 너머로 사라진다.
    // - 괴물은 플레이어를 쫓지 않는다 — 앞서 간 무언가를 따라간다 (1회차는 "친구를 쫓는 괴물"로 읽힌다).
    // - 기어가기 애니메이션은 한 주기 동안 엉덩이 뼈가 앞으로 나아갔다가 다음 주기에 제자리로 돌아온다.
    //   그래서 주기가 넘어갈 때마다 루트를 그 한 주기 거리(loopOffset)만큼 앞으로 옮겨 이어 붙인다 — 끊김 없이 계속 기어간다.
    //   loopOffset에 옆 방향 성분이 있으면 괴물이 비스듬히 새므로, 배치할 때 그만큼 방향을 틀어 둔다. 로컬 +Z가 기어가는 방향.
    public class MonsterReveal : LoopListener
    {
        enum State { Waiting, Holding, Crawling, Done }

        [SerializeField, Min(1)] int loop = 5;
        [SerializeField] PlayerController player;

        [Header("발동 — 이 구역 안에서 괴물 자리를 비추면 (가려져 있으면 안 됨)")]
        [SerializeField] Transform triggerCenter;
        [SerializeField] Vector2 triggerHalfSize = new Vector2(1.5f, 2.8f);
        [Tooltip("카메라 정면과 괴물 방향의 내적이 이 값을 넘어야 한다")]
        [SerializeField, Range(0f, 1f)] float lookDot = 0.6f;
        [Tooltip("시선 검사 지점 높이들 (괴물 발밑 기준, m) — 하나라도 보이면 발동. 모퉁이에 몸 일부만 걸려도 잡히게")]
        [SerializeField] float[] sightHeights = { 0.3f, 0.7f, 1.1f };
        [SerializeField] LayerMask sightBlockers = ~0;

        [Header("괴물")]
        [Tooltip("괴물 루트 (Animator 포함). 평소엔 꺼 둔다")]
        [SerializeField] GameObject creature;
        [SerializeField] Animator animator;
        [SerializeField] string crawlState = "Crawl";
        [Tooltip("모습을 보인 뒤 웅크린 채 멈춰 있는 시간 — 그 사이 소리친다")]
        [SerializeField] float holdTime = 2f;
        [Tooltip("멈춰 있다가 이 시간에 소리친다")]
        [SerializeField] float screamAt = 0.4f;
        [SerializeField] float crawlSpeed = 1.4f;
        [Tooltip("애니메이션 한 주기 동안 엉덩이 뼈가 움직인 거리 (크기 1 기준, 괴물 로컬)")]
        [SerializeField] Vector3 loopOffset = new Vector3(0.247f, 0f, 1.53f);
        [Tooltip("이만큼 기어가면 사라진다 (m) — 갈래 끝 어둠 속, 벽에 닿기 전")]
        [SerializeField] float hideDistance = 7f;

        [Header("소리")]
        [Tooltip("괴물 바깥의 3D AudioSource — 괴물이 꺼져도 잔향이 남게")]
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip scream;
        [SerializeField, Range(0f, 1f)] float screamVolume = 1f;
        [SerializeField] AudioClip gasp;
        [SerializeField, Range(0f, 1f)] float gaspVolume = 0.8f;
        [SerializeField] float gaspDelay = 0.25f;
        [Tooltip("기어가는 동안 몸 위치에서 나는 발소리")]
        [SerializeField] AudioSource stepSource;
        [SerializeField] AudioClip[] steps;
        [SerializeField, Range(0f, 1f)] float stepVolume = 0.6f;
        [SerializeField] float stepInterval = 0.45f;
        [Tooltip("괴물이 사라진 뒤에도 심장이 최고조로 뛰는 시간 — 그 뒤 천천히 가라앉는다")]
        [SerializeField] float heartAfterHide = 2.5f;

        State state = State.Done;
        float stateTime, nextStep, gaspAt = -1f;
        bool screamed;
        AudioSource gaspSource;
        Renderer body;
        Vector3 spawnPos;
        int lastStep = -1;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            body = creature.GetComponentInChildren<Renderer>(true);
            spawnPos = creature.transform.position;
            creature.SetActive(false);

            gaspSource = gameObject.AddComponent<AudioSource>(); // 내 숨 — 머릿속에서 들리는 2D
            gaspSource.playOnAwake = false;
            gaspSource.spatialBlend = 0f;
        }

        protected override void OnLoopChanged(int current)
        {
            creature.SetActive(false);
            creature.transform.position = spawnPos;
            state = current == loop ? State.Waiting : State.Done;
        }

        void Update()
        {
            if (gaspAt >= 0f && Time.time >= gaspAt)
            {
                gaspAt = -1f;
                if (gasp != null) gaspSource.PlayOneShot(gasp, gaspVolume);
            }

            // 괴물을 본 순간부터 사라지고 조금 뒤까지 (세기는 Heartbeat의 5루프 값)
            if (state == State.Holding || state == State.Crawling) Heartbeat.Instance?.Raise(heartAfterHide);

            switch (state)
            {
                case State.Waiting:
                    if (Triggered()) Appear();
                    break;

                case State.Holding:
                    stateTime += Time.deltaTime;
                    if (!screamed && stateTime >= screamAt) Scream();
                    if (stateTime >= holdTime) StartCrawling();
                    break;

                case State.Crawling:
                    if (Time.time >= nextStep) Step();
                    break;
            }
        }

        // 애니메이션이 이번 프레임 자세를 만든 뒤에 루트를 옮겨야 주기가 넘어가는 프레임에 튀지 않는다
        void LateUpdate()
        {
            if (state != State.Crawling) return;

            int loops = Mathf.FloorToInt(animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            float scale = creature.transform.lossyScale.x;
            creature.transform.position = spawnPos + creature.transform.rotation * (loopOffset * (loops * scale));

            if (loops * loopOffset.magnitude * scale >= hideDistance)
            {
                creature.SetActive(false);
                state = State.Done;
            }
        }

        bool Triggered()
        {
            Vector3 d = player.transform.position - triggerCenter.position;
            if (Mathf.Abs(d.x) > triggerHalfSize.x || Mathf.Abs(d.z) > triggerHalfSize.y) return false;

            Vector3 eye = player.CameraPivot.position;
            Vector3 feet = creature.transform.position;
            foreach (float h in sightHeights)
            {
                Vector3 target = feet + Vector3.up * h;
                if (Vector3.Dot(player.CameraPivot.forward, (target - eye).normalized) < lookDot) continue;
                if (!Physics.Linecast(eye, target, sightBlockers, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        void Appear()
        {
            state = State.Holding;
            stateTime = 0f;
            screamed = false;
            creature.transform.position = spawnPos;
            creature.SetActive(true);
            animator.Play(crawlState, 0, 0f);
            animator.speed = 0f; // 웅크린 채 멈춰 있다
        }

        void Scream()
        {
            screamed = true;
            if (body != null) voice.transform.position = body.bounds.center;
            if (scream != null) voice.PlayOneShot(scream, screamVolume);
            gaspAt = Time.time + gaspDelay;
        }

        void StartCrawling()
        {
            state = State.Crawling;
            animator.speed = crawlSpeed;
            nextStep = Time.time + 0.1f;
        }

        void Step()
        {
            nextStep = Time.time + stepInterval / crawlSpeed;
            if (steps == null || steps.Length == 0) return;

            int i = Random.Range(0, steps.Length);
            if (steps.Length > 1 && i == lastStep) i = (i + 1) % steps.Length;
            lastStep = i;
            if (body != null) stepSource.transform.position = body.bounds.center;
            stepSource.pitch = Random.Range(0.9f, 1.1f);
            stepSource.PlayOneShot(steps[i], stepVolume);
        }


        void OnDrawGizmosSelected()
        {
            if (triggerCenter == null) return;
            Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(triggerCenter.position, new Vector3(triggerHalfSize.x * 2f, 0.2f, triggerHalfSize.y * 2f));
        }
    }
}
