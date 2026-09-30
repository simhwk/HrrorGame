using System.Collections.Generic;

namespace CaveGame
{
    // 이번 판에 읽은 메모 기록 — 모든 메모를 읽었는지로 7루프 엔딩이 갈린다 (MonsterEnding).
    // 메모는 루프마다 켜졌다 꺼지는 오브젝트라 "읽었다"는 사실만 이름으로 따로 남긴다.
    public static class NoteLog
    {
        static readonly HashSet<string> read = new HashSet<string>();

        public static int ReadCount => read.Count;
        public static IEnumerable<string> ReadIds => read;

        public static void MarkRead(string id) => read.Add(id);

        public static bool HasRead(string id) => read.Contains(id);

        // 새 판(게임 씬 진입)마다 비운다
        public static void Clear() => read.Clear();

        // 도메인 리로드를 끈 채 플레이를 다시 눌러도 이전 판의 기록이 남지 않게
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => read.Clear();
    }
}
