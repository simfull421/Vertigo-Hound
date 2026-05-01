using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class CameraJuiceController : MonoBehaviour
{
    [Header("Camera Transforms (피벗 분리 핵심)")]
    [Tooltip("상하 회전이 없는 최상위 더미 피벗 (위치 이동 전용)")]
    public Transform positionPivot; 
    
    [Tooltip("실제 화면을 비추는 메인 카메라 (회전 및 FOV 전용)")]
    public Camera mainCamera; 

    [Header("Camera & FOV")]
    public float baseFOV = 68f;

    [Header("URP Post Processing")]
    public Volume postProcessingVolume;

    [Header("Player Reference (허브가 직접 물리 상태를 읽음)")]
    public PlayerController player;
    public CameraActionController actionController;

    [Header("Modules (POCO)")]
    public JuiceSprint sprint = new JuiceSprint();
    public JuiceWallKick wallKick = new JuiceWallKick();
    public JuiceSlide slide = new JuiceSlide();
    public JuiceImpact impact = new JuiceImpact();
    public JuiceStrafeTilt strafeTilt = new JuiceStrafeTilt();
    public JuiceHighVault highVault = new JuiceHighVault();

    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;
    
    // 🚀 핵심: 현재 작동 중인 모듈만 모아두는 대기열
    private HashSet<IJuiceModule> _activeModules = new HashSet<IJuiceModule>();

    void Awake()
    {
        if (positionPivot != null)
        {
            _originalLocalPos = positionPivot.localPosition;
        }

        if (mainCamera != null)
        {
            _originalLocalRot = mainCamera.transform.localRotation;
            mainCamera.fieldOfView = baseFOV;
        }
            
        sprint.Initialize(this);
        wallKick.Initialize(this);
        slide.Initialize(this);
        impact.Initialize(this);
        strafeTilt.Initialize(this);
    }

    public void RegisterModule(IJuiceModule module)
    {
        _activeModules.Add(module);
    }

    public void UnregisterModule(IJuiceModule module)
    {
        _activeModules.Remove(module);
    }

    // ── 단발성 트리거 (이벤트-드리븐 방식으로 플레이어가 찌름) ──
    public void TriggerSlideStart()
    {
        sprint.Suppress(true);
        slide.TriggerSlideStart();
    }

    public void TriggerSlideEnd(bool isJumpHop = false)
    {
        slide.TriggerSlideEnd(isJumpHop);
        sprint.Suppress(false);
        SyncSprintState();
    }
    
    public void TriggerWallKickJuice(float duration) => wallKick.TriggerWallKick(duration);

    public void TriggerSprintStart() => sprint.TriggerSprint(true);
    public void TriggerSprintStop() => sprint.TriggerSprint(false);
    public void TriggerWalkStart() => sprint.TriggerWalk(true);
    public void TriggerWalkStop() => sprint.TriggerWalk(false);
    
    public void TriggerStrafe(float inputX) => strafeTilt.TriggerStrafe(inputX);

    public void TriggerVaultJuice(float duration) => impact.TriggerVaultJuice(duration);
    public void TriggerLandingDrop(float intensity = 1f) => impact.TriggerLandingDrop(intensity);
    public void TriggerPulsingEffect(float duration) => impact.TriggerPulsingEffect(duration);
    public void TriggerBreachJuice(float duration) => impact.TriggerBreachJuice(duration);
    public void TriggerWallAttachJuice(bool isWallRight) => impact.TriggerPulsingEffect(0.15f);
    public void TriggerKickJuice() => impact.TriggerKickJuice();
    public void TriggerActiveLandingRoll() => impact.TriggerActiveLandingRoll();
    public void InterruptPulse() => impact.InterruptPulse();
    
    public void TriggerHighVaultJuice(float duration) => highVault.Trigger(this, duration);

    // ── QTE Trolling Camera Logic ──
    private Coroutine _qteRoutine;
    private Vector3 _qtePosOffset;
    private Quaternion _qteRotOffset = Quaternion.identity;
    private bool _isQTEActive = false;

    public void TriggerQTEPounded(Transform aiTarget)
    {
        if (_qteRoutine != null) StopCoroutine(_qteRoutine);
        _qteRoutine = StartCoroutine(QTEPoundedRoutine(aiTarget));
    }

    public void EndQTEPounded(Transform aiTarget, bool playerWon)
    {
        if (_qteRoutine != null) StopCoroutine(_qteRoutine);
        _qteRoutine = StartCoroutine(QTEEndRoutine(aiTarget, playerWon));
    }

    public void TriggerQTEHitShake(float rollAmount)
    {
        StartCoroutine(QTEHitShakeRoutine(rollAmount));
    }

    private System.Collections.IEnumerator QTEPoundedRoutine(Transform aiTarget)
    {
        _isQTEActive = true;
        float t = 0f;
        while (true) // EndQTEPounded가 호출될 때까지 유지
        {
            t += Time.unscaledDeltaTime * 5f;
            // 바닥으로 -1.5Y 푹 꺼지는 오프셋
            _qtePosOffset = Vector3.Lerp(_qtePosOffset, Vector3.down * 1.5f, Time.unscaledDeltaTime * 10f);
            
            // AI의 상체를 올려다보는 회전값 계산
            if (aiTarget != null)
            {
                Vector3 aiChestPos = aiTarget.position + Vector3.up * 1.5f;
                Vector3 dirToAI = (aiChestPos - mainCamera.transform.position).normalized;
                
                // 원래 카메라 회전을 기준으로, 타겟을 바라보기 위해 필요한 상대 회전(오프셋)을 구합니다.
                // ActionRotation이나 원래 로컬 로테이션이 있으므로, 월드 회전에서 로컬 오프셋을 역산합니다.
                Quaternion targetWorldRot = Quaternion.LookRotation(dirToAI);
                Quaternion currentBaseWorldRot = positionPivot.rotation * _originalLocalRot; 
                Quaternion neededLocalOffset = Quaternion.Inverse(currentBaseWorldRot) * targetWorldRot;

                _qteRotOffset = Quaternion.Slerp(_qteRotOffset, neededLocalOffset, Time.unscaledDeltaTime * 5f);
            }
            yield return null;
        }
    }

    private System.Collections.IEnumerator QTEHitShakeRoutine(float rollAmount)
    {
        Quaternion originalHitRot = _qteRotOffset;
        Quaternion hitRot = originalHitRot * Quaternion.Euler(5f, 0f, rollAmount);
        
        float t = 0f;
        while(t < 0.1f)
        {
            t += Time.unscaledDeltaTime;
            _qteRotOffset = Quaternion.Slerp(originalHitRot, hitRot, t / 0.1f);
            yield return null;
        }
        
        t = 0f;
        while(t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            _qteRotOffset = Quaternion.Slerp(hitRot, originalHitRot, t / 0.15f);
            yield return null;
        }
    }

    private System.Collections.IEnumerator QTEEndRoutine(Transform aiTarget, bool playerWon)
    {
        if (!playerWon && aiTarget != null)
        {
            // 도망가는 AI를 1.5초간 누워서 쳐다봄
            float lookTimer = 0f;
            while (lookTimer < 1.5f)
            {
                lookTimer += Time.deltaTime;
                Vector3 targetChestPos = aiTarget.position + Vector3.up * 1.5f;
                Vector3 dir = (targetChestPos - mainCamera.transform.position).normalized;
                
                Quaternion targetWorldRot = Quaternion.LookRotation(dir);
                Quaternion currentBaseWorldRot = positionPivot.rotation * _originalLocalRot; 
                Quaternion neededLocalOffset = Quaternion.Inverse(currentBaseWorldRot) * targetWorldRot;

                _qteRotOffset = Quaternion.Slerp(_qteRotOffset, neededLocalOffset, Time.deltaTime * 5f);
                yield return null;
            }
        }

        // 기상 연출 (원상복구)
        float getUpTimer = 0f;
        Vector3 startPos = _qtePosOffset;
        Quaternion startRot = _qteRotOffset;

        while (getUpTimer < 0.8f)
        {
            getUpTimer += Time.deltaTime;
            float t = getUpTimer / 0.8f;
            _qtePosOffset = Vector3.Lerp(startPos, Vector3.zero, t);
            _qteRotOffset = Quaternion.Slerp(startRot, Quaternion.identity, t);
            yield return null;
        }

        _qtePosOffset = Vector3.zero;
        _qteRotOffset = Quaternion.identity;
        _isQTEActive = false;
        
        if (player != null && player.movement != null)
        {
            player.enabled = true; // 컨트롤 복구
        }
    }

    public void OnFootstepTriggered(string side)
    {
        sprint.TriggerStep(side); 
    }

    private void SyncSprintState()
    {
        if (player == null || player.Rb == null || player.movement == null || player.InputProv == null) return;

        Vector3 currentXZVelocity = new Vector3(player.Rb.linearVelocity.x, 0f, player.Rb.linearVelocity.z);
        float currentSpeed = currentXZVelocity.magnitude;

        bool isSprinting = player.InputProv.DashHeld && currentSpeed > player.movement.walkSpeed;
        bool isWalking = player.movement.IsGrounded && currentSpeed > 0.1f && !isSprinting;

        sprint.TriggerSprint(isSprinting);
        sprint.TriggerWalk(isWalking);
    }

    void LateUpdate()
    {
        if (mainCamera == null || positionPivot == null) return;

        Vector3 totalPosOffset = Vector3.zero;
        Vector3 totalRotOffset = Vector3.zero;
        float maxFov = baseFOV;
        float totalFovOffset = 0f;

        // 🚀 업그레이드 포인트: 쉬고 있는 모듈은 쳐다보지도 않음!
        foreach (var module in _activeModules)
        {
            totalPosOffset += module.PosOffset;
            totalRotOffset += module.RotOffset;
            
            if (module.FovOverride > maxFov)
            {
                maxFov = module.FovOverride;
            }
            totalFovOffset += module.FovOffset;
        }

        positionPivot.localPosition = _originalLocalPos + totalPosOffset + (_isQTEActive ? _qtePosOffset : Vector3.zero);

        Quaternion actionRot = (actionController != null) ? actionController.ActionRotation : Quaternion.identity;
        Quaternion finalRot = _originalLocalRot * actionRot * Quaternion.Euler(totalRotOffset);
        
        if (_isQTEActive)
        {
            finalRot = finalRot * _qteRotOffset;
        }
        
        mainCamera.transform.localRotation = finalRot;

        mainCamera.fieldOfView = maxFov + totalFovOffset;
    }
}
