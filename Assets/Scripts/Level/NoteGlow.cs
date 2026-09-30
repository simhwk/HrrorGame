using UnityEngine;

namespace CaveGame
{
    // 바닥 메모 테두리가 은은하게 빛나게 — 어두운 동굴에서 손전등이 스치기 전에도 "거기 뭔가 있다"가 보이도록.
    // 메모 모델들의 크기를 재서 그보다 조금 큰 평평한 사각형을 메모 바로 밑에 깔고, NoteGlow 셰이더로 가장자리 빛만 그린다.
    // 메모 오브젝트(Note 루트)에 붙인다. 모델은 바닥에 눕혀 둔 상태여야 한다.
    public class NoteGlow : MonoBehaviour
    {
        [SerializeField] Material material; // CaveGame/NoteGlow
        [Tooltip("메모 가장자리에서 빛이 번지는 폭 (m)")]
        [SerializeField] float margin = 0.06f;
        [Tooltip("바닥에서 띄우는 높이 (m) — 바닥과 겹쳐 깜빡이지 않게")]
        [SerializeField] float lift = 0.004f;

        static readonly int RectId = Shader.PropertyToID("_GlowRect");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static Mesh quad;

        void Start()
        {
            if (material == null) return;

            // 메모 모델들(자식 렌더러)을 이 오브젝트 기준 좌표로 잰다 — 돌려 놓은 메모도 딱 맞게
            bool any = false;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                any = true;
            }
            if (!any) return;

            Vector2 note = new Vector2(max.x - min.x, max.z - min.z);
            Vector2 size = note + Vector2.one * margin * 2f;

            var go = new GameObject("NoteGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3((min.x + max.x) * 0.5f, min.y + lift, (min.z + max.z) * 0.5f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var block = new MaterialPropertyBlock();
            block.SetVector(RectId, new Vector4(size.x, size.y, note.x, note.y));
            block.SetFloat(PhaseId, Random.value * 6.28f);
            mr.SetPropertyBlock(block);
        }

        // 1x1, 가운데 원점, +Z를 보는 사각형 — 90도 눕히면 위를 본다
        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "NoteGlowQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateBounds();
            return quad;
        }
    }
}
