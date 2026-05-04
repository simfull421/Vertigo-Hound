using UnityEngine;

public class KeyInteractable : MonoBehaviour
{
    [Header("Interaction Setup")]
    public GameObject interactUI; // E키 표시 UI
    // 나중에 허브 스크립트로 대체 가능
    public GameObject pingEffectObj; 

    private bool _isPlayerNear = false;
    private Transform _playerTransform;

    void OnTriggerEnter(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            _isPlayerNear = true;
            _playerTransform = pc.transform;
            if (interactUI != null) interactUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            _isPlayerNear = false;
            if (interactUI != null) interactUI.SetActive(false);
        }
    }

    void Update()
    {
        if (_isPlayerNear && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            CollectKey();
        }
    }
    private void CollectKey()
    {
        if (DataKeyManager.Instance != null)
            DataKeyManager.Instance.SetKeyHolder(_playerTransform, true);

        if (interactUI != null) interactUI.SetActive(false);
        
        // Destroy(gameObject); <--- 이거 지우고 아래 걸로 교체
        gameObject.SetActive(false); 
    }
}
