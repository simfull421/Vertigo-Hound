using UnityEngine;

public interface IJuiceModule
{
    bool IsActive { get; }
    Vector3 PosOffset { get; }
    Vector3 RotOffset { get; }
    float FovOverride { get; }
    float FovOffset { get; }
}
