using UnityEngine;

public class WorldSpaceInteractUI : MonoBehaviour
{
    [Header("Settings")]
    public float showDistance = 3f;
    public GameObject uiVisual; // 실제 'E' 키 이미지가 있는 자식 오브젝트

    private Transform _playerCamera;
    private bool _isPlayerNear = false;

    void Start()
    {
        if (Camera.main != null) _playerCamera = Camera.main.transform;
        if (uiVisual != null) uiVisual.SetActive(false);
    }

    void Update()
    {
        if (_playerCamera == null) return;

        float dist = Vector3.Distance(transform.position, _playerCamera.position);
        bool near = dist <= showDistance;

        if (near != _isPlayerNear)
        {
            _isPlayerNear = near;
            if (uiVisual != null) uiVisual.SetActive(_isPlayerNear);
        }
    }

    // [핵심] 빌보드 로직: 카메라를 정면으로 바라봄
    void LateUpdate()
    {
        if (_isPlayerNear && _playerCamera != null && uiVisual != null)
        {
            // UI가 카메라를 등지고 있다면 LookAt을 쓰되 180도 회전이 필요할 수 있음.
            // 보통 UI Canvas는 Forward가 뒤쪽인 경우가 많으므로 아래 방식 사용.
            transform.LookAt(transform.position + _playerCamera.forward);
        }
    }
}
