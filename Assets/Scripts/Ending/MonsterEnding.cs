using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaveGame
{
    // 숨은 엔딩 "You are Monster" — 메모를 전부 읽은 채 7루프 엔딩 판정(끝선 통과 / 잡힘)에 닿으면 점프스케어 대신 이쪽으로 간다.
    // 판정 전까지는 보통 엔딩과 똑같다. 판정 순간부터:
    //  ① 발이 멈추고, 들고 있던 캠코더를 천천히 내려 바닥에 놓는다 — 화면이 끊기지 않고 1인칭 그대로 바닥까지 내려간다.
    //  ② 바닥의 캠 옆을, 쫓아오던 괴물이 멈추지 않고 앞으로 달려 지나간다 — 캠을 든 "사람"은 쫓던 대상이 아니었다.
    //  ③ 그 뒤로 또 하나가 천천히 걸어와 캠 앞을 지나간다 — 방금까지 캠을 들고 있던 것. 플레이어의 정체다.
    //  ④ 그것이 멀어질수록 화면이 어두워지고, 다 검어지기 직전 모습이 스러진다. 어둠 속 멀리서 1엔딩의 비명·찢김·뼈 소리.
    //  ⑤ 검은 화면에 1엔딩과 같은 글씨·방식으로 "You are Monster" → 타이틀 (TV 유리에 같은 문구를 띄운 채 이어받는다).
    public class MonsterEnding : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] ChaseSequence chase;
        [Tooltip("손전등을 든 손 (플레이어 자식 Hand) — 캠과 함께 바닥에 내려놓아 지나가는 것들을 비춘다")]
        [SerializeField] Transform hand;
        [SerializeField] Light flashlight;
        [SerializeField] CamcorderGlitch glitch;

        [Header("① 캠코더 내려놓기")]
        [SerializeField] float lowerTime = 3.4f;
        [Tooltip("내려가는 도중 이 비율까지는 바닥을 내려다보고, 그 뒤 놓이면서 앞을 향한다")]
        [SerializeField, Range(0.2f, 0.9f)] float lookDownPortion = 0.6f;
        [SerializeField] float lookDownPitch = 58f;
        [Tooltip("바닥에 놓인 캠 — 발 앞 거리 / 바닥 위 높이 (m)")]
        [SerializeField] float restForward = 0.4f;
        [SerializeField] float restHeight = 0.1f;
        [Tooltip("놓인 캠 기울기 (도): x = 위로 들림(음수), z = 옆으로 누움")]
        [SerializeField] Vector3 restTilt = new Vector3(-6f, 0f, 7f);
        [Tooltip("놓인 캠이 바라보는 곳 — 경로 위 이만큼 앞 지점 (m)")]
        [SerializeField] float restLookAhead = 5f;
        [Tooltip("손전등이 놓이는 자리 — 캠 기준 (오른쪽, 위, 앞)")]
        [SerializeField] Vector3 flashlightRestOffset = new Vector3(0.28f, -0.03f, 0.05f);
        [Tooltip("손전등이 비추는 곳 — 경로 위 이만큼 앞 (m), 바닥 위 높이 (m)")]
        [SerializeField] float beamAhead = 6f;
        [SerializeField] float beamHeight = 0.9f;
        [SerializeField] float handTremble = 0.012f;
        [SerializeField] AudioClip setDownClip;
        [SerializeField, Range(0f, 1f)] float setDownVolume = 0.35f;
        [Tooltip("놓인 뒤 아무 일 없이 화면이 머무는 시간")]
        [SerializeField] float restHold = 1.4f;

        [Header("② 쫓던 괴물이 지나쳐 달려간다")]
        [Tooltip("캠 뒤 이 경로 거리(m)에서 달려오기 시작한다 — 발소리가 등 뒤에서 먼저 다가온다")]
        [SerializeField] float runFromBehind = 14f;
        [SerializeField] float runSpeed = 7f;
        [Tooltip("캠 앞 이 거리(m)까지 달려가면 사라진다")]
        [SerializeField] float runAwayDistance = 22f;
        [Tooltip("캠에서 옆으로 비켜 지나가는 간격 (m)")]
        [SerializeField] float passClearance = 0.8f;
        [Tooltip("중심선에서 이 이상 비키지 않는다 — 벽에 박히거나 손전등 빛 밖으로 나가지 않게")]
        [SerializeField] float maxSideOffset = 0.7f;
        [SerializeField] float afterRunPause = 1.8f;

        [Header("③ 뒤에서 걸어오는 것")]
        [SerializeField] GameObject walker;
        [SerializeField] Animator walkerAnimator;
        [SerializeField] string walkState = "Walk";
        [Tooltip("걷기 애니메이션 배속 1일 때 발이 땅을 미는 속도 (m/s, 크기 1 기준)")]
        [SerializeField] float walkerAnimGroundSpeed = 2.1f;
        [SerializeField] float walkSpeed = 1.05f;
        [Tooltip("캠 뒤 이 경로 거리(m)에서 걸어 나온다")]
        [SerializeField] float walkFromBehind = 3f;
        [Tooltip("몸 반폭 (m) — 옆으로 비켜 걸을 때 벽과 이만큼은 띄운다")]
        [SerializeField] float walkerRadius = 0.5f;
        [SerializeField] AudioSource walkerStepSource;
        [SerializeField] AudioClip[] walkerSteps;
        [SerializeField, Range(0f, 1f)] float walkerStepVolume = 0.55f;
        [SerializeField] float[] walkerFootContacts = { 0.22f, 0.72f };

        [Header("④ 어두워짐")]
        [SerializeField] Image blackout;
        [Tooltip("걷는 것이 캠 앞 이 거리(m)일 때 어두워지기 시작해서 / 이 거리에서 거의 검다")]
        [SerializeField] Vector2 darkenDistance = new Vector2(3.5f, 8.5f);
        [Tooltip("모습이 스러지는 어둠 정도 — 이후 완전히 검어진다")]
        [SerializeField, Range(0f, 1f)] float vanishAt = 0.9f;
        [SerializeField] float finalFade = 1.4f;
        [Tooltip("심장 세기 배율 — 내려놓을 때 / 다 어두워졌을 때")]
        [SerializeField] Vector2 heartScale = new Vector2(1.34f, 0.5f);

        [Header("④ 1엔딩 소리 — 음량만 줄여서 (어두워지기 시작할 때부터)")]
        [Tooltip("1엔딩(Ending 씬 EndingSceneDirector.attackCues)과 같은 목록 — 비명·살점·뼈. 클립·시각은 그대로, 음량만 distantVolume배")]
        [SerializeField] EndingSceneDirector.SoundCue[] distantCues;
        [Tooltip("1엔딩 대비 음량")]
        [SerializeField, Range(0f, 1f)] float distantVolume = 0.45f;
        [SerializeField, Min(1)] int distantLayers = 8;
        [SerializeField] float tailWaitMax = 4f;

        [Header("⑤ 문구 — 1엔딩과 같은 글씨·크기·자리·방식")]
        [SerializeField] TMP_Text caption;
        [SerializeField] string captionText = "You are Monster";
        [Tooltip("완전히 검어진 뒤 문구까지")]
        [SerializeField] float blackHold = 1.5f;
        [SerializeField] float captionDelay = 1f;
        [SerializeField] float captionFade = 1.2f;
        [SerializeField] float captionHold = 2f;
        [SerializeField] string nextScene = "Title";

        int totalNotes;
        bool running;
        AudioSource[] layers;
        int nextLayer;
        float heart;

        // 이번 판의 메모를 전부 읽었는가 — 7루프에 들어설 때 LoopManager가 물어 엔딩 씬을 미리 불러 둘지 정한다
        public bool Qualifies => totalNotes > 0 && NoteLog.ReadCount >= totalNotes;
        public int TotalNotes => totalNotes;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            NoteLog.Clear(); // 새 판
            // 루프마다 꺼져 있는 메모까지 센다
            totalNotes = FindObjectsByType<Note>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Debug.Log($"[숨은 엔딩] 메모 총 {totalNotes}장 — 기록 초기화");

            if (walker != null) walker.SetActive(false);
            if (caption != null) { caption.text = captionText; caption.alpha = 0f; }
            SetBlack(0f);

            layers = new AudioSource[distantLayers];
            for (int i = 0; i < distantLayers; i++)
            {
                // 1엔딩 layers와 같은 평범한 2D 소스 — 필터 없이 음량만 다르다
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                layers[i] = src;
            }
        }

        public void Begin()
        {
            if (running) return;
            running = true;
            Debug.Log("[숨은 엔딩] 시작");
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // 판정 순간: 발이 멈춘다. 숨소리는 그대로 — 방금까지 달리던 몸이다
            float camS = chase.PathPosition(player.transform.position);
            player.MovementLocked = true;
            player.enabled = false;
            chase.HandOff();
            if (glitch != null) glitch.Burst(0.12f);
            foreach (var sway in hand.GetComponentsInChildren<FlashlightSway>()) sway.enabled = false;
            heart = heartScale.x;
            StartCoroutine(KeepHeart());

            // ① 캠코더를 내려놓는다
            yield return LowerCamera(camS);
            yield return new WaitForSeconds(restHold);

            // ② 캠 옆을 스쳐 괴물이 앞으로 달려간다 — 캠에는 눈길도 주지 않는다
            Transform cam = player.CameraPivot;
            float side = PassSide(cam.position, camS);
            chase.RunPast(camS - runFromBehind, runSpeed, side, camS + runAwayDistance);
            while (chase.IsRunningPast) yield return null;
            yield return new WaitForSeconds(afterRunPause);

            // ③ 뒤에서 걸어오는 것 — 캠 앞을 지나 괴물이 간 쪽으로 천천히 멀어진다
            yield return WalkAway(camS, side);

            // ⑤ 문구
            float tailEnd = Time.time + tailWaitMax;
            while (AnyLayerPlaying() && Time.time < tailEnd) yield return null;
            yield return new WaitForSeconds(blackHold + captionDelay);
            for (float t = 0f; t < captionFade; t += Time.deltaTime)
            {
                caption.alpha = t / captionFade;
                yield return null;
            }
            caption.alpha = 1f;
            yield return new WaitForSeconds(captionHold);

            // 타이틀은 TV 유리에 같은 문구를 띄운 채 시작해 줌아웃한다 — 1엔딩과 같은 이음새
            TitleScreen.ReturnFromEnding = true;
            TitleScreen.EndingCaptionText = captionText;
            if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
        }

        IEnumerator LowerCamera(float camS)
        {
            Transform cam = player.CameraPivot;
            // 굽은 통로에서도 괴물들이 멀어질 길 쪽을 향하게 — 발밑 방향이 아니라 몇 m 앞의 경로 지점을 본다
            Vector3 heading = chase.PathPoint(camS + restLookAhead) - player.transform.position;
            heading.y = 0f;
            heading = heading.sqrMagnitude > 0.01f ? heading.normalized : chase.PathHeading(camS);
            Vector3 feet = chase.GroundAt(player.transform.position);
            Quaternion face = Quaternion.LookRotation(heading);

            Vector3 fromPos = cam.position;
            Quaternion fromRot = cam.rotation;
            Quaternion downRot = face * Quaternion.Euler(lookDownPitch, 0f, 0f);
            Vector3 restPos = chase.GroundAt(feet + heading * restForward) + Vector3.up * restHeight;
            Quaternion restRot = face * Quaternion.Euler(restTilt);

            Vector3 handFromPos = hand.position;
            Quaternion handFromRot = hand.rotation;
            Vector3 handRestPos = restPos + restRot * new Vector3(flashlightRestOffset.x, 0f, flashlightRestOffset.z) + Vector3.up * flashlightRestOffset.y;
            // 손전등은 통로 한가운데 몇 m 앞을 비춘다 — 지나가는 것들이 빛 속을 지나가게
            Vector3 beamTarget = chase.PathPoint(camS + beamAhead) + Vector3.up * beamHeight;
            Quaternion handRestRot = Quaternion.LookRotation(beamTarget - handRestPos);

            float seed = Random.Range(0f, 100f);
            for (float t = 0f; t < lowerTime; t += Time.deltaTime)
            {
                float u = t / lowerTime;
                float k = Mathf.SmoothStep(0f, 1f, u);
                // 쪼그려 앉듯 처음엔 천천히, 바닥 가까이서 조심스레 — 높이는 부드럽게, 앞뒤는 거의 그대로
                Vector3 pos = Vector3.Lerp(fromPos, restPos, k);
                Quaternion rot = u < lookDownPortion
                    ? Quaternion.Slerp(fromRot, downRot, Mathf.SmoothStep(0f, 1f, u / lookDownPortion))
                    : Quaternion.Slerp(downRot, restRot, Mathf.SmoothStep(0f, 1f, (u - lookDownPortion) / (1f - lookDownPortion)));
                // 손떨림 — 바닥에 닿으며 사라진다
                float shake = handTremble * (1f - k);
                pos += new Vector3(Mathf.PerlinNoise(seed, t * 3f) - 0.5f, Mathf.PerlinNoise(seed + 7f, t * 3f) - 0.5f, 0f) * shake * 2f;
                cam.SetPositionAndRotation(pos, rot);
                hand.SetPositionAndRotation(Vector3.Lerp(handFromPos, handRestPos, k), Quaternion.Slerp(handFromRot, handRestRot, k));
                yield return null;
            }
            cam.SetPositionAndRotation(restPos, restRot);
            hand.SetPositionAndRotation(handRestPos, handRestRot);
            if (setDownClip != null) AudioSource.PlayClipAtPoint(setDownClip, restPos, setDownVolume);
        }

        // 캠이 경로 중심선의 어느 쪽에 놓였는지 보고, 반대쪽으로 비켜 지나가게 한다 (+ = 진행 방향 오른쪽)
        float PassSide(Vector3 camPos, float camS)
        {
            Vector3 heading = chase.PathHeading(camS);
            Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
            Vector3 center = chase.PathPoint(camS);
            float camLateral = Vector3.Dot(camPos - center, right);
            float away = camLateral >= 0f ? -1f : 1f;
            return Mathf.Clamp(camLateral + away * passClearance, -maxSideOffset, maxSideOffset);
        }

        IEnumerator WalkAway(float camS, float side)
        {
            float s = camS - walkFromBehind;
            walker.SetActive(true);
            walkerAnimator.Play(walkState, 0, 0f);
            walkerAnimator.speed = walkSpeed / (walkerAnimGroundSpeed * walker.transform.lossyScale.x);
            float lastCycle = 0f;
            float flashBase = flashlight != null ? flashlight.intensity : 0f;
            bool soundStarted = false;
            float dark = 0f;

            while (dark < 1f)
            {
                s += walkSpeed * Time.deltaTime;
                Vector3 heading = chase.PathHeading(s);
                Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
                walker.transform.SetPositionAndRotation(chase.PointBeside(s, side, walkerRadius), Quaternion.LookRotation(heading));
                lastCycle = WalkerSteps(lastCycle);

                float ahead = s - camS;
                dark = Mathf.InverseLerp(darkenDistance.x, darkenDistance.y, ahead);
                if (!soundStarted && ahead >= darkenDistance.x)
                {
                    soundStarted = true;
                    StartCoroutine(PlayDistantCues());
                    StartCoroutine(FadeAmbience(0.15f, 3f));
                }
                SetBlack(dark * vanishAt);
                if (flashlight != null) flashlight.intensity = flashBase * (1f - dark * 0.7f);
                heart = Mathf.Lerp(heartScale.x, heartScale.y, dark);
                yield return null;
            }

            // 다 검어지기 직전 — 모습이 어둠에 스러진다
            walker.SetActive(false);
            for (float t = 0f; t < finalFade; t += Time.deltaTime)
            {
                SetBlack(Mathf.Lerp(vanishAt, 1f, t / finalFade));
                yield return null;
            }
            SetBlack(1f);
            if (flashlight != null) flashlight.enabled = false;
        }

        float WalkerSteps(float lastCycle)
        {
            float t = walkerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if (walkerStepSource != null && walkerSteps != null && walkerSteps.Length > 0)
            {
                walkerStepSource.transform.position = walker.transform.position + Vector3.up * 0.2f;
                foreach (float c in walkerFootContacts)
                    if (Mathf.Floor(t - c) > Mathf.Floor(lastCycle - c))
                    {
                        walkerStepSource.pitch = Random.Range(0.78f, 0.88f);
                        walkerStepSource.PlayOneShot(walkerSteps[Random.Range(0, walkerSteps.Length)], walkerStepVolume);
                    }
            }
            return t;
        }

        // 1엔딩과 같은 시각·같은 클립 — 음량만 줄이고 먹먹한 울림 너머로
        IEnumerator PlayDistantCues()
        {
            float start = Time.time;
            foreach (var cue in distantCues)
            {
                float wait = start + cue.time - Time.time;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                if (cue.clip == null) continue;
                int pick = nextLayer;
                for (int k = 0; k < layers.Length; k++)
                {
                    int i = (nextLayer + k) % layers.Length;
                    if (!layers[i].isPlaying) { pick = i; break; }
                }
                nextLayer = (pick + 1) % layers.Length;
                layers[pick].clip = cue.clip;
                layers[pick].volume = cue.volume * distantVolume;
                layers[pick].Play();
            }
        }

        bool AnyLayerPlaying()
        {
            foreach (var src in layers) if (src.isPlaying) return true;
            return false;
        }

        IEnumerator KeepHeart()
        {
            while (true)
            {
                Heartbeat.Instance?.Raise(0.5f, heart);
                yield return null;
            }
        }

        static IEnumerator FadeAmbience(float to, float time)
        {
            float from = AmbienceDuck.Level;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                AmbienceDuck.Level = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            AmbienceDuck.Level = to;
        }

        void SetBlack(float alpha)
        {
            if (blackout == null) return;
            var c = blackout.color;
            c.a = alpha;
            blackout.color = c;
        }
    }
}
