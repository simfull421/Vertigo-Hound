using UnityEngine;
using System;

[Serializable]
public sealed class PlayerVaultIK
{
    [Header("Vault IK Settings")]
    public float handSpread = 0.3f; // 양손을 모서리에서 얼마나 벌릴지
    public float ikBlendSpeed = 20f; // 0.15초 안에 반응해야 하므로 블렌드 스피드가 매우 빨라야 함

    private PlayerController _hub;
    private Animator _animator;
    private float _vaultIkWeight = 0f;

    public void Initialize(PlayerController hub, Animator animator)
    {
        _hub = hub;
        _animator = animator;
        // PlayerIKRouter를 통해 OnAnimatorIK 이벤트에 연결
        if (_animator != null)
        {
            PlayerIKRouter router = _animator.gameObject.GetComponent<PlayerIKRouter>();
            if (router == null) router = _animator.gameObject.AddComponent<PlayerIKRouter>();
            router.onIK += OnAnimatorIKModule;
        }
    }

    public void UpdateModule()
    {
        // Vault 상태일 때만 Weight를 1로 폭발적으로 올림
        float targetWeight = _hub.vault.IsVaulting ? 1f : 0f;
        _vaultIkWeight = Mathf.Lerp(_vaultIkWeight, targetWeight, Time.deltaTime * ikBlendSpeed);
    }

    public void OnAnimatorIKModule(int layerIndex)
    {
        if (_animator == null || _vaultIkWeight <= 0.01f) return;

        // PlayerVault가 캐싱해둔 모서리 좌표를 가져옴
        Vector3 edgePos = _hub.vault.VaultEdgePos;
        Vector3 forward = _hub.transform.forward;
        Vector3 right = _hub.transform.right;

        // 모서리를 기준으로 양옆(handSpread)으로 살짝 벌린 좌표가 타겟
        Vector3 leftHandPos = edgePos - (right * handSpread) - (forward * 0.1f);
        Vector3 rightHandPos = edgePos + (right * handSpread) - (forward * 0.1f);

        // Vault 포즈는 손이 위를 향해 짚는 형태
        Quaternion handRot = Quaternion.LookRotation(-Vector3.up, forward);

        ApplyVaultIK(AvatarIKGoal.LeftHand, leftHandPos, handRot);
        ApplyVaultIK(AvatarIKGoal.RightHand, rightHandPos, handRot);
    }

    private void ApplyVaultIK(AvatarIKGoal handGoal, Vector3 pos, Quaternion rot)
    {
        _animator.SetIKPositionWeight(handGoal, _vaultIkWeight);
        // Vault는 손바닥을 펴고 짚어야 하므로 Rotation Weight도 적용합니다.
        _animator.SetIKRotationWeight(handGoal, _vaultIkWeight);

        _animator.SetIKPosition(handGoal, pos);
        _animator.SetIKRotation(handGoal, rot);
    }
}
