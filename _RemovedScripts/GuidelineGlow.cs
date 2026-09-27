using UnityEngine;

namespace CaveGame
{
    // 가이드라인 시각 오브젝트에 붙는 발광 효과.
    // 조준 중엔 은은하게 숨쉬듯 빛나고(어둠 속에서 위치 파악용), 확인 순간엔 강하게 번쩍인 뒤 사라진다.
    public class GuidelineGlow : MonoBehaviour
    {
        [SerializeField] Color glowColor = new Color(0.55f, 0.8f, 1f);
        [SerializeField] float hoverIntensity = 0.5f;
        [SerializeField] float hoverFadeSpeed = 4f;
        [SerializeField] float pulseIntensity = 4f;
        [SerializeField] float pulseDuration = 1.2f;
        [SerializeField] float lightRange = 2.5f;
        [SerializeField] float lightIntensity = 1.5f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Material[] materials;
        Color[] baseEmission;
        Light pulseLight;
        float hover;
        float hoverTarget;
        float pulse;

        void Awake()
        {
            var list = new System.Collections.Generic.List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                list.AddRange(r.materials); // 인스턴스 생성 — 공유 머티리얼 에셋은 건드리지 않는다
            materials = list.ToArray();

            baseEmission = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i];
                baseEmission[i] = m.HasProperty(EmissionColorId) ? m.GetColor(EmissionColorId) : Color.black;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            var lightGo = new GameObject("GuidelinePulseLight");
            lightGo.transform.SetParent(transform, false);
            pulseLight = lightGo.AddComponent<Light>();
            pulseLight.type = LightType.Point;
            pulseLight.color = glowColor;
            pulseLight.range = lightRange;
            pulseLight.shadows = LightShadows.None;
            pulseLight.enabled = false;
        }

        public void SetHover(bool on) => hoverTarget = on ? 1f : 0f;

        public void Pulse(Vector3 worldPoint)
        {
            pulse = 1f;
            pulseLight.transform.position = worldPoint;
        }

        void Update()
        {
            hover = Mathf.MoveTowards(hover, hoverTarget, Time.deltaTime * hoverFadeSpeed);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime / pulseDuration);

            float breathe = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            float p = pulse * pulse; // ease-out: 처음에 확 빛나고 천천히 꺼짐
            float intensity = Mathf.Max(hover * hoverIntensity * breathe, p * pulseIntensity);

            Apply(intensity);
            pulseLight.enabled = p > 0.001f;
            pulseLight.intensity = p * lightIntensity;
        }

        void Apply(float intensity)
        {
            for (int i = 0; i < materials.Length; i++)
                materials[i].SetColor(EmissionColorId, baseEmission[i] + glowColor * intensity);
        }

        void OnDisable()
        {
            hover = hoverTarget = pulse = 0f;
            if (materials != null) Apply(0f);
            if (pulseLight != null) pulseLight.enabled = false;
        }

        void OnDestroy()
        {
            if (materials == null) return;
            foreach (var m in materials) Destroy(m);
        }
    }
}
