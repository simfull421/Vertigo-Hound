using UnityEngine;
using System;

[Serializable]
public sealed class PlayerWallTouchIK
{
    [Header("IK Settings")]
    public float maxTouchDistance = 0.6f;
    public float sphereCastRadius = 0.15f;
    
    [Header("IK Speeds (뻗기/거두기 속도 분리)")]
    public float ikReachSpeed = 12f;
    public float ikRetractSpeed = 6f;

    [Header("Hand Rotation Offsets")]
    // 손바닥이 벽을 짚는 용도가 아니라 팔뚝을 밀착시키기 위해 손목이 몸 안쪽을 향하게 회전 보정
    public Vector3 leftHandRotOffset = new Vector3(0f, 90f, 0f);
    public Vector3 rightHandRotOffset = new Vector3(0f, -90f, 0f);
    
    [Header("Animator Pose Layers")]
    public int leftHandPoseLayerIndex = 3;
    public int rightHandPoseLayerIndex = 4;

    private PlayerController _hub;
    private Animator _animator;

    private float _leftIkWeight = 0f;
    private float _rightIkWeight = 0f;
    private float _leftSlideTimer = 0f;
    private float _rightSlideTimer = 0f;

    // 최종 목표 (Lerp 되는 값)
    private Vector3 _leftTargetPos;
    private Quaternion _leftTargetRot;
    private Vector3 _leftHintPos;
    
    private Vector3 _rightTargetPos;
    private Quaternion _rightTargetRot;
    private Vector3 _rightHintPos;

    // 앵커 상태
    private bool _isLeftAnchored = false;
    private Vector3 _leftAnchorPos;
    
    private bool _isRightAnchored = false;
    private Vector3 _rightAnchorPos;

    public void Initialize(PlayerController hub, Animator animator)
    {
        _hub = hub;
        _animator = animator;

        if (_animator != null)
        {
            PlayerIKRouter router = _animator.gameObject.GetComponent<PlayerIKRouter>();
            if (router == null) router = _animator.gameObject.AddComponent<PlayerIKRouter>();
            
            // 기존에 할당된 콜백을 안전하게 제거한 뒤 추가하여 다중 등록 방지
            router.onIK -= OnAnimatorIKModule;
            router.onIK += OnAnimatorIKModule;
        }
    }

    public void UpdateModule()
    {
        if (_animator == null) return;

        bool isValidState = CheckValidIKState();

        if (isValidState)
        {
           CalculateHandIK(HumanBodyBones.LeftShoulder, true, ref _leftTargetPos, ref _leftTargetRot, ref _leftHintPos, ref _leftIkWeight, ref _isLeftAnchored, ref _leftAnchorPos, ref _leftSlideTimer);

            CalculateHandIK(HumanBodyBones.RightShoulder, false, ref _rightTargetPos, ref _rightTargetRot, ref _rightHintPos, ref _rightIkWeight, ref _isRightAnchored, ref _rightAnchorPos, ref _rightSlideTimer);
        }
        else
        {
            _leftIkWeight = Mathf.Lerp(_leftIkWeight, 0f, Time.deltaTime * ikRetractSpeed);
            _rightIkWeight = Mathf.Lerp(_rightIkWeight, 0f, Time.deltaTime * ikRetractSpeed);
            _isLeftAnchored = false;
            _isRightAnchored = false;
        }
        
        if (_animator.layerCount > leftHandPoseLayerIndex)
            _animator.SetLayerWeight(leftHandPoseLayerIndex, _leftIkWeight);
            
        if (_animator.layerCount > rightHandPoseLayerIndex)
            _animator.SetLayerWeight(rightHandPoseLayerIndex, _rightIkWeight);
    }

    private bool CheckValidIKState()
    {
        if (!_hub.movement.IsGrounded) return false;
        if (_hub.slider.IsSliding) return false;
        if (_hub.animatorHandler.currentWeaponType == 1) return false;

        return true;
    }

   private void CalculateHandIK(HumanBodyBones shoulderBone, bool isLeft, ref Vector3 targetPos, ref Quaternion targetRot, ref Vector3 hintPos, ref float weight, ref bool isAnchored, ref Vector3 anchorPos, ref float slideTimer)
{
        Transform shoulder = _animator.GetBoneTransform(shoulderBone);
        if (shoulder == null) return;

        // 1. 레이캐스트: 정확한 측면 검사
        Vector3 sideDir = isLeft ? -_hub.transform.right : _hub.transform.right;
        Ray ray = new Ray(shoulder.position, sideDir);

        bool hitWall = Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxTouchDistance, _hub.wallRunner.wallLayerMask);
        
        float targetWeight = 0f;
        Vector3 currentXZVel = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z);
        bool isMovingFast = currentXZVel.magnitude >= 2.0f;

        if (hitWall)
    {
        bool isVerticalWall = Mathf.Abs(hit.normal.y) < 0.3f;
        Vector3 dirToHit = (hit.point - shoulder.position).normalized;
        float fovDot = Vector3.Dot(Camera.main.transform.forward, dirToHit);

        if (isVerticalWall && fovDot >= -0.1f)
        {
            targetWeight = 1f;

            Vector3 idealTargetPos = shoulder.position + (_hub.transform.forward * 0.35f) - (_hub.transform.up * 0.05f) + (sideDir * 0.15f);
            Vector3 idealHintPos = hit.point;

            if (isMovingFast)
            {
                // [의도하신 로직 적용] 1초(transitionDuration)에 걸쳐 가중치를 증가시키며 완벽하게 고정
                float transitionDuration = 1.0f; // 1초에 걸쳐 고정

                if (slideTimer < transitionDuration)
                {
                    slideTimer += Time.deltaTime;
                    
                    // 진행률 (0.0 ~ 1.0)
                    float ratio = slideTimer / transitionDuration;
                    
                    // 점점 가중치가 더해지는 곡선 (Ease-In). 초반엔 부드럽고 갈수록 강하게 빨려 들어감
                    float t = ratio * ratio; 

                    targetPos = Vector3.Lerp(targetPos, idealTargetPos, t);
                    hintPos = Vector3.Lerp(hintPos, idealHintPos, t);
                }
                else
                {
                    // 1초가 지나면 무의미한 Lerp 연산을 끄고 완벽하게 박제 (Jitter 제로)
                    targetPos = idealTargetPos;
                    hintPos = idealHintPos;
                }
                
                isAnchored = false;
            }
            else
            {
                // [Pushing Mode] 멈췄을 때만 월드 앵커 적용
                slideTimer = 0f; // 멈추면 타이머 리셋 (다시 달릴 때 1초 보간을 위해)

                if (!isAnchored) 
                { 
                    anchorPos = idealTargetPos; 
                    isAnchored = true; 
                    hintPos = idealHintPos;
                }
                targetPos = Vector3.Lerp(targetPos, anchorPos, Time.deltaTime * 15f);
                hintPos = Vector3.Lerp(hintPos, idealHintPos, Time.deltaTime * 15f);
            }
        }
    }
        
      if (targetWeight <= 0f) isAnchored = false;

        // [수정 2] 이 부분이 핵심입니다. 
        // 기존 8f도 1인칭에서는 0.12초 만에 도달하는 매우 빠른 속도입니다.
        // 이것을 4f ~ 5f 수준으로 대폭 낮춰서, 팔이 벽을 향해 뻗어나가는 '중간 프레임'을 카메라에 확실히 보여줍니다.
        float blendSpeed = (targetWeight > weight) ? 4.5f : ikRetractSpeed; 
        weight = Mathf.Lerp(weight, targetWeight, Time.deltaTime * blendSpeed);
    }

    private void OnAnimatorIKModule(int layerIndex)
    {
        if (_animator == null) return;

        ApplyHandIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, _leftTargetPos, _leftTargetRot, _leftHintPos, _leftIkWeight);
        ApplyHandIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, _rightTargetPos, _rightTargetRot, _rightHintPos, _rightIkWeight);
    }

   private void ApplyHandIK(AvatarIKGoal handGoal, AvatarIKHint elbowHint, Vector3 pos, Quaternion rot, Vector3 elbowPos, float weight)
{
    if (weight <= 0.01f)
    {
        _animator.SetIKPositionWeight(handGoal, 0f);
        _animator.SetIKRotationWeight(handGoal, 0f);
        _animator.SetIKHintPositionWeight(elbowHint, 0f);
        return;
    }

    _animator.SetIKPositionWeight(handGoal, weight);
    _animator.SetIKRotationWeight(handGoal, 0f); // [핵심] IK 회전 Weight를 0으로 고정. 애니메이션 고유의 주먹 쥔 포즈를 살림!
    _animator.SetIKHintPositionWeight(elbowHint, weight); 

    _animator.SetIKPosition(handGoal, pos);
    // _animator.SetIKRotation(handGoal, rot); // 이 줄은 지우거나 주석 처리
    _animator.SetIKHintPosition(elbowHint, elbowPos);
}
}
