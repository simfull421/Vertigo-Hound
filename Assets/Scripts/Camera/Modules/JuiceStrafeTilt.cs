using UnityEngine;
using System;

/// <summary>
/// 아주 심플한 횡이동(A/D) 틸트 모듈.
/// 왼쪽 이동 시 살짝 기울어지고, 그 위에 헤드밥이 자연스럽게 합산됩니다.
/// </summary>
[Serializable]
public sealed class JuiceStrafeTilt
{
    [Header("Simple Strafe Tilt")]
    [Tooltip("A/D 이동 시 고정될 카메라 기울기 각도")]
    public float tiltAngle = 3f; 
    
    [Tooltip("기울어지는 반응 속도")]
    public float tiltSpeed = 10f;

    // 외부로 노출할 출력값
    public Vector3 RotOffset { get; private set; }

    private float _currentTiltZ = 0f;

    public void UpdateModule(float strafeInput, bool isActive)
    {
        // isActive가 true일 때만 입력값에 따라 고정 각도로 목표치 설정
        float targetTiltZ = isActive ? -strafeInput * tiltAngle : 0f;

        // 스무스하게 목표 각도로 이동
        _currentTiltZ = Mathf.Lerp(_currentTiltZ, targetTiltZ, Time.deltaTime * tiltSpeed);

        // Z축 회전 오프셋만 출력 (이 값을 CameraJuiceController가 합산함)
        RotOffset = new Vector3(0f, 0f, _currentTiltZ);
    }
}