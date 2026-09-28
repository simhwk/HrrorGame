using UnityEngine;

namespace CaveGame
{
    // 보이지 않는 가이드라인. 화면엔 아무것도 그리지 않고, 손이 줄을 스치는 소리로만 존재한다.
    //  1) 경로(path)는 T자 갈림길 조금 전 직선 구간에서 시작해 갈림길 중앙을 지나 정답 갈래 안쪽에서 끝난다.
    //  2) 플레이어의 보이지 않는 손(PlayerHand — 손전등 빛 한가운데로 뻗은 선분)이 줄을 "가로질러 지나가거나"
    //     줄 둘레(brushRadius) 안으로 새로 들어오는 순간 한 번 소리가 난다.
    //     빛을 줄 위에 가만히 대고 있으면 소리가 나지 않는다 — 줄을 찾으려면 손전등을 흔들어 훑어야 한다.
    //     앞 소리가 끝나고 strokeCooldown이 지나야 다음 소리 — 연달아 붙어 들리지 않는다.
    //     줄은 정답 갈래로 꺾이므로, 훑으며 따라가다 보면 시선이 그쪽으로 돌아간다.
    // 루프별 버전은 MarkerVariant로 켜고 끈다.
    public class GuideRope : MonoBehaviour
    {
        [Header("경로 — 순서대로: 직선 구간 시작 → … → 정답 갈래 안쪽")]
        [SerializeField] Transform[] path;

        [Header("스침 판정 — 손 선분(손전등 빛 중심)의 길이는 플레이어의 PlayerHand에서 조정")]
        [Tooltip("빛과 줄이 이 각도의 사인값보다 나란하면 판정하지 않는다 (나란히 지나갈 때 가짜 스침 방지)")]
        [SerializeField, Range(0.05f, 0.5f)] float minCrossSine = 0.2f;
        [Tooltip("줄의 굵기(반지름). 빛이 이 안으로 새로 들어와도 스친 것으로 본다 — 손전등이 눈 옆에 달려 있어 " +
                 "줄과 나란히 볼 때 빛이 줄을 '가로지르지' 못하고 옆으로 지나가는 경우를 잡는다")]
        [SerializeField] float brushRadius = 0.3f;
        [Tooltip("한 프레임에 이보다 멀리 넘어가면 훑은 게 아니라 순간이동으로 본다")]
        [SerializeField] float maxSweepPerFrame = 1.5f;

        [Header("스치는 소리 — 한 번 넘어갈 때 한 번")]
        [SerializeField] AudioClip[] strokeClips;
        [SerializeField, Range(0f, 1f)] float strokeVolume = 1f;
        [Tooltip("빛이 줄을 이 속도(m/s)로 넘어가면 최대 볼륨 — 천천히 훑으면 작게")]
        [SerializeField] float strokeFullSpeed = 2f;
        [SerializeField, Range(0f, 1f)] float strokeMinVolume = 0.5f;
        [SerializeField] Vector2 strokePitch = new Vector2(0.92f, 1.06f);
        [Tooltip("한 번 스친 뒤 이 시간(초)이 지나고, 앞 소리도 끝나야 다음 소리 — 연달아 나지 않게")]
        [SerializeField] float strokeCooldown = 0.6f;

        PlayerHand hand;
        AudioSource source;
        float[] lastSide; // 줄 구간마다: 빛이 줄의 어느 쪽에 있었나 (부호 있는 거리, NaN = 모름)
        bool[] inside;    // 줄 구간마다: 빛이 줄 둘레 안에 있었나
        Vector3 lastStart, lastDir; // 지난 프레임의 빛 선분 — 넘어간 "그 순간"의 빛 위치를 보간하는 데 쓴다
        float touchFlash; // 기즈모가 잠깐 노랗게 보이는 시간
        int lastClip = -1;
        float lastStrokeTime = float.NegativeInfinity;

        void Awake()
        {
            var player = FindFirstObjectByType<PlayerController>();
            hand = player != null ? player.GetComponent<PlayerHand>() : null;

            var go = new GameObject("RopeHand");
            go.transform.SetParent(transform, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f; // 손이 닿는 거리(팔 길이) 안에선 줄어들지 않게
            source.maxDistance = 8f;
        }

        void OnDisable()
        {
            lastSide = null; // 다시 켜질 때(루프 이동) 이전 위치 기준으로 넘어갔다고 착각하지 않게 (inside도 함께 새로 만든다)
            touchFlash = 0f;
            if (hand != null) hand.Touching = false;
        }

        void Update()
        {
            if (hand == null || path == null || path.Length < 2) return;

            int segments = path.Length - 1;
            if (lastSide == null || lastSide.Length != segments)
            {
                lastSide = new float[segments];
                inside = new bool[segments];
                for (int i = 0; i < segments; i++) lastSide[i] = float.NaN;
            }

            Vector3 start = hand.Start, dir = hand.End - hand.Start;
            bool crossed = false;
            Vector3 crossPoint = default;
            float sweep = 0f;

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = path[i].position, ab = path[i + 1].position - a;
                Vector3 n = Vector3.Cross(ab, dir);
                if (n.magnitude < minCrossSine * ab.magnitude * dir.magnitude)
                {
                    lastSide[i] = float.NaN; // 거의 나란함 — 판정 보류
                    inside[i] = false;
                    continue;
                }

                // 두 직선 사이의 부호 있는 거리. 부호가 바뀌면 이번 프레임에 빛이 줄을 가로질러 지나간 것
                float side = Vector3.Dot(n.normalized, start - a);
                float prev = lastSide[i];
                lastSide[i] = side;

                // 지금 빛이 줄 둘레 안에 있나 (빛 선분·줄 구간 안쪽에서). 한 번 들어오면 조금 더 벗어나야 나간 것으로 본다
                LineParams(start, dir, a, ab, out float sNow, out float tNow);
                bool within = sNow >= 0f && sNow <= 1f && tNow >= 0f && tNow <= 1f;
                bool wasInside = inside[i];
                inside[i] = within && Mathf.Abs(side) <= brushRadius * (wasInside ? 1.3f : 1f);
                bool entered = inside[i] && !wasInside && !float.IsNaN(prev);

                bool signFlip = !float.IsNaN(prev) && (prev < 0f) != (side < 0f) && Mathf.Abs(side - prev) <= maxSweepPerFrame;
                if (signFlip)
                {
                    // 지나간 지점이 빛 선분(팔 길이)과 줄 구간 안쪽이어야 진짜 스침.
                    // 크게 흔들면 지금 프레임의 빛은 이미 교차점을 한참 지나 있으므로, 부호가 0이 되는 순간의 빛으로 확인한다
                    float k = prev / (prev - side);
                    LineParams(Vector3.Lerp(lastStart, start, k), Vector3.Lerp(lastDir, dir, k), a, ab, out float s, out float t);
                    signFlip = s >= 0f && s <= 1f && t >= -0.05f && t <= 1.05f;
                    if (signFlip) tNow = t;
                }
                if (!signFlip && !entered) continue;

                crossed = true;
                crossPoint = a + ab * Mathf.Clamp01(tNow);
                sweep = float.IsNaN(prev) ? 0f : Mathf.Abs(side - prev);
            }

            lastStart = start;
            lastDir = dir;

            if (crossed)
            {
                touchFlash = 0.25f;
                bool ready = !source.isPlaying && Time.time - lastStrokeTime >= strokeCooldown;
                if (ready && Time.deltaTime > 0f)
                {
                    source.transform.position = crossPoint;
                    Stroke(Mathf.Clamp01(sweep / Time.deltaTime / strokeFullSpeed));
                }
            }
            touchFlash = Mathf.Max(0f, touchFlash - Time.deltaTime);
            hand.Touching = touchFlash > 0f;
        }

        void Stroke(float speed01)
        {
            lastStrokeTime = Time.time;
            if (strokeClips == null || strokeClips.Length == 0) return;

            int index = Random.Range(0, strokeClips.Length);
            if (strokeClips.Length > 1 && index == lastClip) index = (index + 1) % strokeClips.Length; // 같은 소리 연속 방지
            lastClip = index;

            source.clip = strokeClips[index];
            source.pitch = Random.Range(strokePitch.x, strokePitch.y);
            source.volume = strokeVolume * Mathf.Lerp(strokeMinVolume, 1f, speed01);
            source.Play(); // PlayOneShot이 아니라 Play — 한 번에 하나만 울린다
        }

        // 두 직선 p + s·d, q + t·e 가 가장 가까워지는 매개변수 (나란하지 않다는 전제)
        static void LineParams(Vector3 p, Vector3 d, Vector3 q, Vector3 e, out float s, out float t)
        {
            Vector3 r = p - q;
            float a = Vector3.Dot(d, d), b = Vector3.Dot(d, e), c = Vector3.Dot(d, r);
            float ee = Vector3.Dot(e, e), f = Vector3.Dot(e, r);
            float denom = a * ee - b * b;
            s = (b * f - c * ee) / denom;
            t = (a * f - b * c) / denom;
        }

#if UNITY_EDITOR
        [Header("에디터 — 우클릭 메뉴 '통로 중앙에 맞추기'")]
        [SerializeField] float centerStep = 1f;
        [SerializeField] float ropeHeightAboveFloor = 0.7f;
        [Tooltip("벽이 이보다 멀면 트인 쪽으로 보고 가운데로 옮기지 않는다")]
        [SerializeField] float maxHalfWidth = 2.5f;
        [Tooltip("첫 구간(갈림길로 들어오는 직선)만: 통로 안 위치. -1 왼쪽 벽 ~ 0 가운데 ~ +1 오른쪽 벽")]
        [SerializeField, Range(-1f, 1f)] float approachSideBias = 0f;
        [Tooltip("벽 쪽으로 붙일 때 벽과 남겨 둘 거리")]
        [SerializeField] float wallMargin = 0.3f;

        // 현재 경로를 따라 centerStep 간격으로 다시 찍되, 각 점을 좌우 벽의 한가운데·바닥 위 일정 높이로 옮긴다.
        // 이 줄이 쓰이는 루프의 지형(MarkerVariant)이 켜져 있어야 벽을 찾을 수 있다.
        [ContextMenu("통로 중앙에 맞추기")]
        void CenterOnCorridor()
        {
            if (path == null || path.Length < 2) return;
            Physics.SyncTransforms();

            var points = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i].position, b = path[i + 1].position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / centerStep));
                float bias = i == 0 ? approachSideBias : 0f;
                for (int k = 0; k < steps; k++)
                    points.Add(CenterPoint(Vector3.Lerp(a, b, (float)k / steps), b - a, bias));
            }
            points.Add(CenterPoint(path[path.Length - 1].position, path[path.Length - 1].position - path[path.Length - 2].position, 0f));

            foreach (var old in path)
                if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);

            path = new Transform[points.Count];
            int blocked = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p = new GameObject("P" + i).transform;
                UnityEditor.Undo.RegisterCreatedObjectUndo(p.gameObject, "Center GuideRope");
                p.SetParent(transform, false);
                p.position = points[i];
                path[i] = p;
                if (i > 0 && Physics.Linecast(points[i - 1], points[i], ~0, QueryTriggerInteraction.Ignore)) blocked++;
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[GuideRope] {name}: 점 {points.Count}개로 다시 배치. 벽을 지나는 구간 {blocked}개", this);
        }

        Vector3 CenterPoint(Vector3 p, Vector3 forward, float bias)
        {
            forward.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);
            // 양쪽 벽이 모두 가까울 때만 가운데로 — 갈림길처럼 한쪽이 트여 있으면 그 자리를 유지
            if (Physics.Raycast(p, side, out RaycastHit r, maxHalfWidth, ~0, QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(p, -side, out RaycastHit l, maxHalfWidth, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 mid = (r.point + l.point) * 0.5f;
                float half = Mathf.Max(0f, Vector3.Distance(r.point, l.point) * 0.5f - wallMargin);
                mid += side * (bias * half); // side는 진행 방향의 오른쪽
                p.x = mid.x;
                p.z = mid.z;
            }
            // 바닥은 천장 바로 아래에서 내려 쏜다 — 바닥 위 바위 속에서 시작해 바위를 못 보는 일이 없게
            Vector3 top = Physics.Raycast(p, Vector3.up, out RaycastHit ceiling, 4f, ~0, QueryTriggerInteraction.Ignore)
                ? ceiling.point + Vector3.down * 0.05f
                : p + Vector3.up * 2f;
            if (Physics.Raycast(top, Vector3.down, out RaycastHit floor, 8f, ~0, QueryTriggerInteraction.Ignore))
                p.y = floor.point.y + ropeHeightAboveFloor;
            return p;
        }
#endif

        void OnDrawGizmos()
        {
            if (path == null) return;
            Gizmos.color = new Color(0.4f, 0.9f, 1f);
            for (int i = 0; i < path.Length - 1; i++)
                if (path[i] != null && path[i + 1] != null)
                    Gizmos.DrawLine(path[i].position, path[i + 1].position);
            foreach (var p in path)
                if (p != null) Gizmos.DrawSphere(p.position, 0.04f);
        }
    }
}
