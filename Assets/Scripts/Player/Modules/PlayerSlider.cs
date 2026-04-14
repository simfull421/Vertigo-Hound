using UnityEngine;
using System;

[Serializable]
public sealed class PlayerSlider
{
    [Header("Sliding (Hardcore)")]
    public float minSpeedForSlide = 5f;     
    public float crouchSpeed = 3f;
    public float slideImpulse = 22f;        // 폭발적 초기 가속
    public float slideDragAccumulation = 25f; // 강한 감속 (짧고 빠르게)
    public float slideHeightRatio = 0.5f;

    public bool IsSliding { get; private set; }

    private float _originalCapsuleHeight;
    private Vector3 _originalCapsuleCenter;

    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
        _originalCapsuleHeight = _hub.Capsule.height;
        _originalCapsuleCenter = _hub.Capsule.center;
    }

    public void UpdateModule()
    {
        CheckSlide();
    }

    public void FixedUpdateModule()
    {
        SlideMovement();
    }

    public void HandleJump()
    {
        float checkDist = _originalCapsuleHeight - _hub.Capsule.height;
        Vector3 bottom = _hub.Capsule.bounds.center - new Vector3(0, _hub.Capsule.bounds.extents.y, 0);
        bool hasCeiling = Physics.SphereCast(bottom + Vector3.up * _hub.Capsule.radius, _hub.Capsule.radius, Vector3.up, out _, checkDist, _hub.movement.groundMask);

        if (!hasCeiling)
        {
            StopSlide(true);
        }
        else 
        {
            return; 
        }

        _hub.Rb.linearVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
        Vector3 jumpDir = Vector3.Lerp(Vector3.up, _hub.movement.GroundNormal, 0.6f).normalized;
        
        Vector3 finalForce = jumpDir * _hub.movement.jumpForce;

        Vector3 currentXZDir = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).normalized;
        finalForce += currentXZDir * (slideImpulse * 0.9f);

        _hub.Rb.AddForce(finalForce, ForceMode.Impulse);

        float v0 = _hub.movement.jumpForce / _hub.Rb.mass;
        float g = Physics.gravity.magnitude; 
        float timeToApex = v0 / g;

        if (_hub.cameraActionController != null)
        {
             _hub.cameraActionController.TriggerRandomPattern(timeToApex);
        }
    }

    private void CheckSlide()
    {
        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z);
        float currentSpeed = currentXZVelocity.magnitude;

        if (!IsSliding)
        {
            if (_hub.InputProv.SlideHeld && _hub.movement.IsGrounded && currentSpeed > minSpeedForSlide && !_hub.wallRunner.IsWallRunning)
            {
                StartSlide();
            }
        }
        else
        {
            if (!_hub.InputProv.SlideHeld || currentSpeed <= crouchSpeed || !_hub.movement.IsGrounded || _hub.wallRunner.IsWallRunning)
            {
                AttemptStopSlide();
            }
        }
    }

    private void StartSlide()
    {
        IsSliding = true;
        
        _hub.Capsule.height = _originalCapsuleHeight * slideHeightRatio;
        float dipAmount = _originalCapsuleHeight * (1f - slideHeightRatio) / 2f;
        _hub.Capsule.center = new Vector3(_originalCapsuleCenter.x, _originalCapsuleCenter.y - dipAmount, _originalCapsuleCenter.z);

        Vector3 slideDir = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z).normalized;
        if (slideDir.sqrMagnitude < 0.1f) slideDir = _hub.transform.forward;
        
        _hub.Rb.AddForce(slideDir * slideImpulse, ForceMode.Impulse);

        if (_hub.juiceController != null)
        {
            _hub.juiceController.TriggerSlideStart(dipAmount);
        }
    }

    private void AttemptStopSlide()
    {
        float checkDist = _originalCapsuleHeight - _hub.Capsule.height;
        Vector3 bottom = _hub.Capsule.bounds.center - new Vector3(0, _hub.Capsule.bounds.extents.y, 0);
        bool hasCeiling = Physics.SphereCast(bottom + Vector3.up * _hub.Capsule.radius, _hub.Capsule.radius, Vector3.up, out _, checkDist, _hub.movement.groundMask);

        if (hasCeiling && _hub.movement.IsGrounded) 
        {
            return;
        }

        StopSlide(false);
    }

    public void StopSlide(bool isJumpHop)
    {
        IsSliding = false;
        _hub.Capsule.height = _originalCapsuleHeight;
        _hub.Capsule.center = _originalCapsuleCenter;

        if (_hub.juiceController != null)
        {
            _hub.juiceController.TriggerSlideEnd(isJumpHop);
        }
    }

    private void SlideMovement()
    {
        Vector2 moveInput = _hub.InputProv.MoveInput;
        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
        
        Vector3 newXZVelocity = Vector3.MoveTowards(currentXZVelocity, Vector3.zero, slideDragAccumulation * Time.fixedDeltaTime);
        
        Vector3 steering = (_hub.transform.right * moveInput.x).normalized * (_hub.movement.walkSpeed * 0.3f);
        newXZVelocity += steering * Time.fixedDeltaTime;

        _hub.Rb.linearVelocity = new Vector3(newXZVelocity.x, _hub.Rb.linearVelocity.y, newXZVelocity.z);
    }
}
