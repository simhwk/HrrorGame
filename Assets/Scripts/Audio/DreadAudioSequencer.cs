using UnityEngine;

namespace CaveGame
{
    // 루프마다 공포 큐(루프 사운드)를 하나씩 쌓아 올리고, 엔딩의 공격 사운드를 재생한다.
    public class DreadAudioSequencer : LoopListener
    {
        [SerializeField] AudioClip[] dreadCues = new AudioClip[5];
        [SerializeField] AudioClip attackFleshClip;
        [SerializeField] AudioClip attackCreatureClip;
        [SerializeField] AudioSource attackSource;
        [SerializeField] Transform cuesParent;
        [SerializeField, Range(0f, 1f)] float cueVolume = 0.7f;

        AudioSource[] activeCues;

        void Awake() => activeCues = new AudioSource[dreadCues.Length];

        // 큐가 루프 수보다 적으면 마지막 큐가 유지된다
        protected override void OnLoopChanged(int loop)
        {
            if (dreadCues.Length == 0) return;

            int index = Mathf.Clamp(loop - 1, 0, dreadCues.Length - 1);
            AudioClip clip = dreadCues[index];
            if (clip == null || activeCues[index] != null) return;

            var go = new GameObject($"DreadCue_{index + 1}");
            go.transform.SetParent(cuesParent, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.volume = cueVolume;
            source.Play();

            activeCues[index] = source;
        }

        public void PlayAttack(Vector3 position)
        {
            if (attackSource == null) return;
            attackSource.transform.position = position;
            if (attackFleshClip != null) attackSource.PlayOneShot(attackFleshClip);
            if (attackCreatureClip != null) attackSource.PlayOneShot(attackCreatureClip);
        }
    }
}
