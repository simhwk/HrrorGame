// =====================================================================
// [스크립트 역할] 캠코더 뷰파인더 스타일 HUD 표시
// 별도의 HUD 없이 캠코더 화면 자체에 REC 표시(깜빡임), 경과 시간 타이머,
// 현재 루프 카운터(LOOP xx / xx)를 텍스트로 갱신해 보여준다.
// =====================================================================
using UnityEngine;
using UnityEngine.UI; // 구형 uGUI의 Text 컴포넌트를 쓰기 위해 필요

namespace CaveGame
{
    /// <summary>별도 HUD 없이 캠코더 뷰파인더 자체가 정보 창구다 — REC / 테이프 타이머 / LOOP 카운터.</summary>
    public class CamcorderUI : MonoBehaviour
    {
        [SerializeField] Text loopText;  // "LOOP 01 / 07" 같은 텍스트를 표시할 UI Text
        [SerializeField] Text timerText; // "00:00" 경과 시간을 표시할 UI Text
        [SerializeField] GameObject recDot;      // REC 표시용 빨간 점 오브젝트(켜짐/꺼짐으로 깜빡임 표현)
        [SerializeField] float blinkInterval = 0.6f; // REC 점 깜빡임 간격(초) — 이 시간마다 켜짐/꺼짐 전환

        float elapsed;     // 게임 시작 후 누적된 경과 시간(초)
        float blinkTimer;  // 마지막 깜빡임 전환 이후 누적된 시간(간격에 도달하면 리셋)

        void OnEnable() // 활성화 시 이벤트 구독
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoopAdvanced += UpdateLoopText; // 루프가 바뀔 때마다 텍스트 자동 갱신되게 연결
        }

        void OnDisable() // 비활성화 시 구독 해제
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoopAdvanced -= UpdateLoopText;
        }

        // 시작 시 현재 루프 텍스트를 한 번 초기화
        void Start() => UpdateLoopText(GameManager.Instance.CurrentLoop, GameManager.Instance.CurrentStage);
        // 이벤트가 처음 발생하기 전에도 화면에 초기값(예: LOOP 01/07)이 바로 보이도록 수동으로 한 번 호출

        void Update() // 매 프레임 실행 — 시간 관련 UI는 실시간으로 계속 갱신해야 하므로 Update에서 처리
        {
            // 경과 시간을 분:초 형식으로 타이머 텍스트에 갱신
            elapsed += Time.deltaTime; // 이번 프레임 동안 지난 시간을 누적(초 단위 총 경과 시간)
            int minutes = Mathf.FloorToInt(elapsed / 60f); // 총 경과 시간을 60으로 나눈 정수 부분 = 분
            int seconds = Mathf.FloorToInt(elapsed % 60f); // 60으로 나눈 나머지 = 초
            timerText.text = $"{minutes:00}:{seconds:00}"; // 문자열 보간 + ":00" 서식으로 항상 두 자리(예: 03:07)로 표시

            // 일정 간격마다 REC 점을 켜고 끄며 깜빡이는 효과 구현
            blinkTimer += Time.deltaTime; // 마지막 전환 이후 지난 시간을 누적
            if (blinkTimer >= blinkInterval) // 설정한 간격(0.6초)이 지났다면
            {
                blinkTimer = 0f; // 타이머 리셋(다음 전환까지 다시 카운트)
                recDot.SetActive(!recDot.activeSelf); // 현재 켜져있으면 끄고, 꺼져있으면 켜서 깜빡임 효과 생성
            }
        }

        // 루프 진행 이벤트에 맞춰 "LOOP 현재/전체" 텍스트 갱신
        void UpdateLoopText(int loop, MarkerStage stage) // 이벤트 시그니처와 일치하는 콜백. stage는 이 함수에서 실제로는 사용하지 않음(이벤트 형태를 맞추기 위한 매개변수)
        {
            loopText.text = $"LOOP {loop:00} / {GameManager.Instance.TotalLoops:00}"; // 예: "LOOP 03 / 07" 형태의 문자열로 조합해 대입
        }
    }
}
