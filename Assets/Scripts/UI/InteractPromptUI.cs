using UnityEngine;

namespace CaveGame
{
    // 플레이어가 만질 수 있는 표식을 조준 중일 때만 상호작용 안내를 보인다.
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractPromptUI : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;

        CanvasGroup group;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        }

        void Update() => group.alpha = interactor != null && interactor.Target != null ? 1f : 0f;
    }
}
