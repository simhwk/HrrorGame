using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace CaveGame
{
    // 개발용 치트. 동작은 에디터와 Development Build에서만 컴파일된다 — 정식 빌드에선 빈 컴포넌트.
    // (클래스 자체를 #if로 지우면 씬에 붙은 컴포넌트가 정식 빌드에서 Missing Script가 된다)
    //  F1: 다음 루프로 건너뛰고 시작 지점으로 이동
    //  F2: 모든 메모를 읽은 것으로 — 숨은 엔딩 확인용 (7루프에 들어서기 전에 눌러야 한다)
    public class DebugCheats : MonoBehaviour
    {
        [SerializeField] LoopManager loopManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Awake()
        {
            if (loopManager == null) loopManager = FindFirstObjectByType<LoopManager>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || loopManager == null) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                loopManager.SkipLoop();
                Debug.Log($"[Cheat] 루프 {GameManager.Instance.CurrentLoop}로 이동");
            }
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                foreach (var note in FindObjectsByType<Note>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    NoteLog.MarkRead(note.Id);
                Debug.Log($"[Cheat] 메모 {NoteLog.ReadCount}장 모두 읽음 처리");
            }
        }
#endif
    }
}
