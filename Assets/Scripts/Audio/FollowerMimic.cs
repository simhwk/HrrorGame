using UnityEngine;

namespace CaveGame
{
    // 4루프: 지정 지점을 지나면, 기어가는 도중 등 뒤 먼 곳에서 동굴을 울리는 낮고 긴 울음이 난다 (5루프에 모습을 드러낼 그 괴물의 목소리).
    // 소리가 먼저 나서 플레이어가 뒤를 돌아보게 만든다 — 돌아본 곳엔 아무것도 없고, 다시 가면 또 난다.
    // - 플레이어가 절대 내지 않는 소리여야 한다: 내 기어가는 소리와 같은 계열이면 들려도 "내 소리"로 넘겨 버린다.
    // - 일정 거리를 나아갈 때마다 한 번 (멈춰 서 있으면 잠시 뒤에도 한 번). stopWhenLookedAt을 켜면 돌아볼 때 끊긴다.
    // - 들릴 때마다 가까워진다 (beatDistances). 마지막 뒤로는 영영 조용하다.
    //
    // "뒤"를 확실히 들리게 하는 법: Unity 기본 3D 사운드는 좌우 음량 차이만 계산해 정면과 정뒤가 똑같이 들린다.
    // 그래서 정뒤가 아니라 뒤쪽 비스듬히(4~5시, 7~8시)에 놓아 좌우 차이를 만들고, 뒤일수록 고음을 살짝 깎고,
    // 소리 직후 캐릭터가 흠칫 숨을 들이켜게 해 "방금 뭔가 있었다"는 걸 몸으로 알려준다.
    public class FollowerMimic : LoopListener
    {
        enum State { Waiting, Listening, Playing, Done }

        [SerializeField, Min(1)] int loop = 4;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerAudioController playerAudio;
        [SerializeField] Transform triggerCenter;
        [SerializeField] float triggerRadius = 2f;

        [Header("기척 — 나아갈 때마다 한 번씩, 점점 가깝게")]
        [Tooltip("귀에서 이 직선거리(m)에서 난다. 개수 = 게임 전체의 최대 횟수 (루프를 다시 와도 늘지 않는다)")]
        [SerializeField] float[] beatDistances = { 7f, 5f, 3.5f, 2f };
        [Tooltip("정뒤에서 좌우로 이만큼(도) 비껴 난다 — 매번 이 범위에서 무작위, 좌우는 번갈아")]
        [SerializeField] Vector2 rearAngle = new Vector2(35f, 50f);
        [Tooltip("이만큼 기어갈 때마다 한 번 (매번 이 범위에서 무작위) — 첫 기척은 트리거 진입 후 이만큼")]
        [SerializeField] Vector2 travelBetweenBeats = new Vector2(2.5f, 4f);
        [Tooltip("멈춰 서 있어도 이 시간이 지나면 한 번 — 기다리는 플레이어도 놓치지 않게")]
        [SerializeField] float idleBeatTime = 5f;
        [Tooltip("카메라 정면과 소리 방향의 내적이 이 값을 넘으면(돌아보면) 끊긴다")]
        [SerializeField, Range(-1f, 1f)] float lookBackDot = 0.1f;
        [Tooltip("켜면 돌아볼 때 소리가 뚝 끊긴다 — 짧은 기척용. 동굴을 울리는 긴 울음은 끝까지 들려야 하니 끈다")]
        [SerializeField] bool stopWhenLookedAt;

        [Header("소리")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioLowPassFilter lowPass;
        [Tooltip("기척 소리들 (동굴을 울리는 낮고 긴 울음) — 기척마다 하나씩, 같은 게 연달아 나오지 않게")]
        [SerializeField] AudioClip[] knocks;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField] Vector2 pitch = new Vector2(0.9f, 1.05f);
        [Tooltip("정뒤일 때 / 옆일 때 저역통과 주파수 — 머리 뒤에서 오는 소리는 살짝 먹먹하다 (너무 깎으면 딸깍이 사라짐)")]
        [SerializeField] Vector2 cutoffBehindToSide = new Vector2(8000f, 16000f);

        [Header("흠칫 — 소리 직후 캐릭터가 숨을 들이켠다 (비우면 없음)")]
        [SerializeField] AudioClip gasp;
        [SerializeField, Range(0f, 1f)] float gaspVolume = 0.6f;
        [SerializeField] float gaspDelay = 0.18f;
        [Tooltip("기척이 나는 동안 플레이어 숨소리 배율 — 숨을 죽인다")]
        [SerializeField, Range(0f, 1f)] float breathDuck = 0.15f;
        [Tooltip("울음이 끝난 뒤에도 심장이 뛰는 시간 (세기는 Heartbeat의 4루프 값)")]
        [SerializeField] float heartAfterBeat = 3f;
        [Tooltip("켜면 발동·기척마다 콘솔에 남기고, F2로 지금 바로 등 뒤에서 기척 하나를 낸다 — 음량 확인용")]
        [SerializeField] bool debugLog;

        const float MaxStep = 2f; // 한 프레임에 이보다 멀리 움직였으면 순간이동 — 이동 거리에 넣지 않는다

        AudioSource gaspSource;
        State state = State.Done;
        int beat, lastKnock = -1;
        float traveled, nextTravel, lastBeatTime, beatEnd, gaspAt = -1f;
        Vector3 lastPos;
        float side = 1f;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (playerAudio == null) playerAudio = FindFirstObjectByType<PlayerAudioController>();
            source.playOnAwake = false;
            source.loop = false;

            gaspSource = gameObject.AddComponent<AudioSource>(); // 내 숨 — 머릿속에서 들리는 2D
            gaspSource.playOnAwake = false;
            gaspSource.spatialBlend = 0f;
        }

        protected override void OnLoopChanged(int current)
        {
            source.Stop();
            gaspAt = -1f;
            lastPos = player.transform.position;
            // 횟수(beat)는 게임 전체에서 센다 — 4루프를 다시 지나도 이미 운 만큼은 다시 울지 않는다
            state = current == loop && beat < beatDistances.Length ? State.Waiting : State.Done;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (playerAudio != null) playerAudio.BreathDuck = 1f;
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLog && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
                DebugBeat();
#endif
            if (gaspAt >= 0f && Time.time >= gaspAt)
            {
                gaspAt = -1f;
                if (gasp != null) gaspSource.PlayOneShot(gasp, gaspVolume);
            }

            if (state == State.Done) return;

            Vector3 pos = player.transform.position;
            float step = Flat(pos - lastPos).magnitude;
            if (step < MaxStep) traveled += step;
            lastPos = pos;

            switch (state)
            {
                case State.Waiting:
                    if (Flat(pos - triggerCenter.position).sqrMagnitude <= triggerRadius * triggerRadius)
                        BeginListening();
                    break;

                case State.Listening:
                    if (traveled >= nextTravel || Time.time - lastBeatTime >= idleBeatTime)
                        StartBeat();
                    break;

                case State.Playing:
                    UpdateCutoff();
                    if (stopWhenLookedAt && LookingAt())
                    {
                        source.Stop(); // 돌아보면 소리가 뚝 끊긴다 — 돌아본 곳엔 아무것도 없다
                        if (debugLog) Debug.Log("[FollowerMimic] 돌아봐서 기척이 끊김", this);
                    }
                    if (!source.isPlaying || Time.time >= beatEnd) EndBeat();
                    break;
            }
        }

        void BeginListening()
        {
            state = State.Listening;
            if (debugLog) Debug.Log("[FollowerMimic] 트리거 진입 — 기어가면 뒤에서 기척", this);
            ResetBeatTimer();
        }

        void ResetBeatTimer()
        {
            traveled = 0f;
            nextTravel = Random.Range(travelBetweenBeats.x, travelBetweenBeats.y);
            lastBeatTime = Time.time;
        }

        void StartBeat()
        {
            PlaceBehind(beatDistances[beat]);
            PlayKnock();
            state = State.Playing;
            beatEnd = Time.time + source.clip.length + 0.2f;
            gaspAt = Time.time + gaspDelay;
            if (playerAudio != null) playerAudio.BreathDuck = breathDuck;
            Heartbeat.Instance?.Raise(source.clip.length + heartAfterBeat); // 울음을 듣고 약하게 — 5루프에 뜻을 알게 될 복선

            if (debugLog)
                Debug.Log($"[FollowerMimic] 기척 {beat + 1}/{beatDistances.Length} — {beatDistances[beat]}m, " +
                          $"{(side > 0 ? "왼쪽" : "오른쪽")} 뒤, 저역 {lowPass.cutoffFrequency:F0}Hz", this);
        }

        void PlayKnock()
        {
            int i = Random.Range(0, knocks.Length);
            if (knocks.Length > 1 && i == lastKnock) i = (i + 1) % knocks.Length;
            lastKnock = i;

            UpdateCutoff();
            source.clip = knocks[i];
            source.pitch = Random.Range(pitch.x, pitch.y);
            source.volume = volume;
            source.Play();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 조건 무시하고 지금 등 뒤 4m에서 한 번 — 루프·횟수에 영향 없음
        void DebugBeat()
        {
            side = -side; // 누를 때마다 좌우 번갈아
            PlaceBehind(4f);
            PlayKnock();
            gaspAt = Time.time + gaspDelay;
            Debug.Log($"[FollowerMimic] F2 테스트 기척 — 4m {(side > 0 ? "왼쪽" : "오른쪽")} 뒤, 저역 {lowPass.cutoffFrequency:F0}Hz", this);
        }
#endif

        void EndBeat()
        {
            beat++;
            ResetBeatTimer();
            side = -side; // 좌우를 번갈아 — 매번 같은 자리면 소리 파일처럼 들린다
            if (playerAudio != null) playerAudio.BreathDuck = 1f;
            state = beat >= beatDistances.Length ? State.Done : State.Listening;
        }

        // 지금 보는 방향의 정반대에서 좌우로 비껴, 바닥 높이에 둔다. 위치는 그 순간 고정 — 돌아봐도 따라오지 않는다.
        void PlaceBehind(float distance)
        {
            side = Random.value < 0.5f && beat == 0 ? -side : side; // 첫 기척의 좌우만 무작위
            Transform cam = player.CameraPivot;
            Vector3 back = -Flat(cam.forward).normalized;
            if (back.sqrMagnitude < 0.01f) back = -player.transform.forward;
            Vector3 dir = Quaternion.AngleAxis(Random.Range(rearAngle.x, rearAngle.y) * side, Vector3.up) * back;
            Vector3 pos = cam.position + dir * distance;
            pos.y = player.transform.position.y + 0.15f; // 바닥의 돌
            source.transform.position = pos;
        }

        Vector3 SoundDirection() => (source.transform.position - player.CameraPivot.position).normalized;

        bool LookingAt() => Vector3.Dot(player.CameraPivot.forward, SoundDirection()) > lookBackDot;

        void UpdateCutoff()
        {
            float dot = Vector3.Dot(player.CameraPivot.forward, SoundDirection()); // -1 정뒤 ~ 0 옆
            lowPass.cutoffFrequency = Mathf.Lerp(cutoffBehindToSide.x, cutoffBehindToSide.y, Mathf.InverseLerp(-1f, 0f, dot));
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        void OnDrawGizmosSelected()
        {
            if (triggerCenter == null) return;
            Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(triggerCenter.position, triggerRadius);
        }
    }
}
