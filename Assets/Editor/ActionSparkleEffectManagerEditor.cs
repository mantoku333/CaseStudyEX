using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionSparkleEffectManager))]
public sealed class ActionSparkleEffectManagerEditor : Editor
{
    private static readonly bool[] LevelFoldouts = { false, true, true, true, true };
    private static bool referencesFoldout = true;
    private static bool levelSettingsFoldout = true;
    private static bool actionSettingsFoldout;
    private static bool advancedFoldout;

    private SerializedProperty targetPlayer;
    private SerializedProperty sparkleEffectRoot;
    private SerializedProperty sparkleParticles;
    private SerializedProperty autoFindPlayerOnStart;
    private SerializedProperty comboGraceSeconds;
    private SerializedProperty overrideParticleSorting;
    private SerializedProperty particleSortingOrder;
    private SerializedProperty fallbackRateOverTime;
    private SerializedProperty fallbackStartSize;
    private SerializedProperty fallbackStartLifetime;
    private SerializedProperty fallbackStartSpeed;
    private SerializedProperty eleganceLevel;
    private SerializedProperty currentAction;
    private SerializedProperty logLevelChanges;
    private SerializedProperty levelSettings;
    private SerializedProperty glideSettings;
    private SerializedProperty recoilJumpSettings;
    private SerializedProperty dodgeSettings;
    private SerializedProperty diveAttackSettings;
    private SerializedProperty forcePreview;
    private SerializedProperty previewLevel;
    private SerializedProperty previewAction;

    private void OnEnable()
    {
        targetPlayer = serializedObject.FindProperty("targetPlayer");
        sparkleEffectRoot = serializedObject.FindProperty("sparkleEffectRoot");
        sparkleParticles = serializedObject.FindProperty("sparkleParticles");
        autoFindPlayerOnStart = serializedObject.FindProperty("autoFindPlayerOnStart");
        comboGraceSeconds = serializedObject.FindProperty("comboGraceSeconds");
        overrideParticleSorting = serializedObject.FindProperty("overrideParticleSorting");
        particleSortingOrder = serializedObject.FindProperty("particleSortingOrder");
        fallbackRateOverTime = serializedObject.FindProperty("fallbackRateOverTime");
        fallbackStartSize = serializedObject.FindProperty("fallbackStartSize");
        fallbackStartLifetime = serializedObject.FindProperty("fallbackStartLifetime");
        fallbackStartSpeed = serializedObject.FindProperty("fallbackStartSpeed");
        eleganceLevel = serializedObject.FindProperty("eleganceLevel");
        currentAction = serializedObject.FindProperty("currentAction");
        logLevelChanges = serializedObject.FindProperty("logLevelChanges");
        levelSettings = serializedObject.FindProperty("levelSettings");
        glideSettings = serializedObject.FindProperty("glideSettings");
        recoilJumpSettings = serializedObject.FindProperty("recoilJumpSettings");
        dodgeSettings = serializedObject.FindProperty("dodgeSettings");
        diveAttackSettings = serializedObject.FindProperty("diveAttackSettings");
        forcePreview = serializedObject.FindProperty("forcePreview");
        previewLevel = serializedObject.FindProperty("previewLevel");
        previewAction = serializedObject.FindProperty("previewAction");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ActionSparkleEffectManager manager = (ActionSparkleEffectManager)target;

        DrawHeader(manager);
        DrawRuntimeState();
        DrawReferences(manager);
        DrawPreview(manager);
        DrawLevelSettings();
        DrawActionSettings();
        DrawAdvancedSettings();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader(ActionSparkleEffectManager manager)
    {
        EditorGUILayout.HelpBox(
            "Playerの子にあるFX_Actionを探して、2つのParticleSystemをアクションLv0-4で制御します。",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Assign", GUILayout.Height(24f)))
            {
                ExecuteManagerAction("Auto Assign Action Sparkle References", manager, m => m.AutoAssignReferences());
            }

            if (GUILayout.Button("Refresh Particles", GUILayout.Height(24f)))
            {
                ExecuteManagerAction("Refresh Action Sparkle Particles", manager, m => m.RefreshParticlesFromEffectRoot());
            }
        }
    }

    private void DrawRuntimeState()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(eleganceLevel, new GUIContent("Current Level"));
                EditorGUILayout.PropertyField(currentAction, new GUIContent("Current Action"));
            }
        }
    }

    private void DrawReferences(ActionSparkleEffectManager manager)
    {
        EditorGUILayout.Space(4f);
        referencesFoldout = EditorGUILayout.Foldout(referencesFoldout, "References", true);
        if (!referencesFoldout)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(targetPlayer, new GUIContent("Player"));
            EditorGUILayout.PropertyField(sparkleEffectRoot, new GUIContent("FX_Action Root"));
            EditorGUILayout.PropertyField(autoFindPlayerOnStart, new GUIContent("Auto Find Player"));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Set From Children", GUILayout.Width(150f)))
                {
                    ExecuteManagerAction("Auto Assign Action Sparkle References", manager, m => m.AutoAssignReferences());
                }
            }
        }
    }

    private void DrawPreview(ActionSparkleEffectManager manager)
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lv1")) Preview(manager, 1);
                if (GUILayout.Button("Lv2")) Preview(manager, 2);
                if (GUILayout.Button("Lv3")) Preview(manager, 3);
                if (GUILayout.Button("Lv4")) Preview(manager, 4);

                if (GUILayout.Button("Stop"))
                {
                    ExecuteManagerAction("Stop Action Sparkle Preview", manager, m => m.StopPreview());
                }
            }

            EditorGUILayout.PropertyField(previewAction, new GUIContent("Preview Action"));
        }
    }

    private void DrawLevelSettings()
    {
        EditorGUILayout.Space(4f);
        levelSettingsFoldout = EditorGUILayout.Foldout(levelSettingsFoldout, "Level Settings", true);
        if (!levelSettingsFoldout || levelSettings == null)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            for (int i = 0; i < levelSettings.arraySize; i++)
            {
                DrawLevel(i, levelSettings.GetArrayElementAtIndex(i));
            }
        }
    }

    private static void DrawLevel(int index, SerializedProperty level)
    {
        SerializedProperty label = level.FindPropertyRelative("label");
        string title = !string.IsNullOrEmpty(label.stringValue) ? label.stringValue : $"Lv{index}";

        LevelFoldouts[index] = EditorGUILayout.Foldout(LevelFoldouts[index], title, true);
        if (!LevelFoldouts[index])
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(level.FindPropertyRelative("emissionEnabled"), new GUIContent("Enable Emission"));
            EditorGUILayout.PropertyField(level.FindPropertyRelative("rateOverTimeMultiplier"), new GUIContent("Amount"));
            EditorGUILayout.PropertyField(level.FindPropertyRelative("startSizeMultiplier"), new GUIContent("Size"));
            EditorGUILayout.PropertyField(level.FindPropertyRelative("startLifetimeMultiplier"), new GUIContent("Lifetime"));
            EditorGUILayout.PropertyField(level.FindPropertyRelative("startSpeedMultiplier"), new GUIContent("Speed"));

            SerializedProperty overrideStartColor = level.FindPropertyRelative("overrideStartColor");
            EditorGUILayout.PropertyField(overrideStartColor, new GUIContent("Override Color"));
            if (overrideStartColor.boolValue)
            {
                EditorGUILayout.PropertyField(level.FindPropertyRelative("startColor"), new GUIContent("Color"));
            }

            DrawParticleSettings(level.FindPropertyRelative("particles"));
        }
    }

    private static void DrawParticleSettings(SerializedProperty particles)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Particle 0 / 1", EditorStyles.boldLabel);

        for (int i = 0; i < particles.arraySize; i++)
        {
            SerializedProperty particle = particles.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Particle {i}", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(particle.FindPropertyRelative("enabled"), new GUIContent("Use"));
                EditorGUILayout.PropertyField(particle.FindPropertyRelative("rateMultiplier"), new GUIContent("Amount"));
                EditorGUILayout.PropertyField(particle.FindPropertyRelative("sizeMultiplier"), new GUIContent("Size"));
                EditorGUILayout.PropertyField(particle.FindPropertyRelative("lifetimeMultiplier"), new GUIContent("Lifetime"));
                EditorGUILayout.PropertyField(particle.FindPropertyRelative("speedMultiplier"), new GUIContent("Speed"));
                EditorGUILayout.PropertyField(particle.FindPropertyRelative("colorMultiplier"), new GUIContent("Color Multiplier"));
            }
        }
    }

    private void DrawActionSettings()
    {
        EditorGUILayout.Space(4f);
        actionSettingsFoldout = EditorGUILayout.Foldout(actionSettingsFoldout, "Action Speed Multipliers", true);
        if (!actionSettingsFoldout)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawActionSpeed(glideSettings, "Glide");
            DrawActionSpeed(recoilJumpSettings, "Recoil Jump");
            DrawActionSpeed(dodgeSettings, "Dodge");
            DrawActionSpeed(diveAttackSettings, "Dive Attack");
        }
    }

    private static void DrawActionSpeed(SerializedProperty action, string label)
    {
        EditorGUILayout.PropertyField(action.FindPropertyRelative("speedMultiplier"), new GUIContent(label));
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.Space(4f);
        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced", true);
        if (!advancedFoldout)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(comboGraceSeconds, new GUIContent("Combo Grace Seconds"));
            EditorGUILayout.PropertyField(logLevelChanges, new GUIContent("Log Level Changes"));
            EditorGUILayout.PropertyField(overrideParticleSorting, new GUIContent("Override Sorting"));
            if (overrideParticleSorting.boolValue)
            {
                EditorGUILayout.PropertyField(particleSortingOrder, new GUIContent("Sorting Order"));
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Fallback Values", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(fallbackRateOverTime, new GUIContent("Amount"));
            EditorGUILayout.PropertyField(fallbackStartSize, new GUIContent("Size"));
            EditorGUILayout.PropertyField(fallbackStartLifetime, new GUIContent("Lifetime"));
            EditorGUILayout.PropertyField(fallbackStartSpeed, new GUIContent("Speed"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(forcePreview, new GUIContent("Force Preview"));
            EditorGUILayout.PropertyField(previewLevel, new GUIContent("Preview Level"));
            EditorGUILayout.PropertyField(sparkleParticles, new GUIContent("Resolved Particles"), true);
        }
    }

    private void Preview(ActionSparkleEffectManager manager, int level)
    {
        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(manager, "Preview Action Sparkle");
        ActionSparkleEffectManager.SparkleAction action =
            (ActionSparkleEffectManager.SparkleAction)previewAction.enumValueIndex;
        manager.PreviewLevel(level, action);
        EditorUtility.SetDirty(manager);
        serializedObject.Update();
    }

    private void ExecuteManagerAction(
        string undoName,
        ActionSparkleEffectManager manager,
        Action<ActionSparkleEffectManager> action)
    {
        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(manager, undoName);
        action(manager);
        EditorUtility.SetDirty(manager);
        serializedObject.Update();
    }
}
