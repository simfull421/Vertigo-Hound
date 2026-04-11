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
    
    [Header("Sprint Juice (Head Bob & Acceleration)")]
    public float sprintBobFrequency = 10f;
    public float sprintBobAmplitudeX = 0.9f;
    public float sprintBobAmplitudeY = 0.5f;
    [SerializeField] private float sprintBobTiltZ = 7.0f; // 좌우 비틀림(Roll) 각도
    [SerializeField] private float sprintBobDipX = 4.0f;  // 발 디딜 때 고개 숙임(Pitch) 각도
    public float sprintStartPitchAngle = 15f; 
    public float sprintMaxFOV = 110f;
    public float sprintEffectSpeed = 12f;

    [Header("Descent & Landing Juice")]
    public float maxShakeIntensity = 0.5f;
    public float shakeFrequency = 50f;
    public float landingDropDepth = -0.5f;   // 착지 웅크림 깊이
    public float dropDuration = 0.1f;        // 빠른 하강 (Fast-down)
    public float recoverDuration = 0.3f;     // 느린 복구 (Slow-up)

    private MotionBlur motionBlur;
    private LensDistortion lensDistortion; 
    private ChromaticAberration chromaticAberration;

    private Coroutine pulseCoroutine;
    private Coroutine landingDropCoroutine;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private float sprintBobTimer = 0f;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            originalLocalPos = targetCamera.transform.localPosition;
            originalLocalRot = targetCamera.transform.localRotation;
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

    public void UpdateSprintJuice(float normalizedAccel, bool isGrounded)
    {
        float bobX = 0f;
        float bobY = 0f;
        float bobTiltZ = 0f;
        float bobDipX = 0f;
        float basePitch = 0f;

        // 인간의 생체 역학 비틀림 로직은 땅을 밟고 있을 때만 완전히 구동
        if (isGrounded)
        {
            sprintBobTimer += Time.deltaTime * normalizedAccel;

            // --- 1. 위치 이동 (기존 U자 궤적) ---
            bobX = Mathf.Sin(sprintBobTimer * sprintBobFrequency) * sprintBobAmplitudeX;
            bobY = -Mathf.Cos(sprintBobTimer * sprintBobFrequency * 2f) * sprintBobAmplitudeY;

            // --- 2. 회전 틸트(Roll) & 끄덕임(Pitch) 계산 ---
            bobTiltZ = -Mathf.Sin(sprintBobTimer * sprintBobFrequency) * sprintBobTiltZ;
            bobDipX = Mathf.Cos(sprintBobTimer * sprintBobFrequency * 2f) * sprintBobDipX;

            basePitch = Mathf.Lerp(sprintStartPitchAngle, 0f, normalizedAccel);
        }

        Vector3 targetPos = originalLocalPos + new Vector3(bobX, bobY, 0f) * normalizedAccel;

        // --- 3. 회전 적용 (가속 복구 피치 + 걸음걸이 끄덕임 + 좌우 비틀림) ---
        float finalPitch = basePitch + (bobDipX * normalizedAccel);
        float finalRoll = bobTiltZ * normalizedAccel;

        // isGrounded가 false가 되는 순간, 연산값은 0이 되어 현재 프레임부터 정면(originalLocalRot)으로 부드럽게 Lerp 복구
        Quaternion targetRot = originalLocalRot * Quaternion.Euler(finalPitch, 0f, finalRoll);

        // --- 4. FOV 보간 (어기적거림과 무관하게 속도 기준으로 펌핑 유지) ---
        float targetFOV = Mathf.Lerp(baseFOV, sprintMaxFOV, normalizedAccel);

        if (targetCamera != null)
        {
            targetCamera.transform.localPosition = Vector3.Lerp(targetCamera.transform.localPosition, targetPos, Time.deltaTime * sprintEffectSpeed);
            targetCamera.transform.localRotation = Quaternion.Lerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * sprintEffectSpeed);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, Time.deltaTime * sprintEffectSpeed);
        }
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

    public void TriggerWallAttachJuice(bool isWallRight)
    {
        // 1. 벽에 닿는 순간 화면이 미세하게 흔들림 (카메라 Y축을 순간적으로 살짝 깎음)
        // 2. FOV를 순간적으로 확 넓혔다가 제자리로 돌아오게 하여 충격파 연출
        TriggerPulsingEffect(0.2f); // 기존 구르기 때 썼던 펄스를 재활용!
    }

    public void UpdateWallRunJuice(bool isWallRight, float normalizedAccel)
    {
        // 1. [수정됨] 체중 이동: 오른쪽 벽이면 왼쪽(+15도)으로 꺾어 벽을 밀어냄
        float targetTilt = isWallRight ? 15f : -15f;
        
        // 2. 짐승의 템포: 일반 달리기보다 발구름 속도 2배 가속
        sprintBobTimer += Time.deltaTime * normalizedAccel * 2.0f;

        // 3. [핵심] Lerp가 뭉개지 못하도록 진폭(Amplitude)을 3배로 폭주시킴!
        float bobY = -Mathf.Abs(Mathf.Sin(sprintBobTimer * sprintBobFrequency)) * (sprintBobAmplitudeY * 3f);
        // 벽 반대편으로 몸이 튕겨 나가는 반발력
        float bobX = (isWallRight ? -1f : 1f) * Mathf.Abs(Mathf.Cos(sprintBobTimer * sprintBobFrequency)) * (sprintBobAmplitudeX * 2f);

        // 4. [NEW 추가] 위치 이동만으론 밋밋함. 발을 구를 때마다 고개(Pitch)를 6도씩 밑으로 쾅쾅 처박음!
        float bobPitch = Mathf.Abs(Mathf.Sin(sprintBobTimer * sprintBobFrequency)) * 6f;

        // 5. Transform 적용 (Lerp)
        Vector3 targetPos = originalLocalPos + new Vector3(bobX, bobY, 0f);
        
        // 타겟 로테이션에 피치(찍어누름)와 롤(반대편 틸트)을 동시에 적용
        Quaternion targetRot = originalLocalRot * Quaternion.Euler(bobPitch, 0f, targetTilt);

        if (targetCamera != null) 
        {
            // 카메라 움직임 (이펙트 스피드를 살짝 높여서 더 날카롭게 따라가도록 함)
            float aggressiveLerpSpeed = sprintEffectSpeed * 1.5f; 
            
            targetCamera.transform.localPosition = Vector3.Lerp(targetCamera.transform.localPosition, targetPos, Time.deltaTime * aggressiveLerpSpeed);
            targetCamera.transform.localRotation = Quaternion.Lerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * aggressiveLerpSpeed);
            
            // FOV 펌핑 (기존 유지)
            float targetFOV = Mathf.Lerp(baseFOV, sprintMaxFOV, normalizedAccel);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, Time.deltaTime * aggressiveLerpSpeed);
        }
    }
}
