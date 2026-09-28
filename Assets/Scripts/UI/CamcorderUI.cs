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

        float elapsed;
        float blinkTimer;

        void Update()
        {
            elapsed += Time.deltaTime;
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
