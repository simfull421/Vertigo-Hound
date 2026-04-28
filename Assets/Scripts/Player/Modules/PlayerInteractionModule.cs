using UnityEngine;
using System;

[Serializable]
public class PlayerInteractionModule
{
    [Header("Interaction Settings")]
    public float interactRange = 2.5f;
    public LayerMask interactLayer;

    private PlayerController _hub;
    private IInteractable _currentInteractable;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void UpdateModule()
    {
        DetectInteractable();

        if (_hub.InputProv.InteractTriggered && _currentInteractable != null)
        {
            _currentInteractable.OnInteract(_hub.transform);
        }
    }

    private void DetectInteractable()
    {
        Ray ray = new Ray(_hub.juiceController.mainCamera.transform.position, _hub.juiceController.mainCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactRange, interactLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                _currentInteractable = interactable;
                // UI 활성화 로직 (WorldSpaceInteractUI가 해당 레이캐스트 결과에 반응하도록 하거나, 직접 호출)
                return;
            }
        }

        _currentInteractable = null;
    }
}
