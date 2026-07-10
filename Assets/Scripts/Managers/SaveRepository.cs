using System;
using System.IO;
using UnityEngine;

public static class SaveRepository
{
    private const string SaveFileNameFormat = "save_slot_{0:D2}.json";
    private const string PreviewFileNameFormat = "save_slot_{0:D2}_preview.png";

    public static bool HasSave(int slotIndex)
    {
        return File.Exists(GetSaveFilePath(slotIndex));
    }

    public static SaveSlotMeta GetSlotMeta(int slotIndex)
    {
        if (!HasSave(slotIndex))
        {
            return new SaveSlotMeta(
                slotIndex: slotIndex,
                hasSave: false,
                isCorrupted: false,
                sceneName: string.Empty,
                locationId: string.Empty,
                savedAtUtc: string.Empty);
        }

        if (!TryRead(slotIndex, out var saveData))
        {
            return new SaveSlotMeta(
                slotIndex: slotIndex,
                hasSave: true,
                isCorrupted: true,
                sceneName: string.Empty,
                locationId: string.Empty,
                savedAtUtc: string.Empty);
        }

        string locationId = CurrentLocationService.GetSavedLocationId(saveData);

        return new SaveSlotMeta(
            slotIndex: slotIndex,
            hasSave: true,
            isCorrupted: false,
            sceneName: saveData.sceneName,
            locationId: locationId,
            savedAtUtc: saveData.savedAtUtc);
    }

    public static bool TryRead(int slotIndex, out SaveGameData saveData)
    {
        saveData = null;

        if (!HasSave(slotIndex))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(GetSaveFilePath(slotIndex));
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            saveData = JsonUtility.FromJson<SaveGameData>(json);
            return saveData != null;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to read save: {exception}");
            return false;
        }
    }

    public static bool TryWrite(int slotIndex, SaveGameData saveData)
    {
        if (saveData == null)
        {
            Debug.LogWarning("[SaveRepository] Save data is null.");
            return false;
        }

        string savePath = GetSaveFilePath(slotIndex);
        string tempPath = savePath + ".tmp";

        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(tempPath, json);

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            File.Move(tempPath, savePath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to write save: {exception}");
            return false;
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    public static bool TryDelete(int slotIndex)
    {
        string savePath = GetSaveFilePath(slotIndex);
        bool previewDeleted = TryDeletePreview(slotIndex);

        if (!File.Exists(savePath))
        {
            return previewDeleted;
        }

        try
        {
            File.Delete(savePath);
            return previewDeleted;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to delete save: {exception}");
            return false;
        }
    }

    public static string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, string.Format(SaveFileNameFormat, slotIndex));
    }

    public static string GetPreviewImagePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, string.Format(PreviewFileNameFormat, slotIndex));
    }

    public static bool HasPreviewImage(int slotIndex)
    {
        return File.Exists(GetPreviewImagePath(slotIndex));
    }

    public static bool TryWritePreviewPng(int slotIndex, byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0)
        {
            Debug.LogWarning("[SaveRepository] Preview PNG data is empty.");
            return false;
        }

        string previewPath = GetPreviewImagePath(slotIndex);
        string tempPath = previewPath + ".tmp";

        try
        {
            File.WriteAllBytes(tempPath, pngBytes);

            if (File.Exists(previewPath))
            {
                File.Delete(previewPath);
            }

            File.Move(tempPath, previewPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to write save preview: {exception}");
            return false;
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    public static bool TryLoadPreviewSprite(int slotIndex, out Sprite sprite)
    {
        sprite = null;
        string previewPath = GetPreviewImagePath(slotIndex);
        if (!File.Exists(previewPath))
        {
            return false;
        }

        try
        {
            byte[] pngBytes = File.ReadAllBytes(previewPath);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = $"SaveSlotPreviewTexture_{slotIndex:D2}";
            if (!texture.LoadImage(pngBytes, markNonReadable: false))
            {
                DestroyRuntimeObject(texture);
                return false;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = $"SaveSlotPreview_{slotIndex:D2}";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to load save preview: {exception}");
            return false;
        }
    }

    public static bool TryDeletePreview(int slotIndex)
    {
        string previewPath = GetPreviewImagePath(slotIndex);
        if (!File.Exists(previewPath))
        {
            return true;
        }

        try
        {
            File.Delete(previewPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to delete save preview: {exception}");
            return false;
        }
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static void DestroyRuntimeObject(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(obj);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
