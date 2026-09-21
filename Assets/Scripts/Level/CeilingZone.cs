// =====================================================================
// [스크립트 역할] 천장이 낮은 구간 트리거 존
// 플레이어가 이 트리거 콜라이더에 들어오면 강제로 구부린 자세(크라우치)를 적용하고,
// 벗어나면 해제한다. 표식 단계와 별개로 지오메트리상 좁아지는 구간에 사용된다.
// =====================================================================
using UnityEngine;

namespace CaveGame
{
    [RequireComponent(typeof(Collider))] // 트리거 감지를 위해 Collider 컴포넌트 필수(Is Trigger 옵션 켜져 있어야 함)
    public class CeilingZone : MonoBehaviour
    {
        [SerializeField] PlayerController player; // 크라우치를 실제로 적용할 대상 플레이어 스크립트 참조

        // 플레이어가 저천장 구역에 진입하면 강제 크라우치 On
        void OnTriggerEnter(Collider other) // 다른 콜라이더가 이 트리거 영역에 "처음 들어오는 순간" 한 번 호출
        {
            if (other.CompareTag("Player")) player.SetZoneCrouch(true); // 들어온 게 플레이어라면 강제 크라우치를 켬(true)
        }

        // 플레이어가 저천장 구역을 벗어나면 강제 크라우치 Off
        void OnTriggerExit(Collider other) // 다른 콜라이더가 이 트리거 영역을 "벗어나는 순간" 한 번 호출
        {
            if (other.CompareTag("Player")) player.SetZoneCrouch(false); // 플레이어가 나갔다면 강제 크라우치를 끔(false)
        }
    }
}
