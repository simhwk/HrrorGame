using UnityEngine;

namespace CaveGame
{
    // 먼저 들어왔던 사람들이 두고 간(혹은 남겨진) 물건 꾸러미. 자식 하나하나가 한 사람 몫의 꾸러미이고,
    // 루프마다 그중 하나만 켠다 — 같은 자리에 매번 다른 사람의 흔적이 쌓여 있는 것처럼.
    // 게임을 시작할 때 꾸러미 순서를 한 번 섞어 루프 1, 2, 3...에 차례로 나눠 준다 — 꾸러미 수만큼의 루프 동안 같은 꾸러미가 두 번 나오지 않는다.
    public class BelongingsPile : LoopListener
    {
        [Tooltip("이 루프들에는 아무것도 두지 않는다")]
        [SerializeField] int[] emptyLoops = { 7 };

        GameObject[] bundles;
        int[] order;

        void Awake()
        {
            bundles = new GameObject[transform.childCount];
            for (int i = 0; i < bundles.Length; i++) bundles[i] = transform.GetChild(i).gameObject;

            order = new int[bundles.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        protected override void OnLoopChanged(int loop)
        {
            int pick = -1;
            if (bundles.Length > 0 && System.Array.IndexOf(emptyLoops, loop) < 0)
                pick = order[(loop - 1) % order.Length];
            for (int i = 0; i < bundles.Length; i++) bundles[i].SetActive(i == pick);
        }
    }
}
