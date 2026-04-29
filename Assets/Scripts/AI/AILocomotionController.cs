using UnityEngine;
using Pathfinding;
using Pathfinding.RVO;

public sealed class AILocomotionController
{
    private const float DirectionEpsilon = 0.01f;
    private readonly IAstarAI _ai;
    private readonly RVOController _rvo;
    private readonly Transform _transform;

    public AILocomotionController(IAstarAI ai, RVOController rvo, Transform transform)
    {
        _ai = ai;
        _rvo = rvo;
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
        // canSearch: 경로 탐색 중지, simulateMovement: 이동/회피 시뮬레이션 중지, isStopped: 경로 유지한 채 정지
        _ai.canSearch = false;
        _ai.simulateMovement = false;
        _ai.isStopped = true;

        if (_rvo != null) _rvo.locked = true;
    }

    public void ResumeMovement()
    {
        if (_ai == null) return;
        // 경로/이동/회피를 모두 재개하여 IAstarAI의 기본 이동으로 복귀
        _ai.canSearch = true;
        _ai.simulateMovement = true;
        _ai.isStopped = false;

        if (_rvo != null) _rvo.locked = false;
    }
}
