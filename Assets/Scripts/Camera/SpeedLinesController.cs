using UnityEngine;

/// <summary>
/// 플레이어의 이동 속도를 감지하여 스피드 라인(화면 외곽 선) 이펙트 머티리얼의 투명도를 조절하는 독립 컨트롤러입니다.
/// </summary>
public class SpeedLinesController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("속도를 측정할 플레이어의 본체 허브")]
    public PlayerController player;
    
    [Tooltip("조작할 스피드 라인 포스트 프로세싱 머티리얼")]
    public Material speedLinesMaterial;

    [Header("Speed Thresholds")]
    [Tooltip("스피드 라인이 보이기 시작하는 최소 속도")]
    public float minSpeedForLines = 8f; 
    [Tooltip("스피드 라인이 최대 진해지는 속도")]
    public float maxSpeedForLines = 18f;
    
    [Tooltip("선이 나타나고 사라지는 부드러운 전환 속도")]
    public float lerpSpeed = 5f;

    private int _intensityPropID;

    private void Awake()
    {
        // 셰이더 프로퍼티 ID 캐싱 (성능 최적화)
        _intensityPropID = Shader.PropertyToID("_Intensity");
        
        // 시작할 때 투명도를 0으로 끄고 시작
        if (speedLinesMaterial != null)
        {
            speedLinesMaterial.SetFloat(_intensityPropID, 0f);
        }
    }

    private void LateUpdate()
    {
        if (speedLinesMaterial == null || player == null || player.Rb == null) return;

        // 1. 플레이어의 실제 물리(Rigidbody) 평면 이동 속도 측정
        // (유저님의 코드는 CharacterController 기준이라, 현재 프로젝트에 맞게 Rb.linearVelocity로 수정했습니다)
        Vector3 horizontalVel = new Vector3(player.Rb.linearVelocity.x, 0f, player.Rb.linearVelocity.z);
        float currentSpeed = horizontalVel.magnitude;

        // 2. 현재 속도를 기준으로 0 ~ 1 사이의 Target Intensity 값 계산
        float targetIntensity = Mathf.InverseLerp(minSpeedForLines, maxSpeedForLines, currentSpeed);

        // 3. 값이 너무 팍팍 변하지 않게 부드럽게 보간 (Ease-Out)
        float currentIntensity = speedLinesMaterial.GetFloat(_intensityPropID);
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * lerpSpeed);

        // 4. 셰이더에 최종 값 쏴주기
        speedLinesMaterial.SetFloat(_intensityPropID, currentIntensity);
    }
}
