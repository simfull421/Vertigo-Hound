using UnityEngine;
using System;

/// <summary>
/// 데이터 키(Key)의 현재 위치와 소유권 상태를 중앙에서 관리하는 싱글톤 매니저입니다.
/// </summary>
public class DataKeyManager : MonoBehaviour
{
    public static DataKeyManager Instance { get; private set; }

    [Header("Key Status")]
    [Tooltip("실제 씬에 존재하는 데이터 키 게임오브젝트 (인스펙터 할당 필수)")]
    public GameObject actualKeyObject;

    [Tooltip("현재 키를 소유하고 있는 대상의 Transform")]
    public Transform currentKeyHolder;
    [Tooltip("키의 현재 월드 좌표")]
    public Vector3 currentKeyPosition;
    [Tooltip("플레이어가 키를 들고 있는지 여부")]
    public bool isKeyHeldByPlayer;

    [Header("Key Prefab (Obsolete - 사용하지 않음)")]
    [Tooltip("사용하지 않음. actualKeyObject를 재배치하는 방식으로 대체됨")]
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

            if (actualKeyObject != null)
            {
                if (isPlayer)
                {
                    // 플레이어가 획득한 경우 키를 뷰모델 등에 넣거나 위치만 맞춥니다.
                    actualKeyObject.transform.SetParent(newHolder);
                    actualKeyObject.transform.localPosition = Vector3.zero;
                }
                else
                {
                    // AI가 획득한 경우 오른손 뼈에 부착합니다.
                    AITrollingModule trollModule = newHolder.GetComponent<AITrollingModule>();
                    if (trollModule != null && trollModule.rightHandBone != null)
                    {
                        actualKeyObject.transform.SetParent(trollModule.rightHandBone);
                        actualKeyObject.transform.localPosition = Vector3.zero;
                        actualKeyObject.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        actualKeyObject.transform.SetParent(newHolder);
                        actualKeyObject.transform.localPosition = Vector3.up * 1f; // 오른손이 없으면 가슴 높이로
                    }
                }
                actualKeyObject.SetActive(true);
            }
        }
        else
        {
            // 소유자가 없을 때: 키 오브젝트를 부모에서 분리
            if (actualKeyObject != null)
            {
                actualKeyObject.transform.SetParent(null);
            }
        }

        OnKeyHolderChanged?.Invoke(newHolder);
    }

    public void DropKeyAt(Vector3 position)
    {
        SetKeyHolder(null, false);
        UpdateKeyPosition(position);

        if (actualKeyObject != null)
        {
            actualKeyObject.transform.SetParent(null);
            actualKeyObject.transform.position = position;
            actualKeyObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[DataKeyManager] actualKeyObject가 null입니다. 인스펙터에서 할당해주세요.");
        }
    }
}
