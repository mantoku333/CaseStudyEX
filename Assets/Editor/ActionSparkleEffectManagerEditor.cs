using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionSparkleEffectManager))]
public sealed class ActionSparkleEffectManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ActionSparkleEffectManager manager = (ActionSparkleEffectManager)target;

        EditorGUILayout.HelpBox(
            "管理用GameObjectにこのManagerを付け、Sparkle Effect Rootには2つのParticleSystemを子に持つエフェクト親を指定します。",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Assign"))
            {
                Undo.RecordObject(manager, "Auto Assign Action Sparkle References");
                manager.AutoAssignReferences();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button("Refresh Particles"))
            {
                Undo.RecordObject(manager, "Refresh Action Sparkle Particles");
                manager.RefreshParticlesFromEffectRoot();
                EditorUtility.SetDirty(manager);
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Lv1"))
            {
                Preview(manager, 1);
            }

            if (GUILayout.Button("Lv2"))
            {
                Preview(manager, 2);
            }

            if (GUILayout.Button("Lv3"))
            {
                Preview(manager, 3);
            }

            if (GUILayout.Button("Lv4"))
            {
                Preview(manager, 4);
            }

            if (GUILayout.Button("Stop"))
            {
                Undo.RecordObject(manager, "Stop Action Sparkle Preview");
                manager.StopPreview();
                EditorUtility.SetDirty(manager);
            }
        }

        EditorGUILayout.Space(8f);
        DrawDefaultInspector();
    }

    private static void Preview(ActionSparkleEffectManager manager, int level)
    {
        Undo.RecordObject(manager, "Preview Action Sparkle");
        manager.PreviewLevel(level, ActionSparkleEffectManager.SparkleAction.Glide);
        EditorUtility.SetDirty(manager);
    }
}
