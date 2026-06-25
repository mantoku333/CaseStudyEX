using Metroidvania.Managers;
using UnityEngine;

namespace GameName.Enemy
{
    public static class EnemyGameplayPause
    {
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
            global::PlayerController playerController =
                global::PlayerReferenceCache.GetController(forceRefresh: true);
            return IsPlayerControllerUsable(playerController) ? playerController : null;
        }

        private static bool IsPlayerControllerUsable(global::PlayerController playerController)
        {
            return playerController != null &&
                   playerController.gameObject.activeInHierarchy;
        }
    }
}
