using UnityEngine;
using System;

[Serializable]
public sealed class PlayerWallTouchIK
{
    [Header("IK Settings")]
    public float maxTouchDistance = 0.6f;
    public float sphereCastRadius = 0.15f;

    [Header("IK Position Offsets (손 짚는 위치 조절)")]
    [Tooltip("어깨 기준 앞으로 손을 뻗는 거리 (0.3 ~ 0.4 권장)")]
    public float handForwardOffset    = 0.35f;
    [Tooltip("어깨 기준 아래로 내리는 거리. 값이 클수록 손이 화면 아래로 내려가 시야를 확보함 (0.2 ~ 0.4 권장)")]
    public float handHeightDownOffset = 0.25f;
    [Tooltip("어깨 기준 좌우로 보내는 거리 (0.1 ~ 0.2 권장)")]
    public float handSideOffset       = 0.15f;

    [Header("IK Smoothing (뻗기/거두기 속도 분리)")]
    [Tooltip("벽으로 손이 뻗어나가는 속도. 낮을수록 민첩. (권장: 0.08 ~ 0.12)")]
    public float reachSmoothTime   = 0.1f;
    [Tooltip("벽에서 손을 뗀 후 달리기 모션으로 스르륵 복귀하는 속도. (권장: 0.3 ~ 0.4)")]
    public float retractSmoothTime = 0.35f;
    [Tooltip("벽이 감지되지 않아도 팔을 유지하는 시간(초). 채터링 원천 차단용. (권장: 0.15)")]
    public float retractDelay      = 0.15f;


    [Header("Animator Pose Layers (RunUpper 애니메이터 기준)")]
    [Tooltip("RunUpper Animator의 왼손 포즈 레이어 인덱스. 애니메이터 컨트롤러 구조에 맞게 조정하세요.")]
    public int leftHandPoseLayerIndex  = 3;
    [Tooltip("RunUpper Animator의 오른손 포즈 레이어 인덱스. 애니메이터 컨트롤러 구조에 맞게 조정하세요.")]
    public int rightHandPoseLayerIndex = 4;

    [Header("Stable Shoulder Height (기준점 높이)")]
    [Tooltip("캡슐 바닥(transform.position) 기준으로 가상 어깨 기준점의 높이. 고개를 숙여도 팔의 수직 기준이 흔들리지 않음. (권장: 1.2 ~ 1.5)")]
    public float stableShoulderHeight = 1.4f;

    [Header("Wrist Rotation Offsets (손목 각도 조정)")]
    [Tooltip("벽을 즧을 때 왼손목 회전 보정값 (Euler). 손목이 빡립는 각도를 인스펝터에서 직접 튜닝하세요.")]
    public Vector3 leftWristRotOffset  = new Vector3(0f,  90f, 0f);
    [Tooltip("벽을 즧을 때 오른손목 회전 보정값 (Euler).")]
    public Vector3 rightWristRotOffset = new Vector3(0f, -90f, 0f);
    [Tooltip("손목 회전이 애니메이션에 섬이는 비율. 0=100% 애니메, 1=100% IK회전 (0.3 ~ 0.7 권장)")]
    public float  wristRotBlend        = 0.5f;


    private PlayerController _hub;
    private Animator _animator;

    // ── IK Weight (SmoothDamp용 현재값 + 속도 변수) ──────────────────────────
    private float _leftIkWeight  = 0f;
    private float _rightIkWeight = 0f;
    private float _leftWeightVel;
    private float _rightWeightVel;

    // ── Retract Delay: 마지막으로 벽을 '본' 시각 ────────────────────────────
    private float _leftLastSeenTime  = -999f;
    private float _rightLastSeenTime = -999f;

    // ── 손 목표 위치/힌트 (Lerp 되는 값) ────────────────────────────────────
    private Vector3    _leftTargetPos;
    private Quaternion _leftTargetRot;
    private Vector3    _leftHintPos;

    private Vector3    _rightTargetPos;
    private Quaternion _rightTargetRot;
    private Vector3    _rightHintPos;

    // ── 앵커 (정지 상태 월드 고정) ──────────────────────────────────────────
    private bool    _isLeftAnchored  = false;
    private Vector3 _leftAnchorPos;
    private float   _leftSlideTimer  = 0f;

    private bool    _isRightAnchored = false;
    private Vector3 _rightAnchorPos;
    private float   _rightSlideTimer = 0f;

    // ────────────────────────────────────────────────────────────────────────
    public void Initialize(PlayerController hub, Animator viewmodelAnimator)
    {
        _hub      = hub;
        _animator = viewmodelAnimator; // RunUpper의 Animator를 주입받음

        if (_animator != null)
        {
            // Run Upper 모델에 PlayerIKRouter 스크립트가 없다면 자동 부착
            PlayerIKRouter router = _animator.gameObject.GetComponent<PlayerIKRouter>();
            if (router == null) router = _animator.gameObject.AddComponent<PlayerIKRouter>();

            // 기존 콜백 안전 제거 후 등록
            router.onIK -= OnAnimatorIKModule;
            router.onIK += OnAnimatorIKModule;
        }
        else
        {
            Debug.LogWarning("[PlayerWallTouchIK] RunUpper Animator가 연결되지 않았습니다! IK가 동작하지 않습니다.");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    public void UpdateModule()
    {
        // RunUpper가 비활성이거나 Animator가 없으면 Weight를 모두 0으로 즉시 수렴시키고 리턴
        if (_animator == null || !_animator.gameObject.activeInHierarchy)
        {
            // RunUpper 비활성 시: 거두기 속도(retractSmoothTime)로 부드럽게 0으로 수렴
            _leftIkWeight  = Mathf.SmoothDamp(_leftIkWeight,  0f, ref _leftWeightVel,  retractSmoothTime);
            _rightIkWeight = Mathf.SmoothDamp(_rightIkWeight, 0f, ref _rightWeightVel, retractSmoothTime);
            return;
        }

        // IK 전역 가동 조건: 슬라이딩 중이거나 총기를 든 상태면 양팔 모두 비활성화
        bool globalIKEnabled = !_hub.slider.IsSliding
                               && _hub.animatorHandler.currentWeaponType == 0;

        if (globalIKEnabled)
        {
            // 양팔 IK를 완전히 독립적으로 계산:
            // 한쪽이 벽을 짚어도 다른 쪽 Weight는 0으로 떨어져 Base Layer 애니메이션을 그대로 재생함.
            CalculateHandIK(
                HumanBodyBones.LeftShoulder, isLeft: true,
                ref _leftTargetPos,  ref _leftTargetRot,  ref _leftHintPos,
                ref _leftIkWeight,   ref _leftWeightVel,
                ref _isLeftAnchored, ref _leftAnchorPos,  ref _leftSlideTimer,
                ref _leftLastSeenTime);

            CalculateHandIK(
                HumanBodyBones.RightShoulder, isLeft: false,
                ref _rightTargetPos,  ref _rightTargetRot,  ref _rightHintPos,
                ref _rightIkWeight,   ref _rightWeightVel,
                ref _isRightAnchored, ref _rightAnchorPos,  ref _rightSlideTimer,
                ref _rightLastSeenTime);
        }
        else
        {
            // 조건 불충족(슬라이딩/총기) → 거두기 속도로 양팔 부드럽게 거두기
            _leftIkWeight  = Mathf.SmoothDamp(_leftIkWeight,  0f, ref _leftWeightVel,  retractSmoothTime);
            _rightIkWeight = Mathf.SmoothDamp(_rightIkWeight, 0f, ref _rightWeightVel, retractSmoothTime);
            _isLeftAnchored  = false;
            _isRightAnchored = false;
        }

        // 레이어 Weight 적용 (좌우 독립)
        if (_animator.layerCount > leftHandPoseLayerIndex)
            _animator.SetLayerWeight(leftHandPoseLayerIndex,  _leftIkWeight);

        if (_animator.layerCount > rightHandPoseLayerIndex)
            _animator.SetLayerWeight(rightHandPoseLayerIndex, _rightIkWeight);
    }

    // ────────────────────────────────────────────────────────────────────────
    private void CalculateHandIK(
        HumanBodyBones shoulderBone, bool isLeft,
        ref Vector3 targetPos, ref Quaternion targetRot, ref Vector3 hintPos,
        ref float weight, ref float weightVel,
        ref bool isAnchored, ref Vector3 anchorPos, ref float slideTimer,
        ref float lastSeenTime)
    {
        Transform shoulder = _animator.GetBoneTransform(shoulderBone);
        if (shoulder == null) return;

        // ── 레이캐스트 ──────────────────────────────────────────────────────
        // [좌표계 분리] 어깨뼈는 카메라와 함께 회전하므로, 방향 벡터는 캡슐 기준 수평으로만 계산.
        // sideDir에도 Y성분 제거해 고개 숙임/젖힘과 완전히 독립.
        Vector3 horizontalForward = new Vector3(_hub.transform.forward.x, 0f, _hub.transform.forward.z).normalized;
        Vector3 horizontalRight   = new Vector3(_hub.transform.right.x,   0f, _hub.transform.right.z  ).normalized;
        Vector3 sideDir = isLeft ? -horizontalRight : horizontalRight;

        Ray ray = new Ray(shoulder.position, sideDir);
        bool hitWall = Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxTouchDistance, _hub.wallRunner.wallLayerMask);

        // ── Retract Delay: 벽을 보면 타임스탬프 갱신 ───────────────────────
        if (hitWall)
        {
            bool isVerticalWall = Mathf.Abs(hit.normal.y) < 0.3f;
            Vector3 dirToHit   = (hit.point - shoulder.position).normalized;

            // fovDot: 수평 전방 기준으로 벽이 플레이어 앞쪽에 있는지 판정
            float fovDot = Vector3.Dot(horizontalForward, dirToHit);

            if (isVerticalWall && fovDot >= -0.1f)
            {
                lastSeenTime = Time.time; // 유효한 벽을 봤을 때만 갱신

                // ── 목표 위치 연산 (캡슐 절대 좌표 기준) ──────────────────
                // [버그2 수정] shoulder.position은 카메라의 자식이라 고개를 숙이면
                // 어깨 자체가 아래로 꺼져서 팔이 솟구치는 현상 발생.
                // 대신, 캡슐의 절대 바닥(_hub.transform.position)에 고정 높이를 더한
                // stableOrigin을 기준점으로 사용해 카메라 회전 영향을 완전히 차단.
                Vector3 stableOrigin = new Vector3(
                    _hub.transform.position.x,
                    _hub.transform.position.y + stableShoulderHeight,
                    _hub.transform.position.z);

                Vector3 currentXZVel = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z);
                bool    isMovingFast = currentXZVel.magnitude >= 2.0f;

                Vector3 idealTargetPos = stableOrigin
                                         + (horizontalForward * handForwardOffset)
                                         - (Vector3.up        * handHeightDownOffset)
                                         + (sideDir           * handSideOffset);
                Vector3 idealHintPos = hit.point;

                if (isMovingFast)
                {
                    // 달리기 중: 1초에 걸쳐 Ease-In으로 고정
                    const float transitionDuration = 1.0f;
                    if (slideTimer < transitionDuration)
                    {
                        slideTimer += Time.deltaTime;
                        float t = (slideTimer / transitionDuration);
                        t = t * t; // Ease-In 곡선
                        targetPos = Vector3.Lerp(targetPos, idealTargetPos, t);
                        hintPos   = Vector3.Lerp(hintPos,   idealHintPos,   t);
                    }
                    else
                    {
                        // 1초 경과 → Jitter 제로 박제
                        targetPos = idealTargetPos;
                        hintPos   = idealHintPos;
                    }
                    isAnchored = false;
                }
                else
                {
                    // 정지 중: 월드 앵커 고정
                    slideTimer = 0f;
                    if (!isAnchored)
                    {
                        anchorPos  = idealTargetPos;
                        isAnchored = true;
                        hintPos    = idealHintPos;
                    }
                    targetPos = Vector3.Lerp(targetPos, anchorPos,    Time.deltaTime * 15f);
                    hintPos   = Vector3.Lerp(hintPos,   idealHintPos, Time.deltaTime * 15f);
                }
            }
        }

        // ── 앵커 리셋: 벽이 완전히 사라진 상태면 리셋 ──────────────────────
        float timeSinceLastSeen = Time.time - lastSeenTime;
        if (timeSinceLastSeen >= retractDelay)
        {
            isAnchored = false;
            slideTimer = 0f;
        }

        // ── Weight 계산: RetractDelay 이내면 1, 초과하면 0으로 SmoothDamp ──
        float targetWeight = (timeSinceLastSeen < retractDelay) ? 1f : 0f;

        // 뻗을 때(reach) vs 거둘 때(retract) 속도 분리
        float smoothTime = (targetWeight > weight) ? reachSmoothTime : retractSmoothTime;
        weight = Mathf.SmoothDamp(weight, targetWeight, ref weightVel, smoothTime);
    }

    // ────────────────────────────────────────────────────────────────────────
    private void OnAnimatorIKModule(int layerIndex)
    {
        if (_animator == null) return;

        ApplyHandIK(AvatarIKGoal.LeftHand,  AvatarIKHint.LeftElbow,  _leftTargetPos,  _leftHintPos,  _leftIkWeight,  isLeft: true);
        ApplyHandIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, _rightTargetPos, _rightHintPos, _rightIkWeight, isLeft: false);
    }

    private void ApplyHandIK(AvatarIKGoal handGoal, AvatarIKHint elbowHint, Vector3 pos, Vector3 elbowPos, float weight, bool isLeft)
    {
        if (weight <= 0.01f)
        {
            _animator.SetIKPositionWeight(handGoal, 0f);
            _animator.SetIKRotationWeight(handGoal, 0f);
            _animator.SetIKHintPositionWeight(elbowHint, 0f);
            return;
        }

        _animator.SetIKPositionWeight(handGoal, weight);
    // [손목 회전] 벽을 즧을 때 팔뎩이 자연스러워 보이도록 있는 오프셋을 적용.
        // 허브의 수평 회전을 기준으로 Euler 오프셋을 월드 회전량으로 변환 후 wristRotBlend로 랜드.
        // wristRotBlend=0 → 애니메이션 100%, wristRotBlend=1 → IK 회전 100%.
        Vector3  rotEuler     = isLeft ? leftWristRotOffset : rightWristRotOffset;
        // 콰슐 수평축 기준 회전을 월드스페이스로 변환
        Quaternion baseRot    = Quaternion.Euler(0f, _hub.transform.eulerAngles.y, 0f);
        Quaternion wristRot   = baseRot * Quaternion.Euler(rotEuler);
        _animator.SetIKRotationWeight(handGoal, weight * wristRotBlend);
        _animator.SetIKRotation(handGoal, wristRot);

        _animator.SetIKHintPositionWeight(elbowHint, 0f); // 팀꿈치 IK 비틀림 차단

        _animator.SetIKPosition(handGoal, pos);
        _animator.SetIKHintPosition(elbowHint, elbowPos);
    }
}
