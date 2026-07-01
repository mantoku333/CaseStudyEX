using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class ActionSparklePrefabSetup
{
    private const string ActionPrefabPath = "Assets/TextMesh Pro/Examples & Extras/Prefabs/FX_Action.prefab";

    [DidReloadScripts]
    private static void AttachAfterScriptsReload()
    {
        EditorApplication.delayCall -= AttachActionSparkleControllerIfNeeded;
        EditorApplication.delayCall += AttachActionSparkleControllerIfNeeded;
    }

    [MenuItem("Tools/Effects/Attach Action Sparkle Controller")]
    public static void AttachActionSparkleControllerIfNeeded()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ActionPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning("FX_Action prefab was not found: " + ActionPrefabPath);
            return;
        }

        try
        {
            ActionSparkleController controller = prefabRoot.GetComponent<ActionSparkleController>();
            if (controller == null)
            {
                controller = prefabRoot.AddComponent<ActionSparkleController>();
            }

            ConfigureController(controller, prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ActionPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ActionPrefabPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("Attached ActionSparkleController to FX_Action prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureController(ActionSparkleController controller, GameObject prefabRoot)
    {
        SerializedObject serializedController = new SerializedObject(controller);

        SetBool(serializedController, "includeRootParticleSystem", false);
        SetBool(serializedController, "stopIgnoredRootParticleSystem", true);
        SetBool(serializedController, "preservePrefabParticleSettings", true);

        ParticleSystem rootParticleSystem = prefabRoot.GetComponent<ParticleSystem>();
        ParticleSystem[] particleSystems = prefabRoot.GetComponentsInChildren<ParticleSystem>(true);
        SerializedProperty actionParticleSystems = serializedController.FindProperty("actionParticleSystems");
        if (actionParticleSystems != null)
        {
            int childParticleCount = CountChildParticleSystems(particleSystems, rootParticleSystem);
            actionParticleSystems.arraySize = childParticleCount;

            int insertIndex = 0;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null || particleSystem == rootParticleSystem)
                {
                    continue;
                }

                actionParticleSystems.GetArrayElementAtIndex(insertIndex).objectReferenceValue = particleSystem;
                insertIndex++;
            }
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static int CountChildParticleSystems(ParticleSystem[] particleSystems, ParticleSystem rootParticleSystem)
    {
        int count = 0;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null && particleSystems[i] != rootParticleSystem)
            {
                count++;
            }
        }

        return count;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }
}
