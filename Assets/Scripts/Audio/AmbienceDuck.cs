namespace CaveGame
{
    // 동굴 환경음(루프 레이어·물방울·공포 큐) 전체에 곱하는 음량 배율. 1 = 평소, 0 = 무음.
    // 연출이 값을 바꾸고(6루프 암전 등), 환경음 쪽은 매 프레임 곱하기만 한다. 부드러운 전환은 바꾸는 쪽 책임.
    // 내 발소리·숨·심장은 여기 걸지 않는다 — 주변이 조용해질수록 내 몸 소리가 드러나야 한다.
    public static class AmbienceDuck
    {
        public static float Level = 1f;

        // 도메인 리로드를 끈 채 플레이를 다시 눌러도 이전 판의 값이 남지 않게
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Level = 1f;
    }
}
