using UnityEditor;
using UnityEngine;

public static class EffectPreviewRigBuilder
{
    private const string DefaultSparklePrefabPath = "Assets/Prefabs/Effects/FX_Sparkle_Hit.prefab";

    [MenuItem("Tools/Effects/Create Preview Rig")]
    public static void CreatePreviewRig()
    {
        GameObject rig = new GameObject("EffectPreviewRig");
        rig.transform.position = Vector3.zero;

        EffectPreviewLooper looper = rig.AddComponent<EffectPreviewLooper>();
        looper.EffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSparklePrefabPath);

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("EffectPreviewCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        Selection.activeObject = rig;
        EditorGUIUtility.PingObject(rig);

        Debug.Log("Created EffectPreviewRig. Assign any effect prefab to Effect Preview Looper, then watch the Scene view.");
    }
}
