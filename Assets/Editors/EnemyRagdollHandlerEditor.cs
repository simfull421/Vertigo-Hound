using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// EnemyRagdollHandler 컴포넌트의 인스펙터 UI를 커스텀합니다.
/// [자동 할당] 버튼으로 레그돌 Rigidbody/Collider를 한 번에 수집하고,
/// 같은 오브젝트의 EnemyAnimatorController에 Animator도 자동 연결합니다.
/// </summary>
[CustomEditor(typeof(EnemyRagdollHandler))]
public class EnemyRagdollHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기존 인스펙터 UI들(변수들)을 그대로 표시
        base.OnInspectorGUI(); 

        EnemyRagdollHandler handler = (EnemyRagdollHandler)target;

        GUILayout.Space(15);

        if (GUILayout.Button("자동 할당 (Auto-Fill Ragdoll Bones)", GUILayout.Height(35)))
        {
            Undo.RecordObject(handler, "Auto-Fill Ragdoll Bones");

            // 1. 하위 뼈대 Rigidbody 긁어오기 (본체 자기 자신의 Rigidbody는 제외)
            handler.ragdollBodies = handler.GetComponentsInChildren<Rigidbody>()
                .Where(rb => rb.gameObject != handler.gameObject)
                .ToArray();

            // 2. 하위 뼈대 Collider 긁어오기 (본체 자기 자신의 Collider는 제외)
            handler.ragdollColliders = handler.GetComponentsInChildren<Collider>()
                .Where(col => col.gameObject != handler.gameObject)
                .ToArray();

            EditorUtility.SetDirty(handler);

            // 3. 같은 오브젝트의 EnemyAnimatorController에 Animator 자동 할당
            EnemyAnimatorController animController = handler.GetComponent<EnemyAnimatorController>();
            if (animController != null && animController.animator == null)
            {
                Undo.RecordObject(animController, "Auto-Fill Animator");
                animController.animator = handler.GetComponentInChildren<Animator>();
                EditorUtility.SetDirty(animController);

                if (animController.animator != null)
                    Debug.Log($"[Ragdoll Builder] Animator 자동 할당 완료: {animController.animator.gameObject.name}");
                else
                    Debug.LogWarning("[Ragdoll Builder] 하위 오브젝트에서 Animator를 찾을 수 없습니다.");
            }

            Debug.Log($"[Ragdoll Builder] 세팅 완료! Rigidbody {handler.ragdollBodies.Length}개, Collider {handler.ragdollColliders.Length}개가 성공적으로 할당되었습니다.");
        }
    }
}