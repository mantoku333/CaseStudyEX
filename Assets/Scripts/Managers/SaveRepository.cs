using System;
using System.IO;
using UnityEngine;

public static class SaveRepository
{
    private const string SaveFileNameFormat = "save_slot_{0:D2}.json";

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
                savedAtUtc: string.Empty);
        }

        if (!TryRead(slotIndex, out var saveData))
        {
            return new SaveSlotMeta(
                slotIndex: slotIndex,
                hasSave: true,
                isCorrupted: true,
                sceneName: string.Empty,
                savedAtUtc: string.Empty);
        }

        return new SaveSlotMeta(
            slotIndex: slotIndex,
            hasSave: true,
            isCorrupted: false,
            sceneName: saveData.sceneName,
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

        if (!File.Exists(savePath))
        {
            return true;
        }

        try
        {
            File.Delete(savePath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveRepository] Failed to delete save: {exception}");
            return false;
        }
    }

    private static string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, string.Format(SaveFileNameFormat, slotIndex));
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
}
