// =====================================================================
// [스크립트 역할] 루프 종료 지점 트리거
// 플레이어가 루프의 끝(공간을 한 바퀴 돈 지점)에 도달했음을 감지해
// LoopManager에 알리는 단순 콜라이더 트리거이다.
// =====================================================================
using UnityEngine; // Collider, GameObject 등 사용

namespace CaveGame
{
    [RequireComponent(typeof(Collider))] // 이 스크립트가 붙은 GameObject에 Collider 컴포넌트가 없으면 자동으로 추가 강제(트리거 감지에 필수)
    public class LoopEndTrigger : MonoBehaviour
    {
        [SerializeField] LoopManager loopManager; // 인스펙터에서 연결해줄 LoopManager 참조 — 이벤트를 전달할 대상

        // 플레이어가 루프 종료 지점 콜라이더에 도달하면 LoopManager에 통지
        void OnTriggerEnter(Collider other) // 유니티 물리 이벤트: 이 오브젝트의 콜라이더(Is Trigger 체크됨)에 다른 콜라이더가 "들어오는 순간" 자동 호출됨. other = 들어온 상대방 콜라이더
        {
            if (other.CompareTag("Player")) // 들어온 오브젝트의 Tag가 "Player"인지 확인(태그 문자열 비교보다 CompareTag가 더 빠름/권장 방식)
                loopManager.OnLoopEndReached(); // 맞다면 LoopManager에 "끝에 도달했다"고 함수 호출로 알림
        }
    }
}
