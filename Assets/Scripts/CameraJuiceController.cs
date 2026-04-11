using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class CameraJuiceController : MonoBehaviour
{
    [Header("Camera Field of View")]
    public Camera targetCamera;
    public float baseFOV = 90f;
    public float burstFOV = 115f;
    
    [Tooltip("펄스 타격감을 제어하는 커브. 0에서 치솟았다가 스르륵 0으로 가라앉는 형태가 이상적입니다.")]
    public AnimationCurve fovPulseCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));

    [Header("URP Post Processing")]
    public Volume postProcessingVolume;
    
    [Header("Descent & Landing Juice")]
    public float maxShakeIntensity = 0.5f;
    public float shakeFrequency = 50f;
    public float landingDropDepth = -0.5f;   // 착지 웅크림 깊이
    public float dropDuration = 0.1f;        // 빠른 하강 (Fast-down)
    public float recoverDuration = 0.4f;     // 느린 복구 (Slow-up)

    private MotionBlur motionBlur;
    private LensDistortion lensDistortion; 
    private ChromaticAberration chromaticAberration;

    private Coroutine pulseCoroutine;
    private Coroutine landingDropCoroutine;
    private Vector3 originalLocalPos;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            originalLocalPos = targetCamera.transform.localPosition;
        }

        if (postProcessingVolume != null && postProcessingVolume.profile != null)
            CacheVolumeComponents();
    }

    private void CacheVolumeComponents()
    {
        postProcessingVolume.profile.TryGet(out motionBlur);
        postProcessingVolume.profile.TryGet(out lensDistortion);
        postProcessingVolume.profile.TryGet(out chromaticAberration);
    }

    public void TriggerPulsingEffect(float apexDuration)
    {
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine(apexDuration));
    }

    private IEnumerator PulseRoutine(float duration)
    {
        float elapsed = 0f;

        if (motionBlur != null) motionBlur.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;
        if (chromaticAberration != null) chromaticAberration.intensity.value = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            float curveValue = fovPulseCurve.Evaluate(normalizedTime);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.LerpUnclamped(baseFOV, burstFOV, curveValue);
            }

            if (motionBlur != null) motionBlur.intensity.Override(Mathf.Lerp(0f, 1f, curveValue)); 
            if (lensDistortion != null) lensDistortion.intensity.Override(Mathf.Lerp(0f, -0.4f, curveValue));
            if (chromaticAberration != null) chromaticAberration.intensity.Override(Mathf.Lerp(0f, 1f, curveValue));

            yield return null;
        }

        if (targetCamera != null) targetCamera.fieldOfView = baseFOV;
        if (motionBlur != null) motionBlur.intensity.Override(0f);
        if (lensDistortion != null) lensDistortion.intensity.Override(0f);
        if (chromaticAberration != null) chromaticAberration.intensity.Override(0f);

        pulseCoroutine = null;
    }

    // 낙하 시 공중에 떠있는 시간과 떨어지는 속도에 비례해서 Perlin Noise 카메라 쉐이크 생성 (공기 저항)
    public void UpdateDescentShake(float airTime, float fallSpeedVy)
    {
        // 일반 점프 시 덜덜 떨리는 현상을 막기 위해 1.0초 이상 공중에 떠있을 때만 쉐이크 발동
        if (airTime >= 1.0f && fallSpeedVy < -5f && landingDropCoroutine == null)
        {
            // 속도가 빨라질수록 쉐이크 강도가 높아짐 (예: -30f 근처가 최대치)
            float speedFactor = Mathf.Clamp01((Mathf.Abs(fallSpeedVy) - 5f) / 25f); 
            float intensity = speedFactor * maxShakeIntensity;
            
            float shakeX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f * intensity;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f * intensity;

            if (targetCamera != null)
                targetCamera.transform.localPosition = originalLocalPos + new Vector3(shakeX, shakeY, 0f);
        }
        else if (landingDropCoroutine == null)
        {
            // 쉐이크가 없는 상태일 때는 스무스하게 원래 위치로 복구
            if (targetCamera != null)
                targetCamera.transform.localPosition = Vector3.Lerp(targetCamera.transform.localPosition, originalLocalPos, Time.deltaTime * 10f);
        }
    }

    // 착지 시 빠른 하강 - 느린 복구 웅크림 연출
    public void TriggerLandingDrop()
    {
        if (landingDropCoroutine != null) StopCoroutine(landingDropCoroutine);
        landingDropCoroutine = StartCoroutine(LandingDropRoutine());
    }

    private IEnumerator LandingDropRoutine()
    {
        float elapsed = 0f;
        Vector3 startPos = targetCamera.transform.localPosition;
        Vector3 dropPos = originalLocalPos + new Vector3(0f, landingDropDepth, 0f);

        // 1. 착지 하중 (무릎 굽힘) - 매우 빠르게 하강
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;
            // 팍! 꽂히는 타격감을 위해 EaseOut 사용
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
            
            if (targetCamera != null)
                targetCamera.transform.localPosition = Vector3.Lerp(startPos, dropPos, easeOutT);
            
            yield return null;
        }
        
        if (targetCamera != null) targetCamera.transform.localPosition = dropPos;
        elapsed = 0f;

        // 2. 하중 복구 (무릎 폄) - 서서히 복귀
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recoverDuration;
            // 스르륵 일어나는 자연스러움을 위해 EaseInOut 사용
            float smoothT = t * t * (3f - 2f * t);
            
            if (targetCamera != null)
                targetCamera.transform.localPosition = Vector3.Lerp(dropPos, originalLocalPos, smoothT);
            
            yield return null;
        }

        if (targetCamera != null) targetCamera.transform.localPosition = originalLocalPos;
        landingDropCoroutine = null;
    }

    public void InterruptPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        
        if (targetCamera != null) targetCamera.fieldOfView = baseFOV;
        if (motionBlur != null) motionBlur.intensity.Override(0f);
        if (lensDistortion != null) lensDistortion.intensity.Override(0f);
        if (chromaticAberration != null) chromaticAberration.intensity.Override(0f);
    }
}
