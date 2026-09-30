using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaveGame
{
    // 키보드(방향키·엔터·ESC) 전용 타이틀 메뉴. 마우스는 숨긴다.
    // 커서 ">" 하나가 선택된 항목으로 미끄러져 가고, 메인 ↔ 설정은 페이드로 넘어간다.
    public class TitleMenu : MonoBehaviour
    {
        [SerializeField] TitleScreen title;

        [Header("페이지")]
        [SerializeField] CanvasGroup mainPage;
        [SerializeField] CanvasGroup settingsPage;
        [SerializeField] float pageFadeTime = 0.15f;
        [SerializeField] float pageSlide = 30f;

        [Header("메인")]
        [SerializeField] MenuItem playItem;
        [SerializeField] MenuItem settingsItem;
        [SerializeField] MenuItem exitItem;

        [Header("설정")]
        [SerializeField] MenuItem volumeItem;
        [SerializeField] MenuItem sensitivityItem;
        [SerializeField] MenuItem brightnessItem;
        [SerializeField] MenuItem fullscreenItem;
        [SerializeField] MenuItem backItem;

        [Header("커서")]
        [SerializeField] RectTransform cursor;
        [Tooltip("커서가 항목 글자 왼쪽으로 떨어진 거리 (캔버스 단위)")]
        [SerializeField] float cursorGap = 18f;
        [SerializeField] float cursorSmoothTime = 0.07f;

        [Header("키 반복")]
        [SerializeField] float repeatDelay = 0.4f;
        [SerializeField] float repeatInterval = 0.09f;

        [Header("소리")]
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip moveClip;
        [SerializeField] AudioClip submitClip;
        [SerializeField] AudioClip backClip;

        MenuItem[] mainItems;
        MenuItem[] settingsItems;
        MenuItem[] items;
        int index;
        bool busy; // 페이지 전환·게임 시작 중엔 입력을 받지 않는다
        Vector3 cursorVelocity;
        int heldDirection; // -1 위/왼쪽, +1 아래/오른쪽 (수직·수평 따로 쓰지 않고 마지막 누른 축 하나만)
        bool heldVertical;
        float nextRepeat;
        readonly Vector3[] corners = new Vector3[4];

        void Awake()
        {
            mainItems = new[] { playItem, settingsItem, exitItem };
            settingsItems = new[] { volumeItem, sensitivityItem, brightnessItem, fullscreenItem, backItem };
            ShowPage(mainPage, true);
            ShowPage(settingsPage, false);
            items = mainItems;
            Select(0, silent: true);
            RefreshValues();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // 첫 프레임엔 레이아웃이 잡힌 뒤라야 위치가 맞다 — 커서를 날아오지 않고 제자리에 둔다
            cursor.position = CursorTarget();
        }

        void Update()
        {
            cursor.position = Vector3.SmoothDamp(cursor.position, CursorTarget(), ref cursorVelocity,
                cursorSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            if (busy || title.IsBusy) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                Submit();
                return;
            }
            if ((kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) && items == settingsItems)
            {
                Back();
                return;
            }

            HandleDirections(kb);
        }

        // 누른 순간 한 번, 계속 누르고 있으면 잠깐 뒤부터 반복 — 볼륨을 쭉 올리거나 목록을 훑을 때
        void HandleDirections(Keyboard kb)
        {
            if (Pressed(kb.upArrowKey, kb.wKey)) Begin(-1, true);
            else if (Pressed(kb.downArrowKey, kb.sKey)) Begin(1, true);
            else if (Pressed(kb.leftArrowKey, kb.aKey)) Begin(-1, false);
            else if (Pressed(kb.rightArrowKey, kb.dKey)) Begin(1, false);
            else if (heldDirection != 0)
            {
                if (!StillHeld(kb))
                {
                    heldDirection = 0;
                }
                else if (Time.unscaledTime >= nextRepeat)
                {
                    nextRepeat = Time.unscaledTime + repeatInterval;
                    Step(heldDirection, heldVertical);
                }
            }
        }

        static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl a, UnityEngine.InputSystem.Controls.KeyControl b)
            => a.wasPressedThisFrame || b.wasPressedThisFrame;

        bool StillHeld(Keyboard kb)
        {
            if (heldVertical) return heldDirection < 0 ? kb.upArrowKey.isPressed || kb.wKey.isPressed : kb.downArrowKey.isPressed || kb.sKey.isPressed;
            return heldDirection < 0 ? kb.leftArrowKey.isPressed || kb.aKey.isPressed : kb.rightArrowKey.isPressed || kb.dKey.isPressed;
        }

        void Begin(int direction, bool vertical)
        {
            heldDirection = direction;
            heldVertical = vertical;
            nextRepeat = Time.unscaledTime + repeatDelay;
            Step(direction, vertical);
        }

        void Step(int direction, bool vertical)
        {
            if (vertical)
            {
                // 끝에서 반대쪽으로 넘어가지 않는다 — 반복 입력 중 맨 위↔맨 아래로 튀면 오히려 헷갈린다
                int next = Mathf.Clamp(index + direction, 0, items.Length - 1);
                if (next != index) Select(next);
            }
            else
            {
                Adjust(items[index], direction);
            }
        }

        void Select(int i, bool silent = false)
        {
            if (items != null && index < items.Length) items[index].Selected = false;
            index = i;
            items[index].Selected = true;
            if (!silent) Play(moveClip);
        }

        void Submit()
        {
            var item = items[index];
            if (item == playItem)
            {
                Play(submitClip);
                busy = true;
                title.Play();
            }
            else if (item == settingsItem)
            {
                Play(submitClip);
                StartCoroutine(SwitchPage(settingsPage, settingsItems, 0));
            }
            else if (item == exitItem)
            {
                Play(submitClip);
                title.Quit();
            }
            else if (item == fullscreenItem)
            {
                Adjust(item, 1); // 켜기/끄기는 엔터로도
            }
            else if (item == backItem)
            {
                Back();
            }
        }

        void Back()
        {
            GameSettings.Save();
            Play(backClip);
            StartCoroutine(SwitchPage(mainPage, mainItems, System.Array.IndexOf(mainItems, settingsItem)));
        }

        void Adjust(MenuItem item, int direction)
        {
            bool changed = true;
            if (item == volumeItem) changed = Change(GameSettings.Volume, direction, 0, GameSettings.VolumeMax, GameSettings.SetVolume);
            else if (item == sensitivityItem) changed = Change(GameSettings.Sensitivity, direction, GameSettings.SensitivityMin, GameSettings.SensitivityMax, GameSettings.SetSensitivity);
            else if (item == brightnessItem) changed = Change(GameSettings.Brightness, direction, GameSettings.BrightnessMin, GameSettings.BrightnessMax, GameSettings.SetBrightness);
            else if (item == fullscreenItem) GameSettings.SetFullscreen(!GameSettings.Fullscreen);
            else return; // 값이 없는 항목

            if (!changed) return;
            RefreshValues();
            item.Punch();
            Play(moveClip);
        }

        static bool Change(int current, int direction, int min, int max, System.Action<int> set)
        {
            int next = Mathf.Clamp(current + direction, min, max);
            if (next == current) return false;
            set(next);
            return true;
        }

        void RefreshValues()
        {
            volumeItem.SetValue(Arrows(Bar(GameSettings.Volume, GameSettings.VolumeMax), GameSettings.Volume, 0, GameSettings.VolumeMax));
            sensitivityItem.SetValue(Arrows(GameSettings.Sensitivity.ToString(), GameSettings.Sensitivity, GameSettings.SensitivityMin, GameSettings.SensitivityMax));
            int b = GameSettings.Brightness;
            brightnessItem.SetValue(Arrows(b > 0 ? "+" + b : b.ToString(), b, GameSettings.BrightnessMin, GameSettings.BrightnessMax));
            fullscreenItem.SetValue("< " + (GameSettings.Fullscreen ? "ON" : "OFF") + " >");
        }

        // 비디오 OSD 볼륨처럼 막대로
        static string Bar(int value, int max) => new string('|', value) + new string('.', max - value);

        // 더 갈 수 없는 쪽 화살표는 숨긴다 (자리는 유지해 글자가 흔들리지 않게)
        static string Arrows(string text, int value, int min, int max)
            => (value > min ? "< " : "<alpha=#00>< <alpha=#FF>") + text + (value < max ? " >" : "<alpha=#00> >");

        IEnumerator SwitchPage(CanvasGroup to, MenuItem[] toItems, int selectIndex)
        {
            busy = true;
            heldDirection = 0;
            CanvasGroup from = to == mainPage ? settingsPage : mainPage;

            yield return Fade(from, 1f, 0f);
            from.gameObject.SetActive(false);

            items[index].Selected = false;
            items = toItems;
            Select(selectIndex, silent: true);

            to.gameObject.SetActive(true);
            yield return Fade(to, 0f, 1f);
            busy = false;
        }

        // 사라질 땐 왼쪽으로, 나타날 땐 오른쪽에서 — 페이지가 옆으로 넘어가는 느낌
        IEnumerator Fade(CanvasGroup page, float from, float to)
        {
            var rect = (RectTransform)page.transform;
            for (float e = 0f; e < pageFadeTime; e += Time.unscaledDeltaTime)
            {
                float a = Mathf.Lerp(from, to, e / pageFadeTime);
                page.alpha = a;
                rect.anchoredPosition = new Vector2((1f - a) * (to > from ? pageSlide : -pageSlide), 0f);
                yield return null;
            }
            page.alpha = to;
            rect.anchoredPosition = Vector2.zero;
        }

        static void ShowPage(CanvasGroup page, bool visible)
        {
            page.alpha = visible ? 1f : 0f;
            page.gameObject.SetActive(visible);
        }

        Vector3 CursorTarget()
        {
            RectTransform label = items[index].LabelRect;
            // 글자 상자의 왼쪽 가운데 — 항목이 선택되며 밀려나는 것까지 따라간다
            label.GetWorldCorners(corners);
            Vector3 leftMiddle = (corners[0] + corners[1]) * 0.5f;
            return leftMiddle + Vector3.left * (cursorGap * cursor.lossyScale.x);
        }

        void Play(AudioClip clip)
        {
            if (clip && sfx) sfx.PlayOneShot(clip);
        }
    }
}
