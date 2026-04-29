using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class JuiceSprint : IJuiceModule
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
    public float runFov = 96f;
    public float impactAmountY = -0.05f;
    public float impactTiltZ = -1.0f;

    public bool IsActive => _routine != null;
    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }
    public float FovOffset => 0f;

    private float _phaseTime = 0f;
    private float _currentFrequency = 0f;
    private float _currentAmplitudeMult = 0f;

    private float _impulseY = 0f;
    private float _impulseZ = 0f;
    private float _targetImpulseY = 0f;
    private float _targetImpulseZ = 0f;

    private float _baseFov = 90f;

    private bool _isWalking = false;
    private bool _isRunning = false;
    private bool _isSuppressed = false;

    private Coroutine _routine;
    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        _baseFov = hub.baseFOV;
        FovOverride = _baseFov;
    }

    public void TriggerWalk(bool active)
    {
        if (_isSuppressed && active) return;
        _isWalking = active;
        EvaluateRoutine();
    }

    public void TriggerSprint(bool active)
    {
        if (_isSuppressed && active) return;
        _isRunning = active;
        EvaluateRoutine();
    }

    public void TriggerStep(string side)
    {
        if (_isSuppressed) return;
        _targetImpulseY = impactAmountY;
        _targetImpulseZ = (side.ToLower() == "left" ? 1f : -1f) * impactTiltZ;
        EvaluateRoutine();
    }

    public void Suppress(bool suppress)
    {
        _isSuppressed = suppress;

        if (suppress)
        {
            _isWalking = false;
            _isRunning = false;
            _targetImpulseY = 0f;
            _targetImpulseZ = 0f;
        }

        EvaluateRoutine();
    }

    private void EvaluateRoutine()
    {
        // Require running the loop if either moving, or if there's residual impulse or frequency
        if (_routine == null && (_isWalking || _isRunning || _currentFrequency > 0.1f || Mathf.Abs(_targetImpulseY) > 0.01f || Mathf.Abs(_targetImpulseZ) > 0.01f))
        {
            _hub.RegisterModule(this);
            _routine = _hub.StartCoroutine(Routine());
        }
    }

    private IEnumerator Routine()
    {
        while (true)
        {
            float targetFreq = 0f;
            float targetAmpMult = 0f;
            float targetFov = _baseFov;

            // PlayerAnimatorHandler에서 연산된 현재 애니메이션 배속(1.0 ~ 1.6) 가져오기
            float multi = 1f;
            if (_hub.player != null && _hub.player.animatorHandler != null)
            {
                multi = _hub.player.animatorHandler.CurrentMoveMultiplier;
            }

            if (_isRunning)
            {
                targetFreq = runFrequency * multi;     // 속도가 오를수록 주파수(흔들림 속도) 폭발
                targetAmpMult = 1.0f * multi;          // 속도가 오를수록 흔들림 진폭도 같이 증가
                
                // 속도(1.0 ~ 1.6)에 따라 FOV를 base(68)에서 run(96)까지 찢음
                float speedRatio = Mathf.Clamp01((multi - 1.0f) / 0.6f);
                targetFov = Mathf.Lerp(_baseFov, runFov, speedRatio);
            }
            else if (_isWalking)
            {
                targetFreq = walkFrequency * multi;
                targetAmpMult = 0.5f * multi;
            }

            if (_isSuppressed)
            {
                targetFreq = 0f;
                targetAmpMult = 0f;
                targetFov = _baseFov;
                _targetImpulseY = 0f;
                _targetImpulseZ = 0f;
            }

            _currentFrequency = Mathf.Lerp(_currentFrequency, targetFreq, Time.deltaTime * 8f);
            _currentAmplitudeMult = Mathf.Lerp(_currentAmplitudeMult, targetAmpMult, Time.deltaTime * 8f);

            _phaseTime += _currentFrequency * Time.deltaTime;

            float finalX = 0f, finalY = 0f, finalRoll = 0f, finalPitch = 0f;

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
                _phaseTime = Mathf.Lerp(_phaseTime, 0f, Time.deltaTime * 5f);
            }

            _targetImpulseY = Mathf.Lerp(_targetImpulseY, 0f, Time.deltaTime * 10f);
            _targetImpulseZ = Mathf.Lerp(_targetImpulseZ, 0f, Time.deltaTime * 10f);
            _impulseY = Mathf.Lerp(_impulseY, _targetImpulseY, Time.deltaTime * 20f);
            _impulseZ = Mathf.Lerp(_impulseZ, _targetImpulseZ, Time.deltaTime * 20f);

            PosOffset = new Vector3(finalX, finalY + _impulseY, 0f);
            RotOffset = new Vector3(finalPitch, 0f, finalRoll + _impulseZ);
            FovOverride = Mathf.Lerp(FovOverride, targetFov, Time.deltaTime * 8f);

            // 종료 조건: 입력을 멈췄고, 모든 값이 0으로 완벽히 수렴했을 때 (여음 종료)
            if (!_isWalking && !_isRunning && _currentFrequency <= 0.1f && Mathf.Abs(_targetImpulseY) < 0.01f && Mathf.Abs(_targetImpulseZ) < 0.01f && Mathf.Abs(FovOverride - _baseFov) < 0.1f)
            {
                PosOffset = Vector3.zero;
                RotOffset = Vector3.zero;
                FovOverride = _baseFov;
                break;
            }

            yield return null;
        }

        _routine = null;
        _hub.UnregisterModule(this);
    }
}
