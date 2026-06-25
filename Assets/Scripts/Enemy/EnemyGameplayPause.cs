using Metroidvania.Managers;
using UnityEngine;

namespace GameName.Enemy
{
    public static class EnemyGameplayPause
    {
        private const string PlayerTag = "Player";
        private const float LookupInterval = 0.5f;

        private static global::PlayerController cachedPlayerController;
        private static DialogueManager cachedDialogueManager;
        private static float nextPlayerLookupTime;
        private static float nextDialogueLookupTime;

        public static bool IsPaused()
        {
            RefreshReferencesIfNeeded();

            if (cachedPlayerController != null && cachedPlayerController.IsExternalControlLocked)
            {
                return true;
            }

            return cachedDialogueManager != null &&
                   cachedDialogueManager.Runner != null &&
                   cachedDialogueManager.Runner.IsDialogueRunning;
        }

        public static void ResetCache()
        {
            cachedPlayerController = null;
            cachedDialogueManager = null;
            nextPlayerLookupTime = 0f;
            nextDialogueLookupTime = 0f;
        }

        private static void RefreshReferencesIfNeeded()
        {
            float now = Time.unscaledTime;
            if (!IsPlayerControllerUsable(cachedPlayerController) && now >= nextPlayerLookupTime)
            {
                nextPlayerLookupTime = now + LookupInterval;
                cachedPlayerController = ResolvePlayerController();
            }

            if (cachedDialogueManager == null && now >= nextDialogueLookupTime)
            {
                nextDialogueLookupTime = now + LookupInterval;
                cachedDialogueManager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            }
        }

        private static global::PlayerController ResolvePlayerController()
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(PlayerTag);
            if (taggedPlayer != null)
            {
                global::PlayerController taggedController = taggedPlayer.GetComponent<global::PlayerController>();
                if (taggedController == null)
                {
                    taggedController = taggedPlayer.GetComponentInParent<global::PlayerController>();
                }

                if (IsPlayerControllerUsable(taggedController))
                {
                    return taggedController;
                }
            }

            global::PlayerController[] candidates =
                Object.FindObjectsByType<global::PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsPlayerControllerUsable(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private static bool IsPlayerControllerUsable(global::PlayerController playerController)
        {
            return playerController != null &&
                   playerController.gameObject.activeInHierarchy;
        }
    }
}
