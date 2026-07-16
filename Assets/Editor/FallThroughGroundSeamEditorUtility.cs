using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FallThroughGroundSeamEditorUtility
{
    private const string FixCapcomScenePath = "Assets/Scenes/FixScenes/Fix_CAPCOM.unity";
    private const string SeamPrefabPath = "Assets/Prefabs/Environment/FallThruGroundSeam.prefab";
    private const string SeamParentName = "FallThruGroundSeams";
    private const string SeamObjectPrefix = "FallThruGroundSeam_";
    private const string GroundLayerName = "Ground";
    private const string FallThroughLayerName = "FallThroughFloor";
    private const float SurfaceGroupingTolerance = 0.03f;
    private const float PlatformJoinTolerance = 0.2f;
    private const float GroundProbeStartHeight = 2f;
    private const float GroundProbeDistance = 4f;
    private const float GroundSurfaceTolerance = 0.3f;
    private const float SeamEndpointTolerance = 0.35f;
    private const float ScanPadding = 1.25f;
    private const float ScanStep = 0.05f;
    private const float SeamHalfWidth = 1f;
    private const float SeamPeakClearance = 0.05f;

    [MenuItem("Tools/Level/Fall-Through Seams/Apply Fix to Fix_CAPCOM")]
    public static void ApplyFixCapcom()
    {
        Scene scene = EditorSceneManager.OpenScene(FixCapcomScenePath, OpenSceneMode.Single);
        Physics2D.SyncTransforms();

        List<PlatformStrip> strips = CollectPlatformStrips();
        List<GroundSeam> seams = FindGroundSeams(strips);
        if (seams.Count == 0)
        {
            throw new InvalidOperationException("No FallThruFloor-to-Ground seams were found in Fix_CAPCOM.");
        }

        GameObject seamPrefab = CreateOrUpdateSeamPrefab();
        Transform seamParent = FindSeamParent(scene);
        RemoveExistingSeamHelpers(scene);

        for (int i = 0; i < seams.Count; i++)
        {
            CreateSeamHelper(seamPrefab, seamParent, seams[i], i + 1);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FallThroughGroundSeam] Applied {seams.Count} seam helpers to {FixCapcomScenePath}.");
    }

    [MenuItem("Tools/Level/Fall-Through Seams/Analyze Fix_CAPCOM")]
    public static void AnalyzeFixCapcom()
    {
        EditorSceneManager.OpenScene(FixCapcomScenePath, OpenSceneMode.Single);
        Physics2D.SyncTransforms();

        List<PlatformStrip> strips = CollectPlatformStrips();
        List<GroundSeam> seams = FindGroundSeams(strips);

        Debug.Log($"[FallThroughGroundSeam] Found {strips.Count} platform strips and {seams.Count} ground seams.");
        for (int i = 0; i < seams.Count; i++)
        {
                GroundSeam seam = seams[i];
                Debug.Log(
                $"[FallThroughGroundSeam] {i + 1}: x={seam.X:F4}, platformY={seam.PlatformY:F4}, " +
                $"groundY={seam.GroundY:F4}, delta={seam.GroundY - seam.PlatformY:F4}, " +
                $"groundIsLeft={seam.GroundIsLeft}, strip={seam.StripMinX:F3}..{seam.StripMaxX:F3}, " +
                $"members={string.Join(", ", seam.MemberNames)}");
        }
    }

    private static List<PlatformStrip> CollectPlatformStrips()
    {
        int fallThroughLayer = LayerMask.NameToLayer(FallThroughLayerName);
        Collider2D[] allColliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        List<Collider2D> platforms = new List<Collider2D>();

        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider2D collider2d = allColliders[i];
            if (collider2d == null ||
                !collider2d.enabled ||
                collider2d.isTrigger ||
                !(collider2d is BoxCollider2D) ||
                !collider2d.name.StartsWith("FallThruFloor", StringComparison.Ordinal) ||
                collider2d.gameObject.layer != fallThroughLayer ||
                !collider2d.usedByEffector)
            {
                continue;
            }

            platforms.Add(collider2d);
        }

        platforms.Sort((a, b) =>
        {
            int yComparison = a.bounds.max.y.CompareTo(b.bounds.max.y);
            return yComparison != 0 ? yComparison : a.bounds.min.x.CompareTo(b.bounds.min.x);
        });

        List<PlatformStrip> strips = new List<PlatformStrip>();
        for (int i = 0; i < platforms.Count; i++)
        {
            Collider2D platform = platforms[i];
            PlatformStrip matchingStrip = null;

            for (int j = 0; j < strips.Count; j++)
            {
                PlatformStrip strip = strips[j];
                bool sameSurface = Mathf.Abs(strip.SurfaceY - platform.bounds.max.y) <= SurfaceGroupingTolerance;
                bool horizontallyJoined =
                    platform.bounds.min.x <= strip.MaxX + PlatformJoinTolerance &&
                    platform.bounds.max.x >= strip.MinX - PlatformJoinTolerance;
                if (sameSurface && horizontallyJoined)
                {
                    matchingStrip = strip;
                    break;
                }
            }

            if (matchingStrip == null)
            {
                matchingStrip = new PlatformStrip(platform);
                strips.Add(matchingStrip);
            }
            else
            {
                matchingStrip.Add(platform);
            }
        }

        return strips;
    }

    private static List<GroundSeam> FindGroundSeams(List<PlatformStrip> strips)
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        int groundMask = groundLayer >= 0 ? 1 << groundLayer : 0;
        List<GroundSeam> seams = new List<GroundSeam>();

        if (groundMask == 0)
        {
            Debug.LogError("[FallThroughGroundSeam] Ground layer is missing.");
            return seams;
        }

        for (int stripIndex = 0; stripIndex < strips.Count; stripIndex++)
        {
            PlatformStrip strip = strips[stripIndex];
            float scanStart = strip.MinX - ScanPadding;
            float scanEnd = strip.MaxX + ScanPadding;
            float previousX = scanStart;
            bool previousHasGround = HasGroundAt(previousX, strip.SurfaceY, groundMask);

            for (float x = scanStart + ScanStep; x <= scanEnd + 0.001f; x += ScanStep)
            {
                bool hasGround = HasGroundAt(x, strip.SurfaceY, groundMask);
                if (hasGround != previousHasGround)
                {
                    float seamX = RefineTransition(
                        previousX,
                        x,
                        strip.SurfaceY,
                        groundMask,
                        previousHasGround);
                    float endpointDistance = Mathf.Min(
                        Mathf.Abs(seamX - strip.MinX),
                        Mathf.Abs(seamX - strip.MaxX));
                    bool groundIsLeft = HasGroundAt(seamX - 0.1f, strip.SurfaceY, groundMask);
                    bool groundIsRight = HasGroundAt(seamX + 0.1f, strip.SurfaceY, groundMask);
                    if (endpointDistance <= SeamEndpointTolerance && groundIsLeft != groundIsRight)
                    {
                        float groundSampleX = seamX + (groundIsLeft ? -0.15f : 0.15f);
                        if (TryGetGroundSurfaceAt(groundSampleX, strip.SurfaceY, groundMask, out float groundY))
                        {
                            seams.Add(new GroundSeam(strip, seamX, groundY, groundIsLeft));
                        }
                    }
                }

                previousX = x;
                previousHasGround = hasGround;
            }
        }

        return seams;
    }

    private static bool HasGroundAt(float x, float platformSurfaceY, int groundMask)
    {
        return TryGetGroundSurfaceAt(x, platformSurfaceY, groundMask, out _);
    }

    private static bool TryGetGroundSurfaceAt(
        float x,
        float platformSurfaceY,
        int groundMask,
        out float groundSurfaceY)
    {
        groundSurfaceY = 0f;
        Vector2 origin = new Vector2(x, platformSurfaceY + GroundProbeStartHeight);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, GroundProbeDistance, groundMask);
        float closestDifference = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            float difference = Mathf.Abs(hit.point.y - platformSurfaceY);
            if (hit.collider != null &&
                !hit.collider.isTrigger &&
                hit.normal.y > 0.5f &&
                difference <= GroundSurfaceTolerance &&
                difference < closestDifference)
            {
                closestDifference = difference;
                groundSurfaceY = hit.point.y;
                found = true;
            }
        }

        return found;
    }

    private static GameObject CreateOrUpdateSeamPrefab()
    {
        int fallThroughLayer = LayerMask.NameToLayer(FallThroughLayerName);
        GameObject template = new GameObject("FallThruGroundSeam")
        {
            layer = fallThroughLayer
        };

        try
        {
            EdgeCollider2D edge = template.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-SeamHalfWidth, 0f),
                new Vector2(0f, SeamPeakClearance),
                new Vector2(SeamHalfWidth, 0f)
            };
            edge.usedByEffector = true;

            PlatformEffector2D effector = template.AddComponent<PlatformEffector2D>();
            effector.useColliderMask = false;
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 165f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, SeamPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Could not create seam prefab at {SeamPrefabPath}.");
            }

            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(template);
        }
    }

    private static Transform FindSeamParent(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root.name == SeamParentName)
            {
                return root.transform;
            }
        }

        GameObject parent = new GameObject(SeamParentName);
        SceneManager.MoveGameObjectToScene(parent, scene);
        return parent.transform;
    }

    private static void RemoveExistingSeamHelpers(Scene scene)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = transforms.Length - 1; i >= 0; i--)
        {
            Transform candidate = transforms[i];
            if (candidate != null &&
                candidate.gameObject.scene == scene &&
                candidate.name.StartsWith(SeamObjectPrefix, StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(candidate.gameObject);
            }
        }
    }

    private static void CreateSeamHelper(
        GameObject seamPrefab,
        Transform parent,
        GroundSeam seam,
        int index)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(seamPrefab, parent);
        instance.name = $"{SeamObjectPrefix}{index:00}_X{seam.X:F3}";
        instance.transform.position = new Vector3(seam.X, seam.PlatformY, 0f);
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        float groundLocalY = seam.GroundY - seam.PlatformY;
        float peakLocalY = Mathf.Max(0f, groundLocalY) + SeamPeakClearance;
        Vector2 groundPoint = new Vector2(
            seam.GroundIsLeft ? -SeamHalfWidth : SeamHalfWidth,
            groundLocalY);
        Vector2 platformPoint = new Vector2(
            seam.GroundIsLeft ? SeamHalfWidth : -SeamHalfWidth,
            0f);

        EdgeCollider2D edge = instance.GetComponent<EdgeCollider2D>();
        edge.points = seam.GroundIsLeft
            ? new[] { groundPoint, new Vector2(0f, peakLocalY), platformPoint }
            : new[] { platformPoint, new Vector2(0f, peakLocalY), groundPoint };

        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(edge);
    }

    private static float RefineTransition(
        float left,
        float right,
        float platformSurfaceY,
        int groundMask,
        bool groundAtLeft)
    {
        for (int i = 0; i < 12; i++)
        {
            float middle = (left + right) * 0.5f;
            bool groundAtMiddle = HasGroundAt(middle, platformSurfaceY, groundMask);
            if (groundAtMiddle == groundAtLeft)
            {
                left = middle;
            }
            else
            {
                right = middle;
            }
        }

        return (left + right) * 0.5f;
    }

    private sealed class PlatformStrip
    {
        private readonly List<Collider2D> members = new List<Collider2D>();

        public PlatformStrip(Collider2D first)
        {
            Add(first);
        }

        public float MinX { get; private set; } = float.PositiveInfinity;
        public float MaxX { get; private set; } = float.NegativeInfinity;
        public float SurfaceY { get; private set; }
        public IReadOnlyList<string> MemberNames => members.Select(member => member.name).ToArray();

        public void Add(Collider2D collider2d)
        {
            members.Add(collider2d);
            MinX = Mathf.Min(MinX, collider2d.bounds.min.x);
            MaxX = Mathf.Max(MaxX, collider2d.bounds.max.x);
            SurfaceY = members.Average(member => member.bounds.max.y);
        }
    }

    private sealed class GroundSeam
    {
        public GroundSeam(PlatformStrip strip, float x, float groundY, bool groundIsLeft)
        {
            X = x;
            PlatformY = strip.SurfaceY;
            GroundY = groundY;
            GroundIsLeft = groundIsLeft;
            StripMinX = strip.MinX;
            StripMaxX = strip.MaxX;
            MemberNames = strip.MemberNames;
        }

        public float X { get; }
        public float PlatformY { get; }
        public float GroundY { get; }
        public bool GroundIsLeft { get; }
        public float StripMinX { get; }
        public float StripMaxX { get; }
        public IReadOnlyList<string> MemberNames { get; }
    }
}
