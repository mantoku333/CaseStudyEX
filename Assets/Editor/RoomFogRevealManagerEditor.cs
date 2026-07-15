using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomFogRevealManager))]
public sealed class RoomFogRevealManagerEditor : Editor
{
    private SerializedProperty fogEnabled;
    private SerializedProperty textureResolution;
    private SerializedProperty worldPadding;
    private SerializedProperty fogShader;
    private SerializedProperty fogColor;
    private SerializedProperty fogAlpha;
    private SerializedProperty edgeSoftness;
    private SerializedProperty noiseStrength;
    private SerializedProperty noiseScale;
    private SerializedProperty sortingOrder;
    private SerializedProperty overlayZ;
    private SerializedProperty revealDuration;
    private SerializedProperty concealDuration;
    private SerializedProperty revealNoiseStrength;
    private SerializedProperty revealPortalEntrances;
    private SerializedProperty portalEntranceDepth;
    private SerializedProperty portalEntranceRadius;
    private SerializedProperty portalEntranceSoftness;
    private SerializedProperty portalEntranceEdgeNoise;

    private void OnEnable()
    {
        fogEnabled = serializedObject.FindProperty("fogEnabled");
        textureResolution = serializedObject.FindProperty("textureResolution");
        worldPadding = serializedObject.FindProperty("worldPadding");
        fogShader = serializedObject.FindProperty("fogShader");
        fogColor = serializedObject.FindProperty("fogColor");
        fogAlpha = serializedObject.FindProperty("fogAlpha");
        edgeSoftness = serializedObject.FindProperty("edgeSoftness");
        noiseStrength = serializedObject.FindProperty("noiseStrength");
        noiseScale = serializedObject.FindProperty("noiseScale");
        sortingOrder = serializedObject.FindProperty("sortingOrder");
        overlayZ = serializedObject.FindProperty("overlayZ");
        revealDuration = serializedObject.FindProperty("revealDuration");
        concealDuration = serializedObject.FindProperty("concealDuration");
        revealNoiseStrength = serializedObject.FindProperty("revealNoiseStrength");
        revealPortalEntrances = serializedObject.FindProperty("revealPortalEntrances");
        portalEntranceDepth = serializedObject.FindProperty("portalEntranceDepth");
        portalEntranceRadius = serializedObject.FindProperty("portalEntranceRadius");
        portalEntranceSoftness = serializedObject.FindProperty("portalEntranceSoftness");
        portalEntranceEdgeNoise = serializedObject.FindProperty("portalEntranceEdgeNoise");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(fogEnabled, new GUIContent("Fogを表示する"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("見た目", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fogColor, new GUIContent("Fogの色"));
        EditorGUILayout.PropertyField(fogAlpha, new GUIContent("Fogの濃さ"));
        EditorGUILayout.PropertyField(edgeSoftness, new GUIContent("境界のぼかし"));
        EditorGUILayout.PropertyField(noiseStrength, new GUIContent("ゆらぎの強さ"));
        EditorGUILayout.PropertyField(noiseScale, new GUIContent("ゆらぎの細かさ"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("エリアに入った時の消え方", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(revealDuration, new GUIContent("消える時間"));
        EditorGUILayout.PropertyField(concealDuration, new GUIContent("出たエリアが隠れる時間"));
        EditorGUILayout.PropertyField(revealNoiseStrength, new GUIContent("消え際のゆらぎ"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("ポータルの凹み", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(revealPortalEntrances, new GUIContent("凹みを表示する"));
        using (new EditorGUI.DisabledScope(!revealPortalEntrances.boolValue))
        {
            EditorGUILayout.PropertyField(portalEntranceDepth, new GUIContent("奥へのえぐれ量"));
            EditorGUILayout.PropertyField(portalEntranceRadius, new GUIContent("口の広がり"));
            EditorGUILayout.PropertyField(portalEntranceSoftness, new GUIContent("凹みのぼかし"));
            EditorGUILayout.PropertyField(portalEntranceEdgeNoise, new GUIContent("輪郭のゆらぎ"));
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(textureResolution, new GUIContent("マスク解像度"));
        EditorGUILayout.PropertyField(worldPadding, new GUIContent("マップ外側の余白"));
        EditorGUILayout.PropertyField(sortingOrder, new GUIContent("描画順"));
        EditorGUILayout.PropertyField(overlayZ, new GUIContent("FOGのZ位置"));
        EditorGUILayout.PropertyField(fogShader, new GUIContent("Fogシェーダー"));

        serializedObject.ApplyModifiedProperties();
    }
}
