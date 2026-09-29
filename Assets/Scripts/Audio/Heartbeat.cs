using UnityEngine;

namespace CaveGame
{
    // 내 심장소리 — "괴물이 가까이 있다"는 신호. 연출 스크립트가 Raise로 올리고, 시간이 지나면 저절로 가라앉는다.
    // - 세기는 루프마다 정해 둔다 (strengthByLoop). 부르는 쪽은 "지금 뛴다"만 알리고 얼마나 센지는 여기서 한 곳에서 조율한다.
    // - 거슬리지 않게: 박동 하나짜리 샘플을 간격만 바꿔 재생하고(pitch를 올리면 음색이 변해 기계처럼 들린다),
    //   저역통과로 먹먹하게 깎아 "들린다"보다 "느껴진다"에 가깝게 둔다. 세질수록 필터가 열려 또렷해진다.
    // - 세기는 서서히 따라간다 — 즉시 튀면 레이더처럼 들린다.
    public class Heartbeat : LoopListener
    {
        public static Heartbeat Instance { get; private set; }

        [Tooltip("쿵-쿵 한 번짜리 박동들 — 매번 다른 걸 골라 같은 소리가 반복되는 티를 없앤다")]
        [SerializeField] AudioClip[] beats;
        [Tooltip("루프별 최대 세기 (0~1). 인덱스 0 = 1루프")]
        [SerializeField] float[] strengthByLoop = { 0f, 0f, 0f, 0.3f, 0.6f, 0.4f, 0.75f };

        [Header("세기 0 → 1 매핑 — 루프마다 빠르기뿐 아니라 크기도 달라진다")]
        [Tooltip("세기 0 / 1일 때 음량. 루프별로 4: 0.55 · 5: 0.74 · 6: 0.61 · 7: 0.84")]
        [SerializeField] Vector2 volume = new Vector2(0.35f, 1f);
        [SerializeField] Vector2 bpm = new Vector2(64f, 100f);
        [Tooltip("저역통과 주파수 — 약할 땐 먹먹하게. 심장 원음은 30Hz라 노트북·이어폰은 재생을 못 한다 — " +
                 "옥타브 올린 층(120~500Hz)이 들려야 하므로 그 아래로 깎지 말 것")]
        [SerializeField] Vector2 cutoff = new Vector2(1500f, 4000f);

        [Header("반응 속도 (초) — 0에서 그 루프의 최대 세기까지")]
        [SerializeField] float attack = 1.2f;
        [SerializeField] float release = 5f;

        AudioSource source;
        AudioLowPassFilter lowPass;
        float loopStrength;
        float level, target, holdUntil, nextBeat;
        int lastBeat = -1;

        public float Level => level;

        void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 머릿속
            lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnLoopChanged(int loop)
        {
            int i = Mathf.Clamp(loop - 1, 0, strengthByLoop.Length - 1);
            loopStrength = strengthByLoop.Length > 0 ? strengthByLoop[i] : 0f;
            holdUntil = 0f; // 루프가 바뀌면 가라앉는다
        }

        // seconds 동안 이 루프의 세기(× scale)로 뛴다. 겹쳐 부르면 더 센 쪽·더 긴 쪽을 따른다.
        // 매 프레임 불러도 된다 (예: 괴물이 보이는 동안 계속).
        public void Raise(float seconds, float scale = 1f)
        {
            float t = loopStrength * Mathf.Clamp01(scale);
            if (Time.time >= holdUntil || t > target) target = t;
            holdUntil = Mathf.Max(holdUntil, Time.time + seconds);
        }

        void Update()
        {
            bool holding = Time.time < holdUntil;
            float goal = holding ? target : 0f;
            // 루프 세기와 상관없이 늘 같은 시간에 걸쳐 오르내린다 — 약한 루프라고 금방 멎지 않게
            float span = Mathf.Max(loopStrength, 0.1f);
            float speed = span / (goal > level ? attack : release);
            level = Mathf.MoveTowards(level, goal, speed * Time.deltaTime);
            if (!holding && level <= 0f) target = 0f;

            if (beats == null || beats.Length == 0 || level <= 0.01f)
            {
                nextBeat = 0f; // 다시 시작하면 바로 첫 박동
                return;
            }

            lowPass.cutoffFrequency = Mathf.Lerp(cutoff.x, cutoff.y, level);
            if (Time.time < nextBeat) return;

            // 시작·끝은 음량이 0에서 스며들고 빠지게 — level 자체가 음량 곡선
            int i = Random.Range(0, beats.Length);
            if (beats.Length > 1 && i == lastBeat) i = (i + 1) % beats.Length;
            lastBeat = i;
            source.PlayOneShot(beats[i], Mathf.Lerp(volume.x, volume.y, level) * Mathf.Clamp01(level * 4f));
            float interval = 60f / Mathf.Lerp(bpm.x, bpm.y, level);
            nextBeat = Time.time + interval * Random.Range(0.97f, 1.03f); // 사람 심장은 메트로놈이 아니다
        }
    }
}
