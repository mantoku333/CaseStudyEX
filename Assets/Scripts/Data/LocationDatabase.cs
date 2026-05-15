using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LocationDatabase", menuName = "Game/Location Database")]
public sealed class LocationDatabase : ScriptableObject
{
    [SerializeField] private LocationDisplayInfo[] locations = Array.Empty<LocationDisplayInfo>();

    public bool TryGetDisplayInfo(string locationId, out LocationDisplayInfo displayInfo)
    {
        displayInfo = default;

        if (locations == null || string.IsNullOrWhiteSpace(locationId))
        {
            return false;
        }

        for (int i = 0; i < locations.Length; i++)
        {
            if (locations[i].Matches(locationId))
            {
                displayInfo = locations[i];
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public struct LocationDisplayInfo
{
    public string locationId;
    public string displayName;
    public Sprite thumbnail;

    public bool Matches(string targetLocationId)
    {
        return !string.IsNullOrWhiteSpace(locationId) &&
               string.Equals(locationId, targetLocationId, StringComparison.Ordinal);
    }
}
