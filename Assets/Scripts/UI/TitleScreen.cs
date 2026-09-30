using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace CaveGame
{
    // 타이틀: 앞에 떠 있는 "CAVE 13" 비디오테이프 → PLAY(TitleMenu가 Play() 호출)를 누르면 UI가 사라지고 테이프가 뒤의 TV(비디오 데크) 투입구로 들어간다
    // → 화면이 켜지며 지지직 → 카메라가 화면 속으로 파고들고, 캠코더 신호 불량(_CamGlitch)이 가득 찬 채 게임 씬으로 넘어간다.
    // 테이프를 "재생"하는 순간 게임이 시작된다 — 게임 전체가 이 테이프의 녹화분이라는 액자.
    // 엔딩에서 돌아올 때는 역순: 꺼진 화면(문구가 뜬 TV 유리)에서 줌아웃 → 테이프가 뱉어져 원래 자리로 돌아가고 → 메뉴.
    public class TitleScreen : MonoBehaviour
    {
        static readonly int GlitchId = Shader.PropertyToID("_CamGlitch");
        static readonly int SeedId = Shader.PropertyToID("_CamGlitchSeed");
        static readonly int PowerId = Shader.PropertyToID("_Power");

        // 엔딩 씬이 넘어오기 직전에 켠다 — 씬 사이에 넘길 건 이 한 가지뿐이라 별도 매니저를 두지 않는다
        public static bool ReturnFromEnding;
        // 엔딩마다 문구가 다르다 — 돌아올 때 TV 유리에 띄울 문구 (비어 있으면 씬에 적힌 1엔딩 문구)
        public static string EndingCaptionText;

        [SerializeField] string gameScene = "Main";

        [Header("UI")]
        [SerializeField] CanvasGroup ui;
        [SerializeField] float uiFadeTime = 0.5f;

        [Header("테이프")]
        [SerializeField] Transform tape;
        [Tooltip("투입구 입구. 파란 축(forward)이 테이프가 밀려 들어가는 방향, 테이프 회전도 이것에 맞춘다")]
        [SerializeField] Transform slot;
        [Tooltip("투입구 안쪽으로 젖혀지는 덮개 (위쪽 경첩이 피벗)")]
        [SerializeField] Transform slotFlap;
        [SerializeField] float flyTime = 1.6f;
        [Tooltip("날아온 테이프 중심이 멈추는 곳 — 투입구 앞 이 거리 (테이프 깊이의 절반 + 여유)")]
        [SerializeField] float entryGap = 0.075f;
        [Tooltip("다 들어갔을 때 테이프 중심이 투입구 안쪽으로 들어간 거리")]
        [SerializeField] float insertDepth = 0.065f;
        [SerializeField] float insertTime = 0.7f;
        [Tooltip("대기 중 테이프가 둥실거리는 정도")]
        [SerializeField] float idleBob = 0.006f;
        [SerializeField] float idleSway = 2.5f;

        [Header("카메라")]
        [SerializeField] Camera cam;
        [Tooltip("테이프를 따라 TV를 바라보는 중간 자세")]
        [SerializeField] Transform watchPose;
        [Tooltip("화면에 코를 박은 마지막 자세")]
        [SerializeField] Transform screenPose;
        [SerializeField] float zoomTime = 2.2f;
        [SerializeField] float endFov = 30f;

        [Header("TV")]
        [SerializeField] Renderer screen;
        [SerializeField] float powerOnTime = 0.35f;
        [SerializeField] Light screenGlow;
        [SerializeField] float glowIntensity = 1.5f;

        [Header("소리")]
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioSource staticLoop;
        [SerializeField] AudioClip insertClip;
        [SerializeField] AudioClip powerOnClip;
        [SerializeField, Range(0f, 1f)] float staticVolume = 0.6f;

        [Header("엔딩에서 돌아올 때")]
        [Tooltip("TV 유리가 화면을 가득 채우는 자세 — 엔딩의 마지막 검은 화면과 이어진다")]
        [SerializeField] Transform returnPose;
        [SerializeField] float returnFov = 30f;
        [Tooltip("TV 유리 위에 뜬 엔딩 문구")]
        [SerializeField] TMP_Text endingCaption;
        [SerializeField] float returnHold = 1.2f;
        [SerializeField] float zoomOutTime = 3.5f;
        [SerializeField] float ejectTime = 0.8f;
        [SerializeField] AudioClip ejectClip;

        [Header("씬 전환")]
        [Tooltip("게임 씬에 들어간 뒤 지지직이 걷히는 시간")]
        [SerializeField] float carryOverTime = 0.9f;

        Vector3 tapeRestPos;
        Quaternion tapeRestRot;
        Material screenMat;
        bool started;

        // 게임 시작 중이거나 엔딩에서 돌아오는 중 — 메뉴 입력을 막는다
        public bool IsBusy => started;

        void Awake()
        {
            screenMat = screen.material; // 인스턴스 — 에셋 머티리얼을 건드리지 않게
            screenMat.SetFloat(PowerId, 0f);
            if (screenGlow) screenGlow.intensity = 0f;
            tapeRestPos = tape.localPosition;
            tapeRestRot = tape.localRotation;
            Shader.SetGlobalFloat(GlitchId, 0f);
            if (endingCaption) endingCaption.gameObject.SetActive(false);

            if (ReturnFromEnding)
            {
                ReturnFromEnding = false;
                started = true;
                StartCoroutine(ReturnSequence());
            }
        }

        void Update()
        {
            if (started) return;
            float t = Time.time;
            tape.localPosition = tapeRestPos + Vector3.up * Mathf.Sin(t * 0.9f) * idleBob;
            tape.localRotation = tapeRestRot * Quaternion.Euler(Mathf.Sin(t * 0.6f) * idleSway, Mathf.Sin(t * 0.43f) * idleSway, 0f);
        }

        public void Play()
        {
            if (started) return;
            started = true;
            StartCoroutine(PlaySequence());
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator PlaySequence()
        {
            // 씬은 미리 불러 두고, 연출이 끝날 때 넘긴다
            var load = SceneManager.LoadSceneAsync(gameScene);
            load.allowSceneActivation = false;

            // 1) UI 사라짐 + 테이프가 투입구 앞으로 날아감, 카메라는 테이프를 따라 TV 쪽으로
            Vector3 camFromPos = cam.transform.position;
            Quaternion camFromRot = cam.transform.rotation;
            Vector3 fromPos = tape.position;
            Quaternion fromRot = tape.rotation;
            Vector3 entry = slot.position - slot.forward * entryGap;
            // 곧장 가지 않고 살짝 떠올랐다 내려앉는 호 — 손으로 집어 넣는 느낌
            Vector3 control = Vector3.Lerp(fromPos, entry, 0.5f) + Vector3.up * 0.12f;

            for (float e = 0f; e < flyTime; e += Time.deltaTime)
            {
                float k = e / flyTime;
                ui.alpha = 1f - Mathf.Clamp01(e / uiFadeTime);
                float s = Smooth(k);
                tape.position = Bezier(fromPos, control, entry, s);
                tape.rotation = Quaternion.Slerp(fromRot, slot.rotation, Smooth(Mathf.Clamp01(k * 1.3f)));
                cam.transform.SetPositionAndRotation(
                    Vector3.Lerp(camFromPos, watchPose.position, s),
                    Quaternion.Slerp(camFromRot, watchPose.rotation, s));
                yield return null;
            }
            ui.alpha = 0f;

            // 2) 밀어 넣기 — 덮개가 젖혀지고, 데크가 테이프를 빨아들이는 기계음
            PlayClip(insertClip);
            Vector3 inStart = tape.position;
            Vector3 inEnd = slot.position + slot.forward * insertDepth;
            Quaternion flapClosed = slotFlap ? slotFlap.localRotation : Quaternion.identity;
            for (float e = 0f; e < insertTime; e += Time.deltaTime)
            {
                float k = e / insertTime;
                tape.position = Vector3.Lerp(inStart, inEnd, Smooth(k));
                if (slotFlap) slotFlap.localRotation = flapClosed * Quaternion.Euler(Mathf.Clamp01(k * 4f) * -80f, 0f, 0f);
                yield return null;
            }
            tape.gameObject.SetActive(false);
            if (slotFlap) slotFlap.localRotation = flapClosed;
            yield return new WaitForSeconds(0.6f);

            // 3) 화면 켜짐 — 가로줄 번쩍 후 지지직
            PlayClip(powerOnClip);
            staticLoop.volume = 0f;
            staticLoop.Play();
            for (float e = 0f; e < powerOnTime; e += Time.deltaTime)
            {
                float p = e / powerOnTime;
                screenMat.SetFloat(PowerId, p);
                if (screenGlow) screenGlow.intensity = p * glowIntensity;
                staticLoop.volume = p * staticVolume * 0.5f;
                yield return null;
            }
            screenMat.SetFloat(PowerId, 1f);
            yield return new WaitForSeconds(0.5f);

            // 4) 화면 속으로 — 가까워질수록 우리 쪽 화면(캠코더)도 신호가 깨진다
            camFromPos = cam.transform.position;
            camFromRot = cam.transform.rotation;
            float fromFov = cam.fieldOfView;
            Vector3 screenCenter = screenPose.position + screenPose.forward * 0.5f;
            for (float e = 0f; e < zoomTime; e += Time.deltaTime)
            {
                float k = e / zoomTime;
                float s = k * k * k; // 처음엔 천천히, 끝에 빨려 들어가듯
                // 위치는 느리게 따라가도 시선은 먼저 화면 중앙에 꽂힌다 — 화면이 프레임 밖으로 새지 않게
                Vector3 pos = Vector3.Lerp(camFromPos, screenPose.position, s);
                Quaternion look = Quaternion.LookRotation(screenCenter - pos, screenPose.up);
                cam.transform.SetPositionAndRotation(pos, Quaternion.Slerp(camFromRot, look, Smooth(k * 2.5f)));
                cam.fieldOfView = Mathf.Lerp(fromFov, endFov, s);
                SetGlitch(Mathf.Clamp01((k - 0.45f) / 0.55f));
                staticLoop.volume = Mathf.Lerp(staticVolume * 0.5f, staticVolume, k);
                yield return null;
            }

            // 5) 로딩이 덜 됐으면 지지직을 유지한 채 기다린다
            while (load.progress < 0.9f)
            {
                SetGlitch(1f);
                yield return null;
            }

            // 지지직을 게임 씬 첫 몇 프레임까지 끌고 간다 — 넘어가는 순간의 끊김을 가린다
            var carry = new GameObject("TitleStaticCarryOver").AddComponent<StaticCarryOver>();
            carry.Begin(staticLoop, carryOverTime);
            load.allowSceneActivation = true;
        }

        IEnumerator ReturnSequence()
        {
            // 첫 프레임: 테이프는 데크 안, 메뉴는 숨김, 카메라는 꺼진 유리에 붙어 문구만 보인다
            Vector3 camHomePos = cam.transform.position;
            Quaternion camHomeRot = cam.transform.rotation;
            float homeFov = cam.fieldOfView;
            Vector3 tapeHomePos = tape.position;
            Quaternion tapeHomeRot = tape.rotation;

            ui.alpha = 0f;
            tape.gameObject.SetActive(false);
            cam.transform.SetPositionAndRotation(returnPose.position, returnPose.rotation);
            cam.fieldOfView = returnFov;
            if (!string.IsNullOrEmpty(EndingCaptionText)) endingCaption.text = EndingCaptionText;
            EndingCaptionText = null;
            endingCaption.gameObject.SetActive(true);
            endingCaption.alpha = 1f;

            yield return new WaitForSeconds(returnHold);

            // 1) 줌아웃 — 화면 밖으로 천천히 빠져나와 TV 전체가 보인다
            for (float e = 0f; e < zoomOutTime; e += Time.deltaTime)
            {
                float s = Smooth(e / zoomOutTime);
                cam.transform.SetPositionAndRotation(
                    Vector3.Lerp(returnPose.position, camHomePos, s),
                    Quaternion.Slerp(returnPose.rotation, camHomeRot, s));
                cam.fieldOfView = Mathf.Lerp(returnFov, homeFov, s);
                yield return null;
            }
            cam.transform.SetPositionAndRotation(camHomePos, camHomeRot);
            cam.fieldOfView = homeFov;
            yield return new WaitForSeconds(0.5f);

            // 2) 테이프가 뱉어진다 — 덮개가 젖혀지고 밀려 나오는 동안 문구가 꺼진다 (재생이 끝났으니 화면에 남을 것이 없다)
            PlayClip(ejectClip);
            Vector3 inside = slot.position + slot.forward * insertDepth;
            Vector3 entry = slot.position - slot.forward * entryGap;
            Quaternion flapClosed = slotFlap ? slotFlap.localRotation : Quaternion.identity;
            tape.SetPositionAndRotation(inside, slot.rotation);
            tape.gameObject.SetActive(true);
            for (float e = 0f; e < ejectTime; e += Time.deltaTime)
            {
                float k = e / ejectTime;
                tape.position = Vector3.Lerp(inside, entry, Smooth(k));
                if (slotFlap) slotFlap.localRotation = flapClosed * Quaternion.Euler(Mathf.Clamp01((1f - k) * 4f) * -80f, 0f, 0f);
                endingCaption.alpha = 1f - k;
                yield return null;
            }
            if (slotFlap) slotFlap.localRotation = flapClosed;
            endingCaption.gameObject.SetActive(false);

            // 3) 들어올 때의 호를 거꾸로 — 원래 떠 있던 자리로
            Vector3 control = Vector3.Lerp(entry, tapeHomePos, 0.5f) + Vector3.up * 0.12f;
            for (float e = 0f; e < flyTime; e += Time.deltaTime)
            {
                float k = e / flyTime;
                float s = Smooth(k);
                tape.position = Bezier(entry, control, tapeHomePos, s);
                tape.rotation = Quaternion.Slerp(slot.rotation, tapeHomeRot, Smooth(Mathf.Clamp01(k * 1.3f)));
                ui.alpha = Mathf.Clamp01((e - (flyTime - uiFadeTime)) / uiFadeTime); // 테이프가 자리 잡을 즈음 메뉴가 뜬다
                yield return null;
            }
            tape.SetPositionAndRotation(tapeHomePos, tapeHomeRot);
            ui.alpha = 1f;
            started = false; // 이제부터 둥실거림 + 메뉴 입력
        }

        void PlayClip(AudioClip clip)
        {
            if (clip) sfx.PlayOneShot(clip);
        }

        static void SetGlitch(float value)
        {
            Shader.SetGlobalFloat(GlitchId, value);
            Shader.SetGlobalFloat(SeedId, Random.Range(0f, 100f));
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
        }

        // 씬을 넘어가 살아남아, 지지직(화면·소리)을 걷어 낸 뒤 스스로 사라진다
        class StaticCarryOver : MonoBehaviour
        {
            AudioSource source;
            float duration;
            float volume;
            float startTime = -1f;

            public void Begin(AudioSource from, float time)
            {
                DontDestroyOnLoad(gameObject);
                duration = Mathf.Max(0.01f, time);
                source = gameObject.AddComponent<AudioSource>();
                source.clip = from.clip;
                source.loop = true;
                source.volume = volume = from.volume;
                source.spatialBlend = 0f;
                source.time = from.time;
                source.Play();
                SceneManager.sceneLoaded += OnLoaded;
            }

            void OnDestroy() => SceneManager.sceneLoaded -= OnLoaded;

            void OnLoaded(Scene scene, LoadSceneMode mode) => startTime = Time.time;

            // 게임 씬의 CamcorderGlitch가 Awake/Start에서 0으로 돌려놓으므로 매 프레임 덮어쓴다
            void LateUpdate()
            {
                if (startTime < 0f)
                {
                    SetGlitch(1f);
                    return;
                }
                float k = (Time.time - startTime) / duration;
                if (k >= 1f)
                {
                    Shader.SetGlobalFloat(GlitchId, 0f);
                    Destroy(gameObject);
                    return;
                }
                float v = 1f - k;
                // 뚝 끊기지 않고 몇 번 되살아나며 걷힌다
                SetGlitch(Random.value < 0.25f ? 0f : v * v);
                source.volume = volume * v;
            }
        }
    }
}
