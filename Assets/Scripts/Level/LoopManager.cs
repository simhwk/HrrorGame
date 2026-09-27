using UnityEngine;

namespace CaveGame
{
    // 루프 이동(순간이동)과 루프별 몸의 변화(강제 웅크림)를 담당한다.
    // 표식·사운드·화면 효과는 각자 LoopListener로 반응하므로 여기서 호출하지 않는다.
    public class LoopManager : LoopListener
    {
        [SerializeField] PlayerController player;
        [Header("순간이동 기준선 — 넘은 만큼 그대로 반대편 선 너머로 옮긴다 (트리거 안에 떨어지지 않음)")]
        [SerializeField] Transform loopStartAnchor; // 정주행으로 끝선을 넘으면 이 선을 넘은 위치에 나타난다
        [SerializeField] Transform loopEndAnchor;   // 역주행으로 시작선을 넘으면 이 선 바로 앞에 나타난다
        [SerializeField] float teleportLift = 0.1f; // 구간마다 바닥 높이 오차가 있어 살짝 띄워 떨어뜨린다
        [SerializeField] EndingSequenceController ending;

        [Header("천장이 낮아져 웅크리게 되는 루프")]
        [SerializeField] int forcedCrouchFromLoop = 4;
        [SerializeField] int forcedCrouchToLoop = 6;
        [Tooltip("이 루프는 웅크린 채로 시작하지만 강제는 아니다 — 웅크리기 키를 눌렀다 떼면 일어선다")]
        [SerializeField] int startCrouchedLoop = 7;

        CharacterController controller;
        bool endingTriggered;
        bool passedCheckpoint; // 마지막으로 중간 체크포인트를 앞으로 넘었는가 — 뒤로 넘으면 다시 false

        public Vector3 PlayerPosition => player.transform.position;
        // 순간이동 직후 모든 LoopTrigger가 즉시 새 위치를 기준으로 다시 잡는다 — 위치 점프를 "선 통과"로 착각하지 않게
        public event System.Action Teleported;

        void Awake() => controller = player.GetComponent<CharacterController>();

        public void OnLoopEndReached(Transform passedLine)
        {
            if (endingTriggered) return;

            // 중간을 안 거치고 끝선을 넘은 경우(워프 지점에서 앞뒤로 왔다 갔다 등): 위치만 이어 붙이고 루프는 그대로
            if (!passedCheckpoint)
            {
                TeleportRelative(passedLine, loopStartAnchor);
                return;
            }

            if (GameManager.Instance.IsLastLoop)
            {
                endingTriggered = true;
                ending.BeginEnding();
                return;
            }

            TeleportRelative(passedLine, loopStartAnchor);
            passedCheckpoint = false;
            GameManager.Instance.AdvanceLoop();
        }

        public void OnCheckpointCrossed(bool forward) => passedCheckpoint = forward;

        public void OnReverseLoopReached(Transform passedLine) => TeleportRelative(passedLine, loopEndAnchor);

        protected override void OnLoopChanged(int loop)
        {
            player.SetLoopForcedCrouch(loop >= forcedCrouchFromLoop && loop <= forcedCrouchToLoop);
            if (loop == startCrouchedLoop) player.HoldCrouchUntilRelease();
        }

        // from 선 기준 플레이어의 상대 위치·방향을 to 선 기준으로 옮긴다. 높이는 월드 값을 유지한다(바닥 높이가 거의 같음).
        // CharacterController가 켜져 있으면 위치 대입이 무시되므로 잠시 끈다
        void TeleportRelative(Transform from, Transform to)
        {
            Transform p = player.transform;
            Vector3 target = to.TransformPoint(from.InverseTransformPoint(p.position));
            target.y = p.position.y + teleportLift;
            Quaternion yawDelta = Quaternion.Euler(0f, to.eulerAngles.y - from.eulerAngles.y, 0f);

            controller.enabled = false;
            p.SetPositionAndRotation(target, yawDelta * p.rotation);
            controller.enabled = true;
            Physics.SyncTransforms();
            Teleported?.Invoke();
        }
    }
}
