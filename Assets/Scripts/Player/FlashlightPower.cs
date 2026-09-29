using System.Collections;
using UnityEngine;

namespace CaveGame
{
    // 손전등 전원. 플레이어는 끌 수 없다 — 연출만 깜빡이게 하거나 끈다 (6루프 암전).
    // 불빛(Light)과 함께 빛줄기 모양 메시·먼지 파티클도 같이 꺼야 "꺼졌다"로 보인다.
    public class FlashlightPower : MonoBehaviour
    {
        [SerializeField] Light beam;
        [Tooltip("불과 함께 꺼질 것들 — 빛줄기 콘, 빛 속 먼지 파티클")]
        [SerializeField] Renderer[] withBeam;

        [Header("깜빡임")]
        [Tooltip("한 번 꺼져 있는 시간 (초)")]
        [SerializeField] Vector2 offTime = new Vector2(0.04f, 0.18f);
        [Tooltip("한 번 켜져 있는 시간 (초) — 끝으로 갈수록 짧아져 숨이 넘어가듯 꺼진다")]
        [SerializeField] Vector2 onTime = new Vector2(0.05f, 0.35f);
        [Tooltip("깜빡이는 동안 켜진 순간의 밝기 배율 — 전력이 모자라 흐릿하다")]
        [SerializeField] Vector2 dimLevel = new Vector2(0.25f, 0.8f);

        [Header("소리 (비우면 없음)")]
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip[] buzz; // 지직 — 깜빡일 때
        [SerializeField, Range(0f, 1f)] float buzzVolume = 0.25f;

        float baseIntensity;
        public bool IsOn { get; private set; } = true;

        void Awake()
        {
            if (beam == null) beam = GetComponent<Light>();
            baseIntensity = beam.intensity;
        }

        void OnDisable() => Set(1f); // 루프 이동 등으로 연출이 끊겨도 영영 꺼진 채로 남지 않게

        public void Set(float level)
        {
            IsOn = level > 0f;
            beam.enabled = IsOn;
            beam.intensity = baseIntensity * level;
            foreach (var r in withBeam)
                if (r != null) r.enabled = IsOn;
        }

        // 깜빡이다가 꺼진다 (turnOn이면 깜빡이다가 켜진다)
        public IEnumerator Flicker(float duration, bool turnOn)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                float progress = 1f - (end - Time.time) / duration; // 0 → 1
                float fading = turnOn ? progress : 1f - progress;   // 1 = 멀쩡한 쪽

                Set(0f);
                yield return new WaitForSeconds(Random.Range(offTime.x, offTime.y));

                Set(Random.Range(dimLevel.x, dimLevel.y) * Mathf.Lerp(0.5f, 1f, fading));
                if (sfx != null && buzz != null && buzz.Length > 0 && Random.value < 0.6f)
                    sfx.PlayOneShot(buzz[Random.Range(0, buzz.Length)], buzzVolume);
                yield return new WaitForSeconds(Mathf.Lerp(onTime.x, onTime.y, fading) * Random.Range(0.6f, 1f));
            }
            Set(turnOn ? 1f : 0f);
        }
    }
}
