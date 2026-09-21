// =====================================================================
// [스크립트 역할] 게임 전체 진행 상태(싱글턴) 관리자
// 현재 몇 번째 루프인지, 어떤 표식 단계(Tape/Guideline/Bloodstain)인지를 관리하고
// 루프가 진행될 때 이벤트를 발행해 다른 스크립트들에 알린다.
// =====================================================================
using System;      // Action<T> 델리게이트(이벤트)를 쓰기 위해 필요
using UnityEngine; // MonoBehaviour, SerializeField 등 유니티 기본 기능

namespace CaveGame
{
    public class GameManager : MonoBehaviour // MonoBehaviour를 상속해야 씬의 GameObject에 컴포넌트로 붙일 수 있음
    {
        public static GameManager Instance { get; private set; } // static: 클래스 전체에서 공유되는 단 하나의 값. 외부에서는 읽기만(get) 가능, 쓰기(set)는 이 클래스 내부에서만 가능(private set)

        [SerializeField] int loopsInTapeStage = 3;       // [SerializeField]: private 필드지만 인스펙터 창에 노출시켜 수치 조절 가능하게 함. Tape 단계가 유지되는 루프 수(1~3루프)
        [SerializeField] int loopsInGuidelineStage = 3;  // Guideline 단계가 유지되는 루프 수(4~6루프)
        [SerializeField] int totalLoops = 7;             // 전체 루프(반복) 횟수

        public int CurrentLoop { get; private set; } = 1; // 현재 루프 번호. 외부는 읽기만, 초기값 1(1루프부터 시작)
        public MarkerStage CurrentStage { get; private set; } = MarkerStage.Tape; // 현재 표식 단계. 초기값은 Tape
        public int TotalLoops => totalLoops; // "=>"는 표현식 본문 프로퍼티: totalLoops 값을 그대로 읽기 전용으로 외부에 공개

        public event Action<int, MarkerStage> LoopAdvanced; // 이벤트 선언. int(루프 번호)와 MarkerStage(단계)를 인자로 넘기며, 루프가 진행될 때마다 구독자들에게 방출됨

        void Awake() // Awake: 이 오브젝트가 씬에 로드될 때 가장 먼저 한 번 호출되는 유니티 생명주기 함수
        {
            // 씬에 인스턴스가 이미 있으면 중복 생성된 이 오브젝트는 제거(싱글턴 보장)
            if (Instance != null && Instance != this) // Instance가 이미 채워져 있고, 그게 "나 자신"이 아니라면 = 중복 생성됐다는 뜻
            {
                Destroy(gameObject); // 이 컴포넌트가 붙어있는 GameObject 전체를 파괴해서 중복을 없앰
                return; // 아래 코드(Instance = this)를 실행하지 않고 함수 종료
            }
            Instance = this; // 아직 아무도 등록 안 됐으면 "나"를 유일한 인스턴스로 등록
        }

        // 시작 시 초기 루프/단계 상태를 구독자들에게 한 번 통지
        void Start() => LoopAdvanced?.Invoke(CurrentLoop, CurrentStage);
        // Start: Awake 다음 프레임에 호출되는 생명주기 함수(다른 오브젝트들의 Awake가 다 끝난 뒤 실행됨을 보장하려는 의도).
        // LoopAdvanced?.Invoke(...): "?."는 널 조건 연산자 — 구독자가 한 명도 없으면(null) 호출하지 않고 그냥 넘어감(에러 방지). 있으면 (CurrentLoop, CurrentStage)를 인자로 실행

        // 다음 루프로 진행. 마지막 루프 여부 판단은 호출자(LoopManager)가 미리 하므로 여기서는 항상 다음 루프로 진행한다
        public void AdvanceLoop() // 외부(LoopManager)에서 "루프 끝에 도달했고, 아직 마지막이 아니다"라고 판단했을 때만 호출하는 함수
        {
            CurrentLoop++; // 루프 번호를 1 증가
            CurrentStage = ResolveStage(CurrentLoop); // 새 루프 번호에 맞는 표식 단계를 다시 계산해 저장
            LoopAdvanced?.Invoke(CurrentLoop, CurrentStage); // 갱신된 루프/단계를 구독자들에게 통지
        }

        // 루프 번호로부터 현재 표식 단계를 계산
        MarkerStage ResolveStage(int loop) // 순수 계산 함수: 입력(loop)만으로 결과가 결정됨
        {
            if (loop <= loopsInTapeStage) return MarkerStage.Tape; // 1~3루프면 Tape
            if (loop <= loopsInTapeStage + loopsInGuidelineStage) return MarkerStage.Guideline; // 4~6루프면 Guideline(3+3=6까지)
            return MarkerStage.Bloodstain; // 그 외(7루프 이상)는 Bloodstain
        }
    }
}
