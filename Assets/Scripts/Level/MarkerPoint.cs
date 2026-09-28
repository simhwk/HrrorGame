using UnityEngine;

namespace CaveGame
{
    // 자식에 있는 모든 MarkerVariant를 루프에 맞춰 켜고 끈다.
    // 새 표식은 프리팹에 MarkerVariant를 붙여 이 오브젝트 아래에 넣기만 하면 된다 — 인스펙터 연결 불필요.
    public class MarkerPoint : LoopListener
    {
        MarkerVariant[] variants;

        void Awake() => variants = GetComponentsInChildren<MarkerVariant>(true);

        protected override void OnLoopChanged(int loop)
        {
            foreach (var variant in variants)
                variant.SetVisible(variant.IsVisibleIn(loop));
        }
    }
}
