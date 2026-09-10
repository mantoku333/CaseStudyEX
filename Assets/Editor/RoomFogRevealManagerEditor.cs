using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomFogRevealManager))]
public sealed class RoomFogRevealManagerEditor : Editor
{
    private SerializedProperty fogEnabled;
    private SerializedProperty textureResolution;
    private SerializedProperty targetWorldUnitsPerPixel;
    private SerializedProperty maximumTextureResolution;
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
    private SerializedProperty maskContinuityDuration;
    private SerializedProperty revealPortalEntrances;
    private SerializedProperty portalEntranceDepth;
    private SerializedProperty portalEntranceRadius;
    private SerializedProperty portalEntranceSoftness;
    private SerializedProperty portalEntranceEdgeNoise;

    private void OnEnable()
    {
        fogEnabled = serializedObject.FindProperty("fogEnabled");
        textureResolution = serializedObject.FindProperty("textureResolution");
        targetWorldUnitsPerPixel = serializedObject.FindProperty("targetWorldUnitsPerPixel");
        maximumTextureResolution = serializedObject.FindProperty("maximumTextureResolution");
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
        maskContinuityDuration = serializedObject.FindProperty("maskContinuityDuration");
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
        if (fogColor != null)
        {
            Color color = fogColor.colorValue;
            Color nextColor = EditorGUILayout.ColorField(
                new GUIContent("Fogの色"),
                color,
                true,
                false,
                false);
            nextColor.a = color.a;
            fogColor.colorValue = nextColor;
        }

        DrawProperty(fogAlpha, "Fogの濃さ");
        DrawProperty(edgeSoftness, "境界のぼかし");
        DrawProperty(noiseStrength, "ゆらぎの強さ");
        DrawProperty(noiseScale, "ゆらぎの細かさ");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("エリアに入った時の消え方", EditorStyles.boldLabel);
        DrawProperty(revealDuration, "消える時間");
        DrawProperty(concealDuration, "出たエリアが隠れる時間");
        DrawProperty(revealNoiseStrength, "消え際のゆらぎ");
        DrawProperty(maskContinuityDuration, "切り替え時のちらつき抑制時間");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("ポータルの凹み", EditorStyles.boldLabel);
        DrawProperty(revealPortalEntrances, "凹みを表示する");
        using (new EditorGUI.DisabledScope(revealPortalEntrances == null || !revealPortalEntrances.boolValue))
        {
            DrawProperty(portalEntranceDepth, "奥へのえぐれ量");
            DrawProperty(portalEntranceRadius, "口の広がり");
            DrawProperty(portalEntranceSoftness, "凹みのぼかし");
            DrawProperty(portalEntranceEdgeNoise, "輪郭のゆらぎ");
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
        DrawProperty(textureResolution, "長辺の最低マスク解像度");
        DrawProperty(targetWorldUnitsPerPixel, "Fogの細かさ");
        DrawProperty(maximumTextureResolution, "長辺の最大マスク解像度");
        DrawProperty(worldPadding, "マップ外側の余白");
        DrawProperty(sortingOrder, "描画順");
        DrawProperty(overlayZ, "FOGのZ位置");
        DrawProperty(fogShader, "Fogシェーダー");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawProperty(SerializedProperty property, string label)
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }
}
