핵심은 “층 생성기(Floor Generator)” + “빌딩 스택커(Stacker)”를 완전히 분리하는 것이다.
여기서 필요한 건 복잡한 수학이 아니라, 좌표계 + 시드 기반 결정 함수 + 규칙 그래프다.

아래를 그대로 설계하면 바로 에디터 툴로 옮길 수 있다.

0. 전체 구조 (중요)
툴 2개 구조
① Floor Generator (1층 생성기)
입력:
seed
grid size
룸 규칙
출력:
1층 프리팹 (또는 GameObject 트리)
② Building Stacker (층 적재기)
입력:
floor prefab
total floors
출력:
다층 건물
1. 핵심 수학: 좌표 시스템 (Grid → World)
기본 공식
Vector3 WorldPos(int x, int z, int yLevel)
{
    return new Vector3(
        x * cellSize,
        yLevel * floorHeight,
        z * cellSize
    );
}
의미
x, z → 평면 배치
yLevel → 층 번호
cellSize → 타일 크기 (예: 4m)
floorHeight → 층 높이 (예: 3.5~4m)

👉 이거 하나로 모든 배치 해결됨

2. 1층 생성 알고리즘 (Floor Generator)
핵심: “룰 기반 타일 배치”
Step 1: Grid 선언
int width = 5;
int height = 5;

CellType[,] grid = new CellType[width, height];
Step 2: 기본 구조 배치 (Deterministic)

👉 이건 랜덤 아님 (항상 동일해야 플레이 가능)

예:

중앙 = 핵심 영역
한쪽 = 계단
grid[0, 2] = CellType.Stair;
grid[2, 2] = CellType.Core;
Step 3: 시드 기반 랜덤 (핵심)
랜덤 초기화
System.Random rng = new System.Random(seed);
랜덤 규칙 (예시)
방 / 복도 생성
for (int x = 0; x < width; x++)
{
    for (int z = 0; z < height; z++)
    {
        if (grid[x, z] != CellType.Empty) continue;

        int roll = rng.Next(0, 100);

        if (roll < 60)
            grid[x, z] = CellType.Room;
        else
            grid[x, z] = CellType.Hallway;
    }
}
Step 4: 연결성 보장 (중요)

👉 이거 없으면 맵 망함

Flood Fill or BFS 사용
시작점: Stair
모든 타일 reachable 확인
bool IsConnected()
{
    // BFS 돌려서 전부 방문 가능한지 체크
}

👉 실패하면:

seed++;
다시 생성
3. 프리팹 배치 알고리즘
셀 → 프리팹 매핑
GameObject GetPrefab(CellType type)
{
    switch(type)
    {
        case CellType.Room: return roomPrefab;
        case CellType.Hallway: return hallwayPrefab;
        case CellType.Stair: return stairPrefab;
    }
}
Instantiate 루프
for (int x = 0; x < width; x++)
{
    for (int z = 0; z < height; z++)
    {
        var prefab = GetPrefab(grid[x,z]);

        Vector3 pos = WorldPos(x, z, 0);

        Instantiate(prefab, pos, Quaternion.identity, parent);
    }
}
4. 계단 문제 (핵심 질문)

맞다.
계단은 큐브 스케일로 해결 못 한다.

정답

👉 계단은 “완성된 프리팹”으로 취급

왜?
경사각 중요
플레이 테스트 필요
파쿠르 타이밍 영향
확장 설계
enum StairType
{
    DogLeg,
    Spiral,
    Broken
}
StairType stair = (StairType)(rng.Next(0, 3));

👉 층마다 계단 변형 가능

5. 층 적재 알고리즘 (Stacker)
핵심 공식
for (int i = 0; i < totalFloors; i++)
{
    Vector3 pos = new Vector3(0, i * floorHeight, 0);

    GameObject floor = Instantiate(floorPrefab, pos, Quaternion.identity);

    SetFloorNumber(floor, i + 1);
}
6. 층수 텍스트 자동화
void SetFloorNumber(GameObject floor, int level)
{
    var texts = floor.GetComponentsInChildren<TextMeshPro>();

    foreach (var t in texts)
    {
        t.text = level + " F";
    }
}
7. 시드 시스템 설계 (중요)
핵심 개념

👉 “층마다 시드 다르게”

방법 1 (추천)
int floorSeed = baseSeed + floorIndex;
방법 2 (더 강력)
int floorSeed = Hash(baseSeed, floorIndex);

예:

int Hash(int a, int b)
{
    return a * 73856093 ^ b * 19349663;
}

👉 결과:

같은 baseSeed → 항상 같은 건물
디버깅 가능
8. 고급: 변형 규칙 (Replayability)
1. 파괴 지점 랜덤
if (rng.Next(0,100) < 20)
    SpawnBrokenWall(x,z);
2. 창문 위치
if (IsEdge(x,z) && rng.Next(0,100) < 30)
    SpawnWindow();
3. 막힌 루트 생성
if (rng.Next(0,100) < 15)
    BlockStairAccess();
9. 에디터 툴 구조 (실전)
ScriptableObject 추천
[CreateAssetMenu]
class FloorConfig : ScriptableObject
{
    public int width;
    public int height;
    public float cellSize;
    public float floorHeight;
}
에디터 버튼
[ContextMenu("Generate Floor")]
void GenerateFloor()
[ContextMenu("Build Tower")]
void BuildTower()
10. 핵심 정리 (중요)
❌ 잘못된 접근
큐브로 모든 걸 해결하려는 것
프로빌더 런타임 제어
✅ 정답 구조
프리팹 = “조각”
Grid = “설계도”
Seed = “변형”
Loop = “생산라인”
한 줄 요약

👉 “좌표 = 산수, 구조 = 규칙, 다양성 = 시드”

문제 (병목 현상): 1층에 프리팹 조각(방, 복도, 계단)이 25개 들어간다고 칩시다. 50층이면 1,250개의 개별 오브젝트(GameObject)가 생깁니다. 유니티는 이 1,250개의 조각을 화면에 그릴 때마다 그래픽 카드에 "이거 그려라"라고 1,250번의 명령(Draw Call)을 보냅니다. 최적화가 박살 납니다.

해결책 (Static Batching): Stacker가 50층 건물을 다 만들고 난 직후, 코드로 생성된 모든 프리팹(방, 복도 외벽 등)의 속성을 isStatic = true로 묶어주거나, 유니티의 StaticBatchingUtility.Combine API를 호출하는 로직을 마지막 Step에 딱 한 줄 추가하십시오.
이렇게 하면 1,250개의 조각이 그래픽 카드 입장에서는 '거대한 건물 덩어리 1개'로 인식되어, 프레임 저하 없이 쾌적한 파쿠르가 가능해집니다.