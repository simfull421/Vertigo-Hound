using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ViewmodelGunController : MonoBehaviour
{
    [Header("Components")]
    public Animator gunAnimator;
    public Transform mainCameraTransform;

    [Header("Gun Stats")]
    public int maxAmmo = 15;
    public int currentAmmo;
    public float fireRate = 0.2f;
    public float reloadTime = 1.5f;
    public float damage = 10f;
    public float headshotMultiplier = 2.5f;
    public float range = 100f;
    public LayerMask hitMask = ~0;
    
    [Header("Auto-Align ADS (Dynamic)")]
    public Transform weaponRoot;       
    public Transform sightNode;        
    public Transform cameraCenter;     
    public Camera viewmodelCamera;    
    public float adsSpeed = 15f;
    public float adsFOV = 35f;      

    [Header("Gameplay Mechanics")]
    public float requiredMoveSpeed = 3.0f;

    [Header("Sway (관성 흔들림)")]
    public float swayAmount = 0.02f;
    public float maxSwayAmount = 0.06f;
    public float swaySmooth = 6f;

    [Header("Procedural Recoil (절차적 반동)")]
    [Tooltip("총 쏠 때 뒤로 밀리는 힘")]
    public Vector3 recoilKickPos = new Vector3(0, 0, -0.1f);
    [Tooltip("총 쏠 때 위로 들리는 힘 (X축 회전)")]
    public Vector3 recoilKickRot = new Vector3(-5f, 0, 0); 
    public float recoilSnappiness = 20f;  // 반동이 튀어오르는 속도 (빠를수록 타격감 증가)
    public float recoilReturnSpeed = 10f; // 원래 자리로 돌아오는 속도

    [Header("Ballistics")]
    [Tooltip("집탄율 (높을수록 널리 퍼짐)")]
    public float spreadAngle = 0.02f;
    [Tooltip("피격 시 물리적 밀어내는 힘")]
    public float totalForce = 50f;

    [Header("Camera Recoil")]
    [Tooltip("사격 시 카메라 위로 튀는 각도")]
    public float cameraRecoilPitch = 1.5f;

    [Header("Tracer")]
    public LineRenderer tracerPrefab;
    public int tracerPoolSize = 12;
    public int tracerMaxPoolSize = 24;
    public float tracerDuration = 0.05f;
    public Transform tracerOrigin;

    // 내부 상태 변수들
    private float _nextFireTime;
    private bool _isReloading = false;
    private bool _isAiming;
    private float _defaultFOV;      
    private Vector3 _hipLocalPos; 
    private Quaternion _hipLocalRot;
    private float _adsWeight = 0f; 
    private Vector3 _currentSway;
    private PlayerController _hub;

    // 반동 연산용 변수
    private Vector3 _recoilTargetPos;
    private Vector3 _recoilCurrentPos;
    private Vector3 _recoilTargetRot;
    private Vector3 _recoilCurrentRot;
    private readonly Queue<LineRenderer> _tracerPool = new Queue<LineRenderer>();
    private int _tracerInstanceCount = 0;

    // [수정] Fire 해시는 이제 애니메이터에서 안 쓰므로 지웠습니다.
    private readonly int hashReload = Animator.StringToHash("TriggerReload");
    
    // [방어] gunAnim.Update(0f) 제거 대체: SetActive 이후 첫 LateUpdate에서 Idle 포즈를 재캡처합니다.
    private bool _needsHipPoseCapture = false;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
        currentAmmo = maxAmmo;

        // [방어] gunAnimator와 World Model animator가 같은 오브젝트를 가리키면 즉시 경고
        if (_hub != null && gunAnimator != null && _hub.animatorHandler.bodyAnimators != null)
        {
            foreach (var worldAnim in _hub.animatorHandler.bodyAnimators)
            {
                if (worldAnim != null && gunAnimator == worldAnim)
                {
                    Debug.LogError("[ViewmodelGunController] ⛔ gunAnimator가 PlayerAnimatorHandler.bodyAnimators에 포함되어 있습니다!\n" +
                                   "이 상태에서는 World Model의 Blend Tree가 GunUpper에 재생됩니다.\n" +
                                   "인스펙터에서 gunAnimator를 GunUpper 전용 Animator로 교체하세요.");
                    break;
                }
            }
        }
        
        // 초기 hip 포즈 캡처 (이 시점은 SetActive 직후라 T-Pose일 수 있음)
        // _needsHipPoseCapture 플래그를 세워서 첫 LateUpdate에서 Animator가 Idle 포즈를 잡은 뒤 재캡처합니다.
        _hipLocalPos = weaponRoot != null ? weaponRoot.localPosition : Vector3.zero;
        _hipLocalRot = weaponRoot != null ? weaponRoot.localRotation : Quaternion.identity;
        _needsHipPoseCapture = true; // 다음 LateUpdate에서 Idle 포즈 확정 후 재캡처

        if (viewmodelCamera != null) _defaultFOV = viewmodelCamera.fieldOfView;

        InitializeTracerPool();
    }

    public void UpdateModule()
    {
        if (_hub == null || _hub.animatorHandler.currentWeaponType != 1) return;

        bool isFiring = _hub.InputProv.FireHeld; 
        _isAiming = _hub.InputProv.AimHeld; 
        bool isReloadingInput = _hub.InputProv.ReloadTriggered; 
        Vector2 lookDelta = _hub.InputProv.LookInput; 
        float playerCurrentSpeed = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).magnitude;

        // 1. ADS 가중치 계산 및 FOV 처리
        _adsWeight = Mathf.MoveTowards(_adsWeight, _isAiming ? 1f : 0f, Time.deltaTime * adsSpeed);
        if (viewmodelCamera != null) viewmodelCamera.fieldOfView = Mathf.Lerp(_defaultFOV, adsFOV, _adsWeight);

        // 2. Sway 계산
        float swayMultiplier = 1f - _adsWeight; 
        float targetMoveX = Mathf.Clamp(-lookDelta.x * swayAmount, -maxSwayAmount, maxSwayAmount) * swayMultiplier;
        float targetMoveY = Mathf.Clamp(-lookDelta.y * swayAmount, -maxSwayAmount, maxSwayAmount) * swayMultiplier;
        _currentSway = Vector3.Lerp(_currentSway, new Vector3(targetMoveX, targetMoveY, 0), Time.deltaTime * swaySmooth);

        // 3. 반동 복구 연산 (목표치를 항상 0으로 되돌림)
        _recoilTargetPos = Vector3.Lerp(_recoilTargetPos, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
        _recoilTargetRot = Vector3.Lerp(_recoilTargetRot, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
        _recoilCurrentPos = Vector3.Lerp(_recoilCurrentPos, _recoilTargetPos, Time.deltaTime * recoilSnappiness);
        _recoilCurrentRot = Vector3.Lerp(_recoilCurrentRot, _recoilTargetRot, Time.deltaTime * recoilSnappiness);

        if (_isReloading) return;

        if (isReloadingInput && currentAmmo < maxAmmo)
        {
            if (playerCurrentSpeed >= requiredMoveSpeed) StartCoroutine(ReloadRoutine());
        }

        if (isFiring && Time.time >= _nextFireTime)
        {
            if (!_isAiming) return; 

            if (currentAmmo > 0) Shoot();
            else if (playerCurrentSpeed >= requiredMoveSpeed) StartCoroutine(ReloadRoutine());
        }
    }

    public void LateUpdateModule()
    {
        if (_hub == null || _hub.animatorHandler.currentWeaponType != 1) return;

        // [방어] Initialize 직후(SetActive 직후)에는 Animator가 아직 T-Pose 상태일 수 있습니다.
        // 첫 LateUpdate 진입 시점(Animator가 Idle 포즈를 확정한 뒤)에 weaponRoot 포즈를 재캡처합니다.
        if (_needsHipPoseCapture && weaponRoot != null)
        {
            _hipLocalPos = weaponRoot.localPosition;
            _hipLocalRot = weaponRoot.localRotation;
            _needsHipPoseCapture = false;
        }

        if (_isAiming)
        {
            Quaternion rotOffset = cameraCenter.rotation * Quaternion.Inverse(sightNode.rotation);
            Quaternion targetRot = rotOffset * weaponRoot.rotation;
            Vector3 targetPos = weaponRoot.position + (cameraCenter.position - sightNode.position);

            // [정조준 고정] 99% 줌 완료 시 럴프 끄고 강제 락인(Lock-in)
            if (_adsWeight >= 0.99f)
            {
                weaponRoot.rotation = targetRot;
                weaponRoot.position = targetPos;
            }
            else
            {
                weaponRoot.rotation = Quaternion.Slerp(weaponRoot.rotation, targetRot, Time.deltaTime * adsSpeed);
                weaponRoot.position = Vector3.Lerp(weaponRoot.position, targetPos, Time.deltaTime * adsSpeed);
            }
        }
        else
        {
            // [노줌 고정] 줌아웃 완료 시 럴프 끄고 힙파이어 좌표에 강제 락인(Lock-in)
            if (_adsWeight <= 0.01f)
            {
                weaponRoot.localPosition = _hipLocalPos;
                weaponRoot.localRotation = _hipLocalRot;
            }
            else
            {
                weaponRoot.localPosition = Vector3.Lerp(weaponRoot.localPosition, _hipLocalPos, Time.deltaTime * adsSpeed);
                weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, _hipLocalRot, Time.deltaTime * adsSpeed);
            }
        }

        // 최종 더하기: Sway(마우스 관성) + Recoil(코딩 반동)
        weaponRoot.localPosition += _currentSway + _recoilCurrentPos;
        // 총구가 위로 들리는 회전 반동 추가
        weaponRoot.localRotation *= Quaternion.Euler(_recoilCurrentRot);
    }

    private void Shoot()
    {
        currentAmmo--;
        _nextFireTime = Time.time + fireRate;
        
        // 절차적 반동(Kick)
        _recoilTargetPos += recoilKickPos;
        _recoilTargetRot += recoilKickRot;

        if (_hub != null)
        {
            _hub.movement.AddRecoilPitch(cameraRecoilPitch);
        }

        // 총구 방향(forward)에 랜덤한 퍼짐(Spread) 값 추가
        Vector3 spread = UnityEngine.Random.insideUnitSphere * spreadAngle;
        if (mainCameraTransform == null) return;

        Vector3 shootDirection = (mainCameraTransform.forward + spread).normalized;
        Vector3 rayOrigin = mainCameraTransform.position;
        Vector3 tracerEnd = rayOrigin + shootDirection * range;

        if (Physics.Raycast(rayOrigin, shootDirection, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            tracerEnd = hit.point;

            HitZone hitZone = ResolveHitZone(hit.collider);
            float finalDamage = damage * (hitZone == HitZone.Head ? headshotMultiplier : 1f);

            EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
            EnemyRagdollHandler ragdoll = hit.collider.GetComponentInParent<EnemyRagdollHandler>();
            Rigidbody hitBone = hit.collider.attachedRigidbody;

            if (health != null)
            {
                bool isDead = !health.TakeHit(finalDamage, hit.point, shootDirection, hitBone);

                if (_hub != null && _hub.audioManager != null)
                {
                    _hub.audioManager.PlayHitAudio(hitZone == HitZone.Head ? HitSfxType.Head : HitSfxType.Body);
                }

                if (ragdoll != null)
                {
                    ragdoll.ApplyHit(hit.point, shootDirection, totalForce, hitBone, isDead);
                }
            }
            else if (ragdoll != null)
            {
                // EnemyHealth가 없을 경우 레거시 처리: 즉시 풀 레그돌
                ragdoll.ApplyHit(hit.point, shootDirection, totalForce, hitBone, true);
            }
        }

        SpawnTracer(rayOrigin, tracerEnd);
    }

    private IEnumerator ReloadRoutine()
    {
        _isReloading = true;
        gunAnimator.SetTrigger(hashReload);
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        _isReloading = false;
    }

    private enum HitZone
    {
        Body,
        Head
    }

    private HitZone ResolveHitZone(Collider hitCollider)
    {
        if (hitCollider == null) return HitZone.Body;

        // 태그 우선 판정, 미설정 시 이름 기반 폴백
        string tag = hitCollider.tag;
        if (tag == "Head") return HitZone.Head;
        if (tag == "Body") return HitZone.Body;

        string name = hitCollider.name;
        if (NameMatchesToken(name, "head")) return HitZone.Head;
        if (NameMatchesToken(name, "body")) return HitZone.Body;

        return HitZone.Body;
    }

    private void InitializeTracerPool()
    {
        if (tracerPrefab == null || tracerPoolSize <= 0) return;

        int initialCount = Mathf.Min(tracerPoolSize, tracerMaxPoolSize);
        for (int i = 0; i < initialCount; i++)
        {
            LineRenderer tracer = Instantiate(tracerPrefab, transform);
            tracer.gameObject.SetActive(false);
            _tracerPool.Enqueue(tracer);
        }
        _tracerInstanceCount = initialCount;
    }

    private void SpawnTracer(Vector3 start, Vector3 end)
    {
        // 풀을 동적 확장하고, tracerMaxPoolSize 초과 시 렌더를 건너뛰며 tracerOrigin이 있으면 start를 대체합니다.
        if (tracerPrefab == null) return;
        if (_tracerPool.Count == 0)
        {
            if (_tracerInstanceCount >= tracerMaxPoolSize) return;
            LineRenderer extraTracer = Instantiate(tracerPrefab, transform);
            extraTracer.gameObject.SetActive(false);
            _tracerPool.Enqueue(extraTracer);
            _tracerInstanceCount++;
        }

        LineRenderer tracer = _tracerPool.Dequeue();
        tracer.gameObject.SetActive(true);

        Vector3 origin = tracerOrigin != null ? tracerOrigin.position : start;
        tracer.SetPosition(0, origin);
        tracer.SetPosition(1, end);

        StartCoroutine(DisableTracerAfter(tracer, tracerDuration));
    }

    private IEnumerator DisableTracerAfter(LineRenderer tracer, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (tracer != null)
        {
            tracer.gameObject.SetActive(false);
            _tracerPool.Enqueue(tracer);
        }
    }

    private bool NameMatchesToken(string name, string token)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        string lower = name.ToLowerInvariant();
        if (lower == token) return true;

        string[] parts = lower.Split(new[] { '_', '-', ' ', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (part == token) return true;
        }

        return false;
    }
}
