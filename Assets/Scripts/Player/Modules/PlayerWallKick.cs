using UnityEngine;
using System;

[Serializable]
public sealed class PlayerWallKick
{
    [Header("Wall Kick")]
    public LayerMask wallLayerMask;
    public float wallCheckDistance = 1.5f;
    public float wallKickForceHorizontal = 15f;
    public float wallKickForceVertical = 5f;
    
    public bool CanWallKick { get; private set; }
    
    private Vector3 _wallNormal;
    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void UpdateModule()
    {
        CheckWall();
    }

    private void CheckWall()
    {
        if (_hub.movement.IsGrounded)
        {
            CanWallKick = false;
            return;
        }

        // 벽 탐색 (정면)
        if (Physics.Raycast(_hub.transform.position, _hub.transform.forward, out RaycastHit hit, wallCheckDistance, wallLayerMask))
        {
            float dot = Vector3.Dot(_hub.transform.forward, hit.normal);
            if (dot < -0.9f)
            {
                CanWallKick = true;
                _wallNormal = hit.normal;
                return;
            }
        }
        
        CanWallKick = false;
    }

    public void HandleJump()
    {
        if (!CanWallKick) return;

        // 기존 운동량 초기화
        _hub.Rb.linearVelocity = Vector3.zero;

        // 벽 노멀 방향으로 강한 수평, 약간의 수직 충격량 적용
        Vector3 kickDir = _wallNormal.normalized;
        Vector3 force = (kickDir * wallKickForceHorizontal) + (Vector3.up * wallKickForceVertical);

        _hub.Rb.AddForce(force, ForceMode.Impulse);

        // 즉시 젖혀지는 주스 효과 트리거
        if (_hub.juiceController != null)
        {
            _hub.juiceController.TriggerWallKickJuice(0.35f); 
        }

        CanWallKick = false;
    }
}
