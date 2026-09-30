using UnityEngine;

namespace CaveGame
{
    // 7루프 추격. 흐름:
    //  ① 갈림길에서 오른쪽(2번 길)에 들어서면 갈림길 쪽에서 큰 돌이 굴러와 떨어지는 소리 → 시작 통로(1번 길) 입구가 돌로 막힌다.
    //     갈림길 가로 통로(2번 ↔ 3번)는 열려 있다 — 돌아갈 길은 없고, 3번 길로 넘어가는 길만 남는다.
    //  ② 2번 길 끝(2-2) 조금 앞에 오면, 2-2 너머 다음 코너(안 보이는 곳)에서 괴물이 울며 튀어나와 빠르게 달려온다.
    //     코너를 돌아 모습이 보이는 순간 캠코더가 치직 — 발견할 거리는 두되 머뭇거릴 틈은 없다.
    //  ③ 괴물이 보이는 순간부터 플레이어는 달린다 (속도·발소리 박자·손전등/눈높이 흔들림·헐떡임이 함께 바뀐다).
    //  ④ 괴물은 플레이어보다 빠르다. 다만 거리는 "3-2까지 얼마나 왔나"에 맞춰 조절된다 —
    //     처음엔 멀고, 3-2에 닿는 순간엔 바로 등 뒤. 그 앞 끝선을 넘으면 엔딩(점프스케어)으로 뚝 끊긴다.
    //  ⑤ 멈춰 서거나 괴물 쪽으로 가면 몇 초 안에 잡힌다 — 잡혀도 같은 엔딩. 코너에서 조금 느려지는 정도로는 안 잡힌다.
    // 괴물은 경로(통로 중심선)를 따라 달리고, 플레이어의 위치도 같은 경로 위 거리로 환산해 비교한다 —
    // 굽은 통로에서 직선거리로 재면 벽 너머의 "가까움"에 속는다.
    public class ChaseSequence : LoopListener
    {
        enum State { Off, Armed, Blocked, Chasing, Done, Handoff, RunPast }

        [SerializeField, Min(1)] int loop = 7;
        [SerializeField] PlayerController player;
        [SerializeField] LoopManager loopManager;

        [Header("① 2번 길 진입 → 돌이 1번 길(시작 통로) 입구를 막는다")]
        [Tooltip("파란 화살표(forward)가 2번 길 안쪽을 향하는 선. 플레이어가 이 선을 넘어 폭 안에 들어서면 시작")]
        [SerializeField] Transform road2Entry;
        [SerializeField] float entryHalfWidth = 2.5f;
        [Tooltip("큰 돌 (콜라이더 포함). 평소엔 꺼져 있다. 갈림길 가로 통로는 건드리지 않게 시작 통로 입구에만 둔다")]
        [SerializeField] GameObject boulder;
        [Tooltip("돌 자리의 3D AudioSource")]
        [SerializeField] AudioSource rockSource;
        [SerializeField] AudioClip rockClip;
        [SerializeField, Range(0f, 1f)] float rockVolume = 1f;
        [Tooltip("소리 속 '쿵' (돌이 자리에 떨어지는) 시각 — 이때 돌이 생긴다")]
        [SerializeField] float boulderLandsAt = 2.05f;
        [Tooltip("돌이 생기는 순간 플레이어가 보고 있거나 이보다 가까우면, 안 볼 때까지 미룬다 (눈앞에서 '뿅' 생기지 않게)")]
        [SerializeField] float boulderMinPlayerDistance = 3f;

        [Header("경로 — 괴물 출발점(2-2 너머) → 2번 길 → 갈림길 가로 통로 → 3번 길 → 3-2 너머")]
        [SerializeField] Transform[] path;
        [Tooltip("3-2 (끝선 바로 앞). 여기서 괴물이 바로 등 뒤(endGap)에 붙어 있어야 한다")]
        [SerializeField] Transform goal;
        [Tooltip("괴물이 생기는 자리 — 2-2 너머 다음 코너 (플레이어 눈에 안 보이는 곳). 여기서 돌아 나와 달려온다")]
        [SerializeField] Transform spawnPoint;
        [Tooltip("플레이어가 이 지점까지 오면 괴물이 나타난다 (2-2 조금 앞)")]
        [SerializeField] Transform revealPoint;
        [Tooltip("2-2까지 안 가고 3번 길로 먼저 넘어가면, 이 지점에서 괴물이 뒤쪽(2번 길 쪽)에서 나타난다")]
        [SerializeField] Transform fallbackRevealPoint;
        [Tooltip("몸 방향 = 경로 위 앞뒤로 이만큼 떨어진 두 점을 잇는 방향 (m)")]
        [SerializeField] float headingSpan = 1f;
        [Tooltip("플레이어가 경로에서 이보다 멀면(엉뚱한 곳) 경로 위치로 치지 않는다")]
        [SerializeField] float maxPathOffset = 4f;

        [Header("② 괴물")]
        [SerializeField] GameObject creature;
        [SerializeField] Animator animator;
        [SerializeField] string chaseState = "Chase";
        [Tooltip("달리기 애니메이션 배속 1일 때 발이 땅을 미는 속도 (m/s, 크기 1 기준) — 이동 속도와 맞춰야 발이 안 미끄러진다")]
        [SerializeField] float animGroundSpeed = 4.2f;
        [Tooltip("발이 땅에 닿는 순간 (애니메이션 한 주기 대비 비율) — 여기서 발소리")]
        [SerializeField] float[] footContacts = { 0.19f, 0.69f };
        [Tooltip("3번 길로 먼저 넘어가 뒤쪽에서 나타날 때, 플레이어와의 경로 거리 (m). 보통은 spawnPoint에 나타난다")]
        [SerializeField] float revealGap = 13f;
        [SerializeField] LayerMask groundMask = ~0;
        [Tooltip("몸 반폭 (m, 크기 1 기준) — 엔딩에서 옆으로 비켜 달릴 때 벽과 이만큼은 띄운다")]
        [SerializeField] float bodyRadius = 0.45f;

        [Header("② 돌진 — 코너에서 튀어나와 이 속도로 달려오다가, 거리가 chaseFromGap까지 줄면 ④ 추격으로 넘어간다")]
        [SerializeField] float chargeSpeed = 7.5f;
        [SerializeField] float chaseFromGap = 10f;
        [Tooltip("몸이 보이는지 검사하는 점 (괴물 로컬, 크기 1 기준) — 하나라도 눈에서 막힘없이 보이면 '봤다'")]
        [SerializeField] Vector3[] sightProbes = { new Vector3(0f, 1.3f, 0.2f), new Vector3(0f, 1.8f, 0.35f), new Vector3(0f, 0.8f, 0f) };

        [Header("④ 추격 속도 — '지금 이만큼 떨어져 있어야 한다'는 목표 거리를 따라간다")]
        [Tooltip("3-2에 닿을 때 목표 거리 (m) — 거의 붙은 상태")]
        [SerializeField] float endGap = 1.4f;
        [Tooltip("목표 거리 곡선 (가로 0 = 추격 시작, 1 = 3-2 도착 / 세로 0 = 시작 거리, 1 = endGap). 아래로 볼록하면 끝에 확 붙는다")]
        [SerializeField] AnimationCurve gapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("목표 거리에 딱 맞을 때 속도 (m/s) — 플레이어 달리기(걷기 2 × 2 = 4)보다 빠르다. 목표 거리가 줄어드는 만큼 실제론 더 빨리 좁혀 온다")]
        [SerializeField] float baseSpeed = 4.4f;
        [Tooltip("목표보다 가까울 때 늦출 수 있는 하한 (m/s) — 코너에서 속도를 잃거나 늦게 반응해도 바로 잡히지 않게. 0보다 커서 멈추면 결국 잡힌다")]
        [SerializeField] float minSpeed = 3f;
        [Tooltip("목표보다 멀 때 따라잡는 상한 (m/s) — 너무 뒤처지지 않게")]
        [SerializeField] float maxSpeed = 6.8f;
        [Tooltip("목표와 1m 어긋날 때 더하거나 빼는 속도 (m/s)")]
        [SerializeField] float catchUpGain = 0.8f;
        [Tooltip("속도 변화 한계 (m/s²) — 순간적으로 튀면 미끄러져 보인다")]
        [SerializeField] float maxAcceleration = 5f;
        [Tooltip("출발 가속 시간 (초) — 멈춘 자세에서 박차고 나온다")]
        [SerializeField] float launchTime = 0.9f;
        [Tooltip("이 거리(m) 안이면 잡힌다 → 엔딩. 단 도망치는 중엔 잡지 않는다 (아래)")]
        [SerializeField] float catchDistance = 1f;
        [Tooltip("플레이어가 경로 방향으로 이보다 빨리(m/s) 가고 있으면 '도망치는 중' — 이때 괴물은 holdGap 뒤에서 붙어 따라올 뿐 잡지 않는다.\n" +
                 "코너에서 벽에 쓸려 조금 느려졌다고 3-2 바로 앞에서 잡히면 억울하다. 멈추거나 되돌아가면 잡는다")]
        [SerializeField] float fleeSpeed = 1.5f;
        [SerializeField] float holdGap = 1.1f;

        [Header("③ 추격 분위기")]
        [Tooltip("등장 순간 캠코더 화면이 아주 짧게 치직 (비우면 없음)")]
        [SerializeField] CamcorderGlitch glitch;
        [SerializeField] float revealGlitchTime = 0.12f;
        [Tooltip("추격 동안 동굴 소리(물방울·바람)를 줄인다 — 쫓는 소리와 내 몸 소리가 앞으로 나오게")]
        [SerializeField, Range(0f, 1f)] float ambienceDuring = 0.35f;
        [SerializeField] float ambienceFade = 1f;
        [Tooltip("심장 세기 배율 — 멀 때 / 바로 뒤일 때 (7루프 최대 세기 기준, 1을 넘으면 끝까지 몰아붙인다)")]
        [SerializeField] Vector2 heartScale = new Vector2(1f, 1.34f);

        [Header("소리 — 거리 따라 커지고 또렷해진다")]
        [Tooltip("괴물을 따라다니는 3D AudioSource (발소리 / 울음). 괴물 밖에 두고 위치만 옮긴다")]
        [SerializeField] AudioSource stepSource;
        [SerializeField] AudioSource voiceSource;
        [SerializeField] AudioClip[] steps;
        [SerializeField, Range(0f, 1f)] float stepVolume = 1f;
        [SerializeField] Vector2 stepPitch = new Vector2(0.85f, 1f);
        [SerializeField] AudioClip roar;
        [SerializeField, Range(0f, 1f)] float roarVolume = 1f;
        [SerializeField] AudioClip[] growls;
        [SerializeField, Range(0f, 1f)] float growlVolume = 0.9f;
        [Tooltip("울음 사이 쉬는 시간 (초) — 멀 때")]
        [SerializeField] Vector2 growlGapFar = new Vector2(2.5f, 4f);
        [Tooltip("울음 사이 쉬는 시간 (초) — 바로 뒤일 때")]
        [SerializeField] Vector2 growlGapNear = new Vector2(0.6f, 1.2f);
        [Tooltip("이 거리(m) 이상이면 '멀다', 이하면 '바로 뒤' — 소리 크기·필터·심장이 이 사이에서 바뀐다")]
        [SerializeField] Vector2 closeRange = new Vector2(14f, 2f);
        [Tooltip("멀 때 / 가까울 때 추가 음량 배율 (3D 감쇠에 곱한다)")]
        [SerializeField] Vector2 closeVolume = new Vector2(0.55f, 1f);
        [Tooltip("멀 때 / 가까울 때 저역통과 (Hz) — 멀면 먹먹하고 가까우면 또렷하다")]
        [SerializeField] Vector2 closeCutoff = new Vector2(1500f, 16000f);

        State state = State.Off;
        float[] cumulative;
        float pathLength, goalS, spawnS, revealS, fallbackS;
        float monsterS, playerS, speed, startGap, startPlayerS, chaseTime, rockTime, lastCycle, nextGrowl;
        float playerPathSpeed, lastPlayerS, moved;
        bool charging, sighted;
        bool boulderPlaced;
        float lateral, runPastSpeed, runPastUntil;
        int lastStep = -1, lastGrowl = -1;
        AudioLowPassFilter stepFilter, voiceFilter;
        Collider playerCollider;
        readonly RaycastHit[] groundHits = new RaycastHit[8];

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (loopManager == null) loopManager = FindFirstObjectByType<LoopManager>();
            playerCollider = player.GetComponent<Collider>();

            cumulative = new float[path.Length];
            for (int i = 1; i < path.Length; i++)
                cumulative[i] = cumulative[i - 1] + Flat(path[i].position - path[i - 1].position).magnitude;
            pathLength = cumulative[path.Length - 1];
            goalS = Project(goal.position, out _);
            spawnS = Project(spawnPoint.position, out _);
            revealS = Project(revealPoint.position, out _);
            fallbackS = fallbackRevealPoint != null ? Project(fallbackRevealPoint.position, out _) : float.PositiveInfinity;

            stepFilter = Filter(stepSource);
            voiceFilter = Filter(voiceSource);
            creature.SetActive(false);
            boulder.SetActive(false);
        }

        static AudioLowPassFilter Filter(AudioSource s)
        {
            var f = s.GetComponent<AudioLowPassFilter>();
            return f != null ? f : s.gameObject.AddComponent<AudioLowPassFilter>();
        }

        protected override void OnLoopChanged(int current)
        {
            bool mine = current == loop;
            state = mine ? State.Armed : State.Off;
            boulderPlaced = false;
            boulder.SetActive(false);
            creature.SetActive(false);
            player.SetRunning(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // 엔딩 씬으로 넘어가도 정적 값이 남지 않게
            if (state != State.Off && state != State.Armed && state != State.Blocked) AmbienceDuck.Level = 1f;
        }

        void Update()
        {
            switch (state)
            {
                case State.Armed:
                    if (EnteredRoad2()) StartRockfall();
                    break;

                case State.Blocked:
                    UpdateBoulder();
                    playerS = PlayerS(out bool onPath);
                    if (onPath && (playerS <= revealS || playerS >= fallbackS)) StartChase();
                    break;

                case State.Chasing:
                    UpdateBoulder();
                    UpdateChase();
                    break;

                case State.RunPast:
                    UpdateRunPast();
                    break;
            }
        }

        // ── ① 돌 ──────────────────────────────────────────────

        bool EnteredRoad2()
        {
            Vector3 local = road2Entry.InverseTransformPoint(player.transform.position);
            return local.z >= 0f && Mathf.Abs(local.x) <= entryHalfWidth && Mathf.Abs(local.y) <= 3f;
        }

        void StartRockfall()
        {
            state = State.Blocked;
            rockTime = Time.time;
            if (rockClip != null) rockSource.PlayOneShot(rockClip, rockVolume);
        }

        // 소리의 '쿵'에 맞춰 돌을 놓는다. 그때 보고 있으면 눈을 돌릴 때까지 기다린다
        void UpdateBoulder()
        {
            if (boulderPlaced || Time.time < rockTime + boulderLandsAt) return;
            if (BoulderInSight()) return;
            boulder.SetActive(true);
            boulderPlaced = true;
        }

        bool BoulderInSight()
        {
            Vector3 eye = player.CameraPivot.position, at = boulder.transform.position;
            if (Flat(at - player.transform.position).magnitude < boulderMinPlayerDistance) return true;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(at);
            bool onScreen = v.z > 0f && v.x > -0.2f && v.x < 1.2f && v.y > -0.2f && v.y < 1.2f;
            return onScreen && !Physics.Linecast(eye, at + Vector3.up, groundMask, QueryTriggerInteraction.Ignore);
        }

        // ── ②~⑤ 추격 ──────────────────────────────────────────

        void StartChase()
        {
            state = State.Chasing;
            startPlayerS = playerS;
            // 보통은 코너(spawnPoint)에서. 3번 길로 먼저 넘어갔다면 뒤쪽(2번 길 쪽) revealGap 거리에서
            monsterS = playerS >= fallbackS ? playerS - revealGap : spawnS;
            startGap = playerS - monsterS;
            speed = moved = playerPathSpeed = 0f;
            lastPlayerS = playerS;
            chaseTime = 0f;
            lastCycle = 0f;

            charging = true;
            sighted = false;

            creature.SetActive(true);
            PlaceMonster();
            animator.Play(chaseState, 0, 0f);
            animator.speed = 0.3f;

            // 모습보다 소리가 먼저 — 코너 너머에서 울고, 보이는 순간(Sighted) 달리기 시작
            MoveVoice();
            if (roar != null) voiceSource.PlayOneShot(roar, roarVolume);
            nextGrowl = Time.time + (roar != null ? roar.length * 0.8f : 0f) + Random.Range(growlGapFar.x, growlGapFar.y);
            StartCoroutine(FadeAmbience(ambienceDuring));
        }

        void UpdateChase()
        {
            chaseTime += Time.deltaTime;
            playerS = PlayerS(out _);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // 경로 방향 속도 — 코너·벽 긁힘으로 튀지 않게 0.3초쯤으로 부드럽게
            playerPathSpeed = Mathf.Lerp(playerPathSpeed, (playerS - lastPlayerS) / dt, 1f - Mathf.Exp(-dt / 0.3f));
            lastPlayerS = playerS;
            bool fleeing = playerPathSpeed > fleeSpeed;

            float gap = playerS - monsterS;
            if (!sighted && (gap <= chaseFromGap || MonsterVisible())) Sighted();

            float want;
            if (charging)
            {
                // 돌진: 코너에서 박차고 나와 빠르게. 충분히 다가오면 거리 맞추기로 넘긴다 — 그 순간의 거리가 시작 거리
                want = chargeSpeed;
                if (gap <= chaseFromGap)
                {
                    charging = false;
                    startGap = gap;
                    startPlayerS = playerS;
                }
            }
            else
            {
                // 목표 거리: 3-2까지 온 비율에 맞춰 시작 거리 → endGap
                float progress = Mathf.InverseLerp(startPlayerS, goalS, playerS);
                float targetGap = Mathf.Lerp(startGap, endGap, gapCurve.Evaluate(progress));
                // 멀면 빨라지고 너무 붙으면 늦춘다 — 거리가 목표를 따라 꾸준히 줄어든다 (앞지르지도, 뒤처지지도 않게)
                want = Mathf.Clamp(baseSpeed + catchUpGain * (gap - targetGap), minSpeed, maxSpeed);
            }
            want *= Mathf.SmoothStep(0f, 1f, chaseTime / Mathf.Max(launchTime, 0.01f));
            speed = Mathf.MoveTowards(speed, want, maxAcceleration * Time.deltaTime);
            float before = monsterS;
            monsterS = Mathf.Min(monsterS + speed * Time.deltaTime, pathLength);
            if (fleeing) monsterS = Mathf.Min(monsterS, playerS - holdGap); // 달리는 동안은 등 뒤에 붙을 뿐 — 앞지르거나 잡지 않는다
            monsterS = Mathf.Max(monsterS, before); // 뒤로 물러서지는 않는다
            PlaceMonster();

            // 다리 빠르기는 실제로 움직인 만큼 — 등 뒤에 붙어 속도가 눌렸을 때 발이 미끄러지지 않게
            moved = Mathf.Lerp(moved, (monsterS - before) / dt, 1f - Mathf.Exp(-dt / 0.15f));
            float scale = creature.transform.lossyScale.x;
            animator.speed = Mathf.Max(0.3f, moved / (animGroundSpeed * scale));
            FootSteps();

            float distance = Vector3.Distance(creature.transform.position, player.transform.position);
            float close = Mathf.InverseLerp(closeRange.x, closeRange.y, distance);
            MoveVoice();
            stepFilter.cutoffFrequency = voiceFilter.cutoffFrequency = Mathf.Lerp(closeCutoff.x, closeCutoff.y, close);
            Growl(close);
            Heartbeat.Instance?.Raise(0.5f, Mathf.Lerp(heartScale.x, heartScale.y, close));

            // 경로 위에서 따라잡혔거나(뒤돌아 괴물 쪽으로 가도) 몸이 닿으면 잡힌다
            if (!fleeing && (distance < catchDistance || playerS - monsterS <= 0f)) Caught();
        }

        void Sighted()
        {
            sighted = true;
            player.SetRunning(true);
            if (glitch != null) glitch.Burst(revealGlitchTime);
        }

        bool MonsterVisible()
        {
            Vector3 eye = player.CameraPivot.position;
            var t = creature.transform;
            foreach (var p in sightProbes)
                if (!Physics.Linecast(eye, t.position + t.rotation * Vector3.Scale(p, t.lossyScale), groundMask, QueryTriggerInteraction.Ignore))
                    return true;
            return false;
        }

        void Caught()
        {
            state = State.Done;
            loopManager.TriggerEnding();
        }

        void PlaceMonster()
        {
            Vector3 dir = Flat(PointAt(monsterS + headingSpan) - PointAt(monsterS - headingSpan));
            Vector3 pos = lateral != 0f ? PointBeside(monsterS, lateral, bodyRadius * creature.transform.lossyScale.x) : Ground(PointAt(monsterS));
            creature.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward));
        }

        // 애니메이션 한 주기 안에서 발이 닿는 순간을 지날 때마다 쿵
        void FootSteps()
        {
            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            foreach (float c in footContacts)
                if (Mathf.Floor(t - c) > Mathf.Floor(lastCycle - c)) Step(); // 지난 프레임 → 이번 프레임 사이에 (정수 주기 + c)를 지났다
            lastCycle = t;
        }

        void Step()
        {
            if (steps == null || steps.Length == 0) return;
            int i = Random.Range(0, steps.Length);
            if (steps.Length > 1 && i == lastStep) i = (i + 1) % steps.Length;
            lastStep = i;
            stepSource.pitch = Random.Range(stepPitch.x, stepPitch.y);
            stepSource.PlayOneShot(steps[i], stepVolume * CloseVolume());
        }

        void Growl(float close)
        {
            if (growls == null || growls.Length == 0 || Time.time < nextGrowl) return;
            int i = Random.Range(0, growls.Length);
            if (growls.Length > 1 && i == lastGrowl) i = (i + 1) % growls.Length;
            lastGrowl = i;
            voiceSource.pitch = Random.Range(0.92f, 1.04f);
            voiceSource.PlayOneShot(growls[i], growlVolume * CloseVolume());
            // 가까울수록 쉴 틈 없이 — 앞 울음이 거의 끝나야 다음 울음
            Vector2 gapRange = Vector2.Lerp(growlGapFar, growlGapNear, close);
            nextGrowl = Time.time + growls[i].length * 0.7f + Random.Range(gapRange.x, gapRange.y);
        }

        float CloseVolume()
        {
            float d = Vector3.Distance(creature.transform.position, player.transform.position);
            return Mathf.Lerp(closeVolume.x, closeVolume.y, Mathf.InverseLerp(closeRange.x, closeRange.y, d));
        }

        void MoveVoice()
        {
            Vector3 p = creature.transform.position;
            stepSource.transform.position = p + Vector3.up * 0.3f;
            voiceSource.transform.position = p + Vector3.up * 1.6f * creature.transform.lossyScale.y;
        }

        System.Collections.IEnumerator FadeAmbience(float to)
        {
            float from = AmbienceDuck.Level;
            for (float t = 0f; t < ambienceFade; t += Time.deltaTime)
            {
                AmbienceDuck.Level = Mathf.Lerp(from, to, t / ambienceFade);
                yield return null;
            }
            AmbienceDuck.Level = to;
        }

        // ── 다른 엔딩(MonsterEnding)에 넘겨주기 ─────────────────
        // 엔딩 판정 순간 추격을 멈추고 괴물을 치운다 — 이후 움직임은 엔딩 쪽이 RunPast로 다시 부른다

        public void HandOff()
        {
            state = State.Handoff;
            creature.SetActive(false);
            lateral = 0f;
        }

        // 경로 위 fromS에서 speed로 달려 untilS를 지나면 사라진다. 잡지도 멈추지도 않는다 — 내려놓인 캠 옆을 지나쳐 앞으로 달려간다.
        // sideOffset: 경로 중심선에서 옆으로 비킨 거리 (m, +는 진행 방향 오른쪽) — 바닥의 캠을 밟고 지나가지 않게
        public void RunPast(float fromS, float speed, float sideOffset, float untilS)
        {
            state = State.RunPast;
            monsterS = fromS;
            runPastSpeed = speed;
            runPastUntil = untilS;
            lateral = sideOffset;
            lastCycle = 0f;
            creature.SetActive(true);
            PlaceMonster();
            animator.Play(chaseState, 0, 0f);
            animator.speed = speed / (animGroundSpeed * creature.transform.lossyScale.x);
            MoveVoice();
            nextGrowl = Time.time + Random.Range(growlGapNear.x, growlGapNear.y);
        }

        public bool IsRunningPast => state == State.RunPast;
        public float MonsterPathPosition => monsterS;

        void UpdateRunPast()
        {
            monsterS += runPastSpeed * Time.deltaTime;
            PlaceMonster();
            FootSteps();
            float distance = Vector3.Distance(creature.transform.position, player.transform.position);
            float close = Mathf.InverseLerp(closeRange.x, closeRange.y, distance);
            MoveVoice();
            stepFilter.cutoffFrequency = voiceFilter.cutoffFrequency = Mathf.Lerp(closeCutoff.x, closeCutoff.y, close);
            Growl(close);
            if (monsterS < runPastUntil) return;
            creature.SetActive(false);
            state = State.Done;
        }

        public float PathLength => pathLength;
        public float PathPosition(Vector3 p) => Project(p, out _);
        public Vector3 PathPoint(float s) => PointAt(s);
        public Vector3 PathHeading(float s) => Flat(PointAt(s + headingSpan) - PointAt(s - headingSpan)).normalized;
        public Vector3 GroundAt(Vector3 p) => Ground(p);

        // 경로 s 지점에서 옆으로 side(m, + = 진행 방향 오른쪽)만큼 비킨 바닥 위치. 벽이 가까우면 radius만큼 띄우도록 덜 비킨다 —
        // 굽은 통로에서 비킨 자리가 벽 속이면 바닥 대신 바위 윗면에 붙어 몸이 벽을 뚫고 올라간다
        public Vector3 PointBeside(float s, float side, float radius)
        {
            Vector3 center = Ground(PointAt(s));
            if (Mathf.Abs(side) < 1e-3f) return center;
            Vector3 right = Vector3.Cross(Vector3.up, Flat(PointAt(s + headingSpan) - PointAt(s - headingSpan))).normalized;
            Vector3 dir = right * Mathf.Sign(side);
            Vector3 origin = center + Vector3.up * 0.8f;
            float room = Physics.Raycast(origin, dir, out RaycastHit hit, Mathf.Abs(side) + radius, groundMask, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(0f, hit.distance - radius) : Mathf.Abs(side);
            return Ground(center + dir * Mathf.Min(Mathf.Abs(side), room), center.y);
        }

        // ── 경로 ─────────────────────────────────────────────

        float PlayerS(out bool onPath)
        {
            float s = Project(player.transform.position, out float offset);
            onPath = offset <= maxPathOffset;
            return s;
        }

        // 경로에서 가장 가까운 지점까지의 경로 거리
        float Project(Vector3 p, out float offset)
        {
            float bestS = 0f;
            offset = float.PositiveInfinity;
            for (int i = 1; i < path.Length; i++)
            {
                Vector3 a = Flat(path[i - 1].position), ab = Flat(path[i].position) - a, ap = Flat(p) - a;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                float d = (ap - ab * t).magnitude;
                if (d < offset)
                {
                    offset = d;
                    bestS = cumulative[i - 1] + t * (cumulative[i] - cumulative[i - 1]);
                }
            }
            return bestS;
        }

        // 경로 위 dist 지점. 양 끝을 넘으면 끝 구간 방향으로 늘인다
        Vector3 PointAt(float dist)
        {
            for (int i = 1; i < path.Length; i++)
            {
                if (dist <= cumulative[i] || i == path.Length - 1)
                {
                    float len = cumulative[i] - cumulative[i - 1];
                    return Vector3.LerpUnclamped(path[i - 1].position, path[i].position, (dist - cumulative[i - 1]) / Mathf.Max(len, 1e-6f));
                }
            }
            return path[0].position;
        }

        // 바닥 높이에 발을 붙인다 — 플레이어 캡슐은 바닥으로 치지 않는다
        Vector3 Ground(Vector3 pos) => Ground(pos, pos.y);

        // 여러 면이 맞으면 기준 높이(경로 바닥)에 가장 가까운 면 — 낮은 천장 아래에선 레이가 바위 위에서 시작해 천장 윗면을 먼저 맞힌다
        Vector3 Ground(Vector3 pos, float refY)
        {
            int n = Physics.RaycastNonAlloc(pos + Vector3.up * 1.2f, Vector3.down, groundHits, 3f, groundMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (groundHits[i].collider == playerCollider) continue;
                float d = Mathf.Abs(groundHits[i].point.y - refY);
                if (d >= best) continue;
                best = d;
                pos.y = groundHits[i].point.y;
            }
            return pos;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDrawGizmosSelected()
        {
            if (path == null || path.Length < 2) return;
            Gizmos.color = new Color(1f, 0.3f, 0.1f);
            for (int i = 1; i < path.Length; i++)
                if (path[i - 1] != null && path[i] != null) Gizmos.DrawLine(path[i - 1].position, path[i].position);
            Gizmos.color = Color.red;
            if (goal != null) Gizmos.DrawWireSphere(goal.position, 0.5f);
            Gizmos.color = Color.yellow;
            if (revealPoint != null) Gizmos.DrawWireSphere(revealPoint.position, 0.5f);
            Gizmos.color = Color.magenta;
            if (spawnPoint != null) Gizmos.DrawWireSphere(spawnPoint.position, 0.6f);
            Gizmos.color = Color.yellow;
            if (fallbackRevealPoint != null) Gizmos.DrawWireSphere(fallbackRevealPoint.position, 0.4f);
            if (road2Entry != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(road2Entry.position - road2Entry.right * entryHalfWidth, road2Entry.position + road2Entry.right * entryHalfWidth);
                Gizmos.DrawRay(road2Entry.position, road2Entry.forward);
            }
        }
    }
}
