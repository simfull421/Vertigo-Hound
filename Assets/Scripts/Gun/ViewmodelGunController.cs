using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.VFX; // VFX Graph 사용을 위해 추가

public class ViewmodelGunController : MonoBehaviour
{
    [Header("Components")]
    public Animator gunAnimator;
    public Transform mainCameraTransform;

    [Header("Gun Stats")]
    public int maxAmmo = 15;
    public int currentAmmo;
    public float fireRate = 0.5f;
    public float reloadTime = 1.5f;
    public float damage = 25f;
    public float headshotMultiplier = 2.0f;
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

    [Header("Ballistics")]
    [Tooltip("집탄율 (높을수록 널리 퍼짐)")]
    public float spreadAngle = 0.02f;
    [Tooltip("피격 시 물리적 밀어내는 힘")]
    public float totalForce = 50f;

    [Header("Camera Recoil")]
    [Tooltip("사격 시 카메라 위로 튀는 각도")]
    public float cameraRecoilPitch = 5f;

    [Header("VFX & Audio (고도화)")]
    public VisualEffect shootVFX;
    public AudioClip shootClip;
    [Range(0f, 1f)]
    public float shootVolume = 0.5f;
    [Tooltip("피격 시 발생하는 스파크/먼지 파티클 프리팹 (유혈 대체)")]
    public GameObject hitSparkPrefab;
    [Tooltip("오브젝트 풀링용 컨테이너 (하이어라키에 빈 게임오브젝트를 할당)")]
    public Transform hitSparkContainer;
    
    // 간단한 오브젝트 풀
    private Queue<GameObject> _hitSparkPool = new Queue<GameObject>();

    private AudioSource _audioSource;
    public Transform tracerOrigin; // 기존에 있던 변수, 필요에 따라 유지

    [Header("UI Feedback")]
    public Color bodyHitColor = Color.white;
    public Color headshotHitColor = Color.red;
    public float hitMarkerDuration = 0.1f;
    [Tooltip("하이어라키에 직접 만든 십자 이미지 오브젝트(4개 이미지 포함)를 여기에 할당하세요.")]
    public GameObject customHitMarker;
    private Coroutine _hitMarkerCoroutine;

    private CanvasGroup _hitMarkerCanvasGroup;
    private Image[] _hitMarkerImages;

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

    private readonly int hashReload = Animator.StringToHash("TriggerReload");
    
    // [방어] gunAnim.Update(0f) 제거 대체: SetActive 이후 첫 LateUpdate에서 Idle 포즈를 재캡처합니다.
    private bool _needsHipPoseCapture = false;

    /// <summary>
    /// 무기 전환 시 호출하여 Sway 잔존값을 초기화합니다.
    /// </summary>
    public void ResetSway()
    {
        _currentSway = Vector3.zero;
        _adsWeight = 0f;
        if (weaponRoot != null)
        {
            weaponRoot.localPosition = _hipLocalPos;
            weaponRoot.localRotation = _hipLocalRot;
        }
    }

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
        currentAmmo = maxAmmo;

        // AudioSource 초기화 (없으면 자동 추가)
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

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
        
        // 초기 hip 포즈 캡처
        _hipLocalPos = weaponRoot != null ? weaponRoot.localPosition : Vector3.zero;
        _hipLocalRot = weaponRoot != null ? weaponRoot.localRotation : Quaternion.identity;
        _needsHipPoseCapture = true;

        if (viewmodelCamera != null) _defaultFOV = viewmodelCamera.fieldOfView;
        
        if (customHitMarker != null)
        {
            _hitMarkerCanvasGroup = customHitMarker.GetComponent<CanvasGroup>();
            if (_hitMarkerCanvasGroup == null) _hitMarkerCanvasGroup = customHitMarker.AddComponent<CanvasGroup>();
            _hitMarkerCanvasGroup.alpha = 0f;
            _hitMarkerImages = customHitMarker.GetComponentsInChildren<Image>();
        }
        else
        {
            CreateProceduralHitMarker();
        }
    }

    public void UpdateModule()
    {
        if (_hub == null || _hub.animatorHandler.currentWeaponType != 1) return;

        bool isFiring = _hub.InputProv.FireTriggered; // 단발 (윙맨 스타일)
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
        // Sway 적용: 절대 좌표 기반 (드리프트 방지)
        // ADS 중에는 Sway 미적용, hip fire에서만 적용
        if (!_isAiming)
        {
            if (_adsWeight <= 0.01f)
            {
                weaponRoot.localPosition = _hipLocalPos + _currentSway;
                weaponRoot.localRotation = _hipLocalRot;
            }
            else
            {
                weaponRoot.localPosition = Vector3.Lerp(weaponRoot.localPosition, _hipLocalPos + _currentSway, Time.deltaTime * adsSpeed);
                weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, _hipLocalRot, Time.deltaTime * adsSpeed);
            }
        }
    }

    private void Shoot()
    {
        // 사운드 재생 (피치 랜덤 적용 및 볼륨 조절)
        if (_audioSource != null && shootClip != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.volume = shootVolume;
            _audioSource.PlayOneShot(shootClip);
        }

        currentAmmo--;
        _nextFireTime = Time.time + fireRate;

        if (_hub != null)
        {
            _hub.movement.AddRecoilPitch(cameraRecoilPitch);
        }

        // 총구 방향(forward)에 랜덤한 퍼짐(Spread) 값 추가
        Vector3 spread = Random.insideUnitSphere * spreadAngle;
        if (mainCameraTransform == null) return;

        Vector3 shootDirection = (mainCameraTransform.forward + spread).normalized;
        Vector3 rayOrigin = mainCameraTransform.position;

        if (Physics.Raycast(rayOrigin, shootDirection, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            HitZone hitZone = ResolveHitZone(hit.collider);
            float finalDamage = damage * (hitZone == HitZone.Head ? headshotMultiplier : 1f);

            EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
            EnemyRagdollHandler ragdoll = hit.collider.GetComponentInParent<EnemyRagdollHandler>();
            Rigidbody hitBone = hit.collider.attachedRigidbody;

            if (hitSparkPrefab != null)
            {
                // 오브젝트 풀링을 활용하여 스파크 파티클 재사용 (GC 방지)
                GameObject spark = GetHitSpark();
                spark.transform.position = hit.point;
                spark.transform.rotation = Quaternion.LookRotation(hit.normal);
                StartCoroutine(ReturnSparkToPool(spark, 1.5f)); // 파티클 재생 시간에 맞춰 반환 대기
            }

            if (health != null)
            {
                bool isDead = !health.TakeHit(finalDamage, hit.point, shootDirection, hitBone);

                if (_hub != null && _hub.audioManager != null)
                {
                    _hub.audioManager.PlayHitAudio(hitZone == HitZone.Head ? HitSfxType.Head : HitSfxType.Body);
                }
                
                TriggerHitMarker(hitZone == HitZone.Head);

                if (isDead)
                {
                    // [사망] 물리 시체 레그돌 켜기
                    if (ragdoll != null) 
                    {
                        ragdoll.ApplyDeathRagdoll(hit.point, shootDirection, totalForce, hitBone);
                    }
                }
                else
                {
                    // [생존]
                    EnemyAI enemyAI = health.GetComponent<EnemyAI>();
                    if (ragdoll != null && enemyAI != null && enemyAI.IsAirborne())
                    {
                        // 공중 피격 시 강제 넉다운 (2.5초 레그돌 후 기상)
                        ragdoll.ApplyKnockdown(2.5f, hit.point, shootDirection, totalForce, hitBone);
                    }
                    else if (hitBone != null)
                    {
                        EnemyAnimatorController animCtrl = hit.collider.GetComponentInParent<EnemyAnimatorController>();
                        if (animCtrl != null)
                        {
                            // 맞은 뼈의 이름을 전달하여 부위별 피격 애니메이션 트리거 발동
                            animCtrl.PlayHitAnimation(hitBone.name); 
                        }
                    }
                }
            }
            else if (ragdoll != null)
            {
                // EnemyHealth가 없을 경우 예거시 처리: 즉시 풀 레그돌
                TriggerHitMarker(hitZone == HitZone.Head);
                ragdoll.ApplyDeathRagdoll(hit.point, shootDirection, totalForce, hitBone);
            }
        }

        // VFX Graph 트리거 (LineRenderer 스폰 제거)
        if (shootVFX != null)
        {
            shootVFX.SendEvent("OnShoot");
        }
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

        string tag = hitCollider.tag;
        if (tag == "Head") return HitZone.Head;
        if (tag == "Body") return HitZone.Body;

        string name = hitCollider.name;
        if (NameMatchesToken(name, "head")) return HitZone.Head;
        if (NameMatchesToken(name, "body")) return HitZone.Body;

        return HitZone.Body;
    }

    private void TriggerHitMarker(bool isHeadshot)
    {
        if (_hitMarkerCanvasGroup == null) return;
        
        if (_hitMarkerCoroutine != null)
        {
            StopCoroutine(_hitMarkerCoroutine);
        }
        _hitMarkerCoroutine = StartCoroutine(HitMarkerRoutine(isHeadshot));
    }

    private IEnumerator HitMarkerRoutine(bool isHeadshot)
    {
        if (_hitMarkerCanvasGroup == null) yield break;

        Color targetColor = isHeadshot ? headshotHitColor : bodyHitColor;
        if (_hitMarkerImages != null)
        {
            foreach (var img in _hitMarkerImages)
            {
                if (img != null) img.color = targetColor;
            }
        }
        
        _hitMarkerCanvasGroup.alpha = 1f;

        float timer = 0f;
        while (timer < hitMarkerDuration)
        {
            timer += Time.deltaTime;
            _hitMarkerCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / hitMarkerDuration);
            yield return null;
        }

        _hitMarkerCanvasGroup.alpha = 0f;
    }

    private void CreateProceduralHitMarker()
    {
        GameObject canvasObj = new GameObject("ProceduralHitMarkerCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.transform.SetParent(transform, false); 

        GameObject markerObj = new GameObject("HitMarkerContainer");
        markerObj.transform.SetParent(canvasObj.transform, false);
        markerObj.transform.localPosition = Vector3.zero;

        _hitMarkerCanvasGroup = markerObj.AddComponent<CanvasGroup>();
        _hitMarkerCanvasGroup.alpha = 0f;

        Vector2 lineSize = new Vector2(2f, 20f); 

        GameObject line1Obj = new GameObject("Line1");
        line1Obj.transform.SetParent(markerObj.transform, false);
        Image img1 = line1Obj.AddComponent<Image>();
        img1.rectTransform.sizeDelta = lineSize;
        img1.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);

        GameObject line2Obj = new GameObject("Line2");
        line2Obj.transform.SetParent(markerObj.transform, false);
        Image img2 = line2Obj.AddComponent<Image>();
        img2.rectTransform.sizeDelta = lineSize;
        img2.rectTransform.localRotation = Quaternion.Euler(0, 0, -45f);

        _hitMarkerImages = new Image[] { img1, img2 };
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

    private GameObject GetHitSpark()
    {
        if (_hitSparkPool.Count > 0)
        {
            GameObject spark = _hitSparkPool.Dequeue();
            spark.SetActive(true);
            return spark;
        }
        else
        {
            // 컨테이너가 없으면 자신을 부모로 설정
            GameObject spark = Instantiate(hitSparkPrefab, hitSparkContainer != null ? hitSparkContainer : transform);
            return spark;
        }
    }

    private IEnumerator ReturnSparkToPool(GameObject spark, float delay)
    {
        yield return new WaitForSeconds(delay);
        spark.SetActive(false);
        _hitSparkPool.Enqueue(spark);
    }
}