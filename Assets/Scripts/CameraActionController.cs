using UnityEngine;
using System.Collections;

public enum CameraRotationPattern
{
    None,
    Backflip,       // X축 360도
    Cartwheel,      // Z축 360도
    Corkscrew,      // Y축 360도 + X축 약간
    HalfTurn        // Y축 360도 턴
}

public class CameraActionController : MonoBehaviour
{
    [Header("Pattern Weights")]
    public float weightBackflip = 40f;
    public float weightCartwheel = 30f;
    public float weightCorkscrew = 10f;
    public float weightHalfTurn = 20f;
    public float weightNone = 0f;

    [Header("Juice / VFX")]
    public CameraJuiceController juiceController;

    [Header("Descent Limits")]
    public float maxDescentPitch = 45f;
    public float descentLookDuration = 1.5f; // 최대 각도까지 숙이는 데 걸리는 누적 시간

    private Coroutine currentActionCoroutine;
    private Coroutine resetCoroutine;
    private Coroutine landingRollCoroutine;

    private float currentDescentPitch = 0f;
    private float actualDescentPitch = 0f; // 부드러운 Lerp를 위한 변수
    private float descentLookTimer = 0f; // 액션 종료 시점부터 시작되는 타이머

    // 외부(CameraJuiceController)에서 읽어가는 액션 회전값
    // 이 컴포넌트는 자체 transform을 더 이상 건드리지 않음
    public Quaternion ActionRotation { get; private set; } = Quaternion.identity;

    void Awake()
    {
        // ActionRotation 기반으로 전환했으므로 이 트랜스폼은 항등으로 고정
        transform.localRotation = Quaternion.identity;
    }

    // 최고점(Apex) 도달 예상 시간을 받아 랜덤 패턴 트리거 (일반 점프 시 회전 제어)
    public void TriggerRandomPattern(float apexTime, bool isNormalJump = true)
    {
        CameraRotationPattern selectedPattern = GetRandomPattern();
        
        // 일반 점프면 360도 회전 등 강제 뷰 변동 연출 비활성화
        if (isNormalJump) 
        {
            selectedPattern = CameraRotationPattern.None;
        }

        Debug.Log($"[CameraAction] Selected Pattern: {selectedPattern} / Apex Time: {apexTime:F2}s");

        // 기존 코루틴 강제 종료
        if (currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        if (landingRollCoroutine != null) StopCoroutine(landingRollCoroutine);

        descentLookTimer = 0f;

        if (selectedPattern != CameraRotationPattern.None)
        {
            currentActionCoroutine = StartCoroutine(PerformRotation(selectedPattern, apexTime));
        }

        // 시각적(FOV, 화면 왜곡) 타격감 분리 실행 (인지 해킹 타이밍) -> 점프 고도감 위해 펄스는 이지 유지
        if (juiceController != null)
        {
            juiceController.TriggerPulsingEffect(apexTime);
        }
    }

    // 슬라이드 배럴롤 등 명시적인 패턴을 띄울 때 사용
    public void TriggerSpecificPattern(float apexTime, CameraRotationPattern pattern)
    {
        Debug.Log($"[CameraAction] Explicit Pattern Triggered: {pattern} / Apex Time: {apexTime:F2}s");

        if (currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        if (landingRollCoroutine != null) StopCoroutine(landingRollCoroutine);

        descentLookTimer = 0f;

        if (pattern != CameraRotationPattern.None)
        {
            currentActionCoroutine = StartCoroutine(PerformRotation(pattern, apexTime));
        }

        if (juiceController != null)
        {
            juiceController.TriggerPulsingEffect(apexTime);
        }
    }

    // 플레이어 측에서 낙하 상황을 감지해 매 프레임 파라미터를 넘김
    public void UpdateDescent(float airTime, float fallSpeedVy)
    {
        // 공중 액션이나 구르기가 진행 중이 아닐 때만 발동
        if (currentActionCoroutine == null && landingRollCoroutine == null)
        {
            // 액션이 끝난 직후부터 타이머를 누적하여, 무조건 0부터 스스륵 꺾이도록(튀는 현상 방지) 설정
            descentLookTimer += Time.deltaTime;

            float normalizedDanger = Mathf.Clamp01(descentLookTimer / descentLookDuration);
            // 목표 피치 각도
            float targetPitch = Mathf.Lerp(0f, maxDescentPitch, normalizedDanger);

            // Time.deltaTime을 이용한 진짜 Lerp를 통해 딱딱하지 않고 부드럽게 스르륵 숙여지는 연출
            actualDescentPitch = Mathf.Lerp(actualDescentPitch, targetPitch, Time.deltaTime * 2.5f);
            currentDescentPitch = actualDescentPitch; // 구루기 연속성을 위해 동기화

            // 로컬 X축에만 숙임 값을 넣고 Y, Z축은 0f로 단단히 고정하여 다른 회전값과의 수학적 충돌(Jitter)을 원천 차단
            ActionRotation = Quaternion.Euler(actualDescentPitch, 0f, 0f);
        }

        // 공기저항 쉐이크 트리거
        if (juiceController != null)
        {
            juiceController.UpdateDescentShake(airTime, fallSpeedVy);
        }
    }

    public void ResetDescentPitch()
    {
        if (currentActionCoroutine == null && landingRollCoroutine == null)
        {
            // 부드러운 원상복구 로직 실행
            if (resetCoroutine != null) StopCoroutine(resetCoroutine);
            resetCoroutine = StartCoroutine(ResetRotationSmoothly(0.15f));
        }
        
        if (juiceController != null)
        {
            juiceController.UpdateDescentShake(0f, 0f); // 쉐이크 끄기
        }
    }

    // 착지 등의 이유로 스무스하게 즉각 원상 복구 (조기 종료/가벼운 착지 시)
    public void InterruptAction()
    {
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = null;
            
            // 시각적 펄스 이펙트도 즉시 중단
            if (juiceController != null) juiceController.InterruptPulse();
        }

        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(ResetRotationSmoothly(0.15f));
        descentLookTimer = 0f;
        
        if (juiceController != null) juiceController.UpdateDescentShake(0f, 0f);

        Debug.Log("[CameraAction] Interrupted! Resetting rotation or stopping early action.");
    }

    // 긴 에어타임 후 착지 시 구르기 타격감 실행
    public void TriggerLandingRoll()
    {
        if (currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        if (landingRollCoroutine != null) StopCoroutine(landingRollCoroutine);
        
        descentLookTimer = 0f;
        landingRollCoroutine = StartCoroutine(PerformLandingRoll(0.4f)); // 유저 인지를 위해 0.4초로 연장
        
        if (juiceController != null)
        {
            juiceController.UpdateDescentShake(0f, 0f);
            juiceController.TriggerLandingDrop();
            juiceController.TriggerPulsingEffect(0.4f); // 구르기 시간에 맞춘 펄스
        }
    }

    private CameraRotationPattern GetRandomPattern()
    {
        float totalWeight = weightBackflip + weightCartwheel + weightCorkscrew + weightHalfTurn + weightNone;
        float randomVal = Random.Range(0f, totalWeight);

        if (randomVal < weightBackflip) return CameraRotationPattern.Backflip;
        randomVal -= weightBackflip;

        if (randomVal < weightCartwheel) return CameraRotationPattern.Cartwheel;
        randomVal -= weightCartwheel;

        if (randomVal < weightCorkscrew) return CameraRotationPattern.Corkscrew;
        randomVal -= weightCorkscrew;

        if (randomVal < weightHalfTurn) return CameraRotationPattern.HalfTurn;
        
        return CameraRotationPattern.None;
    }

    private IEnumerator PerformRotation(CameraRotationPattern pattern, float duration)
    {
        float elapsed = 0f;

        Vector3 targetEuler = Vector3.zero;

        switch (pattern)
        {
            case CameraRotationPattern.Backflip:
                targetEuler = new Vector3(-360f, 0f, 0f); // X축 백덤블링
                break;
            case CameraRotationPattern.Cartwheel:
                targetEuler = new Vector3(0f, 0f, -360f); // Z축 옆돌기
                break;
            case CameraRotationPattern.Corkscrew:
                targetEuler = new Vector3(360f, 360f, 0f); // 스크류 (X와 Y를 동시에 360도 회전)
                break;
            case CameraRotationPattern.HalfTurn:
                targetEuler = new Vector3(0f, 360f, 0f);  // 피겨스케이팅 스핀처럼 Y축 360도 회전
                break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 아크로바틱의 타격감(Spotting)을 살리기 위해 Cubic EaseOut 적용 (처음에 휙 돌고 최고점에서 스무스하게 멈춤)
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

            // x, y, z 각 채널별로 다이렉트 보간하여 Quaternion 계산
            Vector3 currentEuler = Vector3.LerpUnclamped(Vector3.zero, targetEuler, easeOutT);
            ActionRotation = Quaternion.Euler(currentEuler);

            yield return null;
        }

        ActionRotation = Quaternion.identity;
        currentActionCoroutine = null;
    }

    private IEnumerator PerformLandingRoll(float duration)
    {
        float elapsed = 0f;
        
        // 현재 하강으로 인해 꺾여 있던 각도에서 시작하여 연속성을 제공(시야 튐 방지)
        float startPitch = currentDescentPitch;
        float targetPitch = 360f; // 한 바퀴 덤블링

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 구르기는 빠르게 시작해서 서서히 멎는 느낌 EaseOut
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

            float currentPitch = Mathf.Lerp(startPitch, targetPitch, easeOutT);
            // 구르기 회전값 역시 다이렉트로 X에 꽂아 넣어 짐벌 변환 과정의 충돌 여지를 막음
            ActionRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            yield return null;
        }

        ActionRotation = Quaternion.identity;
        currentDescentPitch = 0f;
        landingRollCoroutine = null;
    }

    private IEnumerator ResetRotationSmoothly(float duration)
    {
        float elapsed = 0f;
        Quaternion startRot = ActionRotation;
        Quaternion targetRot = Quaternion.identity;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Slerp을 통한 구면 선형 보간으로 가장 짧은 패스로 복귀
            ActionRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        
        ActionRotation = targetRot;
        currentDescentPitch = 0f;
        actualDescentPitch = 0f;
        descentLookTimer = 0f;
        resetCoroutine = null;
    }
}
