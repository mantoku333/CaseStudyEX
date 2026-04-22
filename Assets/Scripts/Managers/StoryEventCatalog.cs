using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryEventCatalog", menuName = "CaseStudy/Story Event Catalog")]
public sealed class StoryEventCatalog : ScriptableObject
{
    public List<StoryEventDefinition> sceneStartEvents = new List<StoryEventDefinition>();
}
