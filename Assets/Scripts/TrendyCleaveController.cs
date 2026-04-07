using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EzySlice;

public class TrendyCleaveController : MonoBehaviour
{
    [Header("Slice Settings")]
    public Material crossSectionMaterial;
    public float sliceCooldown = 0.5f;

    [Header("Visuals")]
    public TrailRenderer drawTrail;
    public Camera mainCamera;

    [Header("Optimization (Hull Pool)")]
    public int initialPoolSize = 20;
    private Queue<GameObject> hullPool = new Queue<GameObject>();

    // 상태 머신 용 Enum
    private enum CleaveState { Idle, SlowmoWindow, Aiming }
    private CleaveState currentState = CleaveState.Idle;
    
    // PlayerController에서 조준 중일 때 카메라 고정을 위해 알아야 할 프로퍼티
    public bool isAiming => currentState == CleaveState.Aiming;

    private float slowmoTimer = 0f;
    private const float SLOWMO_DURATION = 3.0f;
    private GameObject currentTargetCube;
    private Vector3 drawStartPosition;

    private IInputProvider input;
    private IPlayerState playerState; // DI (결합도 분리)

    public void Initialize(IInputProvider input, IPlayerState playerState)
    {
        this.input = input;
        this.playerState = playerState;
        
        drawTrail.enabled = false;
        drawTrail.emitting = false;
        InitializeHullPool();
    }

    private void InitializeHullPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject hull = new GameObject("SlicedHull_Pooled");
            hull.AddComponent<MeshFilter>();
            hull.AddComponent<MeshRenderer>();
            hull.AddComponent<MeshCollider>().convex = true;
            hull.AddComponent<Rigidbody>();
            hull.SetActive(false);
            hullPool.Enqueue(hull);
        }
    }

    void Update()
    {
        if (input == null || playerState == null) return;
        HandleCleaveFlow();
    }

    private void HandleCleaveFlow()
    {
        switch (currentState)
        {
            case CleaveState.Idle:
                // 공중 상태에서만 타겟 검색 레이캐스트 발사
                if (!playerState.IsGrounded)
                {
                    SearchForTarget();
                }
                break;

            case CleaveState.SlowmoWindow:
                // unscaledDeltaTime으로 멈춰진 시간의 흐름과 무관하게 실제 시간을 잰다.
                slowmoTimer += Time.unscaledDeltaTime;

                if (input.CleaveHeld)
                {
                    StartAiming();
                }
                else if (slowmoTimer >= SLOWMO_DURATION)
                {
                    // 아무것도 안 하고 3초가 경과 시 덤블링 실패 처리
                    FailToCleave();
                }
                break;

            case CleaveState.Aiming:
                UpdateDrawEffect();

                // 마우스 좌클릭을 떼는(Release) 순간 실행
                if (!input.CleaveHeld)
                {
                    ExecuteCleave();
                }
                break;
        }
    }

    private void SearchForTarget()
    {
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 8f)) // 사거리 제한 8f 내
        {
            if (hit.transform.CompareTag("TargetCube"))
            {
                currentTargetCube = hit.transform.gameObject;
                
                // 오토 슬로우모션 진입
                currentState = CleaveState.SlowmoWindow;
                slowmoTimer = 0f;
                Time.timeScale = 0.1f;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }
        }
    }

    private void StartAiming()
    {
        currentState = CleaveState.Aiming;
        drawTrail.enabled = true;
        drawTrail.emitting = true;
        drawTrail.Clear();
        drawStartPosition = Input.mousePosition; 
        
        // 화면을 자유롭게 그릴 수 있도록 커서 락 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void UpdateDrawEffect()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f; 
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        drawTrail.transform.position = worldPos;
    }

    private void ExecuteCleave()
    {
        ResetTimeScale();
        drawTrail.emitting = false;
        drawTrail.enabled = false;
        
        // 다시 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 drawEndPosition = Input.mousePosition;
        Vector2 drawVector = (drawEndPosition - drawStartPosition).normalized;
        
        // 방향이 최소한 1프레임 이상 그어졌고 타겟이 존재하는 경우만 예외 처리 없이 스윽 벰
        if (drawVector != Vector2.zero && currentTargetCube != null && currentTargetCube.activeSelf)
        {
            float sliceAngle = Mathf.Atan2(drawVector.y, drawVector.x) * Mathf.Rad2Deg;
            
            // 기존에는 충돌 지점을 레이캐스트로 쐈으나 이미 추적된 오브젝트이므로 자체 트랜스폼 이용
            Vector3 hitPoint = currentTargetCube.transform.position;
            SliceObject(currentTargetCube, hitPoint, sliceAngle);
        }

        currentState = CleaveState.Idle;
        currentTargetCube = null;
    }

    private void FailToCleave()
    {
        // 시간 배속 원상 복원 및 상태 초기화
        ResetTimeScale();
        currentState = CleaveState.Idle;
        
        // 3초간 입력이 없어 박치기 실패 -> 백덤블링 추진력
        if (currentTargetCube != null)
        {
            Rigidbody playerRb = playerState.PlayerRigidbody;
            if (playerRb != null)
            {
                // 충돌체크하듯 뒤 방향 & 하늘 쪽으로 튕겨냅니다
                playerRb.linearVelocity = Vector3.zero;
                Vector3 bounceDir = (-mainCamera.transform.forward * 1.5f + Vector3.up).normalized;
                playerRb.AddForce(bounceDir * 35f, ForceMode.Impulse);
                
                // TODO: 카메라 회전(백덤블링 연출) 로직 트리거 등을 여기에 추가
            }
        }
        
        currentTargetCube = null;
    }

    private void ResetTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void SliceObject(GameObject obj, Vector3 hitPoint, float angle)
    {
        Quaternion sliceRotation = Quaternion.Euler(0, 0, angle);
        Vector3 sliceNormal = sliceRotation * Vector3.up;

        SlicedHull hull = obj.Slice(hitPoint, sliceNormal, crossSectionMaterial);

        if (hull != null)
        {
            GameObject upperHull = GetPooledHull();
            GameObject lowerHull = GetPooledHull();

            SetupHullData(upperHull, hull.upperHull, obj.transform, sliceNormal * 15f);
            SetupHullData(lowerHull, hull.lowerHull, obj.transform, -sliceNormal * 15f);

            obj.SetActive(false);
        }
    }

    private GameObject GetPooledHull()
    {
        if (hullPool.Count > 0) return hullPool.Dequeue();
        
        GameObject hull = new GameObject("SlicedHull_Pooled");
        hull.AddComponent<MeshFilter>();
        hull.AddComponent<MeshRenderer>();
        hull.AddComponent<MeshCollider>().convex = true;
        hull.AddComponent<Rigidbody>();
        return hull;
    }

    private void SetupHullData(GameObject hullObj, Mesh generatedMesh, Transform originalTransform, Vector3 explodeForce)
    {
        hullObj.transform.position = originalTransform.position;
        hullObj.transform.rotation = originalTransform.rotation;
        hullObj.transform.localScale = originalTransform.localScale;

        hullObj.GetComponent<MeshFilter>().sharedMesh = generatedMesh;
        hullObj.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { crossSectionMaterial };
        hullObj.GetComponent<MeshCollider>().sharedMesh = generatedMesh;

        hullObj.SetActive(true);

        Rigidbody rb = hullObj.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero; 
        rb.AddForce(explodeForce, ForceMode.Impulse); 

        StartCoroutine(ReturnHullToPool(hullObj, 2f));
    }

    private IEnumerator ReturnHullToPool(GameObject hullObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        hullObj.SetActive(false);

        Mesh meshToDestroy = hullObj.GetComponent<MeshFilter>().sharedMesh;
        if (meshToDestroy != null)
        {
            Destroy(meshToDestroy);
        }
        
        hullObj.GetComponent<MeshFilter>().sharedMesh = null;
        hullObj.GetComponent<MeshCollider>().sharedMesh = null;

        hullPool.Enqueue(hullObj);
    }
}