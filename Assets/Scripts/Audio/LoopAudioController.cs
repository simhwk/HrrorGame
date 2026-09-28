using System.Collections;
using UnityEngine;

namespace CaveGame
{
    public class LoopAudioController : LoopListener
    {
        [System.Serializable]
        class Layer
        {
            public string name;
            public AudioClip[] clips;
            public int fromLoop = 1;
            public int toLoop = 7;
            [Range(0f, 1f)] public float volume = 0.5f;
            public bool continuous = true;
            public Vector2 interval = new Vector2(3f, 8f);

            [System.NonSerialized] public AudioSource source;
            [System.NonSerialized] public bool active;
            [System.NonSerialized] public float timer;
        }

        [System.Serializable]
        class LoopEvent
        {
            public string name;
            public int loop = 2;
            public AudioClip clip;
            [Tooltip("trigger가 비어 있으면 루프 시작부터, 있으면 플레이어가 그 지점 반경에 들어간 뒤부터 센다")]
            public float delay = 10f;
            [Range(0f, 1f)] public float volume = 0.8f;
            public Transform trigger;
            public float triggerRadius = 2f;
            [Tooltip("비우면 화면 전체(2D)로 들린다. 지정하면 그 AudioSource(3D)의 위치에서 울린다")]
            public AudioSource emitter;
        }

        [SerializeField] Layer[] layers;
        [SerializeField] LoopEvent[] events;
        [SerializeField] float fadeTime = 3f;

        AudioSource eventSource;
        Transform player;

        void Awake()
        {
            foreach (var layer in layers)
            {
                layer.source = gameObject.AddComponent<AudioSource>();
                layer.source.playOnAwake = false;
                layer.source.loop = layer.continuous;
                layer.source.volume = 0f;
            }

            eventSource = gameObject.AddComponent<AudioSource>();
            eventSource.playOnAwake = false;
        }

        protected override void OnLoopChanged(int loop)
        {
            foreach (var layer in layers)
            {
                bool wasActive = layer.active;
                layer.active = loop >= layer.fromLoop && loop <= layer.toLoop && layer.clips.Length > 0;

                if (layer.active && !wasActive)
                {
                    if (layer.continuous)
                    {
                        layer.source.clip = RandomClip(layer);
                        layer.source.Play();
                    }
                    else
                    {
                        layer.timer = Random.Range(layer.interval.x, layer.interval.y);
                    }
                }
            }

            StopAllCoroutines(); // 이전 루프에서 아직 안 울린 이벤트는 취소
            foreach (var e in events)
            {
                if (e.loop == loop && e.clip != null)
                    StartCoroutine(PlayEvent(e));
            }
        }

        IEnumerator PlayEvent(LoopEvent e)
        {
            if (e.trigger != null)
            {
                if (player == null) player = FindFirstObjectByType<PlayerController>().transform;
                float r2 = e.triggerRadius * e.triggerRadius;
                while (FlatSqrDistance(player.position, e.trigger.position) > r2) yield return null;
            }
            yield return new WaitForSeconds(e.delay);
            (e.emitter != null ? e.emitter : eventSource).PlayOneShot(e.clip, e.volume);
        }

        void Update()
        {
            foreach (var layer in layers)
            {
                float target = layer.active ? layer.volume : 0f;
                layer.source.volume = Mathf.MoveTowards(layer.source.volume, target, Time.deltaTime / fadeTime);

                if (!layer.active && layer.source.volume <= 0f && layer.source.isPlaying)
                    layer.source.Stop();

                if (layer.active && !layer.continuous)
                {
                    layer.timer -= Time.deltaTime;
                    if (layer.timer <= 0f)
                    {
                        layer.source.pitch = Random.Range(0.9f, 1.1f);
                        layer.source.PlayOneShot(RandomClip(layer));
                        layer.timer = Random.Range(layer.interval.x, layer.interval.y);
                    }
                }
            }
        }

        static AudioClip RandomClip(Layer layer) => layer.clips[Random.Range(0, layer.clips.Length)];

        // 높이는 무시 — 웅크림·바닥 요철과 무관하게
        static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return (a - b).sqrMagnitude;
        }
    }
}
