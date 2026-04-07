using UnityEngine;
using System.Collections.Generic;

public class MassiveCubeFieldSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject cubePrefab; // 기본은 형태가 단순한 BoxCollider 추천 (가르기 전까지는)
    public Transform player;

    [Header("Massive Field Settings")]
    public int poolSize = 300; // 큐브 개수 대폭 증가 (자유도를 위해)
    public float fieldRadius = 150f; // 플레이어 주변으로 큐브가 퍼져있는 반경
    public float despawnDistance = 60f; // 플레이어 뒤로 이만큼 멀어지면 앞쪽으로 재배치

    [Header("Random Size Ranges")]
    public Vector2 sizeRangeX = new Vector2(5f, 25f); // 얇은 벽부터 거대한 벽까지
    public Vector2 sizeRangeY = new Vector2(5f, 25f); // 바닥 역할
    public Vector2 sizeRangeZ = new Vector2(5f, 25f);

    private List<GameObject> cubePool;

    void Awake()
    {
        InitializeMassivePool();
    }

    void Update()
    {
        UpdateCubeBubble();
    }

    private void InitializeMassivePool()
    {
        cubePool = new List<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(cubePrefab);
            
            // 플레이어 중심 거대한 구(Sphere) 형태의 무작위 위치에 최초 스폰
            Vector3 randomPos = player.position + Random.insideUnitSphere * fieldRadius;
            
            // 플레이어와 너무 가까운 곳에 스폰되어 시작하자마자 끼이는 것 방지
            if (Vector3.Distance(player.position, randomPos) < 10f)
            {
                randomPos += randomPos.normalized * 15f;
            }

            obj.transform.position = randomPos;
            obj.transform.rotation = Random.rotation;
            SetRandomScale(obj);
            
            cubePool.Add(obj);
        }
    }

    private void UpdateCubeBubble()
    {
        // 플레이어가 코어를 향해 (혹은 어디로든) 달려갈 때,
        // 플레이어 시야 뒤쪽으로 버려진 큐브를 앞으로 다시 가져옴
        foreach (var cube in cubePool)
        {
            // 플레이어 카메라의 Forward 방향을 기준으로 큐브가 뒤에 있는지 내적(Dot)으로 판별
            Vector3 toCube = cube.transform.position - player.position;
            float dotProduct = Vector3.Dot(player.forward, toCube);

            // 큐브가 플레이어 뒤쪽에 있고, 일정 거리 이상 멀어졌다면
            if (dotProduct < 0 && toCube.magnitude > despawnDistance)
            {
                RecycleCubeAhead(cube);
            }
        }
    }

   private void RecycleCubeAhead(GameObject cube)
{
    // fieldRadius를 200~300으로 키우면 훨씬 띄엄띄엄 배치됩니다.
    Vector3 randomDirection = Random.onUnitSphere;
    randomDirection.z = Mathf.Abs(randomDirection.z); 
    randomDirection = player.TransformDirection(randomDirection);

    // 간격을 넓히기 위해 스폰 최소 거리를 보장함
    Vector3 newPos = player.position + (randomDirection * Random.Range(fieldRadius * 0.5f, fieldRadius));

    cube.transform.position = newPos;

    // 경사가 너무 심해 점프가 불가능한 상황을 막기 위해 회전 각도를 제한 (선택 사항)
    // 0~360이 아닌 0~45도 정도로 제한하면 파쿠르가 훨씬 쾌적해집니다.
    cube.transform.rotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f));
    
    SetRandomScale(cube);
    if (!cube.activeInHierarchy) cube.SetActive(true);
}

  private void SetRandomScale(GameObject cube)
{
    // 아주 작은 조각(1x1x1)부터 거대한 벽(25x5x25)까지 극단적인 다양성 부여
    float x = Random.Range(2f, 25f);
    float y = Random.Range(2f, 15f);
    float z = Random.Range(2f, 25f);
    
    // 낮은 확률로 아주 작은 디딤돌 큐브 생성
    if (Random.value > 0.8f) { x = 2f; y = 2f; z = 2f; }

    cube.transform.localScale = new Vector3(x, y, z);
}
}