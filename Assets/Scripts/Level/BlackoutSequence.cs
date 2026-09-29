using System.Collections;
using UnityEngine;

namespace CaveGame
{
    // 6루프: 갈림길 직전, 줄이 끊긴 자리에 닿으면 —
    //  ① 잠시 뒤 동굴 소리가 확 줄어 내 숨·발소리만 남는다
    //  ② 손전등이 깜빡이다 꺼진다 (한 번도 꺼진 적 없는 불 — 고장이 아니라 "무언가"로 읽힌다)
    //  ③ 어둠 속 먼 곳에서 발소리가 천천히 다가오고, 가까워질수록 심장이 빨라진다
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

        [Header("③ 발소리 — 경로 점마다 한 걸음, 먼 곳 → 가까운 곳")]
        [SerializeField] AudioSource stepSource; // 3D. 경로 점의 부모가 아닌 별도 오브젝트
        [SerializeField] Transform[] stepPath;
        [SerializeField] AudioClip[] steps;
        [SerializeField, Range(0f, 1f)] float stepVolume = 0.8f;
        [Tooltip("걸음 사이 간격 — 서두르지 않는 걸음")]
        [SerializeField] Vector2 stepInterval = new Vector2(0.75f, 0.95f);
        [SerializeField] Vector2 stepPitch = new Vector2(0.9f, 1.0f);
        [Tooltip("첫 걸음 / 마지막 걸음의 심장 세기 배율 (6루프 최대 세기 기준)")]
        [SerializeField] Vector2 heartScale = new Vector2(0.35f, 1f);

        [Header("④ 정적")]
        [SerializeField] float silenceTime = 3f;

        [Header("⑤ 복귀")]
        [SerializeField] float flickerOnTime = 0.8f;
        [SerializeField] float restoreTime = 4f;
        [Tooltip("불이 돌아온 뒤 심장이 최고조를 유지하는 시간 — 그 뒤 천천히 가라앉는다")]
        [SerializeField] float heartAfterLight = 1.5f;

        enum State { Idle, Armed, Running, Done }
        State state = State.Done;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (stepSource != null) stepSource.playOnAwake = false;
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
            yield return new WaitForSeconds(darkBeforeSteps);

            // ③
            int last = -1;
            for (int i = 0; i < stepPath.Length; i++)
            {
                float t = stepPath.Length > 1 ? (float)i / (stepPath.Length - 1) : 1f;
                Heartbeat.Instance?.Raise(stepInterval.y + 0.5f, Mathf.Lerp(heartScale.x, heartScale.y, t)); // 다음 걸음까지 이어진다

                stepSource.transform.position = stepPath[i].position;
                int c = Random.Range(0, steps.Length);
                if (steps.Length > 1 && c == last) c = (c + 1) % steps.Length;
                last = c;
                stepSource.pitch = Random.Range(stepPitch.x, stepPitch.y);
                stepSource.PlayOneShot(steps[c], stepVolume);
                yield return new WaitForSeconds(Random.Range(stepInterval.x, stepInterval.y));
            }

            // ④ 심장은 최고조로 뛴 채
            Heartbeat.Instance?.Raise(silenceTime + flickerOnTime + heartAfterLight, heartScale.y);
            yield return new WaitForSeconds(silenceTime);

            // ⑤ — 불이 먼저, 소리는 천천히
            yield return flashlight.Flicker(flickerOnTime, turnOn: true);
            yield return FadeAmbience(1f, restoreTime);
            state = State.Done;
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
