using UnityEngine;

namespace CaveGame
{
    // 손전등 빛의 한가운데를 따라 뻗은 보이지 않는 손.
    // 보이지 않는 가이드라인(GuideRope)이 이 선분과의 거리로 "손이 줄에 닿았는지"를 판정한다.
    // 씬 뷰에서 항상 기즈모로 보이며, 줄에 닿아 있으면 노란색이 된다.
    public class PlayerHand : MonoBehaviour
    {
        [Tooltip("손전등(Spot Light). 비워두면 자식에서 찾는다")]
        [SerializeField] Transform beam;
        [SerializeField] float reach = 1.8f;

        public bool Touching { get; set; }

        public Vector3 Start => Beam.position;
        public Vector3 End => Beam.position + Beam.forward * reach;

        Transform Beam
        {
            get
            {
                if (beam == null)
                {
                    foreach (var light in GetComponentsInChildren<Light>(true))
                        if (light.type == LightType.Spot) { beam = light.transform; break; }
                    if (beam == null) beam = transform;
                }
                return beam;
            }
        }

        void OnDrawGizmos()
        {
            Vector3 start = Start, end = End;
            Gizmos.color = Touching ? Color.yellow : new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.04f);
        }
    }
}
