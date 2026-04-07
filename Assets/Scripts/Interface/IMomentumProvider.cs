public interface IMomentumProvider
{
    float Value { get; }
    float MaxValue { get; }
    float NormalizedValue { get; } // 0~1 사이 값 (이펙트 보간용으로 개꿀)
}