using UnityEngine;

namespace CaveGame
{
    // 정해진 루프 동안 캠코더 화면이 이따금 "치칙" 끊긴다 — 화면은 CamcorderGlitch 셰이더(전역 _CamGlitch), 소리는 절차 생성한 정전기.
    // 한 번의 끊김(burst)은 짧은 조각들로 쪼개져 켜졌다 꺼졌다 한다 — 계속 켜 두면 필터처럼 보이고, 끊겨야 신호 불량처럼 보인다.
    [RequireComponent(typeof(AudioSource))]
    public class CamcorderGlitch : LoopListener
    {
        static readonly int GlitchId = Shader.PropertyToID("_CamGlitch");
        static readonly int SeedId = Shader.PropertyToID("_CamGlitchSeed");

        [SerializeField, Min(1)] int fromLoop = 2;
        [SerializeField, Min(1)] int toLoop = 2;

        [Header("타이밍 (초)")]
        [SerializeField] Vector2 firstDelay = new Vector2(4f, 8f);   // 루프 시작 후 첫 끊김까지
        [SerializeField] Vector2 interval = new Vector2(8f, 16f);    // 끊김 사이
        [SerializeField] Vector2 burstLength = new Vector2(0.15f, 0.45f);
        [SerializeField] Vector2 chopLength = new Vector2(0.03f, 0.07f); // 끊김 안에서 켜짐/꺼짐이 바뀌는 간격
        [SerializeField, Range(0f, 1f)] float chopOffChance = 0.3f;
        [Tooltip("한 루프에서 최대 몇 번 끊길지 — 너무 잦으면 연출이 아니라 필터가 된다")]
        [SerializeField, Min(1)] int maxBursts = 3;
        [Tooltip("마지막 루프(toLoop) 끝선을 넘는 순간 이만큼 길게 끊긴다 — 순간이동도 함께 가려진다. 0이면 없음")]
        [SerializeField, Min(0f)] float exitBurstLength = 1f;

        [Header("세기")]
        [SerializeField] Vector2 intensity = new Vector2(0.5f, 1f);
        [SerializeField, Range(0f, 1f)] float volume = 0.35f;

        AudioSource source;
        bool active;
        int lastLoop;
        int burstsLeft;
        float nextBurst;
        float burstEnd;
        float nextChop;
        float current; // 현재 조각의 세기 (0 = 꺼진 조각)

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 캠코더 자체 소리
            source.clip = CreateStaticClip();
            Shader.SetGlobalFloat(GlitchId, 0f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetGlitch(0f);
        }

        protected override void OnLoopChanged(int loop)
        {
            bool exiting = lastLoop == toLoop && loop > toLoop && exitBurstLength > 0f;
            lastLoop = loop;
            active = loop >= fromLoop && loop <= toLoop;
            burstsLeft = maxBursts;
            nextBurst = Time.time + Random.Range(firstDelay.x, firstDelay.y);
            burstEnd = 0f;
            SetGlitch(0f);

            if (exiting) StartBurst(Time.time, exitBurstLength);
        }

        void Update()
        {
            float now = Time.time;
            if (!active && now >= burstEnd)
            {
                if (current > 0f || source.isPlaying) SetGlitch(0f); // 끝선 끊김이 막 끝남
                return;
            }

            if (now < burstEnd)
            {
                if (now >= nextChop) Chop(now);
                return;
            }

            if (current > 0f || source.isPlaying) SetGlitch(0f); // 끊김이 막 끝남

            if (burstsLeft > 0 && now >= nextBurst)
            {
                burstsLeft--;
                StartBurst(now, Random.Range(burstLength.x, burstLength.y));
                nextBurst = burstEnd + Random.Range(interval.x, interval.y);
            }
        }

        void StartBurst(float now, float length)
        {
            burstEnd = now + length;
            source.time = Random.Range(0f, source.clip.length * 0.9f); // 매번 다른 지직거림
            source.Play();
            Chop(now);
        }

        void Chop(float now)
        {
            nextChop = now + Random.Range(chopLength.x, chopLength.y);
            SetGlitch(Random.value < chopOffChance ? 0f : Random.Range(intensity.x, intensity.y));
            Shader.SetGlobalFloat(SeedId, Random.Range(0f, 100f));
            if (!source.isPlaying) source.Play();
        }

        void SetGlitch(float value)
        {
            current = value;
            Shader.SetGlobalFloat(GlitchId, value);
            if (source == null) return;
            source.volume = value * volume;
            if (value <= 0f && Time.time >= burstEnd) source.Stop();
        }

        // 1초짜리 정전기: 짧은 조각마다 세기가 튀고, 일부 조각은 끊긴다. 1차 차분으로 저음을 빼 "치직"하게 만든다.
        static AudioClip CreateStaticClip()
        {
            const int rate = 44100;
            var data = new float[rate];
            var rng = new System.Random(7);
            float gain = 0f, prev = 0f;
            int segmentLeft = 0;

            for (int i = 0; i < data.Length; i++)
            {
                if (--segmentLeft <= 0)
                {
                    segmentLeft = rng.Next(rate / 200, rate / 30); // 5~33ms
                    gain = rng.NextDouble() < 0.2 ? 0f : 0.25f + (float)rng.NextDouble() * 0.75f;
                }
                float white = (float)rng.NextDouble() * 2f - 1f;
                data[i] = (white - prev) * 0.5f * gain;
                prev = white;
            }

            var clip = AudioClip.Create("CamcorderStatic", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
