using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaveGame
{
    // 루프 이동(순간이동)과 루프별 몸의 변화(4루프부터 천장 자동 웅크림, 7루프 웅크린 채 시작)를 담당한다.
    // 표식·사운드·화면 효과는 각자 LoopListener로 반응하므로 여기서 호출하지 않는다.
    public class LoopManager : LoopListener
    {
        [SerializeField] PlayerController player;
        [Header("순간이동 기준선 — 넘은 만큼 그대로 반대편 선 너머로 옮긴다 (트리거 안에 떨어지지 않음)")]
        [SerializeField] Transform loopStartAnchor; // 정주행으로 끝선을 넘으면 이 선을 넘은 위치에 나타난다
        [SerializeField] Transform loopEndAnchor;   // 역주행으로 시작선을 넘으면 이 선 바로 앞에 나타난다
        [SerializeField] float teleportLift = 0.1f; // 구간마다 바닥 높이 오차가 있어 살짝 띄워 떨어뜨린다
        [Tooltip("마지막 루프 끝선을 넘으면 바로 넘어가는 엔딩 씬 — 화면이 뚝 끊기는 것 자체가 연출이다")]
        [SerializeField] string endingScene = "Ending";
        [Tooltip("메모를 전부 읽었으면 같은 판정에서 엔딩 씬 대신 이 숨은 엔딩으로 간다")]
        [SerializeField] MonsterEnding monsterEnding;

        [Header("웅크림")]
        [Tooltip("이 루프들에서 머리 위 천장이 가까우면 자동으로 웅크린다 (낮은 천장 맵이 켜지는 루프). 7루프는 서서 달려야 해서 뺀다")]
        [SerializeField] int autoCrouchFromLoop = 4;
        [SerializeField] int autoCrouchToLoop = 6;
        [Tooltip("이 루프는 웅크린 채로 시작하지만 강제는 아니다 — 웅크리기 키를 눌렀다 떼면 일어선다. 0 = 없음")]
        [SerializeField] int startCrouchedLoop = 0;

        CharacterController controller;
        bool endingTriggered;
        bool monsterRoute; // 마지막 루프에 들어설 때 정한다 — 그 뒤엔 메모가 없어 바뀌지 않는다
        AsyncOperation endingLoad; // 마지막 루프에 들어서면 미리 불러 둔다 — 끝선을 넘는 순간 멈칫 없이 점프스케어가 떠야 한다
        bool passedCheckpoint; // 마지막으로 중간 체크포인트를 앞으로 넘었는가 — 뒤로 넘으면 다시 false
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public Vector3 PlayerPosition => player.transform.position;
        // 순간이동 직후 모든 LoopTrigger가 즉시 새 위치를 기준으로 다시 잡는다 — 위치 점프를 "선 통과"로 착각하지 않게
        public event System.Action Teleported;

        void Awake()
        {
            controller = player.GetComponent<CharacterController>();
            spawnPosition = player.transform.position;
            spawnRotation = player.transform.rotation;
        }

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
                TriggerEnding();
                return;
            }

            TeleportRelative(passedLine, loopStartAnchor);
            passedCheckpoint = false;
            GameManager.Instance.AdvanceLoop();
        }

        // 마지막 루프의 끝 — 끝선을 넘었거나 추격하던 괴물에게 잡혔다. 화면이 뚝 끊기고 엔딩(점프스케어)으로
        public void TriggerEnding()
        {
            if (endingTriggered) return;
            endingTriggered = true;
            Debug.Log($"[엔딩 판정] 엔딩 발동 → {(monsterRoute ? "숨은 엔딩" : "1엔딩")}");
            if (monsterRoute)
            {
                monsterEnding.Begin();
                return;
            }
            if (endingLoad != null) endingLoad.allowSceneActivation = true;
            else SceneManager.LoadScene(endingScene);
        }

        public void OnCheckpointCrossed(bool forward) => passedCheckpoint = forward;

        public void OnReverseLoopReached(Transform passedLine) => TeleportRelative(passedLine, loopEndAnchor);

        // 치트: 다음 루프로 넘기고 시작 지점으로 옮긴다 (지형이 바뀌어 벽 속에 끼지 않게)
        public void SkipLoop()
        {
            if (endingTriggered || GameManager.Instance.IsLastLoop) return;
            passedCheckpoint = false;
            GameManager.Instance.AdvanceLoop();
            Place(spawnPosition, spawnRotation);
        }

        protected override void OnLoopChanged(int loop)
        {
            player.SetCeilingAutoCrouch(loop >= autoCrouchFromLoop && loop <= autoCrouchToLoop);
            if (loop == startCrouchedLoop) player.HoldCrouchUntilRelease();

            if (GameManager.Instance.IsLastLoop && !monsterRoute && endingLoad == null)
            {
                // 숨은 엔딩으로 갈 판엔 엔딩 씬을 미리 불러 두지 않는다 — 활성화를 막아 둔 로드가 남으면 뒤이은 타이틀 로드가 그 뒤에 묶인다
                monsterRoute = monsterEnding != null && monsterEnding.Qualifies;
                Debug.Log($"[엔딩 판정] 마지막 루프 진입 — 읽은 메모 {NoteLog.ReadCount}/{(monsterEnding != null ? monsterEnding.TotalNotes : -1)} ({string.Join(", ", NoteLog.ReadIds)}) → {(monsterRoute ? "숨은 엔딩" : "1엔딩")}");
                if (monsterRoute) return;

                endingLoad = SceneManager.LoadSceneAsync(endingScene);
                endingLoad.allowSceneActivation = false;
            }
        }

        // from 선 기준 플레이어의 상대 위치·방향을 to 선 기준으로 옮긴다. 높이는 월드 값을 유지한다(바닥 높이가 거의 같음).
        void TeleportRelative(Transform from, Transform to)
        {
            Transform p = player.transform;
            Vector3 target = to.TransformPoint(from.InverseTransformPoint(p.position));
            target.y = p.position.y + teleportLift;
            Quaternion yawDelta = Quaternion.Euler(0f, to.eulerAngles.y - from.eulerAngles.y, 0f);
            Place(target, yawDelta * p.rotation);
        }

        // CharacterController가 켜져 있으면 위치 대입이 무시되므로 잠시 끈다
        void Place(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            Physics.SyncTransforms();
            Teleported?.Invoke();
        }
    }
}
