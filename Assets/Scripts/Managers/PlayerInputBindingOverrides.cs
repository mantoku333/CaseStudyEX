using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class PlayerInputBindingOverrides
{
    private const string PlayerPrefsKey = "InputBindingOverrides_Player";
    private const string PlayerActionMapName = "Player";
    private const string KeyboardMouseGroupName = "Keyboard&Mouse";
    private const string RecoilJumpActionName = "RecoilJump";

    private static readonly HashSet<int> LoadedActionAssetIds = new HashSet<int>();
    private static bool _sceneHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnLoad()
    {
        if (!_sceneHookRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHookRegistered = true;
        }

        ApplyOverridesToAllPlayerInputsInScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyOverridesToAllPlayerInputsInScene();
    }

    private static void ApplyOverridesToAllPlayerInputsInScene()
    {
        var playerInputs = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            var playerInput = playerInputs[i];
            if (playerInput == null || playerInput.actions == null)
            {
                continue;
            }

            EnsureOverridesLoaded(playerInput.actions);
        }
    }

    public static void EnsureOverridesLoaded(InputActionAsset actions)
    {
        if (actions == null)
        {
            return;
        }

        int assetId = actions.GetInstanceID();
        if (LoadedActionAssetIds.Contains(assetId))
        {
            return;
        }

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            actions.LoadBindingOverridesFromJson(json);
        }

        if (DisableRecoilJumpKeyboardBindings(actions))
        {
            Save(actions);
        }

        LoadedActionAssetIds.Add(assetId);
    }

    public static void Save(InputActionAsset actions)
    {
        if (actions == null)
        {
            return;
        }

        string json = actions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    public static bool TryGetBindingEffectivePath(
        InputActionAsset actions,
        string actionName,
        string bindingGroup,
        out string effectivePath,
        string preferredPathPrefix = null)
    {
        effectivePath = string.Empty;
        if (!TryFindBindingIndex(actions, actionName, bindingGroup, out var action, out int bindingIndex, preferredPathPrefix))
        {
            return false;
        }

        effectivePath = action.bindings[bindingIndex].effectivePath ?? string.Empty;
        return true;
    }

    public static bool TrySetBinding(InputActionAsset actions, string actionName, string bindingGroup, string newPath, string preferredPathPrefix)
    {
        if (actions == null || string.IsNullOrEmpty(newPath))
        {
            return false;
        }

        EnsureOverridesLoaded(actions);

        if (!TryFindBindingIndex(actions, actionName, bindingGroup, out var action, out int bindingIndex, preferredPathPrefix))
        {
            return false;
        }

        action.ApplyBindingOverride(bindingIndex, newPath);
        Save(actions);
        return true;
    }

    public static void ResetGroup(InputActionAsset actions, string bindingGroup)
    {
        if (actions == null)
        {
            return;
        }

        EnsureOverridesLoaded(actions);

        InputActionMap actionMap = actions.FindActionMap(PlayerActionMapName, false);
        if (actionMap == null)
        {
            return;
        }

        for (int actionIndex = 0; actionIndex < actionMap.actions.Count; actionIndex++)
        {
            var action = actionMap.actions[actionIndex];
            for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
            {
                var binding = action.bindings[bindingIndex];
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                if (!BindingContainsGroup(binding.groups, bindingGroup))
                {
                    continue;
                }

                action.RemoveBindingOverride(bindingIndex);
            }
        }

        Save(actions);
    }

    private static bool TryFindBindingIndex(
        InputActionAsset actions,
        string actionName,
        string bindingGroup,
        out InputAction action,
        out int bindingIndex,
        string preferredPathPrefix = null)
    {
        action = null;
        bindingIndex = -1;

        if (actions == null || string.IsNullOrEmpty(actionName))
        {
            return false;
        }

        InputActionMap actionMap = actions.FindActionMap(PlayerActionMapName, false);
        if (actionMap == null)
        {
            return false;
        }

        action = actionMap.FindAction(actionName, false);
        if (action == null)
        {
            return false;
        }

        int fallbackIndex = -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            if (!BindingContainsGroup(binding.groups, bindingGroup))
            {
                continue;
            }

            if (fallbackIndex < 0)
            {
                fallbackIndex = i;
            }

            string effectivePath = binding.effectivePath ?? binding.path ?? string.Empty;
            if (!string.IsNullOrEmpty(preferredPathPrefix) &&
                effectivePath.StartsWith(preferredPathPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                bindingIndex = i;
                return true;
            }
        }

        if (fallbackIndex >= 0)
        {
            bindingIndex = fallbackIndex;
            return true;
        }

        return false;
    }

    private static bool DisableRecoilJumpKeyboardBindings(InputActionAsset actions)
    {
        InputActionMap actionMap = actions.FindActionMap(PlayerActionMapName, false);
        InputAction recoilJumpAction = actionMap != null ? actionMap.FindAction(RecoilJumpActionName, false) : null;
        if (recoilJumpAction == null)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < recoilJumpAction.bindings.Count; i++)
        {
            InputBinding binding = recoilJumpAction.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            if (!BindingContainsGroup(binding.groups, KeyboardMouseGroupName))
            {
                continue;
            }

            if (binding.overridePath == string.Empty)
            {
                continue;
            }

            recoilJumpAction.ApplyBindingOverride(i, new InputBinding { overridePath = string.Empty });
            changed = true;
        }

        return changed;
    }

    private static bool BindingContainsGroup(string bindingGroups, string targetGroup)
    {
        if (string.IsNullOrEmpty(bindingGroups) || string.IsNullOrEmpty(targetGroup))
        {
            return false;
        }

        var groups = bindingGroups.Split(';');
        for (int i = 0; i < groups.Length; i++)
        {
            if (string.Equals(groups[i], targetGroup, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
