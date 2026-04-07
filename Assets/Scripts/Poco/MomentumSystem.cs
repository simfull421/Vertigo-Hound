using UnityEngine;

public class MomentumSystem : IMomentumProvider
{
    public float Value { get; private set; }
    public float MaxValue { get; private set; } = 100f;
    public float NormalizedValue => Value / MaxValue;

    public void Calculate(float currentSpeed, float walkSpeed, bool isDashing, float deltaTime)
    {
        if (isDashing)
            Value = Mathf.MoveTowards(Value, 0f, 25f * deltaTime);
        else if (currentSpeed > walkSpeed * 0.8f)
            Value = Mathf.MoveTowards(Value, MaxValue, 15f * deltaTime);
        else
            Value = Mathf.MoveTowards(Value, 0f, 10f * deltaTime);
    }

    public void AddBoost(float amount)
    {
        Value = Mathf.Clamp(Value + amount, 0f, MaxValue);
    }
}