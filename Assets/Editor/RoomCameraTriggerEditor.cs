using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomCameraTrigger))]
public sealed class RoomCameraTriggerEditor : Editor
{
    private SerializedProperty roomCamera;
    private SerializedProperty useDefaultCameraWhenEntered;
    private SerializedProperty overrideDefaultFollowCameraOrthographicSize;
    private SerializedProperty defaultFollowCameraOrthographicSize;
    private SerializedProperty overrideDefaultFollowCameraTargetOffsetY;
    private SerializedProperty defaultFollowCameraTargetOffsetY;
    private SerializedProperty overrideDefaultFollowCameraScreenPositionY;
    private SerializedProperty defaultFollowCameraScreenPositionY;
    private SerializedProperty useHorizontalFollowCameraWhenEntered;
    private SerializedProperty horizontalFollowCamera;
    private SerializedProperty useManualYForHorizontalFollow;
    private SerializedProperty horizontalFollowFixedY;
    private SerializedProperty clampHorizontalFollowXToArea;
    private SerializedProperty horizontalFollowSmoothTime;
    private SerializedProperty playerTag;
    private SerializedProperty activePriority;
    private SerializedProperty inactivePriority;
    private SerializedProperty previewDefaultCameraByAreaBounds;
    private SerializedProperty defaultCameraPreviewWeight;
    private SerializedProperty defaultCameraPreviewZoomWeight;
    private SerializedProperty maxDefaultCameraPreviewSize;
    private SerializedProperty areaColliders2D;
    private SerializedProperty areaColliders;

    private static bool showHorizontalFollowSettings;
    private static bool showPortalPreviewSettings;
    private static bool showAdvancedSettings;
    private static bool showAreaBoundsSettings;

    private void OnEnable()
    {
        roomCamera = serializedObject.FindProperty("_roomCamera");
        useDefaultCameraWhenEntered = serializedObject.FindProperty("_useDefaultCameraWhenEntered");
        overrideDefaultFollowCameraOrthographicSize =
            serializedObject.FindProperty("_overrideDefaultFollowCameraOrthographicSize");
        defaultFollowCameraOrthographicSize =
            serializedObject.FindProperty("_defaultFollowCameraOrthographicSize");
        overrideDefaultFollowCameraTargetOffsetY =
            serializedObject.FindProperty("_overrideDefaultFollowCameraTargetOffsetY");
        defaultFollowCameraTargetOffsetY =
            serializedObject.FindProperty("_defaultFollowCameraTargetOffsetY");
        overrideDefaultFollowCameraScreenPositionY =
            serializedObject.FindProperty("_overrideDefaultFollowCameraScreenPositionY");
        defaultFollowCameraScreenPositionY =
            serializedObject.FindProperty("_defaultFollowCameraScreenPositionY");
        useHorizontalFollowCameraWhenEntered =
            serializedObject.FindProperty("_useHorizontalFollowCameraWhenEntered");
        horizontalFollowCamera = serializedObject.FindProperty("_horizontalFollowCamera");
        useManualYForHorizontalFollow = serializedObject.FindProperty("_useManualYForHorizontalFollow");
        horizontalFollowFixedY = serializedObject.FindProperty("_horizontalFollowFixedY");
        clampHorizontalFollowXToArea = serializedObject.FindProperty("_clampHorizontalFollowXToArea");
        horizontalFollowSmoothTime = serializedObject.FindProperty("_horizontalFollowSmoothTime");
        playerTag = serializedObject.FindProperty("_playerTag");
        activePriority = serializedObject.FindProperty("_activePriority");
        inactivePriority = serializedObject.FindProperty("_inactivePriority");
        previewDefaultCameraByAreaBounds = serializedObject.FindProperty("_previewDefaultCameraByAreaBounds");
        defaultCameraPreviewWeight = serializedObject.FindProperty("_defaultCameraPreviewWeight");
        defaultCameraPreviewZoomWeight = serializedObject.FindProperty("_defaultCameraPreviewZoomWeight");
        maxDefaultCameraPreviewSize = serializedObject.FindProperty("_maxDefaultCameraPreviewSize");
        areaColliders2D = serializedObject.FindProperty("_areaColliders2D");
        areaColliders = serializedObject.FindProperty("_areaColliders");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCameraModeSection();
        DrawDefaultFollowSection();
        DrawHorizontalFollowSection();
        DrawPortalPreviewSection();
        DrawAreaBoundsSection();
        DrawAdvancedSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCameraModeSection()
    {
        EditorGUILayout.LabelField("カメラ方式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useDefaultCameraWhenEntered, new GUIContent("通常フォローカメラを使う"));

        using (new EditorGUI.DisabledScope(useDefaultCameraWhenEntered.boolValue))
        {
            EditorGUILayout.PropertyField(roomCamera, new GUIContent("固定カメラ"));
        }

        if (useDefaultCameraWhenEntered.boolValue)
        {
            EditorGUILayout.HelpBox(
                "このエリアでは CN_FollowCam を使います。固定カメラは使われません。",
                MessageType.Info);
        }
        else if (roomCamera.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "固定カメラを使う場合は、ここに CN_*** を入れてください。",
                MessageType.Warning);
        }
    }

    private void DrawDefaultFollowSection()
    {
        if (!useDefaultCameraWhenEntered.boolValue)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("通常フォローカメラのエリア別調整", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "チェックした項目だけ、このエリア内で CN_FollowCam に反映します。未チェックの項目は通常値に戻ります。",
                MessageType.None);

            EditorGUILayout.PropertyField(
                overrideDefaultFollowCameraOrthographicSize,
                new GUIContent("画角を変える"));
            if (overrideDefaultFollowCameraOrthographicSize.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    defaultFollowCameraOrthographicSize,
                    new GUIContent("画角", "大きいほどカメラが引きます。例: 12, 14, 17"));
                if (defaultFollowCameraOrthographicSize.floatValue <= 0f)
                {
                    EditorGUILayout.HelpBox(
                        "画角を変える場合は 0 より大きい値を入れてください。0 のままだと通常値を使います。",
                        MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(
                overrideDefaultFollowCameraTargetOffsetY,
                new GUIContent("注視点の高さを変える"));
            if (overrideDefaultFollowCameraTargetOffsetY.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    defaultFollowCameraTargetOffsetY,
                    new GUIContent("注視点Y", "プラスで上、マイナスで下へ注視点をずらします。"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(
                overrideDefaultFollowCameraScreenPositionY,
                new GUIContent("プレイヤーの画面内Y位置を変える"));
            if (overrideDefaultFollowCameraScreenPositionY.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    defaultFollowCameraScreenPositionY,
                    new GUIContent("画面Y位置", "-1で下寄り、0で中央、1で上寄り。上寄りにすると下側が見えやすくなります。"));
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawHorizontalFollowSection()
    {
        EditorGUILayout.Space(6f);
        showHorizontalFollowSettings = EditorGUILayout.Foldout(
            showHorizontalFollowSettings,
            "横長エリア用フォローカメラ",
            true);
        if (!showHorizontalFollowSettings)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(
            useHorizontalFollowCameraWhenEntered,
            new GUIContent("横方向だけ追従するカメラを使う"));
        if (useHorizontalFollowCameraWhenEntered.boolValue)
        {
            EditorGUILayout.PropertyField(horizontalFollowCamera, new GUIContent("横フォローカメラ"));
            EditorGUILayout.PropertyField(useManualYForHorizontalFollow, new GUIContent("Y座標を手動固定"));
            if (useManualYForHorizontalFollow.boolValue)
            {
                EditorGUILayout.PropertyField(horizontalFollowFixedY, new GUIContent("固定Y"));
            }

            EditorGUILayout.PropertyField(clampHorizontalFollowXToArea, new GUIContent("X移動をエリア内に制限"));
            EditorGUILayout.PropertyField(horizontalFollowSmoothTime, new GUIContent("追従の滑らかさ"));
        }

        EditorGUI.indentLevel--;
    }

    private void DrawPortalPreviewSection()
    {
        EditorGUILayout.Space(6f);
        showPortalPreviewSettings = EditorGUILayout.Foldout(
            showPortalPreviewSettings,
            "ポータル遷移中の見え方",
            true);
        if (!showPortalPreviewSettings)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(previewDefaultCameraByAreaBounds, new GUIContent("エリア範囲からプレビューする"));
        using (new EditorGUI.DisabledScope(!previewDefaultCameraByAreaBounds.boolValue))
        {
            EditorGUILayout.PropertyField(defaultCameraPreviewWeight, new GUIContent("中心寄せの強さ"));
            EditorGUILayout.PropertyField(defaultCameraPreviewZoomWeight, new GUIContent("引き具合の強さ"));
            EditorGUILayout.PropertyField(maxDefaultCameraPreviewSize, new GUIContent("最大画角"));
        }

        EditorGUI.indentLevel--;
    }

    private void DrawAreaBoundsSection()
    {
        EditorGUILayout.Space(6f);
        showAreaBoundsSettings = EditorGUILayout.Foldout(
            showAreaBoundsSettings,
            "エリア判定",
            true);
        if (!showAreaBoundsSettings)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(areaColliders2D, new GUIContent("2Dコライダー"), true);
        EditorGUILayout.PropertyField(areaColliders, new GUIContent("3Dコライダー"), true);
        EditorGUI.indentLevel--;
    }

    private void DrawAdvancedSection()
    {
        EditorGUILayout.Space(6f);
        showAdvancedSettings = EditorGUILayout.Foldout(
            showAdvancedSettings,
            "詳細設定",
            true);
        if (!showAdvancedSettings)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(playerTag, new GUIContent("プレイヤータグ"));
        EditorGUILayout.PropertyField(activePriority, new GUIContent("有効時 Priority"));
        EditorGUILayout.PropertyField(inactivePriority, new GUIContent("無効時 Priority"));
        EditorGUI.indentLevel--;
    }
}
