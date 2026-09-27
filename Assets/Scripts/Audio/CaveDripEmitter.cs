using UnityEngine;

namespace CaveGame
{
    // Plays distant water drops around the listener. A drop "spot" is held for several drops
    // so it reads as one leak somewhere in the cave, then relocates.
    public class CaveDripEmitter : MonoBehaviour
    {
        [SerializeField] AudioClip[] clips;
        [SerializeField] Transform listener;

        [Header("Timing")]
        [SerializeField] Vector2 interval = new Vector2(1.5f, 2.5f);
        [SerializeField] Vector2Int dropsPerSpot = new Vector2Int(4, 10);

        [Header("Placement")]
        [SerializeField] Vector2 distance = new Vector2(10f, 30f);
        [SerializeField] Vector2 height = new Vector2(-2f, 6f);
        // Hysteresis: a spot is kept until the player is this far outside the distance range.
        [SerializeField] float relocateMargin = 5f;

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;
        [SerializeField] Vector2 pitch = new Vector2(0.9f, 1.1f);
        [SerializeField, Range(0f, 360f)] float spread = 90f;
        [SerializeField] int voices = 3;

        AudioSource[] pool;
        int nextVoice;
        Vector3 spot;
        int dropsLeft;
        float timer;

        void Awake()
        {
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("DripVoice" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.spread = spread;
                s.dopplerLevel = 0f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = distance.x;
                s.maxDistance = distance.y * 4f;
                pool[i] = s;
            }
        }

        void Start()
        {
            if (listener == null)
            {
                var l = FindFirstObjectByType<AudioListener>();
                if (l != null) listener = l.transform;
            }
            timer = Random.Range(interval.x, interval.y);
        }

        void Update()
        {
            if (listener == null || clips.Length == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(interval.x, interval.y);

            // Relocate when the leak has dripped enough or the player walked well outside the distance range.
            float d = Vector3.Distance(spot, listener.position);
            if (dropsLeft <= 0 || d < distance.x - relocateMargin || d > distance.y + relocateMargin)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(distance.x, distance.y);
                spot = listener.position + new Vector3(dir.x, Random.Range(height.x, height.y), dir.y);
                dropsLeft = Random.Range(dropsPerSpot.x, dropsPerSpot.y + 1);
            }
            dropsLeft--;

            var s = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            s.transform.position = spot;
            s.pitch = Random.Range(pitch.x, pitch.y);
            s.clip = clips[Random.Range(0, clips.Length)];
            s.volume = volume * Random.Range(0.7f, 1f);
            s.Play();
        }
    }
}
