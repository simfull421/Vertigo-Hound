using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// 빌딩 스태커 에디터.
/// 직접 만든 1층 프리팹(또는 씬 오브젝트)을 받아서 N층으로 복사 적재하고,
/// TextMeshPro 컴포넌트의 텍스트를 층수에 맞게 자동 갱신합니다.
///
/// 사용법:
///   1. Tools > Map Generator > Building Stacker 열기
///   2. 직접 만든 1층 프리팹을 Floor Prefab 슬롯에 드래그
///   3. 층 높이, 총 층수 설정
///   4. "빌딩 쌓기" 클릭
/// </summary>
public class BuildingStackerEditor : EditorWindow
{
    private GameObject floorPrefab;
    private float floorHeight = 3.5f;
    private int totalFloors = 10;

    private const string ContainerName = "[Stacked_Building]";

    [MenuItem("Tools/Map Generator/Building Stacker")]
    public static void ShowWindow()
    {
        GetWindow<BuildingStackerEditor>("Building Stacker");
    }

    private void OnGUI()
    {
        GUILayout.Label("빌딩 스태커", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "직접 만든 1층 프리팹을 복사해서 층층이 쌓고,\n" +
            "TMP 텍스트의 층수(1F, 2F, 3F...)를 자동으로 갱신합니다.",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // ─── 설정 ───
        floorPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Floor Prefab", floorPrefab, typeof(GameObject), true
        );
        floorHeight = EditorGUILayout.FloatField("층 높이 (m)", floorHeight);
        totalFloors = EditorGUILayout.IntSlider("총 층수", totalFloors, 1, 100);

        EditorGUILayout.Space();

        // ─── 쌓기 버튼 ───
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button($"빌딩 쌓기 ({totalFloors}층)", GUILayout.Height(35)))
        {
            if (floorPrefab == null)
            {
                Debug.LogError("[BuildingStacker] Floor Prefab을 먼저 할당하세요.");
                return;
            }
            StackBuilding();
        }

        EditorGUILayout.Space();

        // ─── 삭제 버튼 ───
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("빌딩 삭제", GUILayout.Height(28)))
        {
            ClearBuilding();
        }
        GUI.backgroundColor = Color.white;
    }

    // ────────────────────────────────────────────
    //  빌딩 쌓기
    // ────────────────────────────────────────────

    private void StackBuilding()
    {
        ClearBuilding();

        GameObject root = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(root, "Stack Building");

        bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(floorPrefab);

        for (int i = 0; i < totalFloors; i++)
        {
            Vector3 pos = new Vector3(0f, i * floorHeight, 0f);

            GameObject floor;
            if (isPrefabAsset)
            {
                floor = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                floor.transform.position = pos;
            }
            else
            {
                floor = Instantiate(floorPrefab, pos, Quaternion.identity);
            }

            floor.name = $"Floor_{i + 1}F";
            floor.transform.SetParent(root.transform);

            // TMP 텍스트 층수 갱신
            UpdateFloorTexts(floor, i + 1);

            Undo.RegisterCreatedObjectUndo(floor, "Stack Floor");
        }

        // Static Batching
        StaticBatchingUtility.Combine(root);

        SceneView.RepaintAll();
        Debug.Log($"[BuildingStacker] {totalFloors}층 빌딩 쌓기 완료!");
    }

    // ────────────────────────────────────────────
    //  TMP 층수 텍스트 갱신
    // ────────────────────────────────────────────

    /// <summary>
    /// 해당 층 내부의 모든 TextMeshPro / TextMeshProUGUI에서
    /// "1F" 또는 "1 F" 패턴을 찾아 현재 층수로 교체합니다.
    /// 패턴이 없으면 그냥 텍스트 전체를 "NF"로 설정합니다.
    /// </summary>
    private void UpdateFloorTexts(GameObject floor, int level)
    {
        // 3D 텍스트 (TextMeshPro)
        TextMeshPro[] tmp3d = floor.GetComponentsInChildren<TextMeshPro>(true);
        foreach (TextMeshPro t in tmp3d)
        {
            t.text = $"{level}F";
        }

        // UI 텍스트 (TextMeshProUGUI) — Canvas 안에 있을 경우
        TextMeshProUGUI[] tmpUI = floor.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI t in tmpUI)
        {
            t.text = $"{level}F";
        }
    }

    // ────────────────────────────────────────────
    //  삭제
    // ────────────────────────────────────────────

    private void ClearBuilding()
    {
        GameObject existing = GameObject.Find(ContainerName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            SceneView.RepaintAll();
            Debug.Log("[BuildingStacker] 빌딩 삭제 완료.");
        }
    }
}
