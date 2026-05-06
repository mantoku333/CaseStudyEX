using UnityEngine;

public static class MinimapSystemFactory
{
    private const string PrefabResourcePath = "UI/MinimapSystem";

    public static MinimapManager EnsureInstance()
    {
        MinimapManager existing = MinimapManager.Instance;
        if (existing != null)
        {
            return existing;
        }

        existing = Object.FindFirstObjectByType<MinimapManager>();
        if (existing != null)
        {
            return existing;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab);
            instance.name = "MinimapSystem";
            existing = instance.GetComponent<MinimapManager>();
            if (existing != null)
            {
                return existing;
            }
        }

        GameObject fallback = new GameObject("MinimapSystem");
        existing = fallback.AddComponent<MinimapManager>();
        return existing;
    }
}
