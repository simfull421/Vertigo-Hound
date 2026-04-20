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
        
        _currentTiltDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f; // 랜덤 방향
    }

    public void TriggerSlideEnd(bool isJumpHop = false)
    {
        if (_standUpDipCoroutine != null) _hub.StopCoroutine(_standUpDipCoroutine);
        _standUpDipCoroutine = _hub.StartCoroutine(StandUpDipRoutine(isJumpHop));
    }

    public void UpdateModule()
    {
        if (_hub.player == null) return;

        // 중앙 관제: 플레이어의 물리 상태를 직접 확인
        bool isSliding = _hub.player.slider.IsSliding;
        bool isCrouching = _hub.player.slider.IsCrouching;

        // ── 1. 위치 오프셋 (PosOffset) — 캡슐 하강량 기반 동적 계산 ──────────────
        // positionPivot은 Pitch 회전이 전혀 없는 더미 노드이므로,
        // localPosition.y를 변경하면 카메라가 완전한 월드 수직으로만 이동함.
        // (Camera.main.localPosition을 건드리면 Pitch에 의해 궤도 운동이 발생했음)
        Vector3 targetPos = Vector3.zero;

        if (isSliding || isCrouching)
        {
            // 캡슐의 실제 축소 데이터에서 하강량을 역산
            // slideHeightRatio만큼 줄어든 캡슐에서 midpoint가 내려가는 양 = dropAmount
            float originalHeight = _hub.player.slider.slideHeightRatio > 0
                ? _hub.player.Capsule.height / _hub.player.slider.slideHeightRatio   // 현재 줄어든 상태라면 역산
                : _hub.player.Capsule.height;
            // 실제로는 StartSlide에서 이미 축소됐으므로 간단히 Capsule 원본값 근사
            // → slider 모듈이 노출한 slideHeightRatio와 원본 캡슐 높이(2.0 고정)으로 계산
            float dropAmount = _hub.player.Capsule.height < 1.9f
                ? (2.0f * (1f - _hub.player.slider.slideHeightRatio)) * 0.5f   // 이미 앉은 상태
                : 0f;

            if (isSliding)
                targetPos = new Vector3(0f, -dropAmount, 0.3f);   // 슬라이딩: Z 0.3 앞 기울기
            else
                targetPos = new Vector3(0f, -dropAmount, 0f);     // 앉기: 순수 하강만
        }

        PosOffset = Vector3.Lerp(PosOffset, targetPos, Time.deltaTime * 12f);



        // ── 2. 회전 및 FOV 오프셋 (슬라이딩 전용 쥬스) ──
        if (isSliding)
        {
            Vector3 targetRot = new Vector3(slidePitchAngle, 0f, slideTiltZ * _currentTiltDirection);

            float lerpSpeed = Time.deltaTime * slideJuiceSpeed;
            RotOffset = Vector3.Lerp(RotOffset, targetRot, lerpSpeed);
            FovOverride = Mathf.Lerp(FovOverride, _hub.sprint.runFov, lerpSpeed); 
        }
        else if (_standUpDipCoroutine == null)
        {
            // 코루틴도 실행되지 않는 유휴 상태일 때 0으로 롤백
            Vector3 targetRot = Vector3.zero;

            float lerpSpeed = Time.deltaTime * slideJuiceSpeed;
            RotOffset = Vector3.Lerp(RotOffset, targetRot, lerpSpeed);
            FovOverride = Mathf.Lerp(FovOverride, _hub.baseFOV, lerpSpeed);
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
            
            float dipT = Mathf.Sin(t * Mathf.PI);
            float currentPitch = Mathf.LerpAngle(0f, targetDipPitch, dipT);
            
            Vector3 currentRot = Vector3.Lerp(startRot, Vector3.zero, t);
            currentRot.x += currentPitch; // 훅 찍히는 피치(X축)를 추가 가산
            RotOffset = currentRot;
            
            FovOverride = Mathf.Lerp(FovOverride, _hub.baseFOV, t);

            yield return null;
        }

        RotOffset = Vector3.zero;
        FovOverride = _hub.baseFOV;
        
        _standUpDipCoroutine = null;
    }
}
