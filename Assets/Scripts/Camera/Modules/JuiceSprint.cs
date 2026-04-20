using UnityEngine;
using System;

[Serializable]
public sealed class JuiceSprint
{
    [Header("Headbob Speed (물리 속도와 완전 분리)")]
    [Tooltip("걷기 상태일 때의 헤드밥 고정 속도")]
    public float walkFrequency = 6.0f;
    [Tooltip("달리기 상태일 때의 헤드밥 고정 속도")]
    public float runFrequency = 10.0f;

    [Header("Trajectory Amplitudes (4대 궤적 진폭)")]
    public float amplitudeX = 0.05f;
    public float amplitudeY = 0.1f;
    public float amplitudeRoll = 1.5f;
    public float amplitudePitch = 1.0f;

    [Header("FOV & Event (최소한의 필수 설정)")]
    public float runFov = 105f;
    public float impactAmountY = -0.05f;
    public float impactTiltZ = -1.0f;

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }

    private float _phaseTime = 0f;
    private float _currentFrequency = 0f;
    private float _currentAmplitudeMult = 0f; // 걷기(0.5) vs 달리기(1.0) 보간용

    private float _impulseY = 0f;
    private float _impulseZ = 0f;
    private float _targetImpulseY = 0f;
    private float _targetImpulseZ = 0f;

    private float _baseFov = 90f; // 초기화 시 받아옴

    public void Initialize(CameraJuiceController hub)
    {
        _baseFov = hub.baseFOV;
        FovOverride = _baseFov;
    }

    public void UpdateModule(bool isWalking, bool isRunning, float currentSpeed, float maxSpeed)
    {
        // 1. 상태에 따른 목표 수치 설정 (currentSpeed 의존성 완벽 제거)
        float targetFreq = 0f;
        float targetAmpMult = 0f;
        float targetFov = _baseFov;

        if (isRunning)
        {
            targetFreq = runFrequency;
            targetAmpMult = 1.0f; // 달리기는 진폭 100%
            targetFov = runFov;
        }
        else if (isWalking)
        {
            targetFreq = walkFrequency;
            targetAmpMult = 0.5f; // 걷기는 진폭 50%
        }

        // 2. 부드러운 전환 보간
        _currentFrequency = Mathf.Lerp(_currentFrequency, targetFreq, Time.deltaTime * 8f);
        _currentAmplitudeMult = Mathf.Lerp(_currentAmplitudeMult, targetAmpMult, Time.deltaTime * 8f);

        // 3. 기저 위상 함수(Phase) 계산 (여기서 currentSpeed가 사라졌습니다!)
        _phaseTime += _currentFrequency * Time.deltaTime;

        float finalX = 0f, finalY = 0f, finalRoll = 0f, finalPitch = 0f;

        // 이동 중이거나 멈출 때의 여음(Frequency가 0.1 이상)이 남아있을 때 연산
        if (_currentFrequency > 0.1f)
        {
            float pT = Mathf.Sin(_phaseTime);

            finalX = (pT * amplitudeX) * _currentAmplitudeMult;
            finalY = (-Mathf.Abs(pT) * amplitudeY) * _currentAmplitudeMult;
            finalRoll = (-pT * amplitudeRoll) * _currentAmplitudeMult;
            finalPitch = (Mathf.Abs(pT) * amplitudePitch) * _currentAmplitudeMult;
        }
        else
        {
            // 완전히 멈추면 위상 초기화
            _phaseTime = Mathf.Lerp(_phaseTime, 0f, Time.deltaTime * 5f);
        }

        // 4. 발소리 타격감 제어 (기존의 복잡한 이중 Lerp 단순화)
        _targetImpulseY = Mathf.Lerp(_targetImpulseY, 0f, Time.deltaTime * 10f);
        _targetImpulseZ = Mathf.Lerp(_targetImpulseZ, 0f, Time.deltaTime * 10f);
        _impulseY = Mathf.Lerp(_impulseY, _targetImpulseY, Time.deltaTime * 20f);
        _impulseZ = Mathf.Lerp(_impulseZ, _targetImpulseZ, Time.deltaTime * 20f);

        // 5. 최종 출력
        PosOffset = new Vector3(finalX, finalY + _impulseY, 0f);
        RotOffset = new Vector3(finalPitch, 0f, finalRoll + _impulseZ);
        
        FovOverride = Mathf.Lerp(FovOverride, targetFov, Time.deltaTime * 8f);
    }

    public void TriggerStep(string side)
    {
        _targetImpulseY = impactAmountY;
        _targetImpulseZ = (side.ToLower() == "left" ? 1f : -1f) * impactTiltZ;
    }
}