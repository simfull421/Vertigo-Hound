using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class PlayerVault
{
    [Header("Vault Settings")]
    public LayerMask vaultLayerMask;
    public float vaultCheckDistance = 2.2f; // 1.6 -> 2.2f로 상향 (멀리서도 선입력 반응)
    public float vaultCheckRadius = 0.4f;
    public float vaultMaxHeight = 2.0f;
    public float vaultMinHeight = 0.5f;
    public float vaultDuration = 0.15f;      // 0.2초 -> 0.15초로 대폭 단축하여 짐승같은 스피드 구현
    public float postVaultImpulse = 15f;
    
    public bool IsVaulting { get; private set; }
    public bool CanVault { get; private set; }
    public Vector3 VaultEdgePos { get; private set; }
    
    private Vector3 _cachedVaultTarget;

    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void UpdateModule()
    {
        CanVault = false;
        if (IsVaulting) return;

        // 뜀틀 속도 제한 조건 완전 삭제 (정지하거나 천천히 걸어도 무조건 발동)

        Vector3 forward = _hub.transform.forward;
        Vector3 pos = _hub.transform.position;
        Vector3 origin = pos + Vector3.up * 0.5f;

        if (Physics.SphereCast(origin, vaultCheckRadius, forward, out RaycastHit wallHit, vaultCheckDistance, vaultLayerMask))
        {
            Vector3 topOrigin = wallHit.point + forward * 0.3f + Vector3.up * vaultMaxHeight;
            if (Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, vaultMaxHeight - vaultMinHeight, vaultLayerMask))
            {
                if (Vector3.Angle(Vector3.up, topHit.normal) <= 45f)
                {
                    CanVault = true;
                    // IK가 짚을 모서리 끝단 좌표를 저장합니다.
                    VaultEdgePos = topHit.point; 
                    
                    // 착지 지점 오프셋(Clearance Offset): 물체의 끝을 완전히 가로지르도록 정면(forward) 방향으로 여유분 0.6f 추가
                    _cachedVaultTarget = topHit.point + forward * 0.6f + Vector3.up * (_hub.Capsule.bounds.extents.y + 0.1f);
                }
            }
        }
    }

    public void HandleJump()
    {
        if (CanVault)
        {
            // 스마트 레티클 캐싱 좌표 활용 및 조준점 즉시 초기화
            CanVault = false; 
            _hub.StartCoroutine(VaultRoutine(_cachedVaultTarget));
        }
    }

    private IEnumerator VaultRoutine(Vector3 targetPos)
    {
        IsVaulting = true;
        _hub.Rb.useGravity = false;
        _hub.Rb.linearVelocity = Vector3.zero;

        if (_hub.juiceController != null)
        {
            _hub.juiceController.TriggerVaultJuice(vaultDuration);
        }

        Vector3 startPos = _hub.transform.position;
        float elapsed = 0f;

        // (루프 진입 전) 현재 카메라의 로컬 X회전값(위아래 고개 숙임) 저장 및 원래 로컬 포지션 저장
        Transform camTransform = Camera.main.transform;
        float originalPitch = camTransform.localEulerAngles.x;
        Vector3 originalCamLocalPos = camTransform.localPosition;

        while (elapsed < vaultDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / vaultDuration;
            
            // Ease in-out 보간
            float smoothT = t * t * (3f - 2f * t);

            // 포물선 궤적 (아크 높이를 0.7f로 올려 부드러운 모서리 넘기 구현, 위치만 보간)
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.7f;

            _hub.Rb.MovePosition(currentPos);

            // [카메라 헤드 딥(Head Dip) 로직 추가]
            // Sin 그래프를 이용해 0 -> 1 -> 0 으로 값이 변하게 함
            float dipAmount = Mathf.Sin(t * Mathf.PI); 
            
            // 최대 15도 정도 아래를 쳐다보게 만듦 (수치는 취향껏 조절)
            float targetPitch = originalPitch + (15f * dipAmount); 
            
            // 카메라 로컬 X축(Pitch)에 적용 및 클리핑 방지를 위해 가슴 밖으로 살짝 내밂(+0.1f Y, +0.2f Z)
            camTransform.localEulerAngles = new Vector3(targetPitch, camTransform.localEulerAngles.y, camTransform.localEulerAngles.z);
            camTransform.localPosition = new Vector3(originalCamLocalPos.x, originalCamLocalPos.y + (0.1f * dipAmount), originalCamLocalPos.z + (0.2f * dipAmount));

            yield return new WaitForFixedUpdate();
        }

        // 루프 종료 후 카메라 앵글 및 위치 원상복구
        camTransform.localEulerAngles = new Vector3(originalPitch, camTransform.localEulerAngles.y, camTransform.localEulerAngles.z);
        camTransform.localPosition = originalCamLocalPos;

        _hub.transform.position = targetPos;
        _hub.Rb.useGravity = true;

        // "유저 요구사항: Vault가 끝나고 중력을 켜는 바로 그 프레임에 전방 아래쪽으로 폭발적 애드포스"
        Vector3 impulseDir = (_hub.transform.forward + Vector3.down * 0.2f).normalized;
        _hub.Rb.AddForce(impulseDir * postVaultImpulse, ForceMode.Impulse);

        IsVaulting = false;
    }
}
