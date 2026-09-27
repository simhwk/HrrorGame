using System.Collections.Generic;
using UnityEngine;

namespace CaveGame
{
    public class MarkerSpawner : MonoBehaviour
    {
        [SerializeField] List<MarkerPoint> markers = new List<MarkerPoint>();

        public void SetStage(MarkerStage stage)
        {
            foreach (var marker in markers)
                marker.SetStage(stage);
        }
    }
}
