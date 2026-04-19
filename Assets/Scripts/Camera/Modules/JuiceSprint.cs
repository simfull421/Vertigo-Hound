using UnityEngine;
using System;

[Serializable]
public sealed class JuiceSprint
{
    [Header("진폭 설정 (Animation Event Driven)")]
    public float dipAmount = 0.04f;      // 발이 닿을 때 아래로 툭 떨어지는 정도
    public float tiltAmount = 1.2f;     // 발이 닿을 때 좌우로 틸트되는 정도
    public float horizontalSway = 0.05f; // 좌우로 흔들리는 정도
    
    [Header("복원 속도 (Recovery)")]
    public float recoverySpeed = 10f;    // 원래 위치로 돌아오려는 탄성

    [Header("FOV 설정")]
    public float walkFOV = 90f;
    public float maxFOV = 110f;

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }

    private float _stepTime;
    private bool _isLeft;
    private float _smoothedRatio;
    private Vector3 _targetPosOffset;
    private Vector3 _targetRotOffset;

    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;
    }

    /// <summary>
    /// 애니메이션 이벤트 'PlayFootstep'이 트리거될 때 호출됩니다.
    /// </summary>
    public void TriggerStep(string side)
    {
        _isLeft = (side == "Left");
        
        // 1. 발이 닿는 순간의 충격량(Impulse)을 즉시 적용
        // 아래로 툭(Y), 좌우로 휙(X/Z)
        float xSway = _isLeft ? -horizontalSway : horizontalSway;
        _targetPosOffset = new Vector3(xSway, -dipAmount, 0f);

        float zTilt = _isLeft ? tiltAmount : -tiltAmount;
        _targetRotOffset = new Vector3(dipAmount * 20f, 0f, zTilt); // 약간의 Pitch 포함
    }

    /// <summary>
    /// 매 프레임 호출되어 오프셋을 부드럽게 복원시킵니다.
    /// </summary>
    public void UpdateModule(bool isWalking, bool isRunning, float currentSpeed, float maxSpeed)
    {
        // 1. 상태 계수 (FOV 보간용)
        float targetSpeedRatio = 0f;
        if (isRunning || isWalking)
        {
            targetSpeedRatio = maxSpeed > 0f ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
        }
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, targetSpeedRatio, Time.deltaTime * 6f);

        // 2. [핵심] 타겟 오프셋을 항상 제로(0,0,0)로 수렴시킵니다 (탄성 복원)
        _targetPosOffset = Vector3.Lerp(_targetPosOffset, Vector3.zero, Time.deltaTime * recoverySpeed);
        _targetRotOffset = Vector3.Lerp(_targetRotOffset, Vector3.zero, Time.deltaTime * recoverySpeed);

        // 3. 현재 오프셋에 적용
        PosOffset = Vector3.Lerp(PosOffset, _targetPosOffset, Time.deltaTime * recoverySpeed * 2f);
        RotOffset = Vector3.Lerp(RotOffset, _targetRotOffset, Time.deltaTime * recoverySpeed * 2f);

        // 4. FOV 스무딩
        FovOverride = Mathf.Lerp(walkFOV, maxFOV, _smoothedRatio);
    }
}