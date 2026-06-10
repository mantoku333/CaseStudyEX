using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleMenuHierarchySetup
{
    private const string SpriteFolderFilter = "Assets/Art/Texture/UI/Title";

    [MenuItem("Tools/Title Menu/Setup Button Hierarchy")]
    public static void SetupButtonHierarchy()
    {
        SetupButton("Btn_NewGame",
            FindSprite("Start 1"),
            FindSprite("Start No select 1"));

        SetupButton("Btn_Countinue",
            FindSpriteContaining("Continue", mustContain: "select", mustNotContain: "No"),
            FindSpriteContaining("Continue", mustContain: "No"));

        SetupButton("Btn_ExitGame",
            FindSprite("Finish select 1"),
            FindSprite("Finish Noselect"));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[TitleMenuSetup] Setup complete.");
    }

    private static void SetupButton(string buttonName, Sprite selectSprite, Sprite notSelectSprite)
    {
        GameObject buttonObj = GameObject.Find(buttonName);
        if (buttonObj == null)
        {
            Debug.LogWarning($"[TitleMenuSetup] '{buttonName}' not found in scene.");
            return;
        }

        ClearParentImage(buttonObj);
        EnsureStateChild(buttonObj.transform, "Select",     selectSprite);
        EnsureStateChild(buttonObj.transform, "Not Select", notSelectSprite);
        EditorUtility.SetDirty(buttonObj);
    }

    private static void ClearParentImage(GameObject buttonObj)
    {
        Image image = buttonObj.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private static void EnsureStateChild(Transform parent, string childName, Sprite sprite)
    {
        Transform existing = parent.Find(childName);
        GameObject stateObj;
        if (existing != null)
        {
            stateObj = existing.gameObject;
        }
        else
        {
            stateObj = new GameObject(childName);
            stateObj.transform.SetParent(parent, false);
            stateObj.AddComponent<RectTransform>();
        }

        RectTransform rt = stateObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localPosition = Vector3.zero;
        rt.localScale    = Vector3.one;

        Image image = stateObj.GetComponent<Image>();
        if (image == null)
        {
            image = stateObj.AddComponent<Image>();
        }

        image.sprite = sprite;
        image.raycastTarget = false;
    }

    private static Sprite FindSprite(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:Texture2D", new[] { SpriteFolderFilter });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        Debug.LogWarning($"[TitleMenuSetup] Sprite '{assetName}' not found.");
        return null;
    }

    private static Sprite FindSpriteContaining(string baseName, string mustContain, string mustNotContain = null)
    {
        string[] guids = AssetDatabase.FindAssets($"{baseName} t:Texture2D", new[] { SpriteFolderFilter });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            bool hasMust    = fileName.Contains(mustContain);
            bool hasExclude = mustNotContain != null && fileName.Contains(mustNotContain);

            if (!hasMust || hasExclude)
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        Debug.LogWarning($"[TitleMenuSetup] Sprite for '{baseName}' (contains '{mustContain}') not found.");
        return null;
    }
}
