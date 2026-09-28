using System.Collections.Generic;
using UnityEngine;

namespace CaveGame
{
    // 정해진 루프 동안 숨을 쉴 때마다 캠 렌즈에 김이 서렸다가 천천히 걷힌다 (CamcorderGlitch 셰이더의 _CamFog).
    // 숨소리 클립의 음량 곡선을 미리 읽어 두고 현재 재생 위치의 값을 쓰므로, 호흡 클립을 바꿔도 박자가 저절로 맞는다.
    public class LensFog : LoopListener
    {
        static readonly int FogId = Shader.PropertyToID("_CamFog");
        const float Window = 0.05f; // 음량 곡선 해상도 (초)

        [SerializeField, Min(1)] int fromLoop = 4;
        [SerializeField, Min(1)] int toLoop = 4;
        [SerializeField] PlayerAudioController playerAudio;

        [Header("숨 음량(dB) → 김 세기")]
        [Tooltip("숨소리가 이 음량 이하면 김이 새로 서리지 않는다 (숨 사이 정적)")]
        [SerializeField] float quietDb = -32f;
        [Tooltip("이 음량이면 김이 최대로 서린다")]
        [SerializeField] float loudDb = -20f;
        [SerializeField, Range(0f, 1f)] float maxFog = 0.7f;

        [Header("속도 (초)")]
        [Tooltip("김이 차오르는 데 걸리는 시간")]
        [SerializeField] float fogUpTime = 0.35f;
        [Tooltip("김이 걷히는 데 걸리는 시간 — 차오를 때보다 느리게")]
        [SerializeField] float clearTime = 1.6f;

        readonly Dictionary<AudioClip, float[]> envelopes = new Dictionary<AudioClip, float[]>();
        bool active;
        float fog;

        void Awake()
        {
            if (playerAudio == null) playerAudio = FindFirstObjectByType<PlayerAudioController>();
        }

        protected override void OnLoopChanged(int loop) => active = loop >= fromLoop && loop <= toLoop;

        protected override void OnDisable()
        {
            base.OnDisable();
            fog = 0f;
            Shader.SetGlobalFloat(FogId, 0f);
        }

        void Update()
        {
            float target = active ? BreathLevel() * maxFog : 0f;
            float time = target > fog ? fogUpTime : clearTime;
            fog = Mathf.MoveTowards(fog, target, Time.deltaTime / Mathf.Max(time, 0.01f));
            Shader.SetGlobalFloat(FogId, fog);
        }

        // 지금 들리는 숨의 세기 0~1
        float BreathLevel()
        {
            AudioSource breath = playerAudio != null ? playerAudio.BreathSource : null;
            if (breath == null || !breath.isPlaying || breath.clip == null) return 0f;

            float[] env = Envelope(breath.clip);
            if (env == null) return 0f;
            int i = Mathf.Clamp((int)(breath.time / Window), 0, env.Length - 1);
            return Mathf.InverseLerp(quietDb, loudDb, env[i]);
        }

        float[] Envelope(AudioClip clip)
        {
            if (envelopes.TryGetValue(clip, out float[] env)) return env;

            // GetData는 Decompress On Load 클립에서만 동작한다 — 실패하면 김 없이 넘어간다
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0))
            {
                envelopes[clip] = null;
                return null;
            }

            int step = Mathf.Max(1, (int)(clip.frequency * Window)) * clip.channels;
            env = new float[(data.Length + step - 1) / step];
            for (int w = 0; w < env.Length; w++)
            {
                double sum = 0;
                int end = Mathf.Min(data.Length, (w + 1) * step);
                for (int s = w * step; s < end; s++) sum += data[s] * data[s];
                float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, end - w * step)));
                env[w] = 20f * Mathf.Log10(rms + 1e-6f);
            }
            envelopes[clip] = env;
            return env;
        }
    }
}
