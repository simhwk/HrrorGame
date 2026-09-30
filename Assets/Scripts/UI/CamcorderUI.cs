using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CaveGame
{
    public class CamcorderUI : LoopListener
    {
        [SerializeField] TMP_Text loopText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] GameObject recDot;
        [SerializeField] float blinkInterval = 0.6f;
        [SerializeField] Image batteryCells;
        [Tooltip("앞 씬의 녹화 시간을 이어서 센다 — 엔딩 HUD용. 끄면 이 씬에서 녹화를 새로 시작한다")]
        [SerializeField] bool continueRecording;

        // 녹화 시작 시각. Time.time은 씬이 바뀌어도 이어지므로 이것만 넘기면 화면이 꺼져 있던 동안(엔딩 암전)도 녹화된 걸로 쳐진다
        static float recordingStart = -1f;

        float blinkTimer;

        void Awake()
        {
            if (!continueRecording || recordingStart < 0f) recordingStart = Time.time;
        }

        void Update()
        {
            float elapsed = Time.time - recordingStart;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            int centiseconds = Mathf.FloorToInt(elapsed * 100f) % 100;
            timerText.SetText("{0:00}:{1:00}:{2:00}", minutes, seconds, centiseconds); // 매 프레임 호출 — 문자열 할당 없는 버전

            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                recDot.SetActive(!recDot.activeSelf);
            }
        }

        // 루프가 진행될수록 배터리가 줄어든다
        protected override void OnLoopChanged(int loop)
        {
            int total = GameManager.Instance.TotalLoops;
            loopText.text = $"{loop:00} / {total:00}";
            batteryCells.fillAmount = (float)(total - loop + 1) / total;
        }
    }
}
