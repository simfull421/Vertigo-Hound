using UnityEngine;
using Pathfinding;

public sealed class AILocomotionController
{
    private const float DirectionEpsilon = 0.01f;
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

        // desiredVelocity가 0에 가까울 때는 경로 계산 중이거나 정지 중일 수 있어 목적지 방향으로 보완합니다.
        if (desired.sqrMagnitude < DirectionEpsilon)
        {
            Vector3 destination = _ai.destination;
            if (destination != Vector3.positiveInfinity)
            {
                Vector3 toTarget = destination - _transform.position;
                toTarget.y = 0f;
                desired = toTarget;
            }
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
        // canSearch: 경로 탐색 중지, canMove: 이동/회피 시뮬레이션 중지, isStopped: 경로 유지한 채 정지
        _ai.canSearch = false;
        _ai.canMove = false;
        _ai.isStopped = true;
    }

    public void ResumeMovement()
    {
        if (_ai == null) return;
        // 경로/이동/회피를 모두 재개하여 FollowerEntity 기본 이동으로 복귀
        _ai.canSearch = true;
        _ai.canMove = true;
        _ai.isStopped = false;
    }
}
