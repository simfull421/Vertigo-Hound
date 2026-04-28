using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BreachDoor : MonoBehaviour, IBreachable, IInteractable
{
    [Header("References")]
    [Tooltip("회전축 (반드시 문의 측면 경첩 부분에 위치해야 함!)")]
    public Transform pivot; 
    
    [Header("Soft Open Settings")]
    public float openAngle = 90f;
    public float openSpeed = 2f;

    private Rigidbody _rb;
    private bool _isBreached = false;
    private bool _isOpened = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        // [수정] 평소에는 중력/물리 연산을 끄고(Kinematic) 코드로만 움직이게 단단히 고정합니다.
        _rb.isKinematic = true; 
        
        if (pivot == null) pivot = transform.parent;
    }

    // --- IInteractable 구현 ---
    public void OnInteract(Transform initiator)
    {
        if (_isBreached || _isOpened) return;
        
        StartCoroutine(OpenDoorRoutine(initiator));
    }

    private IEnumerator OpenDoorRoutine(Transform player)
    {
        _isOpened = true;
        
        Vector3 dirToPlayer = (player.position - pivot.position).normalized;
        float dot = Vector3.Dot(pivot.forward, dirToPlayer);

        float targetY = dot > 0 ? -openAngle : openAngle;
        Quaternion targetRot = Quaternion.Euler(0, targetY, 0);
        Quaternion startRot = pivot.localRotation;

        float elapsed = 0;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime * openSpeed;
            pivot.localRotation = Quaternion.Slerp(startRot, targetRot, elapsed);
            yield return null;
        }
    }

    // --- IBreachable 구현 ---
    public void OnBreached(BreachData data)
    {
        if (_isBreached) return;
        _isBreached = true;
        StopAllCoroutines(); // 열리고 있었다면 중단

        // [핵심 1] 문을 피벗(부모)으로부터 완전히 독립시킵니다! (허공에 매달리는 현상 원천 차단)
        transform.SetParent(null);

        // [핵심 2] 물리 연산을 켜서 중력과 충격력을 받게 만듭니다.
        _rb.isKinematic = false; 

        Vector3 hitPoint = data.impactPoint + Vector3.up * 0.5f; 
        _rb.AddForceAtPosition(data.impactForce, hitPoint, ForceMode.Impulse);

        _rb.linearDamping = 1.5f;
        _rb.angularDamping = 2.0f;
    }
}