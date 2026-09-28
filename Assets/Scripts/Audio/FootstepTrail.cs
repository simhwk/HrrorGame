using System.Collections;
using UnityEngine;

namespace CaveGame
{
    // 플레이어가 지정 지점 근처에 오면, 경로를 따라 멀어지는 발소리를 한 번 들려준다 (루프마다 한 번).
    // 3루프: 무너진 케언에 다가가면 누군가 갈래 안쪽으로 급히 걸어가는 소리 — 무너진 표식 대신 소리가 길을 알려준다.
    // 경로 점(path)마다 발소리 한 번. 소리 위치(source)가 실제로 옮겨가므로 거리 감쇠만으로 점점 멀어진다.
    // source는 경로 점들의 부모가 아닌 별도 오브젝트여야 한다 — 부모를 옮기면 경로 점도 같이 끌려간다.
    public class FootstepTrail : LoopListener
    {
        [SerializeField, Min(1)] int loop = 3;
        [SerializeField] AudioSource source;
        [SerializeField] Transform triggerCenter;
        [SerializeField] float triggerRadius = 3f;
        [SerializeField] Transform[] path;
        [SerializeField] AudioClip[] clips;
        [Tooltip("걸음 사이 간격(초) — 짧을수록 급하게 들린다")]
        [SerializeField] Vector2 stepInterval = new Vector2(0.26f, 0.34f);
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [Tooltip("마지막 걸음의 음량 배율 — 거리 감쇠만으론 뚝 끊겨 들려서, 걸음마다 조금씩 더 줄여 흐려지며 끝나게 한다")]
        [SerializeField, Range(0f, 1f)] float lastStepVolume = 0.35f;
        [SerializeField] Vector2 pitch = new Vector2(0.95f, 1.05f);

        Transform player;
        bool armed;

        void Awake()
        {
            source.playOnAwake = false;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        protected override void OnLoopChanged(int current)
        {
            StopAllCoroutines();
            armed = current == loop;
        }

        void Update()
        {
            if (!armed || player == null) return;

            Vector3 d = player.position - triggerCenter.position;
            d.y = 0f; // 높이는 무시 — 웅크림·바닥 요철과 무관하게
            if (d.sqrMagnitude > triggerRadius * triggerRadius) return;

            armed = false;
            StartCoroutine(Walk());
        }

        IEnumerator Walk()
        {
            int last = -1;
            for (int step = 0; step < path.Length; step++)
            {
                source.transform.position = path[step].position;
                float fade = Mathf.Lerp(1f, lastStepVolume, path.Length > 1 ? (float)step / (path.Length - 1) : 0f);
                int i = Random.Range(0, clips.Length);
                if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length; // 같은 발소리 연속 금지
                last = i;
                source.pitch = Random.Range(pitch.x, pitch.y);
                source.PlayOneShot(clips[i], volume * fade);
                yield return new WaitForSeconds(Random.Range(stepInterval.x, stepInterval.y));
            }
        }

        void OnDrawGizmosSelected()
        {
            if (triggerCenter != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(triggerCenter.position, triggerRadius);
            }
            if (path == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == null) continue;
                Gizmos.DrawSphere(path[i].position, 0.12f);
                if (i > 0 && path[i - 1] != null) Gizmos.DrawLine(path[i - 1].position, path[i].position);
            }
        }
    }
}
