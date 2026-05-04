using UnityEngine;
using System;

/// <summary>
/// 데이터 키(Key)의 현재 위치와 소유권 상태를 중앙에서 관리하는 싱글톤 매니저입니다.
/// </summary>
public class DataKeyManager : MonoBehaviour
{
    public static DataKeyManager Instance { get; private set; }

    [Header("Key Status")]
    [Tooltip("현재 키를 소유하고 있는 대상의 Transform")]
    public Transform currentKeyHolder;
    [Tooltip("키의 현재 월드 좌표")]
    public Vector3 currentKeyPosition;
    [Tooltip("플레이어가 키를 들고 있는지 여부")]
    public bool isKeyHeldByPlayer;

    [Header("Key Prefab")]
    [Tooltip("월드에 떨어뜨릴 키 프리팹")]
    public GameObject keyDropPrefab;

    /// <summary>
    /// 키의 소유자가 변경되었을 때 발생하는 이벤트 (매개변수: 새로운 소유자의 Transform)
    /// </summary>
    public event Action<Transform> OnKeyHolderChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 씬 뷰 전환 시 유지하려면 주석 해제
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 키 오브젝트가 물리적으로 날아가거나 바닥에 있을 때 위치를 지속적으로 갱신합니다.
    /// </summary>
    public void UpdateKeyPosition(Vector3 position)
    {
        currentKeyPosition = position;
    }

    /// <summary>
    /// 키를 누군가 획득/탈취했을 때 호출합니다.
    /// </summary>
    public void SetKeyHolder(Transform newHolder, bool isPlayer)
    {
        if (currentKeyHolder == newHolder) return;

        currentKeyHolder = newHolder;
        isKeyHeldByPlayer = isPlayer;
        
        if (newHolder != null)
        {
            currentKeyPosition = newHolder.position;
        }

        OnKeyHolderChanged?.Invoke(newHolder);
    }

    public void DropKeyAt(Vector3 position)
    {
        SetKeyHolder(null, false);
        UpdateKeyPosition(position);

        if (keyDropPrefab != null)
        {
            Instantiate(keyDropPrefab, position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[DataKeyManager] keyDropPrefab이 설정되지 않아 키 오브젝트를 생성하지 못했습니다.");
        }
    }
}
