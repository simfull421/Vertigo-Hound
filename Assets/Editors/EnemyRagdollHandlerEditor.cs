// EnemyRagdollHandlerEditor.cs 수정본
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(EnemyRagdollHandler))]
public class EnemyRagdollHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EnemyRagdollHandler handler = (EnemyRagdollHandler)target;

        if (GUILayout.Button("Auto-Build Ragdoll Setup", GUILayout.Height(30)))
        {
            Undo.RecordObject(handler, "Auto-Build Ragdoll");

            // 1. 하위 뼈대 Rigidbody 긁어오기 (본체 자기 자신 제외)
            handler.ragdollBodies = handler.GetComponentsInChildren<Rigidbody>()
                .Where(rb => rb.gameObject != handler.gameObject)
                .ToArray();

            // [수정] ragdollColliders 할당 로직 삭제 (필드가 없어짐)

            EditorUtility.SetDirty(handler);

            // 2. 같은 오브젝트의 EnemyAnimatorController에 Animator 자동 할당
            EnemyAnimatorController animController = handler.GetComponent<EnemyAnimatorController>();
            if (animController != null && animController.animator == null)
            {
                Undo.RecordObject(animController, "Auto-Fill Animator");
                animController.animator = handler.GetComponentInChildren<Animator>();
                EditorUtility.SetDirty(animController);

                if (animController.animator != null)
                    Debug.Log($"[Ragdoll Builder] Animator 자동 할당 완료: {animController.animator.gameObject.name}");
            }

            Debug.Log($"[Ragdoll Builder] 세팅 완료! Rigidbody {handler.ragdollBodies.Length}개가 성공적으로 할당되었습니다.");
        }
    }
}
