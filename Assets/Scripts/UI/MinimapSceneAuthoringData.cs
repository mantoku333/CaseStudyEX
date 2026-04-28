using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UI/Minimap Scene Authoring Data")]
[DisallowMultipleComponent]
public sealed class MinimapSceneAuthoringData : MonoBehaviour
{
    [SerializeField] private List<MinimapLinkDefinition> manualLinks = new List<MinimapLinkDefinition>();

    public IReadOnlyList<MinimapLinkDefinition> ManualLinks => manualLinks;

    public void SetManualLinks(IEnumerable<MinimapLinkDefinition> links)
    {
        manualLinks.Clear();

        if (links == null)
        {
            return;
        }

        foreach (MinimapLinkDefinition link in links)
        {
            if (link == null || !link.IsValid)
            {
                continue;
            }

            manualLinks.Add(link.Clone());
        }
    }

    private void OnValidate()
    {
        manualLinks = CloneValidLinks(manualLinks);
    }

    private static List<MinimapLinkDefinition> CloneValidLinks(IEnumerable<MinimapLinkDefinition> links)
    {
        var cloned = new List<MinimapLinkDefinition>();
        if (links == null)
        {
            return cloned;
        }

        foreach (MinimapLinkDefinition link in links)
        {
            if (link == null || !link.IsValid)
            {
                continue;
            }

            cloned.Add(link.Clone());
        }

        return cloned;
    }
}
