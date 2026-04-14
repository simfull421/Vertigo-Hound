using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System;

[Serializable]
public sealed class JuiceImpact
{
    [Header("Descent Shake")]
    public float descentShakeMultiplier = 0.05f;
    public float maxDescentShake = 0.5f;

    [Header("Landing & Drop")]
    public float maxLandingDrop = 1.5f;
    public float dropDuration = 0.15f;
    public float recoverDuration = 0.35f;

    [Header("Vault Juice")]
    public float vaultDipDepth = -0.4f;
    public float vaultTiltAngle = 20f; // 랜덤 액션을 위한 강한 오프셋 보강 (20도로 상향)
    
    [Header("Pulse Juice (Dash/Attach)")]
    public float burstFOV = 115f;
    public AnimationCurve fovCurve;

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOffset { get; private set; }

    private Coroutine _vaultJuiceCoroutine;
    private Coroutine _landingDropCoroutine;
    private Coroutine _pulseCoroutine;

    private CameraJuiceController _hub;
    
    private MotionBlur _motionBlur;
    private LensDistortion _lensDistortion;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        
        if (_hub.postProcessingVolume != null && _hub.postProcessingVolume.profile != null)
        {
            _hub.postProcessingVolume.profile.TryGet(out _motionBlur);
            _hub.postProcessingVolume.profile.TryGet(out _lensDistortion);
        }
    }

    public void UpdateModule()
    {
        // 지속 효과가 필요한 로직을 여기서 돌립니다.
        // 강하 쉐이크는 이벤트 기반으로 들어오므로 대기합니다.
    }

    public void UpdateDescentShake(float airTime, float yVelocity)
    {
        // 공중 체공 흔들림(Descent Shake) 로직 완전히 삭제 (Hotfix)
    }

    public void TriggerVaultJuice(float duration)
    {
        if (_vaultJuiceCoroutine != null) _hub.StopCoroutine(_vaultJuiceCoroutine);
        _vaultJuiceCoroutine = _hub.StartCoroutine(VaultJuiceRoutine(duration));
    }

    private IEnumerator VaultJuiceRoutine(float duration)
    {
        float targetRoll = UnityEngine.Random.value > 0.5f ? vaultTiltAngle : -vaultTiltAngle;
        
        // 3단계: 부드럽게 꺾이고 → 유지 → 부드럽게 복귀
        float tiltInTime = 0.3f;       // 틸트 진입 (부드럽게)
        float recoverTime = 0.25f;     // 틸트 복귀
        float holdTime = Mathf.Max(0f, duration - tiltInTime - recoverTime); // 중간 유지

        float elapsed = 0f;

        // Phase 1: 부드럽게 꺾임
        while (elapsed < tiltInTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tiltInTime);
            float curveT = t * t * (3f - 2f * t); // SmoothStep

            PosOffset = new Vector3(0f, Mathf.Lerp(0f, vaultDipDepth, curveT), 0f);
            float currentRoll = Mathf.Lerp(0f, targetRoll, curveT);
            float currentPitch = Mathf.Lerp(0f, 5f, curveT);
            RotOffset = new Vector3(currentPitch, 0f, currentRoll);

            yield return null;
        }

        // Phase 2: 볼트 도중 유지
        if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }

        // Phase 3: 부드럽게 복귀
        elapsed = 0f;
        Vector3 peakPos = PosOffset;
        Vector3 peakRot = RotOffset;

        while (elapsed < recoverTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoverTime);
            float curveT = t * t * (3f - 2f * t); // SmoothStep

            PosOffset = Vector3.Lerp(peakPos, Vector3.zero, curveT);
            RotOffset = Vector3.Lerp(peakRot, Vector3.zero, curveT);

            yield return null;
        }

        PosOffset = Vector3.zero;
        RotOffset = Vector3.zero;
        _vaultJuiceCoroutine = null;
    }

    public void TriggerLandingDrop(float intensityMultiplier = 1f)
    {
        if (_landingDropCoroutine != null) _hub.StopCoroutine(_landingDropCoroutine);
        
        float dropTarget = Mathf.Clamp(maxLandingDrop * intensityMultiplier, 0.5f, maxLandingDrop);
        _landingDropCoroutine = _hub.StartCoroutine(LandingDropRoutine(dropTarget));
    }

    private IEnumerator LandingDropRoutine(float dropTarget)
    {
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;
            PosOffset = Vector3.Lerp(Vector3.zero, Vector3.down * dropTarget, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recoverDuration;
            PosOffset = Vector3.Lerp(Vector3.down * dropTarget, Vector3.zero, t);
            yield return null;
        }

        PosOffset = Vector3.zero;
        _landingDropCoroutine = null;
    }

    public void TriggerPulsingEffect(float duration)
    {
        if (_pulseCoroutine != null) _hub.StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = _hub.StartCoroutine(PulseRoutine(duration));
    }

    private IEnumerator PulseRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = (fovCurve != null && fovCurve.length > 0) ? fovCurve.Evaluate(t) : Mathf.Sin(t * Mathf.PI);

            // Additive를 위해 BaseFOV 대비 얼마나 치솟을 것인지 계산
            FovOffset = Mathf.LerpUnclamped(0f, burstFOV - _hub.baseFOV, curveValue);

            if (_motionBlur != null) _motionBlur.intensity.value = Mathf.Lerp(0f, 1f, curveValue);
            if (_lensDistortion != null) _lensDistortion.intensity.value = Mathf.Lerp(0f, -0.5f, curveValue);

            yield return null;
        }

        FovOffset = 0f;
        if (_motionBlur != null) _motionBlur.intensity.value = 0f;
        if (_lensDistortion != null) _lensDistortion.intensity.value = 0f;
        _pulseCoroutine = null;
    }

    public void InterruptPulse()
    {
        if (_pulseCoroutine != null)
        {
            _hub.StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
        FovOffset = 0f;
        if (_motionBlur != null) _motionBlur.intensity.value = 0f;
        if (_lensDistortion != null) _lensDistortion.intensity.value = 0f;
    }
}
