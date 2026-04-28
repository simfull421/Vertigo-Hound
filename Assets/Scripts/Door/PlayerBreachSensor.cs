using UnityEngine;

public class PlayerBreachSensor : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("플레이어의 PlayerController 컴포넌트를 연결해주세요.")]
    public PlayerController hub;

    private void OnTriggerEnter(Collider other)
    {
        // 허브 연결이 안 되어 있거나 모듈이 없으면 작동하지 않음
        if (hub == null || hub.breachModule == null) return;

        // 부딪힌 객체(또는 부모)가 부술 수 있는(IBreachable) 문인지 확인
        IBreachable target = other.GetComponentInParent<IBreachable>();
        if (target != null)
        {
            // [핵심] 센서가 직접 부수지 않고, 모듈에게 실행을 위임합니다!
            // 이렇게 해야 모듈 안에 있는 카메라 쥬스 코드가 함께 실행됩니다.
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            hub.breachModule.ExecuteBreach(target, hitPoint);
        }
    }
}