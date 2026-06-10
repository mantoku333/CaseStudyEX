using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OptionsSpriteToImageAutomation
{
    private static readonly string[] ButtonObjectNames =
    {
        "Q",
        "E",
        "Back Button",
        "Map No Select 1_0",
        "Map Select 1_0",
        "Decoration No Select 1_0",
        "Decoration Select 1_0",
        "Note No Select 1_0",
        "Note Select 1_0",
        "Option text No select 1_0",
        "Option text select 1_0",
    };

    [MenuItem("Mantoku/Fix Options - Convert SpriteRenderers to UI Images")]
    public static void Run()
    {
        const string prefabPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError("[OptionsSpriteToImage] Failed to load prefab: " + prefabPath);
            return;
        }

        try
        {
            Transform optionPanel = prefabRoot.transform.Find("MenuRoot/OptionPanel");
            if (optionPanel == null)
            {
                Debug.LogError("[OptionsSpriteToImage] MenuRoot/OptionPanel not found in prefab.");
                return;
            }

            SpriteRenderer[] spriteRenderers = optionPanel.GetComponentsInChildren<SpriteRenderer>(true);
            int converted = 0;

            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr.GetComponent<RectTransform>() != null)
                {
                    continue;
                }

                GameObject original = sr.gameObject;
                Sprite sprite = sr.sprite;
                Color color = sr.color;
                string objName = original.name;
                bool wasActive = original.activeSelf;
                Vector3 localPos = original.transform.localPosition;
                int siblingIndex = original.transform.GetSiblingIndex();
                Transform parent = original.transform.parent;

                GameObject replacement = new GameObject(objName);
                replacement.layer = LayerMask.NameToLayer("UI");
                replacement.transform.SetParent(parent, false);

                RectTransform rt = replacement.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(localPos.x, localPos.y);

                if (sprite != null)
                {
                    rt.sizeDelta = sprite.rect.size;
                }

                Image img = replacement.AddComponent<Image>();
                img.sprite = sprite;
                img.color = color;
                img.raycastTarget = NeedsButton(objName);

                if (NeedsButton(objName))
                {
                    Button btn = replacement.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.targetGraphic = img;
                }

                replacement.SetActive(wasActive);
                replacement.transform.SetSiblingIndex(siblingIndex);

                Object.DestroyImmediate(original);
                converted++;
                Debug.Log($"[OptionsSpriteToImage] Converted: {objName}");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            Debug.Log($"[OptionsSpriteToImage] Done. Converted {converted} SpriteRenderer objects to UI Image.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool NeedsButton(string objName)
    {
        for (int i = 0; i < ButtonObjectNames.Length; i++)
        {
            if (ButtonObjectNames[i] == objName)
            {
                return true;
            }
        }

        return false;
    }
}
