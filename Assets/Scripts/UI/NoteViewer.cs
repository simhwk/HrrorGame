using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaveGame
{
    // 메모 내용을 화면에 띄우는 창. 게임 전체에 하나만 두고 Note가 내용(과 필요하면 글씨체)만 바꿔 끼운다.
    // 읽는 동안 PlayerController를 꺼서 이동·시점을 멈춘다 — PlayerInteractor가 이를 보고 조준과 E 안내도 알아서 끈다.
    // 이 스크립트가 붙은 오브젝트는 켜 둔 채 panel 자식만 끄고 켠다 (꺼 두면 Awake가 돌지 않아 Instance가 비어 있음).
    public class NoteViewer : MonoBehaviour
    {
        public static NoteViewer Instance { get; private set; }
        public bool IsOpen => panel.activeSelf;

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text text;
        [SerializeField] PlayerController player;

        TMP_FontAsset defaultFont;
        int openedFrame = -1;
        int closedFrame = -1;

        void Awake()
        {
            Instance = this;
            panel.SetActive(false);
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            defaultFont = text.font;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(string content, TMP_FontAsset font = null)
        {
            // 닫기에 누른 E가 같은 프레임에 PlayerInteractor에서 다시 "열기"로 잡히는 것을 막는다
            if (panel.activeSelf || Time.frameCount == closedFrame) return;

            text.font = font != null ? font : defaultFont;
            text.text = content;
            panel.SetActive(true);
            player.enabled = false;
            openedFrame = Time.frameCount;
        }

        void Update()
        {
            // 연 프레임엔 닫기를 받지 않는다 — PlayerInteractor가 이 스크립트보다 먼저 돌면,
            // 열 때 누른 E가 같은 프레임의 "닫기"로도 잡혀 열리자마자 닫힌다 (실행 순서는 보장되지 않음)
            if (!panel.activeSelf || Time.frameCount == openedFrame) return;

            // 플레이어를 꺼 둔 상태라 그쪽 입력 대신 키보드를 직접 읽는다
            var kb = Keyboard.current;
            if (kb == null || !(kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) return;

            panel.SetActive(false);
            player.enabled = true;
            closedFrame = Time.frameCount;
        }
    }
}
