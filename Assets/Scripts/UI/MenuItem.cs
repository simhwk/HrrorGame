using TMPro;
using UnityEngine;

namespace CaveGame
{
    // 메뉴 한 줄. 선택되면 글자가 밝아지며 살짝 오른쪽으로 밀려나고, 값이 바뀌면 값 글자가 한 번 튄다.
    // 상태는 TitleMenu가 정하고, 여기선 그 상태로 부드럽게 따라가기만 한다.
    public class MenuItem : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [Tooltip("설정 값 표시 (없으면 버튼형 항목)")]
        [SerializeField] TMP_Text value;
        [SerializeField] Color idleColor = new Color(0.55f, 0.58f, 0.55f, 0.75f);
        [SerializeField] Color selectedColor = Color.white;
        [SerializeField] float selectedShift = 16f;
        [SerializeField] float smoothTime = 0.08f;

        RectTransform labelRect;
        Vector2 labelHome;
        float highlight;
        float highlightVelocity;
        float punch;

        public bool Selected { get; set; }
        public RectTransform LabelRect => labelRect;

        void Awake()
        {
            labelRect = label.rectTransform;
            labelHome = labelRect.anchoredPosition;
        }

        public void SetValue(string text)
        {
            if (value) value.text = text;
        }

        public void Punch() => punch = 1f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            highlight = Mathf.SmoothDamp(highlight, Selected ? 1f : 0f, ref highlightVelocity, smoothTime, Mathf.Infinity, dt);

            Color c = Color.Lerp(idleColor, selectedColor, highlight);
            label.color = c;
            labelRect.anchoredPosition = labelHome + Vector2.right * (selectedShift * highlight);

            if (!value) return;
            value.color = c;
            punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
            value.rectTransform.localScale = Vector3.one * (1f + 0.18f * punch * punch);
        }
    }
}
