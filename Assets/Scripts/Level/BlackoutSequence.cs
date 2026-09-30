using System.Collections;
using UnityEngine;

namespace CaveGame
{
    // 6루프: 갈림길 직전, 줄이 끊긴 자리에 닿으면 —
    //  ① 잠시 뒤 동굴 소리(물방울·바람)가 사라져 내 숨·발소리만 남는다
    //  ② 손전등이 깜빡이다 꺼진다 (한 번도 꺼진 적 없는 불 — 고장이 아니라 "무언가"로 읽힌다)
    //  ③ 어둠 속에서 늑대가 자갈 위를 달리는 발소리가 네 발 질주 리듬("다닥-다닥—")으로 점점 멀어지며(먹먹해지며) 달려가고(가까워질수록 심장이 빨라진다)
    //  ④ 발소리가 멈추고 정적 — 심장은 최고조로 뛴 채
    //  ⑤ 불이 깜빡이며 돌아온다 — 아무것도 없다. 심장이 천천히 가라앉고 동굴 소리가 돌아온다
    // 얼굴은 7루프까지 아낀다 — 여기선 절대 모습을 보이지 않는다.
    // 트리거는 줄이 끊긴 끝 조금 너머 — 줄 소리가 사라진 걸 플레이어가 느낀 직후여야 한다.
    public class BlackoutSequence : LoopListener
    {
        [SerializeField, Min(1)] int loop = 6;
        [SerializeField] PlayerController player;
        [SerializeField] FlashlightPower flashlight;
        [SerializeField] Transform triggerCenter;
        [SerializeField] float triggerRadius = 1.2f;

        [Header("① 동굴 소리 줄이기")]
        [Tooltip("트리거에 들어온 뒤 이만큼 있다가 시작 — 끊긴 줄을 더듬어 볼 짧은 틈")]
        [SerializeField] float startDelay = 1.5f;
        [SerializeField, Range(0f, 1f)] float duckLevel = 0.12f;
        [SerializeField] float duckTime = 1f;

        [Header("② 손전등")]
        [SerializeField] float flickerOutTime = 2f;
        [SerializeField] float darkBeforeSteps = 2.5f;
        [Tooltip("불이 완전히 꺼지는 순간 캠코더 화면이 아주 짧게 치직 (비우면 없음)")]
        [SerializeField] CamcorderGlitch glitch;
        [SerializeField] float offGlitchTime = 0.18f;

        [Header("③ 발소리 — 경로를 따라 stepLength마다 발 하나. 왼쪽 갈래에서 나와 갈림길 앞을 지나 오른쪽 갈래로 멀어진다")]
        [SerializeField] AudioSource stepSource; // 3D. 경로 점의 부모가 아닌 별도 오브젝트
        [SerializeField] Transform[] stepPath;
        [SerializeField] AudioClip[] steps;
        [SerializeField, Range(0f, 1f)] float stepVolume = 1f;
        [Tooltip("경로의 이 비율부터 끝까지 음량이 끝 배율(endVolume)로 줄어든다 — 3D 감쇠에 더해 멀어지는 느낌")]
        [SerializeField, Range(0f, 1f)] float fadeFrom = 0.5f;
        [SerializeField, Range(0f, 1f)] float endVolume = 0.15f;
        [Tooltip("걸음 사이 간격 (초) — 짧을수록 달려간다")]
        [SerializeField] Vector2 stepInterval = new Vector2(0.22f, 0.28f);
        [Tooltip("한 걸음에 나아가는 거리 (m) — 경로 점 간격과 무관하게 이만큼씩 옮겨 가며 소리 낸다")]
        [SerializeField] float stepLength = 0.9f;
        [Tooltip("0보다 크면 걸음 수를 딱 이만큼으로 — 경로에서 플레이어와 가장 가까운 지점부터 stepLength씩 끝 쪽으로 친다 (점점 멀어지기만 한다). 음량은 fadeFrom 대신 firstStepVolume→endVolume")]
        [SerializeField, Min(0)] int stepCount = 0;
        [Tooltip("stepCount 모드: 첫 걸음 음량 배율 — 마지막 걸음(endVolume)까지 고르게 줄어든다")]
        [SerializeField, Range(0f, 1f)] float firstStepVolume = 0.7f;
        [SerializeField] Vector2 stepPitch = new Vector2(0.9f, 1.0f);
        [Header("③ 달리는 느낌 — 왼발·오른발이 똑같으면 기계가 두드리는 소리가 된다")]
        [Tooltip("두 발 간격의 비대칭 — 한쪽 발 뒤는 간격 x(1+이 값), 다른 쪽 발 뒤는 x(1-이 값)")]
        [SerializeField, Range(0f, 0.3f)] float gaitSwing = 0.06f;
        [Tooltip("약한 쪽 발의 세기 / 음높이 배율 — 사람도 짐승도 두 발이 똑같이 딛지 않는다")]
        [SerializeField, Range(0f, 1f)] float offFootVolume = 0.8f;
        [SerializeField] float offFootPitch = 1.05f;
        [Tooltip("걸음마다 흔들리는 박자 (초)")]
        [SerializeField] float timingJitter = 0.01f;
        [Tooltip("네 발 질주(갤럽) — 비우지 않으면 stepCount = 보폭 수, stepInterval = 한 보폭 시간, stepLength = 한 보폭 거리.\n" +
                 "발 하나씩: x = 다음 발까지 간격 (보폭 시간 대비 비율, 합 1), y = 세기, z = 음높이 배율")]
        [SerializeField] Vector3[] gallopFeet;
        [Tooltip("stepCount 모드: 첫 걸음 / 마지막 걸음의 저역통과 주파수(Hz) — 멀어질수록 먹먹해진다")]
        [SerializeField] Vector2 stepCutoff = new Vector2(16000f, 2500f);
        [Tooltip("발소리가 이 거리(m)보다 멀 때 / 이 거리 안일 때 — 심장 세기가 최소 / 최대")]
        [SerializeField] Vector2 heartDistance = new Vector2(9f, 3f);
        [Tooltip("가장 멀 때 / 가장 가까울 때 심장 세기 배율 (6루프 최대 세기 기준)")]
        [SerializeField] Vector2 heartScale = new Vector2(0.35f, 1f);
        [Tooltip("멀리서 작게 들리는 여자 비명 (비우면 없음) — 발소리가 달려간 쪽 끝에서")]
        [SerializeField] AudioClip scream;
        [SerializeField, Range(0f, 1f)] float screamVolume = 0.35f;
        [Tooltip("발소리가 경로의 이 비율만큼 갔을 때 비명")]
        [SerializeField, Range(0f, 1f)] float screamAt = 0.7f;
        [Tooltip("비명 위치 = 경로 끝에서 끝 방향으로 이만큼 더 (m)")]
        [SerializeField] float screamBeyond = 6f;

        [Header("④ 정적")]
        [SerializeField] float silenceTime = 3f;

        [Header("⑤ 복귀")]
        [SerializeField] float flickerOnTime = 0.8f;
        [SerializeField] float restoreTime = 4f;
        [Tooltip("불이 돌아온 뒤 심장이 최고조를 유지하는 시간 — 그 뒤 천천히 가라앉는다")]
        [SerializeField] float heartAfterLight = 1.5f;

        enum State { Idle, Armed, Running, Done }

        // 발 하나 — 경로 위 거리, 음량 배율, 멀어진 정도(0~1, 기존 모드는 -1), 다음 발까지 간격, 음높이 배율
        struct Footfall { public float d, volume, far, gap, pitch; }
        AudioSource screamSource;
        AudioLowPassFilter stepLowPass;
        State state = State.Done;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (stepSource != null)
            {
                stepSource.playOnAwake = false;
                stepLowPass = stepSource.GetComponent<AudioLowPassFilter>();
                if (stepLowPass == null) stepLowPass = stepSource.gameObject.AddComponent<AudioLowPassFilter>();
                stepLowPass.cutoffFrequency = 22000f;
            }

            // 비명은 발소리보다 훨씬 먼 곳 — 기본 3D 감쇠(1m부터)면 안 들리니, 거리감은 소리 자체(먹먹함·잔향)에 맡기고 감쇠는 느슨하게
            screamSource = new GameObject("Loop6_ScreamSource").AddComponent<AudioSource>();
            screamSource.playOnAwake = false;
            screamSource.spatialBlend = 0.7f;
            screamSource.rolloffMode = AudioRolloffMode.Logarithmic;
            screamSource.minDistance = 12f;
            screamSource.maxDistance = 60f;
        }

        protected override void OnLoopChanged(int current)
        {
            Abort();
            state = current == loop ? State.Armed : State.Done;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Abort();
        }

        // 도중에 루프가 바뀌어도 불이 꺼진 채, 동굴이 조용한 채로 남지 않게
        void Abort()
        {
            if (state != State.Running) return;
            StopAllCoroutines();
            AmbienceDuck.Level = 1f;
            if (flashlight != null) flashlight.Set(1f);
            if (stepLowPass != null) stepLowPass.cutoffFrequency = 22000f;
        }

        void Update()
        {
            if (state != State.Armed) return;
            Vector3 d = player.transform.position - triggerCenter.position;
            d.y = 0f;
            if (d.sqrMagnitude > triggerRadius * triggerRadius) return;

            state = State.Running;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(startDelay);

            // ①
            yield return FadeAmbience(duckLevel, duckTime);

            // ②
            yield return flashlight.Flicker(flickerOutTime, turnOn: false);
            if (glitch != null) glitch.Burst(offGlitchTime);
            yield return new WaitForSeconds(darkBeforeSteps);

            // ③
            int last = -1;
            float peak = heartScale.x, total = 0f;
            for (int i = 1; i < stepPath.Length; i++) total += Vector3.Distance(stepPath[i - 1].position, stepPath[i].position);
            bool screamed = scream == null;
            var walk = new System.Collections.Generic.List<Footfall>();
            bool gallop = stepCount > 0 && gallopFeet != null && gallopFeet.Length > 0;
            if (stepCount > 0)
            {
                // 지금 플레이어와 가장 가까운 지점에서 시작해 끝으로 — 첫 걸음부터 멀어지기만 한다
                float nearD = 0f, best = float.MaxValue;
                for (float d = 0f; d <= total; d += 0.25f)
                {
                    float dist = Vector3.Distance(PathPoint(d), player.transform.position);
                    if (dist < best) { best = dist; nearD = d; }
                }
                if (gallop)
                {
                    // 보폭마다 네 발 — 보폭 안에서 발이 닿는 비율만큼 앞으로 나아간다
                    int feet = gallopFeet.Length, all = stepCount * feet;
                    for (int k = 0; k < stepCount; k++)
                    {
                        float stride = Random.Range(stepInterval.x, stepInterval.y), into = 0f;
                        for (int j = 0; j < feet; j++)
                        {
                            float t = all > 1 ? (k * feet + j) / (all - 1f) : 0f;
                            walk.Add(new Footfall
                            {
                                d = Mathf.Min(total, nearD + (k + into) * stepLength),
                                volume = Mathf.Lerp(firstStepVolume, endVolume, t) * gallopFeet[j].y,
                                far = t,
                                gap = stride * gallopFeet[j].x,
                                pitch = gallopFeet[j].z,
                            });
                            into += gallopFeet[j].x;
                        }
                    }
                }
                else
                {
                    for (int k = 0; k < stepCount; k++)
                    {
                        float t = stepCount > 1 ? k / (stepCount - 1f) : 0f;
                        bool off = k % 2 == 1; // 두 발 — 왼발·오른발 박자·세기·음높이가 조금씩 다르다
                        walk.Add(new Footfall
                        {
                            d = Mathf.Min(total, nearD + k * stepLength),
                            volume = Mathf.Lerp(firstStepVolume, endVolume, t) * (off ? offFootVolume : 1f),
                            far = t,
                            gap = Random.Range(stepInterval.x, stepInterval.y) * (off ? 1f - gaitSwing : 1f + gaitSwing),
                            pitch = off ? offFootPitch : 1f,
                        });
                    }
                }
            }
            else
            {
                for (float d = 0f; d <= total; d += stepLength)
                    walk.Add(new Footfall
                    {
                        d = d,
                        volume = Mathf.Lerp(1f, endVolume, Mathf.InverseLerp(total * fadeFrom, total, d)),
                        far = -1f,
                        gap = Random.Range(stepInterval.x, stepInterval.y),
                        pitch = 1f,
                    });
            }

            for (int n = 0; n < walk.Count; n++)
            {
                var w = walk[n];
                float d = w.d;
                Vector3 at = PathPoint(d);
                // 가까울수록 빨라진다 — 지나쳐 멀어져도 한 번 치솟은 심장은 쉽게 가라앉지 않는다
                float near = Mathf.InverseLerp(heartDistance.x, heartDistance.y, Vector3.Distance(at, player.transform.position));
                peak = Mathf.Max(peak, Mathf.Lerp(heartScale.x, heartScale.y, near));
                Heartbeat.Instance?.Raise(stepInterval.y + 0.5f, peak); // 다음 걸음까지 이어진다

                if (!screamed && d >= total * screamAt)
                {
                    screamed = true;
                    Vector3 end = stepPath[stepPath.Length - 1].position, dir = (end - stepPath[stepPath.Length - 2].position).normalized;
                    screamSource.transform.position = end + dir * screamBeyond;
                    screamSource.PlayOneShot(scream, screamVolume);
                }

                stepSource.transform.position = at;
                int c = Random.Range(0, steps.Length);
                if (steps.Length > 1 && c == last) c = (c + 1) % steps.Length;
                last = c;
                stepSource.pitch = Random.Range(stepPitch.x, stepPitch.y) * w.pitch;
                if (w.far >= 0f) stepLowPass.cutoffFrequency = Mathf.Lerp(stepCutoff.x, stepCutoff.y, w.far);
                stepSource.PlayOneShot(steps[c], stepVolume * w.volume);
                yield return new WaitForSeconds(Mathf.Max(0.005f, w.gap + Random.Range(-timingJitter, timingJitter)));
            }

            stepLowPass.cutoffFrequency = 22000f;

            // ④ 심장은 최고조로 뛴 채
            Heartbeat.Instance?.Raise(silenceTime + flickerOnTime + heartAfterLight, peak);
            yield return new WaitForSeconds(silenceTime);

            // ⑤ — 불이 먼저, 소리는 천천히
            yield return flashlight.Flicker(flickerOnTime, turnOn: true);
            yield return FadeAmbience(1f, restoreTime);
            state = State.Done;
        }

        // 경로 첫 점에서 d(m) 간 자리
        Vector3 PathPoint(float d)
        {
            for (int i = 1; i < stepPath.Length; i++)
            {
                Vector3 a = stepPath[i - 1].position, b = stepPath[i].position;
                float len = Vector3.Distance(a, b);
                if (d <= len) return Vector3.Lerp(a, b, len > 0f ? d / len : 0f);
                d -= len;
            }
            return stepPath[stepPath.Length - 1].position;
        }

        static IEnumerator FadeAmbience(float to, float time)
        {
            float from = AmbienceDuck.Level;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                AmbienceDuck.Level = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
                yield return null;
            }
            AmbienceDuck.Level = to;
        }

        void OnDrawGizmosSelected()
        {
            if (triggerCenter != null)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.6f);
                Gizmos.DrawWireSphere(triggerCenter.position, triggerRadius);
            }
            if (stepPath == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.1f);
            for (int i = 0; i < stepPath.Length; i++)
            {
                if (stepPath[i] == null) continue;
                Gizmos.DrawSphere(stepPath[i].position, 0.1f);
                if (i > 0 && stepPath[i - 1] != null) Gizmos.DrawLine(stepPath[i - 1].position, stepPath[i].position);
            }
        }
    }
}
