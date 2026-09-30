using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

namespace CaveGame
{
    // 엔딩 씬 전체 흐름. 7루프 끝선을 넘는 순간 괴물 얼굴 클로즈업 한 컷 + 포효, 곧바로 신호가 몇 초간 크게 지직거리다 끊긴다.
    // 공격 자체는 보여주지 않는다 — 검은 화면 속 비명 두 번, 찢기는 소리, 뼈 씹는 소리만으로 전달하고, 소리가 다 끝나는 순간 캠이 떨어진다.
    // 신호를 되찾은 캠은 흑백으로 뭉개진 화질로 바닥에서 출구 쪽을 비스듬히 찍고 있다. 빛 쪽을 보고 선 괴물의 실루엣이 운다.
    // 마지막엔 테이프 재생이 끝난 TV처럼 치직거리다 브라운관이 꺼지고, 꺼진 화면에 문구가 뜬 채 타이틀로 돌아간다 (타이틀이 역순으로 이어받는다).
    public class EndingSceneDirector : MonoBehaviour
    {
        // 검은 화면에서 정해진 시각에 나는 소리 한 줄
        [System.Serializable]
        public struct SoundCue
        {
            [Tooltip("첫 비명 기준 몇 초 뒤")]
            public float time;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        static readonly int GlitchId = Shader.PropertyToID("_CamGlitch");
        static readonly int SeedId = Shader.PropertyToID("_CamGlitchSeed");

        [Header("화면")]
        [SerializeField] Image blackout;
        [SerializeField] Transform cam;
        [Tooltip("바닥에 떨어진 뒤 캠의 최종 자세")]
        [SerializeField] Transform restPose;
        [SerializeField] Volume exposureVolume;
        [SerializeField] GameObject camcorderOverlay;

        [Header("점프스케어")]
        [SerializeField] Transform jumpscarePose;
        [Tooltip("얼굴이 화면을 채우는 자리에 서 있는 괴물 — 달려오지 않고 한 컷만 보여준다")]
        [SerializeField] Transform creature;
        [SerializeField] Animator creatureAnimator;
        [Tooltip("멈춰 둘 동작 프레임 (0~1) — 얼굴이 가장 잘 잡히는 순간")]
        [SerializeField, Range(0f, 1f)] float faceFrame = 0.3f;
        [SerializeField] AudioClip jumpscareRoar;
        [SerializeField] float jumpscareTime = 0.45f;
        [SerializeField] float jumpscareShake = 0.02f;

        [Header("점프스케어 뒤 지직거림")]
        [SerializeField] AudioSource staticLoop;
        [SerializeField, Range(0f, 1f)] float staticVolume = 0.85f;
        [Tooltip("크게 지직거리는 시간 — 끝나면 뚝 끊기고 조용한 검은 화면에서 공격 소리가 시작된다")]
        [SerializeField] float staticTime = 2.5f;

        [Header("공격 (검은 화면)")]
        [SerializeField] AudioSource voice;       // 2D — 캠코더 마이크에 들어온 소리
        [Tooltip("비명 → 찢김 → 뼈 씹기를 정해진 순서로. 매번 똑같이 난다 — 마음에 안 드는 줄은 지우거나 클립만 바꾸면 된다 (시간순 정렬)")]
        [SerializeField] SoundCue[] attackCues;
        [SerializeField] AudioClip cry;

        [Header("캠 낙하")]
        [SerializeField] AudioClip dropThud;
        [SerializeField] AudioClip staticNoise;

        [Header("빛 쪽")]
        [SerializeField] AudioSource outsideAmbience; // 출구 쪽 3D 바람 — 정적 이후에만

        [Header("타이밍 (초)")]
        [Tooltip("지직거림이 끊긴 뒤 비명까지 — 잠깐의 정적이 있어야 소리가 먹히지 않는다")]
        [SerializeField] float openingBlack = 0.8f;
        [Tooltip("소리 꼬리가 다 끝나길 기다리는 최대 시간 — 끝나는 즉시 캠이 떨어진다")]
        [SerializeField] float tailWaitMax = 3f;
        [Tooltip("겹쳐 울리는 소리를 나눠 트는 소스들 (돌아가며 쓴다)")]
        [SerializeField] AudioSource[] layers;
        [SerializeField] float cryTail = 0.6f;
        [SerializeField] float dropFall = 0.35f;
        [SerializeField] float signalReturn = 0.9f;
        [Tooltip("자동 노출: 처음엔 빛에 하얗게 날아갔다가 이 시간에 걸쳐 잡힌다")]
        [SerializeField] float exposureSettle = 4f;
        [SerializeField] float startExposure = 2.5f;
        [Tooltip("울음이 끝난 뒤 동굴을 더 보여주는 시간")]
        [SerializeField] float holdOnLight = 5f;
        [SerializeField] string nextScene = "Title";

        [Header("동굴 장면 — 망가진 화질 + 역광 괴물")]
        [Tooltip("출구 빛 쪽을 보고 선 괴물 — 캠에는 검은 윤곽만 보인다")]
        [SerializeField, FormerlySerializedAs("eatingCreature")] GameObject backlitCreature;
        [Tooltip("메인 카메라를 이 저해상도 텍스처에 그려 화면에 늘려 붙인다 — VHS처럼 뭉개진 화질")]
        [SerializeField] RenderTexture lowFiTarget;
        [SerializeField] RawImage lowFiScreen;
        [Tooltip("동굴 장면 내내 깔리는 약한 신호 떨림")]
        [SerializeField, Range(0f, 1f)] float lowFiGlitch = 0.07f;
        [Tooltip("오래된 흑백 사진처럼 — 색을 빼고 대비를 올린다")]
        [SerializeField] float lowFiSaturation = -100f;
        [SerializeField] float lowFiContrast = 15f;
        [Tooltip("떨어진 캠은 줌이 걸린 채다 — 출구와 그 앞의 형체가 크게 잡히게")]
        [SerializeField] float lowFiFov = 32f;

        [Header("끝 — TV 꺼짐")]
        [Tooltip("메인 카메라 뒤에서 화면 전체를 검게 지우는 카메라 — 메인 화면을 가로줄로 눌러도 바깥이 지저분하지 않게")]
        [SerializeField] Camera backdrop;
        [Tooltip("꺼질 때 눌린 영상 위에 겹치는 흰 선 — 어두운 영상을 눌러 봤자 어두운 줄이라, 브라운관의 하얀 잔광은 따로 그린다")]
        [SerializeField] Image crtLine;
        [SerializeField] TMP_Text endCaption;
        [SerializeField] AudioClip powerOffClip;
        [SerializeField] float endStatic = 1.4f;
        [Tooltip("위아래로 눌려 가로줄이 되는 시간")]
        [SerializeField] float collapseTime = 0.22f;
        [Tooltip("가로줄이 가운데 점으로 줄어드는 시간")]
        [SerializeField] float shrinkTime = 0.16f;
        [SerializeField] float dotLinger = 0.25f;
        [SerializeField] float captionDelay = 1f;
        [SerializeField] float captionFade = 1.2f;
        [SerializeField] float captionHold = 2f;

        ColorAdjustments colorAdjust;
        int cullingMask;
        int nextLayer;

        void Awake()
        {
            SetBlack(1f);
            SetGlitch(0f);
            if (camcorderOverlay != null) camcorderOverlay.SetActive(false);
            if (outsideAmbience != null) outsideAmbience.volume = 0f;

            if (exposureVolume != null) exposureVolume.profile.TryGet(out colorAdjust);
            if (endCaption != null) endCaption.alpha = 0f;
            if (backdrop != null) backdrop.enabled = false;
            if (crtLine != null) crtLine.enabled = false;
            if (creature != null) creature.gameObject.SetActive(false);
            if (backlitCreature != null) backlitCreature.SetActive(false);
            if (lowFiScreen != null) lowFiScreen.enabled = false;
        }

        void Start() => StartCoroutine(Sequence());

        void OnDisable() => SetGlitch(0f);

        IEnumerator Sequence()
        {
            // 0. 점프스케어 한 컷 → 몇 초간 크게 지직 → 뚝 끊기고 조용한 검은 화면
            yield return Jumpscare();
            yield return LoudStatic();
            yield return new WaitForSeconds(openingBlack);

            // 1. 공격 — 검은 화면에서 비명 두 번, 찢기는 소리, 뼈 씹는 소리. 꼬리까지 다 끝나야 화면이 돌아온다
            yield return PlayCues(attackCues);
            float tailEnd = Time.time + tailWaitMax;
            while (AnyLayerPlaying() && Time.time < tailEnd) yield return null;

            // 2. 캠이 떨어진다: 쿵 + 신호 끊김, 그 사이 화면이 돌아오면 흑백으로 망가져 있고 빛 쪽에 괴물이 서 있다
            backlitCreature.SetActive(true);
            SetLowFi(true);
            yield return DropCamera();
            var vhs = StartCoroutine(VhsJitter());

            // 3. 울음 → 조금 더 지켜본다
            voice.PlayOneShot(cry);
            yield return new WaitForSeconds(cry.length + cryTail);
            yield return new WaitForSeconds(holdOnLight);
            StopCoroutine(vhs);

            // 4. 테이프가 끝난다 — 치직거리다 브라운관이 꺼진다
            yield return EndStatic();
            yield return PowerOff();

            // 5. 꺼진 화면에 문구
            yield return new WaitForSeconds(captionDelay);
            for (float t = 0f; t < captionFade; t += Time.deltaTime)
            {
                endCaption.alpha = t / captionFade;
                yield return null;
            }
            endCaption.alpha = 1f;
            yield return new WaitForSeconds(captionHold);

            // 타이틀은 TV 유리에 같은 문구를 띄운 채 화면에 코를 박은 자세로 시작해서 줌아웃한다 — 여기의 마지막 프레임과 이어진다
            TitleScreen.ReturnFromEnding = true;
            if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
        }

        IEnumerator Jumpscare()
        {
            cam.SetPositionAndRotation(jumpscarePose.position, jumpscarePose.rotation);
            creature.gameObject.SetActive(true);
            // 동작을 한 프레임에 멈춘다 — 움직임 없이 얼굴만 정면으로 박힌다
            creatureAnimator.Play(0, 0, faceFrame);
            creatureAnimator.Update(0f);
            creatureAnimator.speed = 0f;
            if (camcorderOverlay != null) camcorderOverlay.SetActive(true); // 녹화 화면 그대로 — 게임 화면에서 이어진다
            SetBlack(0f);
            voice.PlayOneShot(jumpscareRoar);

            Vector3 camPos = jumpscarePose.position;
            for (float t = 0f; t < jumpscareTime; t += Time.deltaTime)
            {
                cam.position = camPos + Random.insideUnitSphere * jumpscareShake; // 놀라 손이 떨린다
                yield return null;
            }
            cam.position = camPos;
            creature.gameObject.SetActive(false);
        }

        // 신호가 터진다: 화면 전체가 찢어지고 치직 소리가 크게 — 얼굴 뒤에 무슨 일이 있었는지는 플레이어 상상에 맡긴다
        IEnumerator LoudStatic()
        {
            var c = cam.GetComponent<Camera>();
            cullingMask = c.cullingMask;
            c.cullingMask = 0; // 영상은 사라지고 신호 노이즈만
            staticLoop.volume = staticVolume;
            staticLoop.Play();

            float start = Time.time;
            for (float t = 0f; t < staticTime; t = Time.time - start)
            {
                // 첫 순간엔 HUD가 깨지며 남아 있다가 꺼진다
                if (camcorderOverlay != null && t > 0.25f) camcorderOverlay.SetActive(false);
                float g = Random.Range(0.6f, 1f);
                SetGlitch(g);
                Shader.SetGlobalFloat(SeedId, Random.Range(0f, 100f));
                staticLoop.volume = staticVolume * Random.Range(0.75f, 1f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
            }

            // 뚝 — 소리까지 한 번에 끊긴다
            staticLoop.Stop();
            voice.Stop();
            SetGlitch(0f);
            SetBlack(1f);
            if (camcorderOverlay != null) camcorderOverlay.SetActive(false);
            c.cullingMask = cullingMask;
        }

        IEnumerator PlayCues(SoundCue[] cues)
        {
            float start = Time.time;
            foreach (var cue in cues)
            {
                float wait = start + cue.time - Time.time;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                Layer(cue.clip, cue.volume);
            }
        }

        // 소스를 돌아가며 써서 앞 소리의 꼬리를 자르지 않고 겹친다
        void Layer(AudioClip clip, float volume)
        {
            if (clip == null || layers.Length == 0) return;
            // 쉬고 있는 소스부터 — 길게 깔린 찢김 바닥이 뒤 소리에 잘리지 않게. 다 바쁘면 차례대로
            int pick = nextLayer;
            for (int k = 0; k < layers.Length; k++)
            {
                int i = (nextLayer + k) % layers.Length;
                if (!layers[i].isPlaying) { pick = i; break; }
            }
            var src = layers[pick];
            nextLayer = (pick + 1) % layers.Length;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }

        bool AnyLayerPlaying()
        {
            foreach (var src in layers) if (src.isPlaying) return true;
            return false;
        }

        // 떨어진 충격으로 캠이 망가졌다: 저해상도로 뭉개지고 신호가 계속 미세하게 떨린다
        void SetLowFi(bool on)
        {
            var c = cam.GetComponent<Camera>();
            c.targetTexture = on ? lowFiTarget : null;
            if (on) c.fieldOfView = lowFiFov;
            lowFiScreen.enabled = on;
            if (backdrop != null) backdrop.enabled = on; // 메인 카메라가 화면에 안 그리는 동안 화면을 지워 줄 카메라
            if (colorAdjust != null)
            {
                colorAdjust.saturation.overrideState = true;
                colorAdjust.saturation.value = on ? lowFiSaturation : 0f;
                colorAdjust.contrast.value = on ? lowFiContrast : 0f;
            }
        }

        IEnumerator VhsJitter()
        {
            while (true)
            {
                SetGlitch(Random.value < 0.06f ? Random.Range(0.2f, 0.4f) : lowFiGlitch);
                Shader.SetGlobalFloat(SeedId, Random.Range(0f, 100f));
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
            }
        }

        IEnumerator EndStatic()
        {
            voice.PlayOneShot(staticNoise);
            // 조각마다 WaitForSeconds로 쉬므로 경과 시간은 시작 시각 기준으로 잰다 (deltaTime을 더하면 한 프레임치만 쌓인다)
            float start = Time.time;
            for (float t = 0f; t < endStatic; t = Time.time - start)
            {
                float k = t / endStatic;
                // 처음엔 드문드문, 끝으로 갈수록 화면을 다 덮는다
                bool on = Random.value < Mathf.Lerp(0.35f, 1f, k);
                SetGlitch(on ? Mathf.Lerp(0.4f, 1f, k) * Random.Range(0.7f, 1f) : 0f);
                Shader.SetGlobalFloat(SeedId, Random.Range(0f, 100f));
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
            }
            SetGlitch(0f);
        }

        // 브라운관 꺼짐: 영상 자체가 위아래로 눌려 밝은 가로줄이 되고, 그 줄이 가운데 점으로 줄었다 사라진다.
        // 마스크로 가리는 게 아니라 카메라 viewport를 줄여서 영상이 실제로 찌그러진다.
        IEnumerator PowerOff()
        {
            var c = cam.GetComponent<Camera>();
            c.aspect = c.aspect; // 화면비를 고정해야 rect를 줄일 때 영상이 "작아지지" 않고 "눌린다"
            if (backdrop != null) backdrop.enabled = true;
            crtLine.enabled = true;
            if (camcorderOverlay != null) camcorderOverlay.SetActive(false); // 녹화 표시는 영상이 아니라 뷰파인더 몫
            voice.Stop();
            if (outsideAmbience != null) outsideAmbience.Stop();
            voice.PlayOneShot(powerOffClip);

            for (float t = 0f; t < collapseTime; t += Time.deltaTime)
            {
                float k = t / collapseTime;
                float h = Mathf.Lerp(1f, 0.004f, k * k);
                SetPicture(c, 1f, h);
                SetLine(1f, h, Mathf.InverseLerp(0.1f, 0.01f, h)); // 거의 선이 됐을 때만 하얗게 타오른다 — 두꺼울 때 덮으면 회색 판이 된다
                ApplyExposure(Mathf.Lerp(0f, 4f, k));
                yield return null;
            }
            for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
            {
                float k = t / shrinkTime;
                float w = Mathf.Lerp(1f, 0.004f, k * k);
                SetPicture(c, w, 0.004f);
                SetLine(w, 0.004f, 1f);
                yield return null;
            }

            // 가운데 남은 점이 잔광처럼 사그라든다
            c.enabled = false;
            lowFiScreen.enabled = false;
            SetBlack(1f);
            for (float t = 0f; t < dotLinger; t += Time.deltaTime)
            {
                SetLine(0.004f, 0.004f, 1f - t / dotLinger);
                yield return null;
            }
            crtLine.enabled = false;
        }

        void SetLine(float width, float height, float alpha)
        {
            var rt = crtLine.rectTransform;
            rt.anchorMin = new Vector2(0.5f - width * 0.5f, 0.5f - height * 0.5f);
            rt.anchorMax = new Vector2(0.5f + width * 0.5f, 0.5f + height * 0.5f);
            var col = crtLine.color;
            col.a = alpha;
            crtLine.color = col;
        }

        // 영상을 가운데로 누른다. 저화질 화면이 떠 있으면 그 화면을, 아니면 카메라 viewport를 누른다
        void SetPicture(Camera c, float width, float height)
        {
            if (lowFiScreen.enabled)
            {
                var rt = lowFiScreen.rectTransform;
                rt.anchorMin = new Vector2(0.5f - width * 0.5f, 0.5f - height * 0.5f);
                rt.anchorMax = new Vector2(0.5f + width * 0.5f, 0.5f + height * 0.5f);
            }
            else
            {
                c.rect = new Rect(0.5f - width * 0.5f, 0.5f - height * 0.5f, width, height);
            }
        }

        IEnumerator DropCamera()
        {
            // 쿵 직전까지 약간 높은 곳에서 떨어지는 중 — 화면은 아직 검다
            Vector3 fallStart = restPose.position + Vector3.up * 0.6f;
            Quaternion fallStartRot = restPose.rotation * Quaternion.Euler(-35f, 10f, -40f);
            cam.SetPositionAndRotation(fallStart, fallStartRot);

            for (float t = 0f; t < dropFall; t += Time.deltaTime)
            {
                float k = t / dropFall;
                cam.SetPositionAndRotation(Vector3.Lerp(fallStart, restPose.position, k * k),
                                           Quaternion.Slerp(fallStartRot, restPose.rotation, k));
                yield return null;
            }

            voice.PlayOneShot(dropThud);
            voice.PlayOneShot(staticNoise, 0.5f);
            if (camcorderOverlay != null) camcorderOverlay.SetActive(true);
            if (outsideAmbience != null) StartCoroutine(FadeAudio(outsideAmbience, 0.5f, exposureSettle));
            StartCoroutine(AutoExposure());

            // 신호 복귀: 끊김 조각들 사이로 화면이 보였다 말았다 하다가 안정되고, 캠은 한 번 튕겨서 자리 잡는다
            Vector3 restPos = restPose.position;
            Quaternion restRot = restPose.rotation;
            float start = Time.time;
            for (float t = 0f; t < signalReturn; t = Time.time - start)
            {
                float k = t / signalReturn;
                float bounce = Mathf.Sin(k * Mathf.PI * 3f) * (1f - k) * (1f - k);
                cam.SetPositionAndRotation(restPos + Vector3.up * Mathf.Abs(bounce) * 0.05f,
                                           restRot * Quaternion.Euler(bounce * 6f, 0f, bounce * 9f));

                bool visible = Random.value < Mathf.Lerp(0.3f, 0.95f, k);
                SetBlack(visible ? 0f : 1f);
                SetGlitch(Mathf.Lerp(1f, 0.15f, k) * Random.Range(0.6f, 1f));
                Shader.SetGlobalFloat(SeedId, Random.Range(0f, 100f));
                yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
            }

            cam.SetPositionAndRotation(restPos, restRot);
            SetBlack(0f);
            SetGlitch(0f);
        }

        IEnumerator AutoExposure()
        {
            for (float t = 0f; t < exposureSettle; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / exposureSettle, 3f); // 빠르게 잡혔다가 천천히 안착
                ApplyExposure(Mathf.Lerp(startExposure, 0f, k));
                yield return null;
            }
            ApplyExposure(0f);
        }

        void ApplyExposure(float ev)
        {
            if (colorAdjust != null) colorAdjust.postExposure.value = ev;
        }

        static IEnumerator FadeAudio(AudioSource source, float target, float duration)
        {
            if (!source.isPlaying) source.Play();
            float from = source.volume;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                source.volume = Mathf.Lerp(from, target, t / duration);
                yield return null;
            }
            source.volume = target;
        }

        void SetBlack(float alpha)
        {
            if (blackout == null) return;
            var c = blackout.color;
            c.a = alpha;
            blackout.color = c;
        }

        static void SetGlitch(float value) => Shader.SetGlobalFloat(GlitchId, value);
    }
}
