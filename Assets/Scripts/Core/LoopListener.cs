using UnityEngine;

namespace CaveGame
{
    // 루프 변화에 반응하는 컴포넌트의 베이스. 구독/해제와 늦게 켜진 경우의 즉시 적용을 대신 처리한다.
    public abstract class LoopListener : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            var game = GameManager.Instance;
            if (game == null) return;

            game.LoopChanged += OnLoopChanged;
            if (game.HasStarted) OnLoopChanged(game.CurrentLoop);
        }

        protected virtual void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoopChanged -= OnLoopChanged;
        }

        protected abstract void OnLoopChanged(int loop);
    }
}
