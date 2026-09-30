using System.Collections;
using TMPro;
using UnityEngine;

namespace CaveGame
{
    // 게임 시작 컷신: 걷기만 막은 채(시점·시간은 그대로) 수첩을 화면 아래에서 눈앞으로 들어 올린 뒤 메모를 띄운다.
    // 메모를 닫으면(E/ESC — NoteViewer가 처리) 수첩을 내리고 걷기를 풀어 준다. 루프는 씬을 다시 불러오지 않으므로 한 번만 나온다.
    public class OpeningNote : MonoBehaviour
    {
        [TextArea(6, 20)] [SerializeField] string content;
        [Tooltip("비워 두면 NoteViewer의 기본 손글씨")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField] PlayerController player;

        [Header("수첩 — 카메라 자식으로 두고 꺼 둔다")]
        [SerializeField] Transform notebook;
        [Tooltip("눈앞에 든 자세 (카메라 기준)")]
        [SerializeField] Vector3 heldPosition = new Vector3(0f, -0.06f, 0.36f);
        [SerializeField] Vector3 heldRotation = new Vector3(-55f, 0f, 0f);
        [Tooltip("화면 밖 아래 — 여기서 올라온다")]
        [SerializeField] Vector3 loweredPosition = new Vector3(0.05f, -0.45f, 0.3f);
        [SerializeField] Vector3 loweredRotation = new Vector3(-10f, 0f, 8f);

        [Header("타이밍")]
        [Tooltip("씬 진입 후 수첩을 들기까지 — 타이틀에서 넘어온 지지직이 걷히는 시간과 맞춘다")]
        [SerializeField] float delay = 1.5f;
        [SerializeField] float raiseTime = 1.1f;
        [Tooltip("수첩을 다 든 뒤 메모 창이 뜨기까지")]
        [SerializeField] float readPause = 0.4f;
        [SerializeField] float lowerTime = 0.7f;

        [Tooltip("수첩을 들 때 소리 (종이 바스락 등). 비워 두면 무음")]
        [SerializeField] AudioClip raiseSound;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            player.MovementLocked = true;
            if (notebook != null) notebook.gameObject.SetActive(false);
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(delay);

            if (notebook != null)
            {
                notebook.gameObject.SetActive(true);
                if (raiseSound) AudioSource.PlayClipAtPoint(raiseSound, notebook.position);
                yield return Move(loweredPosition, loweredRotation, heldPosition, heldRotation, raiseTime);
                yield return new WaitForSeconds(readPause);
            }

            if (NoteViewer.Instance != null)
            {
                NoteViewer.Instance.Open(content, font);
                while (NoteViewer.Instance.IsOpen) yield return null;
            }

            player.MovementLocked = false;

            if (notebook != null)
            {
                yield return Move(heldPosition, heldRotation, loweredPosition, loweredRotation, lowerTime);
                notebook.gameObject.SetActive(false);
            }
        }

        IEnumerator Move(Vector3 fromPos, Vector3 fromRot, Vector3 toPos, Vector3 toRot, float time)
        {
            Quaternion a = Quaternion.Euler(fromRot), b = Quaternion.Euler(toRot);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                notebook.localPosition = Vector3.Lerp(fromPos, toPos, k);
                notebook.localRotation = Quaternion.Slerp(a, b, k);
                yield return null;
            }
            notebook.localPosition = toPos;
            notebook.localRotation = b;
        }
    }
}
