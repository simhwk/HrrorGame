using TMPro;
using UnityEngine;

namespace CaveGame
{
    // 바닥에 놓인 메모. 조준 + E로 NoteViewer에 내용을 띄운다. 콜라이더가 이 오브젝트나 자식에 있어야 조준된다.
    // 몇 루프에 보일지는 같은 오브젝트의 MarkerVariant로 정한다 (MarkerPoints 아래에 두면 자동 적용).
    public class Note : MonoBehaviour, IInteractable
    {
        [TextArea(6, 20)] [SerializeField] string content;
        [Tooltip("비워 두면 NoteViewer의 기본 손글씨 — 루프가 갈수록 글씨체를 흐트러뜨릴 때 지정")]
        [SerializeField] TMP_FontAsset font;

        public void SetHover(bool on) { }

        public void Interact(Vector3 point)
        {
            if (NoteViewer.Instance != null) NoteViewer.Instance.Open(content, font);
            NoteLog.MarkRead(Id);
            Debug.Log($"[메모] {Id} 읽음 — {NoteLog.ReadCount}장 ({string.Join(", ", NoteLog.ReadIds)}) / 루프 {GameManager.Instance?.CurrentLoop}");
        }

        // 메모마다 이름이 다르다 (Note_L1, Note_V2 …) — 같은 메모를 여러 번 읽어도 한 번으로 친다
        public string Id => name;
    }
}
