using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class EffectPreviewLooper : MonoBehaviour
{
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private float intervalSeconds = 1.1f;
    [SerializeField] private int maxLiveInstances = 4;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private bool playInEditMode = true;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool useTransformRotation;

    private readonly List<GameObject> spawnedInstances = new List<GameObject>();
    private double nextSpawnTime;

    public GameObject EffectPrefab
    {
        get => effectPrefab;
        set => effectPrefab = value;
    }

    private void OnEnable()
    {
        nextSpawnTime = 0d;

        if (playOnEnable)
        {
            SpawnPreviewInstance();
        }
    }

    private void OnDisable()
    {
        ClearPreviewInstances();
    }

    private void Update()
    {
        if (!ShouldPreview())
        {
            return;
        }

        double now = GetEditorAwareTime();
        if (now < nextSpawnTime)
        {
            RequestEditorRepaint();
            return;
        }

        SpawnPreviewInstance();
        nextSpawnTime = now + Mathf.Max(0.05f, intervalSeconds);
        RequestEditorRepaint();
    }

    [ContextMenu("Spawn Once")]
    public void SpawnPreviewInstance()
    {
        if (effectPrefab == null)
        {
            return;
        }

        CleanupMissingInstances();

        while (spawnedInstances.Count >= Mathf.Max(1, maxLiveInstances))
        {
            DestroyPreviewInstance(spawnedInstances[0]);
            spawnedInstances.RemoveAt(0);
        }

        Vector3 position = transform.position + spawnOffset;
        Quaternion rotation = useTransformRotation ? transform.rotation : Quaternion.identity;
        GameObject instance = InstantiateEffect(effectPrefab, position, rotation);
        instance.name = effectPrefab.name + "_Preview";
        instance.transform.SetParent(transform, true);

        if (!Application.isPlaying)
        {
            instance.hideFlags = HideFlags.DontSaveInEditor;
        }

        PlayParticleSystems(instance);
        spawnedInstances.Add(instance);
    }

    [ContextMenu("Clear Preview Instances")]
    public void ClearPreviewInstances()
    {
        for (int i = spawnedInstances.Count - 1; i >= 0; i--)
        {
            DestroyPreviewInstance(spawnedInstances[i]);
        }

        spawnedInstances.Clear();
    }

    private bool ShouldPreview()
    {
        return Application.isPlaying || playInEditMode;
    }

    private static double GetEditorAwareTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return EditorApplication.timeSinceStartup;
        }
#endif

        return Time.timeAsDouble;
    }

    private static GameObject InstantiateEffect(GameObject prefab, Vector3 position, Quaternion rotation)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object prefabInstance = PrefabUtility.InstantiatePrefab(prefab);
            if (prefabInstance is GameObject prefabGameObject)
            {
                prefabGameObject.transform.SetPositionAndRotation(position, rotation);
                return prefabGameObject;
            }
        }
#endif

        return Instantiate(prefab, position, rotation);
    }

    private static void PlayParticleSystems(GameObject root)
    {
        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void CleanupMissingInstances()
    {
        for (int i = spawnedInstances.Count - 1; i >= 0; i--)
        {
            if (spawnedInstances[i] == null)
            {
                spawnedInstances.RemoveAt(i);
            }
        }
    }

    private static void DestroyPreviewInstance(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(instance);
        }
        else
        {
            DestroyImmediate(instance);
        }
    }

    private static void RequestEditorRepaint()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }
}
