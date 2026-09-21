// =====================================================================
// [스크립트 역할] 루프 반대편(시작점 뒤쪽) 트리거
// 플레이어가 앞으로 나아가지 않고 뒤로 걸어 나가려 할 때 감지해서, 루프 진행 없이
// 반대쪽 끝으로만 순간이동시킨다(공간을 벗어날 수 없다는 연출).
// =====================================================================
using UnityEngine; // Collider, GameObject 등 사용

namespace CaveGame
{
    [RequireComponent(typeof(Collider))] // 이 스크립트가 붙은 GameObject에 Collider 컴포넌트가 없으면 자동으로 추가 강제(트리거 감지에 필수)
    public class LoopReverseTrigger : MonoBehaviour
    {
        [SerializeField] LoopManager loopManager; // 인스펙터에서 연결해줄 LoopManager 참조 — 이벤트를 전달할 대상

        // 플레이어가 시작점 뒤쪽 콜라이더에 도달하면 LoopManager에 통지
        void OnTriggerEnter(Collider other) // 유니티 물리 이벤트: 이 오브젝트의 콜라이더(Is Trigger 체크됨)에 다른 콜라이더가 "들어오는 순간" 자동 호출됨. other = 들어온 상대방 콜라이더
        {
            if (other.CompareTag("Player")) // 들어온 오브젝트의 Tag가 "Player"인지 확인
                loopManager.OnReverseLoopReached(); // 맞다면 LoopManager에 "반대편으로 나가려 한다"고 함수 호출로 알림
        }
    }
}
