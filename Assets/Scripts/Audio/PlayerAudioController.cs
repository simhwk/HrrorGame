using UnityEngine;

namespace CaveGame
{
    public class PlayerAudioController : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        [Header("Footsteps")]
        [SerializeField] AudioClip[] steps;
        [SerializeField, Range(0f, 1f)] float stepVolume = 0.6f;
        [SerializeField] float stepInterval = 0.55f;
        [SerializeField] float crouchStepInterval = 0.75f;
        [SerializeField] int offBeatFromLoop = 3;
        [SerializeField] float offBeatAmount = 0.12f;

        [Header("Breathing")]
        [SerializeField] AudioClip[] breathingByLoop = new AudioClip[7];
        [SerializeField, Range(0f, 1f)] float breathingVolume = 0.4f;
        [SerializeField] float breathingFadeTime = 2f;

        [Header("Crouch")]
        [SerializeField] AudioClip[] crouchClips;
        [SerializeField, Range(0f, 1f)] float crouchVolume = 0.5f;

        [Header("Crawl (웅크린 채 이동) — 끌리는 소리 루프 + 손·무릎 짚는 소리 + 옷 스치는 소리")]
        [SerializeField] AudioClip crawlDragLoop; // 긴 자갈 녹음. 매번 랜덤 지점부터 재생해 반복이 티 나지 않게 한다
        [SerializeField, Range(0f, 1f)] float crawlDragVolume = 0.25f;
        [SerializeField] float crawlDragPitch = 0.8f;
        [SerializeField] float crawlDragFadeTime = 0.25f;
        [SerializeField] AudioClip[] crawlContacts;
        [SerializeField, Range(0f, 1f)] float crawlContactVolume = 0.45f;
        [SerializeField] AudioClip[] crawlCloth;
        [SerializeField, Range(0f, 1f)] float crawlClothVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] float crawlClothChance = 0.6f;

        AudioSource stepSource;
        AudioSource breathSource;
        AudioSource dragSource;
        float stepTimer;
        int stepIndex;
        bool wasCrouched;

        int CurrentLoop => GameManager.Instance != null ? GameManager.Instance.CurrentLoop : 1;

        void Awake()
        {
            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;

            breathSource = gameObject.AddComponent<AudioSource>();
            breathSource.playOnAwake = false;
            breathSource.loop = true;
            breathSource.volume = 0f;

            dragSource = gameObject.AddComponent<AudioSource>();
            dragSource.playOnAwake = false;
            dragSource.loop = true;
            dragSource.volume = 0f;
        }

        void Update()
        {
            UpdateFootsteps();
            UpdateBreathing();
            UpdateCrouch();
            UpdateCrawlDrag();
        }

        void UpdateFootsteps()
        {
            bool moving = player.MoveInput.sqrMagnitude > 0.01f && player.IsGrounded;
            if (!moving)
            {
                stepTimer = 0f;
                return;
            }

            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;

            PlayStep();

            float interval = player.IsCrouched ? crouchStepInterval : stepInterval;
            if (CurrentLoop >= offBeatFromLoop)
                interval += Random.Range(-offBeatAmount, offBeatAmount);
            stepTimer = interval;
        }

        bool Crawling => player.IsCrouched && crawlContacts.Length > 0;

        void PlayStep()
        {
            if (Crawling)
            {
                PlayCrawl();
                return;
            }
            if (steps.Length == 0) return;
            stepSource.pitch = Random.Range(0.9f, 1.1f);
            stepSource.PlayOneShot(steps[stepIndex], stepVolume);
            stepIndex = (stepIndex + 1) % steps.Length;
        }

        // 손/무릎이 바닥을 짚는 소리. 옷 스침은 확률로 살짝 늦게 겹쳐 리듬을 흐트러뜨린다
        void PlayCrawl()
        {
            stepSource.pitch = Random.Range(0.75f, 0.95f);
            stepSource.PlayOneShot(crawlContacts[Random.Range(0, crawlContacts.Length)], crawlContactVolume);
            if (crawlCloth.Length > 0 && Random.value < crawlClothChance)
                stepSource.PlayOneShot(crawlCloth[Random.Range(0, crawlCloth.Length)], crawlClothVolume);
        }

        void UpdateCrawlDrag()
        {
            if (crawlDragLoop == null) return;

            bool dragging = player.IsCrouched && player.IsGrounded && player.MoveInput.sqrMagnitude > 0.01f;
            float target = dragging ? crawlDragVolume : 0f;
            dragSource.volume = Mathf.MoveTowards(dragSource.volume, target, Time.deltaTime * crawlDragVolume / crawlDragFadeTime);

            if (dragging && !dragSource.isPlaying)
            {
                dragSource.clip = crawlDragLoop;
                dragSource.pitch = crawlDragPitch;
                dragSource.time = Random.Range(0f, crawlDragLoop.length * 0.9f);
                dragSource.Play();
            }
            else if (!dragging && dragSource.isPlaying && dragSource.volume <= 0f)
            {
                dragSource.Stop();
            }
        }

        void UpdateBreathing()
        {
            int index = Mathf.Clamp(CurrentLoop - 1, 0, breathingByLoop.Length - 1);
            AudioClip target = breathingByLoop.Length > 0 ? breathingByLoop[index] : null;
            float fadeStep = Time.deltaTime * breathingVolume / breathingFadeTime;

            if (breathSource.clip != target)
            {
                breathSource.volume = Mathf.MoveTowards(breathSource.volume, 0f, fadeStep);
                if (breathSource.volume <= 0f)
                {
                    breathSource.clip = target;
                    if (target != null) breathSource.Play();
                    else breathSource.Stop();
                }
                return;
            }

            if (target != null)
                breathSource.volume = Mathf.MoveTowards(breathSource.volume, breathingVolume, fadeStep);
        }

        void UpdateCrouch()
        {
            bool crouched = player.IsCrouched;
            if (crouched != wasCrouched && crouchClips.Length > 0)
                stepSource.PlayOneShot(crouchClips[Random.Range(0, crouchClips.Length)], crouchVolume);
            wasCrouched = crouched;
        }
    }
}
