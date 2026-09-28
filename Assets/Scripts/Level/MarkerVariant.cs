using UnityEngine;

namespace CaveGame
{
    // 표식 하나(케언, 가이드라인 등)의 루트에 붙여 "몇 루프부터 몇 루프까지 보이는지"를 지정한다.
    // MarkerPoint 아래 아무 깊이에 두기만 하면 자동으로 수집된다.
    public class MarkerVariant : MonoBehaviour
    {
        [SerializeField, Min(1)] int fromLoop = 1;
        [SerializeField, Min(1)] int toLoop = 1;

        public bool IsVisibleIn(int loop) => loop >= fromLoop && loop <= toLoop;

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        void OnValidate()
        {
            if (toLoop < fromLoop) toLoop = fromLoop;
        }
    }
}
