using UnityEngine;

public struct BreachData
{
    public Vector3 impactForce;
    public Vector3 impactPoint;
    public float sourceSpeed;
}

// 맞고 날아갈 수 있는 오브젝트들이 상속받을 인터페이스
public interface IBreachable
{
    void OnBreached(BreachData data);
}