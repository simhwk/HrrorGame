// =====================================================================
// [스크립트 역할] 여러 MarkerPoint를 한 번에 관리하는 표식 그룹 제어기
// 등록된 모든 표식(MarkerPoint)의 단계를 한 번의 호출로 동시에 갱신시킨다.
// =====================================================================
using System.Collections.Generic; // List<T> 사용을 위해 필요
using UnityEngine;

namespace CaveGame
{
    /// <summary>공간 1개 원칙 — 같은 갈림길에 놓인 표식들의 단계를 한 번에 갱신한다.</summary>
    public class MarkerSpawner : MonoBehaviour
    {
        [SerializeField] List<MarkerPoint> markers = new List<MarkerPoint>();
        // 이 스포너가 관리하는 표식 목록. 인스펙터에서 씬의 MarkerPoint들을 드래그해 등록.
        // "= new List<MarkerPoint>()"는 기본값으로 빈 리스트를 만들어 null 참조 오류를 방지하는 초기화

        // 등록된 모든 표식의 단계를 동일하게 일괄 변경
        public void SetStage(MarkerStage stage) // LoopManager가 루프 진행 시 이 함수를 호출
        {
            foreach (var marker in markers) // 리스트에 담긴 MarkerPoint를 하나씩 순회
                marker.SetStage(stage);      // 각 표식에게 동일한 새 단계를 전달해 비주얼/상태를 갱신시킴
        }
    }
}
