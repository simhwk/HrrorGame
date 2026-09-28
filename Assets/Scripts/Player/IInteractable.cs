using UnityEngine;

namespace CaveGame
{
    // PlayerInteractor가 조준(SphereCast)으로 찾아 E 입력을 전달하는 대상. 콜라이더가 이 오브젝트나 부모 쪽에 있어야 한다.
    public interface IInteractable
    {
        void SetHover(bool on);
        void Interact(Vector3 point);
    }
}
