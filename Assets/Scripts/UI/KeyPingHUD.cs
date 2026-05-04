using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 에이펙스 스타일의 오프스크린 핑(Ping) 시스템.
/// 뒤돌아봤을 때 핑이 튀는 현상을 막고, 360도 부드러운 가장자리 추적을 지원합니다.
/// </summary>
public class KeyPingHUD : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform pingContainer;
    public RectTransform arrowIcon;
    public TextMeshProUGUI distanceText;
    public CanvasGroup pingCanvasGroup;

    [Header("Settings")]
    public float edgePadding = 0.08f;
    public float maxScale = 1.3f;
    public float minScale = 0.8f;
    [Tooltip("이미지가 위를 보고 있다면 -90, 오른쪽을 보고 있다면 0을 입력하세요.")]
    public float arrowAngleOffset = -90f; 

    private Camera _mainCamera;
    private RectTransform _canvasRect;

    void Start()
    {
        _mainCamera = Camera.main;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            _canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    void LateUpdate()
    {
        if (_mainCamera == null || _canvasRect == null || pingContainer == null) return;
        
        // 키가 없거나 플레이어가 키를 들고 있으면 전체 숨김
        if (DataKeyManager.Instance == null || DataKeyManager.Instance.isKeyHeldByPlayer)
        {
            if (pingCanvasGroup != null) pingCanvasGroup.alpha = 0f;
            return;
        }

        // actualKeyObject 하나만 추적 (부착/드랍 상관없이 안정적)
        if (DataKeyManager.Instance.actualKeyObject == null)
        {
            if (pingCanvasGroup != null) pingCanvasGroup.alpha = 0f;
            return;
        }
        Vector3 targetPos = DataKeyManager.Instance.actualKeyObject.transform.position + Vector3.up * 2.2f;
        float distance = Vector3.Distance(_mainCamera.transform.position, targetPos);
        if (distanceText != null) distanceText.text = distance.ToString("F0") + "m";

        // 1. 카메라 로컬 좌표로 변환 (타겟이 내 앞인지 뒤인지, 왼쪽인지 오른쪽인지 파악)
        Vector3 localPos = _mainCamera.transform.InverseTransformPoint(targetPos);
        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(targetPos);
        
        // 화면 밖인지 판별
        bool isOffScreen = localPos.z < 0 || viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.y < 0f || viewportPos.y > 1f;

        if (isOffScreen)
        {
            // [오프스크린] 360도 부드러운 방향 계산
            Vector2 screenDir = new Vector2(localPos.x, localPos.y);

            // [핵심] 타겟이 등 뒤(z < 0)에 있으면, 핑을 화면 아래쪽(Bottom)으로 부드럽게 당겨줍니다.
            if (localPos.z < 0f)
            {
                screenDir.y = -Mathf.Abs(localPos.y) - Mathf.Abs(localPos.z);
            }
            screenDir.Normalize();

            // 직사각형 화면의 가장자리에 완벽하게 스냅시키는 수학 공식
            Vector2 center = new Vector2(0.5f, 0.5f);
            float xDist = screenDir.x > 0 ? (1f - edgePadding - center.x) : (center.x - edgePadding);
            float yDist = screenDir.y > 0 ? (1f - edgePadding - center.y) : (center.y - edgePadding);

            float tx = xDist / Mathf.Abs(screenDir.x);
            float ty = yDist / Mathf.Abs(screenDir.y);
            float t = Mathf.Min(tx, ty); // 가로, 세로 가장자리 중 더 가까운 곳에 맞춤

            Vector2 edgePos = center + screenDir * t;
            viewportPos = new Vector3(edgePos.x, edgePos.y, 0f);

            // 화살표를 타겟 방향으로 회전
            if (arrowIcon != null)
            {
                arrowIcon.gameObject.SetActive(true);
                float angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
                arrowIcon.localEulerAngles = new Vector3(0, 0, angle + arrowAngleOffset);
            }
            
            pingContainer.localScale = Vector3.one * minScale;
            if (pingCanvasGroup != null) pingCanvasGroup.alpha = 0.8f;
        }
        else
        {
            // [온스크린] 화면 안에 있을 때
            viewportPos.x = Mathf.Clamp(viewportPos.x, edgePadding, 1f - edgePadding);
            viewportPos.y = Mathf.Clamp(viewportPos.y, edgePadding, 1f - edgePadding);

            // 디자인 일관성을 위해 화살표를 끄지 않고, 아래(👇)를 향하게 고정하여 마커처럼 보이게 함
            if (arrowIcon != null)
            {
                arrowIcon.gameObject.SetActive(true);
                // -90도는 수학적으로 아래(Down) 방향을 뜻함
                arrowIcon.localEulerAngles = new Vector3(0, 0, -90f + arrowAngleOffset);
            }

            float distFromCenter = Vector2.Distance(new Vector2(0.5f, 0.5f), new Vector2(viewportPos.x, viewportPos.y));
            float scale = Mathf.Lerp(maxScale, minScale, distFromCenter * 2f); 
            pingContainer.localScale = Vector3.one * scale;
            if (pingCanvasGroup != null) pingCanvasGroup.alpha = 1f;
        }

        // Viewport 좌표를 캔버스 기준 로컬 좌표로 변환하여 UI 적용
        Vector2 screenPos = new Vector2(viewportPos.x * _mainCamera.pixelWidth, viewportPos.y * _mainCamera.pixelHeight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, _canvasRect.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay ? null : _mainCamera, out Vector2 localUIPos);
        
        pingContainer.localPosition = localUIPos;
    }
}