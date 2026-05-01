using UnityEngine;
using Pathfinding;

public sealed class AILocomotionController
{
    private const float DirectionEpsilon = 0.01f;
    
    // IAstarAI와 RVOController를 통합하는 FollowerEntity
    private readonly FollowerEntity _ai;
    private readonly Transform _transform;

    public AILocomotionController(FollowerEntity ai, Transform transform)
    {
        _ai = ai;
        _transform = transform;
    }

    public bool IsValid => _ai != null;

    public Vector3 CurrentVelocity
    {
        get
        {
            if (_ai == null) return Vector3.zero;
            Vector3 velocity = _ai.velocity;
            velocity.y = 0f;
            return velocity;
        }
    }

    public float CurrentSpeed => CurrentVelocity.magnitude;

    public Vector3 GetDesiredDirection()
    {
        if (_ai == null || _transform == null) return Vector3.zero;

        Vector3 desired = _ai.desiredVelocity;
        desired.y = 0f;

        // desiredVelocity가 0에 가까울 때는 경로 계산 중이거나 정지 중일 수 있어 steeringTarget으로 보완합니다.
        if (desired.sqrMagnitude < DirectionEpsilon)
        {
            Vector3 toTarget = _ai.steeringTarget - _transform.position;
            toTarget.y = 0f;
            desired = toTarget;
        }

        return desired.sqrMagnitude > DirectionEpsilon ? desired.normalized : Vector3.zero;
    }

    public void SetDestination(Vector3 destination)
    {
        if (_ai == null) return;
        _ai.destination = destination;
    }

    public void Teleport(Vector3 position, bool clearPath = true)
    {
        if (_ai == null) return;
        _ai.Teleport(position, clearPath);
    }

    public void PauseMovement()
    {
        if (_ai == null) return;
        
        // [수정] 구조체이므로 변수에 담아서 수정 (CS1612 해결)
        var repath = _ai.autoRepath;
        repath.mode = AutoRepathPolicy.Mode.Never; 
        _ai.autoRepath = repath;

        _ai.isStopped = true;
        _ai.simulateMovement = false; // canMove의 최신 이름

        // RVO 설정
        var rvoSettings = _ai.rvoSettings;
        rvoSettings.locked = true;
        _ai.rvoSettings = rvoSettings;
    }

    public void ResumeMovement()
    {
        if (_ai == null) return;
        
        // [수정] 구조체이므로 변수에 담아서 수정
        var repath = _ai.autoRepath;
        repath.mode = AutoRepathPolicy.Mode.Dynamic;
        _ai.autoRepath = repath;

        _ai.isStopped = false;
        _ai.simulateMovement = true;

        // RVO 설정 복구
        var rvoSettings = _ai.rvoSettings;
        rvoSettings.locked = false;
        _ai.rvoSettings = rvoSettings;
    }
}