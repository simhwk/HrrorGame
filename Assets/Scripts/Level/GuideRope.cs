using UnityEngine;

namespace CaveGame
{
    // 보이지 않는 가이드라인. 화면엔 아무것도 그리지 않고, 손이 줄에 스치는 소리로만 존재한다.
    //  1) 경로(path)는 T자 갈림길 조금 전 직선 구간에서 시작해 갈림길 중앙을 지나 정답 갈래 안쪽에서 끝난다.
    //  2) 플레이어의 보이지 않는 손(PlayerHand — 손전등 빛 한가운데로 뻗은 선분)이 줄에 닿으면 "스윽" 쓸리는 소리가 난다.
    //     닿는 순간 한 번, 이후엔 손이 줄 위를 strokeEveryMeters만큼 움직일 때마다 한 번씩 — 한 번에 하나만 울린다.
    //     줄은 정답 갈래로 꺾이므로, 손으로 계속 훑으려면 시선이 그쪽으로 돌아간다.
    // 루프별 버전은 MarkerVariant로 켜고 끈다.
    public class GuideRope : MonoBehaviour
    {
        [Header("경로 — 순서대로: 직선 구간 시작 → … → 정답 갈래 안쪽")]
        [SerializeField] Transform[] path;

        [Header("닿음 판정 — 손 선분(손전등 빛 중심)의 길이는 플레이어의 PlayerHand에서 조정")]
        [Tooltip("손 선분과 줄 사이가 이 거리 안이면 닿은 것")]
        [SerializeField] float touchRadius = 0.18f;
        [Tooltip("닿은 뒤엔 이 배수만큼 벗어나야 떨어진 것으로 본다 (경계에서 소리가 떨리지 않게)")]
        [SerializeField] float releaseMultiplier = 1.4f;

        [Header("쓸리는 소리 — 손이 천을 훑는 \"스윽\" 한 번씩")]
        [SerializeField] AudioClip[] strokeClips;
        [SerializeField, Range(0f, 1f)] float strokeVolume = 1f;
        [Tooltip("손이 줄 위를 이만큼(m) 움직일 때마다 한 번 쓸린다")]
        [SerializeField] float strokeEveryMeters = 0.35f;
        [Tooltip("이 속도(m/s)로 훑으면 최대 볼륨 — 느리게 훑으면 작게")]
        [SerializeField] float strokeFullSpeed = 0.8f;
        [SerializeField, Range(0f, 1f)] float strokeMinVolume = 0.5f;
        [SerializeField] Vector2 strokePitch = new Vector2(0.92f, 1.06f);
        [Tooltip("앞 소리가 이만큼 재생된 뒤에야 다음 소리 — 두 번씩 겹쳐 들리지 않게")]
        [SerializeField, Range(0f, 1f)] float overlapAfter = 0.7f;

        PlayerHand hand;
        AudioSource source;
        bool touching;
        Vector3 lastHandPoint;
        Vector3 lastRopePoint;
        float travelled;
        int lastClip = -1;

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
            touching = false;
            if (hand != null) hand.Touching = false;
        }

        void Update()
        {
            if (hand == null || path == null || path.Length < 2) return;

            float distance = ClosestToPath(hand.Start, hand.End, out Vector3 handPoint, out Vector3 ropePoint);

            bool wasTouching = touching;
            touching = distance <= touchRadius * (touching ? releaseMultiplier : 1f);
            hand.Touching = touching;

            bool previousDone = !source.isPlaying || source.clip == null || source.time >= source.clip.length * overlapAfter;
            if (touching && !wasTouching)
            {
                if (previousDone) // 줄 경계를 빠르게 들락날락해도 연달아 울리지 않게
                {
                    source.transform.position = ropePoint;
                    Stroke(1f); // 손이 줄을 찾은 순간
                }
            }
            else if (touching && Time.deltaTime > 0f)
            {
                // 시점을 돌리면 손 쪽 점이, 걸으면 줄 쪽 점이 움직인다 — 둘 중 많이 움직인 쪽
                float moved = Mathf.Max(Vector3.Distance(handPoint, lastHandPoint), Vector3.Distance(ropePoint, lastRopePoint));
                travelled += moved;
                if (travelled >= strokeEveryMeters && previousDone)
                {
                    source.transform.position = ropePoint;
                    Stroke(Mathf.Clamp01(moved / Time.deltaTime / strokeFullSpeed));
                }
            }
            lastHandPoint = handPoint;
            lastRopePoint = ropePoint;
        }

        void Stroke(float speed01)
        {
            travelled = 0f;
            if (strokeClips == null || strokeClips.Length == 0) return;

            int index = Random.Range(0, strokeClips.Length);
            if (strokeClips.Length > 1 && index == lastClip) index = (index + 1) % strokeClips.Length; // 같은 소리 연속 방지
            lastClip = index;

            source.clip = strokeClips[index];
            source.pitch = Random.Range(strokePitch.x, strokePitch.y);
            source.volume = strokeVolume * Mathf.Lerp(strokeMinVolume, 1f, speed01);
            source.Play(); // PlayOneShot이 아니라 Play — 앞 소리를 끊고 하나만 울린다
        }

        // 손 선분과 줄(꺾은선) 사이의 최단 거리. 양쪽의 가장 가까운 점도 돌려준다
        float ClosestToPath(Vector3 handStart, Vector3 handEnd, out Vector3 handPoint, out Vector3 ropePoint)
        {
            float best = float.MaxValue;
            handPoint = handStart;
            ropePoint = path[0].position;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float d = ClosestSegmentSegment(handStart, handEnd, path[i].position, path[i + 1].position,
                                                out Vector3 onHand, out Vector3 onRope);
                if (d < best)
                {
                    best = d;
                    handPoint = onHand;
                    ropePoint = onRope;
                }
            }
            return Mathf.Sqrt(best);
        }

        // 두 선분 사이 최단 거리의 제곱 (Ericson, Real-Time Collision Detection 5.1.9)
        static float ClosestSegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= Mathf.Epsilon && e <= Mathf.Epsilon) { s = t = 0f; }
            else if (a <= Mathf.Epsilon) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= Mathf.Epsilon) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).sqrMagnitude;
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
