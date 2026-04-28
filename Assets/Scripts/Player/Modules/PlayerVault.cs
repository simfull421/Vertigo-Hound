using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class PlayerVault
{
    [Header("Standard Vault Settings")]
    public LayerMask vaultLayerMask;
    public float vaultCheckDistance = 2.2f;
    public float vaultCheckRadius = 0.4f;
    public float vaultMaxHeight = 2.0f;
    public float vaultMinHeight = 0.5f;
    public float vaultDuration = 0.15f; // 일반 볼트는 고정 시간으로 짧고 굵게!
    public float postVaultImpulse = 15f;

    [Header("High Vault (Slip/Kong) Settings")]
    [Tooltip("높은 벽이나 창문 등을 위한 전용 레이어")]
    public LayerMask highVaultLayerMask; 
    public float highVaultCheckDistance = 3.0f;
    public float highVaultMaxHeight = 10.0f; // [추가] 하이 볼트 최대 허용 높이
    public float highVaultSpeed = 15f; // 하이볼트는 거리 대비 속도 적용

    public bool IsVaulting { get; private set; }
    public bool CanVault { get; private set; }
    public bool CanHighVault { get; private set; }

    public Vector3 VaultEdgePos { get; private set; }
    
    // 모서리 높이 캐싱
    private Vector3 _cachedVaultTarget;
    private Vector3 _cachedHighVaultTarget;
    private float _highVaultApexY; // 하이볼트에서 손으로 짚을 모서리의 Y좌표

    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void UpdateModule()
    {
        CanVault = false;
        CanHighVault = false;
        if (IsVaulting) return;

        Vector3 forward = _hub.transform.forward;
        Vector3 pos = _hub.transform.position;
        Vector3 origin = pos + Vector3.up * 0.5f;
        Vector3 headOrigin = pos + Vector3.up * 1.5f;

        // 1. [High Vault 우선 감지] - 높은 장애물 감지
        if (Physics.Raycast(headOrigin, forward, out RaycastHit highHit, highVaultCheckDistance, highVaultLayerMask))
        {
            // [원인 해결] 부딪힌 지점(highHit.point)에서 무조건 2.5m 위에서 쏘는 게 아닙니다.
            // 플레이어 바닥을 기준으로 하이볼트 최대 높이(highVaultMaxHeight)보다 조금 더 높은 곳에서 수직으로 쏴야 정확한 옥상 높이를 찾을 수 있습니다.
            Vector3 topCheckOrigin = highHit.point + forward * 0.3f;
            topCheckOrigin.y = _hub.transform.position.y + highVaultMaxHeight + 0.5f;

            if (Physics.Raycast(topCheckOrigin, Vector3.down, out RaycastHit topHit, highVaultMaxHeight + 1.0f, highVaultLayerMask))
            {
                // 찾아낸 모서리의 실제 높이가 플레이어 기준 몇 미터인지 계산
                float obstacleHeight = topHit.point.y - _hub.transform.position.y;

                // 유저님 의도: 2f 초과 ~ 10f 이하일 경우에만 하이볼트 발동!
                if (obstacleHeight > vaultMaxHeight && obstacleHeight <= highVaultMaxHeight)
                {
                    CanHighVault = true;
                    _highVaultApexY = topHit.point.y; // 윗 벽의 진짜 높이 저장!
                    VaultEdgePos = topHit.point;
                    
                    // 모서리에서 2m 앞으로 강하게 쏘아질 타겟 지점
                    _cachedHighVaultTarget = topHit.point + forward * 2.0f + Vector3.up * 0.2f; 
                    return; 
                }
            }
            // 조건에 맞지 않으면(예: 1.8m 벽) 함수를 빠져나가지 않고 바로 아래의 2번 Standard Vault 로직이 처리하도록 흐름을 넘깁니다.
        }

        // 2. [Standard Vault 감지] - 기존의 낮고 빠른 장애물
        if (Physics.SphereCast(origin, vaultCheckRadius, forward, out RaycastHit wallHit, vaultCheckDistance, vaultLayerMask))
        {
            Vector3 topOrigin = wallHit.point + forward * 0.3f + Vector3.up * vaultMaxHeight;
            if (Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, vaultMaxHeight - vaultMinHeight, vaultLayerMask))
            {
                if (Vector3.Angle(Vector3.up, topHit.normal) <= 45f)
                {
                    CanVault = true;
                    VaultEdgePos = topHit.point;
                    _cachedVaultTarget = topHit.point + forward * 0.6f + Vector3.up * (_hub.Capsule.bounds.extents.y + 0.1f);
                }
            }
        }
    }

    public void HandleJump()
    {
        if (CanHighVault)
        {
            CanHighVault = false;
            // 하이 볼트: 웅장한 베지어 곡선 + 카메라 쥬스 호출
            _hub.StartCoroutine(HighVaultRoutine(_cachedHighVaultTarget, _highVaultApexY));
        }
        else if (CanVault)
        {
            CanVault = false; 
            // 일반 볼트: 기존의 짧고 빠른 점프 호출
            _hub.StartCoroutine(VaultRoutine(_cachedVaultTarget));
        }
    }

    // 🏃 [일반 볼트] 무릎 높이 장애물을 짧고 빠르게 휙!
    private IEnumerator VaultRoutine(Vector3 targetPos)
    {
        IsVaulting = true;
        _hub.Rb.useGravity = false;
        _hub.Rb.linearVelocity = Vector3.zero;
        if (_hub.Capsule != null) _hub.Capsule.enabled = false;

        // 일반 볼트용 짧은 흔들림 (기존 로직 유지)
        if (_hub.juiceController != null) _hub.juiceController.TriggerVaultJuice(vaultDuration);

        Vector3 startPos = _hub.transform.position;
        float elapsed = 0f;

        while (elapsed < vaultDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / vaultDuration;
            float smoothT = t * t * (3f - 2f * t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.7f; // 무조건 0.7m 가볍게 뜀
            _hub.Rb.MovePosition(currentPos);

            yield return new WaitForFixedUpdate();
        }

        _hub.transform.position = targetPos;
        _hub.Rb.useGravity = true;
        if (_hub.Capsule != null) _hub.Capsule.enabled = true;

        Vector3 impulseDir = (_hub.transform.forward + Vector3.down * 0.2f).normalized;
        _hub.Rb.AddForce(impulseDir * postVaultImpulse, ForceMode.Impulse);

        IsVaulting = false;
    }

    // 🚀 [하이 볼트] 윗 벽 잡고 발 차며 누워서 통과하는 로직!
    private IEnumerator HighVaultRoutine(Vector3 targetPos, float apexY)
    {
        IsVaulting = true;
        _hub.Rb.useGravity = false;
        _hub.Rb.linearVelocity = Vector3.zero;
        if (_hub.Capsule != null) _hub.Capsule.enabled = false;

        Vector3 startPos = _hub.transform.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float calculatedDuration = distance / highVaultSpeed;

        // 🎥 카메라 쥬스 호출 (누웠다가 일어나는 그 효과!)
        if (_hub.juiceController != null) _hub.juiceController.TriggerHighVaultJuice(calculatedDuration);

        float elapsed = 0f;

        // [도약의 정점] 시작점과 타겟의 30% 지점에서 이미 최고 높이(모서리)에 도달 (잡는 느낌)
        Vector3 apexPos = startPos + (targetPos - startPos) * 0.3f;
        apexPos.y = apexY + 0.5f; // 모서리보다 몸통을 위로 띄움

        while (elapsed < calculatedDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / calculatedDuration;
            
            // 처음엔 확 솟구치고 뒤로 갈수록 길게 쏘아지는 Ease-Out 곡선
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            // 2차 베지어 곡선으로 '솟구침 -> 누워서 활강' 궤적 생성
            Vector3 m1 = Vector3.Lerp(startPos, apexPos, smoothT);
            Vector3 m2 = Vector3.Lerp(apexPos, targetPos, smoothT);
            Vector3 currentPos = Vector3.Lerp(m1, m2, smoothT);

            _hub.Rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        _hub.transform.position = targetPos;
        _hub.Rb.useGravity = true;
        if (_hub.Capsule != null) _hub.Capsule.enabled = true;

        // 통과 후 발로 찬 탄력을 살려 강하게 앞으로 밀어줌
        _hub.Rb.AddForce(_hub.transform.forward * (postVaultImpulse * 1.5f), ForceMode.Impulse);
        IsVaulting = false;
    }
}