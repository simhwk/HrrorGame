using System.Collections.Generic;
using UnityEngine;

namespace CaveGame
{
    // 플레이어가 조준해서 E로 만질 수 있는 표식(가이드라인 등). 콜라이더가 이 오브젝트나 자식에 있어야 한다.
    // 조준 중엔 은은하게 숨쉬듯 빛나고(어둠 속 위치 파악용), 만질 때마다 강하게 번쩍인 뒤 사라진다.
    public class TouchableMarker : MonoBehaviour
    {
        [Header("조준 중 발광")]
        [SerializeField] Color glowColor = new Color(0.55f, 0.8f, 1f);
        [SerializeField] float hoverIntensity = 0.5f;
        [SerializeField] float hoverFadeSpeed = 4f;

        [Header("터치 순간 번쩍임")]
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

        public bool Touched { get; private set; }

        void Awake()
        {
            CreateMaterialInstances();
            CreatePulseLight();
        }

        public void SetHover(bool on) => hoverTarget = on ? 1f : 0f;

        public void Touch(Vector3 worldPoint)
        {
            Touched = true;
            pulse = 1f;
            pulseLight.transform.position = worldPoint;
        }

        public void ResetTouch() => Touched = false;

        void Update()
        {
            hover = Mathf.MoveTowards(hover, hoverTarget, Time.deltaTime * hoverFadeSpeed);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime / pulseDuration);

            float breathe = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            float p = pulse * pulse; // ease-out: 처음에 확 빛나고 천천히 꺼짐
            ApplyEmission(Mathf.Max(hover * hoverIntensity * breathe, p * pulseIntensity));

            pulseLight.enabled = p > 0.001f;
            pulseLight.intensity = p * lightIntensity;
        }

        void OnDisable()
        {
            hover = hoverTarget = pulse = 0f;
            if (materials != null) ApplyEmission(0f);
            if (pulseLight != null) pulseLight.enabled = false;
        }

        void OnDestroy()
        {
            if (materials == null) return;
            foreach (var m in materials) Destroy(m);
        }

        // renderer.materials는 인스턴스를 만든다 — 같은 머티리얼을 쓰는 다른 오브젝트까지 빛나지 않도록
        void CreateMaterialInstances()
        {
            var list = new List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                list.AddRange(r.materials);
            materials = list.ToArray();

            baseEmission = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i];
                baseEmission[i] = m.HasProperty(EmissionColorId) ? m.GetColor(EmissionColorId) : Color.black;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        // emission은 자기 자신만 밝힐 뿐 주변을 비추지 않으므로, 주변 벽을 밝힐 짧은 포인트 라이트를 둔다
        void CreatePulseLight()
        {
            var go = new GameObject("PulseLight");
            go.transform.SetParent(transform, false);
            pulseLight = go.AddComponent<Light>();
            pulseLight.type = LightType.Point;
            pulseLight.color = glowColor;
            pulseLight.range = lightRange;
            pulseLight.shadows = LightShadows.None;
            pulseLight.enabled = false;
        }

        void ApplyEmission(float intensity)
        {
            for (int i = 0; i < materials.Length; i++)
                materials[i].SetColor(EmissionColorId, baseEmission[i] + glowColor * intensity);
        }
    }
}
