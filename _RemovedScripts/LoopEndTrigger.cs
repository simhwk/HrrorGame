using UnityEngine;

namespace CaveGame
{
    [RequireComponent(typeof(Collider))]
    public class LoopEndTrigger : MonoBehaviour
    {
        [SerializeField] LoopManager loopManager;

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                loopManager.OnLoopEndReached();
        }
    }
}
