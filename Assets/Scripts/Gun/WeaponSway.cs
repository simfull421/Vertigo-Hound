using UnityEngine;

public class WeaponSway : MonoBehaviour
{
    [Header("Sway Settings")]
    public float smooth = 8f;
    public float multiplier = 2f;
    public float maxAmount = 5f; // 총이 화면 밖으로 너무 벗어나지 않게 제한

    private Vector3 initialPosition;

    void Start()
    {
        // 총의 원래 위치 저장
        initialPosition = transform.localPosition;
    }

    void Update()
    {
        // 마우스 입력값 가져오기 (InputProvider를 연결하셔도 됩니다)
        float mouseX = Input.GetAxisRaw("Mouse X") * multiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * multiplier;

        // 흔들림 최대치 제한
        mouseX = Mathf.Clamp(mouseX, -maxAmount, maxAmount);
        mouseY = Mathf.Clamp(mouseY, -maxAmount, maxAmount);

        // 마우스를 움직인 반대 방향으로 총을 살짝 이동시킴 (관성 효과)
        Vector3 targetPosition = new Vector3(-mouseX, -mouseY, 0);

        // 부드럽게 원래 위치로 복구
        transform.localPosition = Vector3.Lerp(transform.localPosition, initialPosition + targetPosition, Time.deltaTime * smooth);
    }
}