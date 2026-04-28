using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 아주 심플한 횡이동(A/D) 틸트 모듈.
/// 왼쪽 이동 시 살짝 기울어지고, 그 위에 헤드밥이 자연스럽게 합산됩니다.
/// </summary>
[Serializable]
public sealed class JuiceStrafeTilt : IJuiceModule
{
    [Header("Simple Strafe Tilt")]
    [Tooltip("A/D 이동 시 고정될 카메라 기울기 각도")]
    public float tiltAngle = 3f; 
    
    [Tooltip("기울어지는 반응 속도")]
    public float tiltSpeed = 10f;

    public bool IsActive => _routine != null;
    public Vector3 PosOffset => Vector3.zero;
    public Vector3 RotOffset { get; private set; }
    public float FovOverride => 0f;
    public float FovOffset => 0f;

    private float _currentTiltZ = 0f;
    private float _targetInputX = 0f;
    private Coroutine _routine;
    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
    }

    public void TriggerStrafe(float inputX)
    {
        _targetInputX = inputX;

        if (_routine == null && (Mathf.Abs(inputX) > 0.01f || Mathf.Abs(_currentTiltZ) > 0.01f))
        {
            _hub.RegisterModule(this);
            _routine = _hub.StartCoroutine(Routine());
        }
    }

    private IEnumerator Routine()
    {
        while (true)
        {
            float targetTiltZ = -_targetInputX * tiltAngle;
            _currentTiltZ = Mathf.Lerp(_currentTiltZ, targetTiltZ, Time.deltaTime * tiltSpeed);
            RotOffset = new Vector3(0f, 0f, _currentTiltZ);

            if (Mathf.Abs(_targetInputX) < 0.01f && Mathf.Abs(_currentTiltZ) < 0.01f)
            {
                _currentTiltZ = 0f;
                RotOffset = Vector3.zero;
                break;
            }

            yield return null;
        }

        _routine = null;
        _hub.UnregisterModule(this);
    }
}