using UnityEngine;
using System;

[Serializable]
public class PlayerBreachModule
{
    [Header("Breach Settings")]
    public float breachMinSpeed = 8f;
    public float breachForceMultiplier = 50f;
    public float breachCooldown = 0.5f;

    private float _lastBreachTime = -999f;
    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    public void ExecuteBreach(IBreachable target, Vector3 impactPoint)
    {
        if (Time.time - _lastBreachTime < breachCooldown) return;

        Vector3 velocity = _hub.Rb.linearVelocity;
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speed = flatVelocity.magnitude;

        if (speed < breachMinSpeed) return;

        // 1. 물리 데이터 구성
        BreachData data = new BreachData
        {
            impactForce = _hub.transform.forward * speed * breachForceMultiplier,
            impactPoint = impactPoint,
            sourceSpeed = speed
        };

        // 2. 타겟 실행
        target.OnBreached(data);

        // 3. 쥬스 및 쿨다운
        _lastBreachTime = Time.time;
        
        if (_hub.juiceController != null)
        {
            _hub.juiceController.TriggerBreachJuice(0.4f);
        }

        // [Optional] 만약 필요하다면 오디오나 다른 효과도 여기서 실행
        Debug.Log($"[Breach] Door breached at speed: {speed}");
    }
}
