using System.Collections;
using UnityEngine;

namespace CaveGame
{
    public class EndingSequenceController : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] Transform cutsceneCameraTarget;
        [SerializeField] Transform droppedCameraRestPose;
        [SerializeField] Light endingLight;
        [SerializeField] DreadAudioSequencer audioSequencer;
        [SerializeField] float cutsceneDuration = 3f;
        [SerializeField] float dropDuration = 1.2f;
        [SerializeField] float attackToLightDelay = 4f;

        public void BeginEnding() => StartCoroutine(EndingRoutine());

        IEnumerator EndingRoutine()
        {
            player.enabled = false;

            yield return MoveCamera(player.CameraPivot, cutsceneCameraTarget, cutsceneDuration);
            yield return MoveCamera(player.CameraPivot, droppedCameraRestPose, dropDuration);

            audioSequencer.PlayAttack(player.CameraPivot.position);

            yield return new WaitForSeconds(attackToLightDelay);

            if (endingLight != null)
                endingLight.enabled = true;
        }

        IEnumerator MoveCamera(Transform cam, Transform target, float duration)
        {
            Vector3 startPos = cam.position;
            Quaternion startRot = cam.rotation;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                cam.position = Vector3.Lerp(startPos, target.position, k);
                cam.rotation = Quaternion.Slerp(startRot, target.rotation, k);
                yield return null;
            }

            cam.SetPositionAndRotation(target.position, target.rotation);
        }
    }
}
