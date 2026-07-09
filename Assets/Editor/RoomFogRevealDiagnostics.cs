using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RoomFogRevealDiagnostics
{
    private const string OverlayObjectName = "FOG";
    private const string ShaderName = "CaseStudy/RoomFogOverlay";

    [MenuItem("Tools/CaseStudy/Environment/Log Room Fog Status")]
    private static void LogRoomFogStatus()
    {
        RoomFogRevealManager manager = UnityEngine.Object.FindFirstObjectByType<RoomFogRevealManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogWarning("[RoomFogDiagnostics] RoomFogRevealManager was not found in the active scene.");
            return;
        }

        Type managerType = typeof(RoomFogRevealManager);
        object overlayObject = GetField(managerType, manager, "overlayObject");
        object overlayRenderer = GetField(managerType, manager, "overlayRenderer");
        object fogMaterial = GetField(managerType, manager, "fogMaterial");
        object maskTexture = GetField(managerType, manager, "maskTexture");
        object entranceMaskTexture = GetField(managerType, manager, "entranceMaskTexture");
        object rooms = GetField(managerType, manager, "rooms");
        object currentRoom = GetField(managerType, manager, "currentRoom");
        object worldBounds = GetField(managerType, manager, "worldBounds");
        object hasRooms = GetField(managerType, manager, "hasRooms");
        object fogEnabled = GetField(managerType, manager, "fogEnabled");

        MeshRenderer renderer = overlayRenderer as MeshRenderer;
        GameObject overlay = overlayObject as GameObject;
        Texture2D mask = maskTexture as Texture2D;
        Texture2D entranceMask = entranceMaskTexture as Texture2D;
        Material material = fogMaterial as Material;
        ICollection roomCollection = rooms as ICollection;
        Shader shader = Shader.Find(ShaderName);

        Transform discoveredOverlay = FindOverlayTransform();
        string rendererStatus = renderer == null
            ? "null"
            : $"enabled={renderer.enabled}, sortingLayer={renderer.sortingLayerID}, sortingOrder={renderer.sortingOrder}, material={renderer.sharedMaterial?.name ?? "null"}";
        string overlayStatus = overlay == null
            ? "null"
            : $"name={overlay.name}, activeSelf={overlay.activeSelf}, activeInHierarchy={overlay.activeInHierarchy}, position={overlay.transform.position}, parent={overlay.transform.parent?.name ?? "null"}";
        string materialStatus = material == null
            ? "null"
            : $"name={material.name}, shader={material.shader?.name ?? "null"}";
        string maskStatus = mask == null
            ? "null"
            : $"{mask.width}x{mask.height}, format={mask.format}";
        string entranceMaskStatus = entranceMask == null
            ? "null"
            : $"{entranceMask.width}x{entranceMask.height}, format={entranceMask.format}";
        string discoveredStatus = discoveredOverlay == null
            ? "not found by name"
            : $"found name={discoveredOverlay.name}, activeInHierarchy={discoveredOverlay.gameObject.activeInHierarchy}, parent={discoveredOverlay.parent?.name ?? "null"}";

        Debug.Log(
            "[RoomFogDiagnostics]\n" +
            $"Scene: {manager.gameObject.scene.name}\n" +
            $"Manager: activeSelf={manager.gameObject.activeSelf}, enabled={manager.enabled}, position={manager.transform.position}\n" +
            $"Serialized: fogEnabled={fogEnabled}, hasRooms={hasRooms}, roomCount={roomCollection?.Count.ToString() ?? "null"}, currentRoom={currentRoom ?? "null"}, worldBounds={worldBounds}\n" +
            $"Shader.Find(\"{ShaderName}\"): {(shader == null ? "null" : shader.name)}\n" +
            $"Overlay field: {overlayStatus}\n" +
            $"Overlay search: {discoveredStatus}\n" +
            $"Renderer: {rendererStatus}\n" +
            $"Material: {materialStatus}\n" +
            $"Mask: {maskStatus}\n" +
            $"EntranceMask: {entranceMaskStatus}");
    }

    private static object GetField(Type type, object target, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(target) : null;
    }

    private static Transform FindOverlayTransform()
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name.StartsWith(OverlayObjectName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
