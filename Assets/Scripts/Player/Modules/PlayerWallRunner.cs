using UnityEngine;
using System;
using System.Collections;

[Serializable]
public sealed class PlayerWallRunner
{
    [Header("Wall Run & Jump")]
    public LayerMask wallLayerMask;
    public float wallCheckDistance = 1.5f;
    public float wallRunSpeed = 15f;
    public float wallRunGravity = 2f; 
    public float wallJumpForce = 15f;

    public bool IsWallRunning { get; private set; }
    public bool IsWallRight { get; private set; }
    
    private bool _isWallLeft;
    private bool _wasWallRunning;
    private Vector3 _wallNormal;
    private float _wallJumpTimer = 0f;

    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void UpdateModule()
    {
        if (_wallJumpTimer > 0f) _wallJumpTimer -= Time.deltaTime;
        CheckWall();
    }

    public void FixedUpdateModule()
    {
        WallRunMovement();
    }

    public void HandleJump()
    {
        _hub.Rb.useGravity = true;
        
        _hub.Rb.linearVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);

        Vector3 jumpDir = (_hub.movement.cameraPitchPivot.forward * 0.5f) + (_wallNormal * 1.5f) + (Vector3.up * 1.0f);
        _hub.Rb.AddForce(jumpDir.normalized * wallJumpForce, ForceMode.Impulse);

        _wallJumpTimer = 0.2f;
        IsWallRunning = false;
    }

    private void CheckWall()
    {
        if (_wallJumpTimer > 0f) 
        {
            _isWallLeft = false;
            IsWallRight = false;
            IsWallRunning = false;
            return;
        }

        IsWallRight = Physics.Raycast(_hub.transform.position, _hub.transform.right, out RaycastHit rightHit, wallCheckDistance, wallLayerMask);
        _isWallLeft = Physics.Raycast(_hub.transform.position, -_hub.transform.right, out RaycastHit leftHit, wallCheckDistance, wallLayerMask);

        if (IsWallRight) _wallNormal = rightHit.normal;
        else if (_isWallLeft) _wallNormal = leftHit.normal;
        else _wallNormal = Vector3.zero;

        _wasWallRunning = IsWallRunning;
        
        IsWallRunning = !_hub.movement.IsGrounded && (_isWallLeft || IsWallRight) && (_hub.InputProv.MoveInput.y > 0);

        if (IsWallRunning && !_wasWallRunning)
        {
            if (_hub.juiceController != null) 
            {
                _hub.juiceController.TriggerWallAttachJuice(IsWallRight);
            }
        }
    }

    private void WallRunMovement()
    {
        _hub.Rb.useGravity = false;
        
        Vector3 wallForward = Vector3.Cross(_wallNormal, Vector3.up);

        if (Vector3.Dot(_hub.transform.forward, wallForward) < 0)
        {
            wallForward = -wallForward; 
        }

        _hub.Rb.linearVelocity = new Vector3(wallForward.x * wallRunSpeed, -wallRunGravity, wallForward.z * wallRunSpeed);
    }
}
