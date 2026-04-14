using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class JuiceSlide
{
    [Header("Slide Juice")]
    public float slidePitchAngle = -25f;
    public float slideTiltZ = 5f;        // 슬라이딩 시 랜덤 좌/우 기울기 (도)
    public float slideJuiceSpeed = 15f;
    public float standUpDipPitch = 20f;
    public float standUpRecoverDuration = 0.4f;

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }

    private bool _isSlidingJuice;
    private float _targetSlideDipAmount;
    private float _currentTiltDirection; // 랜덤 좌/우 틸트 방향
    private Coroutine _standUpDipCoroutine;

    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;
    }

    public void TriggerSlideStart(float dipAmount)
    {
        if (_standUpDipCoroutine != null) _hub.StopCoroutine(_standUpDipCoroutine);
        
        _isSlidingJuice = true;
        _targetSlideDipAmount = dipAmount;
        _currentTiltDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f; // 랜덤 방향
    }

    public void TriggerSlideEnd(bool isJumpHop = false)
    {
        _isSlidingJuice = false;
        
        if (_standUpDipCoroutine != null) _hub.StopCoroutine(_standUpDipCoroutine);
        _standUpDipCoroutine = _hub.StartCoroutine(StandUpDipRoutine(isJumpHop));
    }

    public void UpdateModule()
    {
        if (_isSlidingJuice)
        {
            Vector3 targetPos = Vector3.down * _targetSlideDipAmount;
            Vector3 targetRot = new Vector3(slidePitchAngle, 0f, slideTiltZ * _currentTiltDirection);

            float lerpSpeed = Time.deltaTime * slideJuiceSpeed;
            PosOffset = Vector3.Lerp(PosOffset, targetPos, lerpSpeed);
            RotOffset = Vector3.Lerp(RotOffset, targetRot, lerpSpeed);
            FovOverride = Mathf.Lerp(FovOverride, _hub.sprint.maxFOV, lerpSpeed); 
        }
        else if (_standUpDipCoroutine == null)
        {
            // 코루틴도 실행되지 않는 유휴 상태일 때 0으로 롤백
            Vector3 targetPos = Vector3.zero;
            Vector3 targetRot = Vector3.zero;

            float lerpSpeed = Time.deltaTime * slideJuiceSpeed;
            PosOffset = Vector3.Lerp(PosOffset, targetPos, lerpSpeed);
            RotOffset = Vector3.Lerp(RotOffset, targetRot, lerpSpeed);
            FovOverride = Mathf.Lerp(FovOverride, _hub.baseFOV, lerpSpeed);
        }
    }

    private IEnumerator StandUpDipRoutine(bool isJumpHop)
    {
        float elapsed = 0f;
        Vector3 startPos = PosOffset;
        Vector3 startRot = RotOffset;
        
        float targetDipPitch = isJumpHop ? standUpDipPitch * 1.5f : standUpDipPitch;
        float duration = isJumpHop ? standUpRecoverDuration * 0.8f : standUpRecoverDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            float dipT = Mathf.Sin(t * Mathf.PI);
            float currentPitch = Mathf.LerpAngle(0f, targetDipPitch, dipT);
            
            PosOffset = Vector3.Lerp(startPos, Vector3.zero, t);
            
            Vector3 currentRot = Vector3.Lerp(startRot, Vector3.zero, t);
            currentRot.x += currentPitch; // 훅 찍히는 피치(X축)를 추가 가산
            RotOffset = currentRot;
            
            FovOverride = Mathf.Lerp(FovOverride, _hub.baseFOV, t);

            yield return null;
        }

        PosOffset = Vector3.zero;
        RotOffset = Vector3.zero;
        FovOverride = _hub.baseFOV;
        
        _standUpDipCoroutine = null;
    }
}
