using System;
using UnityEngine;

public static class CurrentLocationService
{
    private const string SectionKey = "current_location_v1";
    private static readonly CurrentLocationSaveModule module = new CurrentLocationSaveModule();
    private static string currentLocationId = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.RegisterModule(module);
    }

    public static string CurrentLocationId => currentLocationId;

    public static void SetCurrentLocation(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return;
        }

        currentLocationId = locationId.Trim();
    }

    public static void ClearCurrentLocation()
    {
        currentLocationId = string.Empty;
    }

    public static string GetSavedLocationId(SaveGameData saveData)
    {
        if (saveData == null)
        {
            return string.Empty;
        }

        string json = saveData.GetCustomSectionJson(SectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            var payload = JsonUtility.FromJson<CurrentLocationPayload>(json);
            return payload != null ? payload.locationId ?? string.Empty : string.Empty;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CurrentLocationService] Failed to parse saved location. {exception}");
            return string.Empty;
        }
    }

    public static void RestoreFromSaveData(SaveGameData saveData)
    {
        currentLocationId = GetSavedLocationId(saveData);
    }

    private static CurrentLocationPayload CreatePayload()
    {
        return new CurrentLocationPayload
        {
            locationId = currentLocationId ?? string.Empty
        };
    }

    [Serializable]
    private sealed class CurrentLocationPayload
    {
        public string locationId;
    }

    private sealed class CurrentLocationSaveModule : ISaveDataModule
    {
        public int Priority => 220;

        public void Capture(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(CreatePayload());
            saveData.SetCustomSectionJson(SectionKey, json);
        }

        public void Restore(SaveGameData saveData)
        {
            RestoreFromSaveData(saveData);
        }
    }
}
