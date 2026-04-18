using UnityEngine;
using System;

[Serializable]
public sealed class PlayerWallTouchIK
{
    [Header("IK Settings")]
    public float maxTouchDistance = 0.8f;
    public float sphereCastRadius = 0.15f;
    public float ikBlendSpeed = 8f;
    
    [Header("Hand Rotation Offsets (Karate Chop Fix)")]
    public Vector3 leftHandRotOffset = new Vector3(0f, 90f, 0f);
    public Vector3 rightHandRotOffset = new Vector3(0f, -90f, 0f);
    
    [Header("Animator Pose Layers")]
    public int leftHandPoseLayerIndex = 3;
    public int rightHandPoseLayerIndex = 4;

    private PlayerController _hub;
    private Animator _animator;

    private float _leftIkWeight = 0f;
    private float _rightIkWeight = 0f;

    private Vector3 _leftTargetPos;
    private Quaternion _leftTargetRot;
    private Vector3 _rightTargetPos;
    private Quaternion _rightTargetRot;

    public void Initialize(PlayerController hub, Animator animator)
    {
        _hub = hub;
        _animator = animator;

        if (_animator != null)
        {
            // Animator 게임오브젝트에 라우터 스크립트 붙이기
            PlayerIKRouter router = _animator.gameObject.GetComponent<PlayerIKRouter>();
            if (router == null)
            {
                router = _animator.gameObject.AddComponent<PlayerIKRouter>();
            }
            router.onIK = OnAnimatorIKModule;
        }
    }

    public void UpdateModule()
    {
        if (_animator == null) return;

        bool isValidState = CheckValidIKState();

        if (isValidState)
        {
            CalculateHandIK(HumanBodyBones.LeftShoulder, true, ref _leftTargetPos, ref _leftTargetRot, ref _leftIkWeight);
            CalculateHandIK(HumanBodyBones.RightShoulder, false, ref _rightTargetPos, ref _rightTargetRot, ref _rightIkWeight);
        }
        else
        {
            // 상호작용 불가 상태에서는 부드럽게 IK 가중치를 뺌
            _leftIkWeight = Mathf.Lerp(_leftIkWeight, 0f, Time.deltaTime * ikBlendSpeed);
            _rightIkWeight = Mathf.Lerp(_rightIkWeight, 0f, Time.deltaTime * ikBlendSpeed);
        }
        
        // 양손을 각각의 독립된 핸드 포즈 레이어(3번, 4번)에 연결하여 허공 손 오므라듦 방지
        if (_animator.layerCount > leftHandPoseLayerIndex)
            _animator.SetLayerWeight(leftHandPoseLayerIndex, _leftIkWeight);
            
        if (_animator.layerCount > rightHandPoseLayerIndex)
            _animator.SetLayerWeight(rightHandPoseLayerIndex, _rightIkWeight);
    }

    private bool CheckValidIKState()
    {
        if (!_hub.movement.IsGrounded) return false;
        if (_hub.slider.IsSliding) return false;
        
        // 무기 장착 시 상체 마스크가 제어권을 가지므로 IK 개입을 막음
        if (_hub.animatorHandler.currentWeaponType == 1) return false;

        // 달리기 중 작동 방지
        Vector3 currentXZVel = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z);
        if (_hub.InputProv.DashHeld && currentXZVel.magnitude > 6.5f) return false;

        return true;
    }

    private void CalculateHandIK(HumanBodyBones shoulderBone, bool isLeft, ref Vector3 targetPos, ref Quaternion targetRot, ref float weight)
    {
        Transform shoulder = _animator.GetBoneTransform(shoulderBone);
        if (shoulder == null) return;

        Vector3 sideDir = isLeft ? -_hub.transform.right : _hub.transform.right;
        Vector3 rayDir = (sideDir * 0.8f + _hub.transform.forward).normalized;

        Ray ray = new Ray(shoulder.position, rayDir);
        bool hitWall = Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxTouchDistance, _hub.wallRunner.wallLayerMask);

        float targetWeight = 0f;

        if (hitWall)
        {
            // [핵심 추가] 내적(Dot Product)을 이용한 벽 방향 완벽 검증
            // 플레이어의 오른쪽 벡터와 벽면의 수직 방향(Normal)을 곱해 벽이 어느 쪽에 있는지 수학적으로 판별합니다.
            float dot = Vector3.Dot(hit.normal, _hub.transform.right);
            
            // 왼쪽 벽의 Normal은 오른쪽(양수)을 향하고, 오른쪽 벽의 Normal은 왼쪽(음수)을 향합니다.
            // 정면 벽(0에 가까움)이나 반대쪽 벽을 맞췄다면 isCorrectSide가 false가 되어 짚지 않습니다.
            bool isCorrectSide = isLeft ? (dot > 0.2f) : (dot < -0.2f);

            if (isCorrectSide)
            {
                targetPos = hit.point + (hit.normal * 0.05f); 
                Quaternion baseLookRot = Quaternion.LookRotation(-hit.normal, _hub.transform.up);
                Vector3 offset = isLeft ? leftHandRotOffset : rightHandRotOffset;
                targetRot = baseLookRot * Quaternion.Euler(offset);
                
                targetWeight = 1f;
            }
        }

        weight = Mathf.Lerp(weight, targetWeight, Time.deltaTime * ikBlendSpeed);
        
        // [보너스] 씬 뷰(Scene View)에서 눈으로 확인 가능하도록 레이저를 그립니다.
        // 플레이하면서 레이저가 허공을 짚을 때 어딜 때리는지 직관적으로 볼 수 있습니다.
        Debug.DrawRay(ray.origin, ray.direction * maxTouchDistance, isLeft ? Color.red : Color.blue);
    }

    private void OnAnimatorIKModule(int layerIndex)
    {
        if (_animator == null) return;

        // [주의] Base Layer뿐 아니라 Weapon Layer, Melee Layer에서도 
        // 설정 창 톱니바퀴 -> IK Pass 가 반드시 켜져 있어야 IK가 정상 작동함.

        ApplyHandIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, _leftTargetPos, _leftTargetRot, _leftIkWeight, true);
        ApplyHandIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, _rightTargetPos, _rightTargetRot, _rightIkWeight, false);
    }

    private void ApplyHandIK(AvatarIKGoal handGoal, AvatarIKHint elbowHint, Vector3 pos, Quaternion rot, float weight, bool isLeft)
    {
        if (weight <= 0.01f)
        {
            _animator.SetIKPositionWeight(handGoal, 0f);
            _animator.SetIKRotationWeight(handGoal, 0f);
            _animator.SetIKHintPositionWeight(elbowHint, 0f);
            return;
        }

        _animator.SetIKPositionWeight(handGoal, weight);
        _animator.SetIKRotationWeight(handGoal, weight);
        // 팔꿈치는 힌트 무시 현상을 막기 위해 1.0f로 확실하게 락온
        _animator.SetIKHintPositionWeight(elbowHint, weight * 1.0f); 

        _animator.SetIKPosition(handGoal, pos);
        _animator.SetIKRotation(handGoal, rot);

        // 팔꿈치(Elbow) 힌트 누락 및 기괴함 방지
        Transform shoulder = _animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
        if (shoulder != null)
        {
            float distToWall = Vector3.Distance(shoulder.position, pos);
            // 거리가 짧아질수록(벽에 가까울수록) 1에 가까워지는 구부림 계수
            float bendFactor = Mathf.Clamp01(1f - (distToWall / maxTouchDistance)); 
            
            // 더 과격한 오프셋 부여 (기본 0.4에서 1.0까지 더 확 밀어버림)
            float sideOffset = Mathf.Lerp(0.4f, 1.0f, bendFactor);
            float backOffset = Mathf.Lerp(0.2f, 0.8f, bendFactor);

            Vector3 sideDir = isLeft ? -_hub.transform.right : _hub.transform.right;
            
            Vector3 hintPos = shoulder.position + sideDir * sideOffset - _hub.transform.forward * backOffset - _hub.transform.up * 0.2f;
            _animator.SetIKHintPosition(elbowHint, hintPos);
        }
    }
}
