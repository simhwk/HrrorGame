// =====================================================================
// [스크립트 역할] 공포감(dread) 배경 오디오 누적 재생 관리자
// 루프(반복 구간)가 진행될수록 5단계 공포 효과음을 하나씩 추가로 재생시켜
// 긴장감을 누적시키고, 접촉(공격) 발생 시 별도의 원샷 효과음을 재생한다.
// =====================================================================
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// 효과음 5단계 누적 — 거리(1) → 표식 이상(2) → 근접 리듬(3) → 접촉(4) → 부재(5).
    /// 한 번 시작된 큐는 멈추지 않고 계속 쌓인다.
    /// </summary>
    public class DreadAudioSequencer : MonoBehaviour
    {
        [SerializeField] AudioClip[] dreadCues = new AudioClip[5]; // 단계별(1~5) 반복 재생용 공포 효과음 클립 배열. 크기 5로 미리 고정
        [SerializeField] AudioClip attackFleshClip;                // 공격(살점 뜯기는) 원샷 효과음
        [SerializeField] AudioClip attackCreatureClip;              // 공격(크리처 울음) 원샷 효과음
        [SerializeField] AudioSource attackSource;                  // 공격 효과음을 재생할 전용 AudioSource(씬에 미리 배치)
        [SerializeField] Transform cuesParent;                      // 생성되는 큐 오브젝트들이 정리되어 붙을 부모 트랜스폼(하이어라키 정리용)

        readonly AudioSource[] activeLoops = new AudioSource[5];
        // 현재 재생 중인 단계별 루프 오디오 소스를 저장하는 캐시 배열(중복 재생 방지용).
        // readonly: 배열 "자체"를 다른 배열로 통째로 교체하는 건 금지, 배열 안의 각 칸(요소) 값은 바꿀 수 있음

        // 지정한 루프 단계에 해당하는 공포 효과음을 반복 재생 시작 (이미 재생 중이면 무시)
        public void PlayCueForLoop(int loopIndex) // LoopManager가 루프 번호를 넘겨 호출
        {
            int cueIndex = Mathf.Clamp(loopIndex - 1, 0, dreadCues.Length - 1);
            // 루프는 1부터 시작하지만 배열 인덱스는 0부터라 -1. Clamp로 0~4 범위를 벗어나지 않게 안전하게 보정(7루프여도 배열은 5개뿐이므로)
            if (activeLoops[cueIndex] != null) return; // 이 단계 큐가 이미 재생 중(캐시에 등록됨)이면 다시 만들지 않고 종료

            AudioClip clip = dreadCues[cueIndex]; // 해당 단계에 대응하는 오디오 클립 가져오기
            if (clip == null) return; // 인스펙터에서 클립을 안 넣어놨으면 재생할 게 없으니 종료

            var go = new GameObject($"DreadCue_{cueIndex + 1}"); // 이 단계 전용으로 새 빈 GameObject를 코드로 생성(이름에 단계 번호 표시)
            go.transform.SetParent(cuesParent, false); // 지정된 부모 밑으로 정리(false: 월드 위치는 유지하지 않고 부모 기준 로컬 좌표 0으로)

            var source = go.AddComponent<AudioSource>(); // 방금 만든 오브젝트에 AudioSource 컴포넌트를 코드로 추가
            source.clip = clip;        // 재생할 클립 지정
            source.loop = true;        // 계속 반복 재생(끝나면 다시 처음부터)
            source.spatialBlend = 1f;  // 0=완전 2D, 1=완전 3D. 1f로 설정해 위치 기반의 입체 음향으로 만듦
            source.volume = 0.7f;      // 볼륨을 70%로 설정
            source.Play();             // 재생 시작

            activeLoops[cueIndex] = source; // 캐시에 등록 — 다음에 같은 단계로 또 호출돼도 중복 생성되지 않도록 기록
        }

        // 지정한 위치에서 공격(접촉) 원샷 효과음 재생
        public void PlayAttack(Vector3 position) // EndingSequenceController가 엔딩 습격 시점에 호출
        {
            if (attackSource == null) return; // 공격용 오디오 소스가 연결 안 돼있으면 아무것도 하지 않음
            attackSource.transform.position = position; // 소스 위치를 공격이 발생한 지점으로 옮겨서 그 방향에서 소리가 나는 것처럼 처리
            if (attackFleshClip != null) attackSource.PlayOneShot(attackFleshClip); // PlayOneShot: loop 설정과 무관하게 한 번만 재생, 겹쳐서 여러 소리를 동시에 낼 수 있음
            if (attackCreatureClip != null) attackSource.PlayOneShot(attackCreatureClip); // 살점 소리와 크리처 울음을 동시에 겹쳐 재생
        }
    }
}
