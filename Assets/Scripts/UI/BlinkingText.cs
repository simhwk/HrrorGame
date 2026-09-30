using TMPro;
using UnityEngine;

namespace CaveGame
{
    // 시계를 한 번도 맞춘 적 없는 비디오 데크의 "12:00" 깜빡임
    [RequireComponent(typeof(TMP_Text))]
    public class BlinkingText : MonoBehaviour
    {
        [SerializeField] float period = 1f;

        TMP_Text text;

        void Awake() => text = GetComponent<TMP_Text>();

        void Update() => text.enabled = Time.time % period < period * 0.5f;
    }
}
