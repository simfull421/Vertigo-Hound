using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class JuiceSlide : IJuiceModule
{
    [Header("Slide Juice")]
    public float slidePitchAngle = -25f;
    public float slideTiltZ = 5f;        // 슬라이딩 시 랜덤 좌/우 기울기 (도)
    public float slideJuiceSpeed = 15f;
    public float standUpDipPitch = 20f;
    public float standUpRecoverDuration = 0.4f;

    public bool IsActive => _slideRoutine != null || _standUpDipCoroutine != null;
    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }
    public float FovOffset => 0f;

    private float _currentTiltDirection; 
    private Coroutine _slideRoutine;
    private Coroutine _standUpDipCoroutine;

    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;
    }

    private void EvaluateActiveState()
    {
        if (IsActive)
        {
            _hub.RegisterModule(this);
        }
        else
        {
            _hub.UnregisterModule(this);
            PosOffset = Vector3.zero;
            RotOffset = Vector3.zero;
            FovOverride = _hub.baseFOV;
        }
    }

    public void TriggerSlideStart()
    {
        if (_standUpDipCoroutine != null) 
        {
            _hub.StopCoroutine(_standUpDipCoroutine);
            _standUpDipCoroutine = null;
        }
        if (_slideRoutine != null)
        {
            _hub.StopCoroutine(_slideRoutine);
        }
        
        _currentTiltDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f; 
        
        _slideRoutine = _hub.StartCoroutine(SlideRoutine());
        EvaluateActiveState();
    }

    public void TriggerSlideEnd(bool isJumpHop = false)
    {
        if (_slideRoutine != null) 
        {
            _hub.StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        if (_standUpDipCoroutine != null) _hub.StopCoroutine(_standUpDipCoroutine);
        _standUpDipCoroutine = _hub.StartCoroutine(StandUpDipRoutine(isJumpHop));
        EvaluateActiveState();
    }

    private IEnumerator SlideRoutine()
    {
        while (true)
        {
            float targetPosDrop = 0f;
            Vector3 targetPos = Vector3.zero;

            if (_hub.player != null)
            {
                float dropAmount = _hub.player.Capsule.height < 1.9f
                    ? (2.0f * (1f - _hub.player.slider.slideHeightRatio)) * 0.5f
                    : 0f;

                targetPosDrop = dropAmount;
                // isSliding true일 때는 Z 0.3 앞 기울기
                if (_hub.player.slider.IsSliding)
                {
                    targetPos = new Vector3(0f, -targetPosDrop, 0.3f);
                }
                else
                {
                    targetPos = new Vector3(0f, -targetPosDrop, 0f);
                }
            }

            PosOffset = Vector3.Lerp(PosOffset, targetPos, Time.deltaTime * 12f);

            Vector3 targetRot = new Vector3(slidePitchAngle, 0f, slideTiltZ * _currentTiltDirection);
            float lerpSpeed = Time.deltaTime * slideJuiceSpeed;
            RotOffset = Vector3.Lerp(RotOffset, targetRot, lerpSpeed);
            FovOverride = Mathf.Lerp(FovOverride, _hub.sprint.runFov, lerpSpeed);

            yield return null;
        }
    }

    private IEnumerator StandUpDipRoutine(bool isJumpHop)
    {
        float elapsed = 0f;
        Vector3 startRot = RotOffset;
        
        float targetDipPitch = isJumpHop ? standUpDipPitch * 1.5f : standUpDipPitch;
        float duration = isJumpHop ? standUpRecoverDuration * 0.8f : standUpRecoverDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // 위치 서서히 원복
            PosOffset = Vector3.Lerp(PosOffset, Vector3.zero, Time.deltaTime * 12f);

            float dipT = Mathf.Sin(t * Mathf.PI);
            float currentPitch = Mathf.LerpAngle(0f, targetDipPitch, dipT);
            
            Vector3 currentRot = Vector3.Lerp(startRot, Vector3.zero, t);
            currentRot.x += currentPitch;
            RotOffset = currentRot;
            
            FovOverride = Mathf.Lerp(FovOverride, _hub.baseFOV, t);

            yield return null;
        }

        PosOffset = Vector3.zero;
        RotOffset = Vector3.zero;
        FovOverride = _hub.baseFOV;
        
        _standUpDipCoroutine = null;
        EvaluateActiveState();
    }
}
