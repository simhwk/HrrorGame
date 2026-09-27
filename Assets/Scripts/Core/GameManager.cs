using System;
using UnityEngine;

namespace CaveGame
{
    // 현재 루프 번호의 단일 출처. 루프가 바뀌면 LoopChanged로 알린다(구독은 LoopListener 상속으로).
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] int totalLoops = 7;

        public int CurrentLoop { get; private set; } = 1;
        public int TotalLoops => totalLoops;
        public bool IsLastLoop => CurrentLoop >= totalLoops;
        public bool HasStarted { get; private set; }

        public event Action<int> LoopChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 모든 Awake가 끝난 뒤 첫 루프를 알린다 — 구독자들이 참조를 다 잡은 상태를 보장
        void Start()
        {
            HasStarted = true;
            LoopChanged?.Invoke(CurrentLoop);
        }

        public void AdvanceLoop()
        {
            CurrentLoop++;
            LoopChanged?.Invoke(CurrentLoop);
        }
    }
}
