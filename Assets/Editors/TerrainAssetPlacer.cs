using UnityEngine;
using UnityEditor;

public class TerrainAssetPlacer : EditorWindow
{
    private GameObject assetPrefab;
    private int count = 100;
    private float minScale = 0.8f;
    private float maxScale = 1.5f;
    private float slopeLimit = 30f;
    
    // 하이어라키를 깔끔하게 유지할 부모 컨테이너 이름
    private string containerName = "[Environment_Assets]"; 

    [MenuItem("Tools/Map Generator/Auto Asset Placer (V2)")]
    public static void ShowWindow() => GetWindow<TerrainAssetPlacer>("Asset Placer");

    private void OnGUI()
    {
        GUILayout.Label("스마트 오브젝트 자동 배치 (컨테이너 지원)", EditorStyles.boldLabel);
        
        assetPrefab = (GameObject)EditorGUILayout.ObjectField("배치할 프리팹(나무/바위)", assetPrefab, typeof(GameObject), false);
        count = EditorGUILayout.IntField("배치 개수", count);
        minScale = EditorGUILayout.Slider("최소 크기", minScale, 0.1f, 3f);
        maxScale = EditorGUILayout.Slider("최대 크기", maxScale, 0.1f, 5f);
        slopeLimit = EditorGUILayout.Slider("경사 제한(절벽 방지)", slopeLimit, 0f, 90f);

        EditorGUILayout.Space();

        // 🟢 생성 버튼
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // 연두색
        if (GUILayout.Button("지형에 자동 배치 시작", GUILayout.Height(30)))
        {
            PlaceAssets();
        }

        EditorGUILayout.Space();

        // 🔴 삭제 버튼
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // 연한 빨간색
        if (GUILayout.Button("배치된 에셋 싹 다 지우기 (초기화)", GUILayout.Height(30)))
        {
            ClearAssets();
        }
        GUI.backgroundColor = Color.white; // 색상 원상복구
    }

    private void PlaceAssets()
    {
        if (assetPrefab == null) { Debug.LogWarning("프리팹을 먼저 할당해주세요."); return; }
        
        Terrain terrain = Terrain.activeTerrain;
        if (!terrain) { Debug.LogError("씬에 활성화된 Terrain이 없습니다."); return; }

        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // 1. 컨테이너(부모) 찾기 또는 생성
        GameObject container = GameObject.Find(containerName);
        if (container == null)
        {
            container = new GameObject(containerName);
        }

        int placedCount = 0;

        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(0f, data.size.x);
            float z = Random.Range(0f, data.size.z);
            float y = terrain.SampleHeight(new Vector3(x, 0, z) + terrainPos);
            Vector3 worldPos = new Vector3(x, y, z) + terrainPos;

            float normalX = x / data.size.x;
            float normalZ = z / data.size.z;
            float slope = data.GetSteepness(normalX, normalZ);

            if (slope <= slopeLimit)
            {
                GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(assetPrefab);
                obj.transform.position = worldPos;
                obj.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                obj.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
                
                // 2. 생성된 에셋을 컨테이너 안에 넣기 (하이어라키 정리)
                obj.transform.SetParent(container.transform);
                
                // Undo 지원
                Undo.RegisterCreatedObjectUndo(obj, "Auto Place Asset");
                placedCount++;
            }
        }
        Debug.Log($"[{containerName}] 안에 {placedCount}개의 에셋 배치를 완료했습니다.");
    }

    private void ClearAssets()
    {
        GameObject container = GameObject.Find(containerName);
        if (container != null)
        {
            Undo.DestroyObjectImmediate(container);
            Debug.Log("배치된 에셋 컨테이너가 깔끔하게 삭제되었습니다.");
        }
        else
        {
            Debug.Log("삭제할 에셋 컨테이너가 없습니다.");
        }
    }
}