using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

namespace Metroidvania.Managers
{
    public class DialogueGamePauser : MonoBehaviour
    {
        [SerializeField] private DialogueRunner dialogueRunner = null!;

        [Header("Default Policy")]
        [SerializeField] private StoryPausePolicy defaultPausePolicy = StoryPausePolicy.TimeScaleZero;

        private static readonly string[] PlayerControlBehaviourNames =
        {
            "PlayerController",
            "PlayerController_ozono",
            "PlayerPlatformerMockController",
            "DodgeController",
            "PlayerShooter",
            "GunController",
            "UmbrellaController",
            "UmbrellaAttackController",
            "UmbrellaParryController"
        };

        private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
        private PlayerInput pausedPlayerInput;
        private bool previousPlayerInputEnabled;
        private float previousTimeScale = 1f;
        private bool gameplayPaused;
        private bool timeScalePaused;
        private bool callbacksRegistered;

        private void OnEnable()
        {
            StoryPauseRuntime.DialogueDefaultPolicy = defaultPausePolicy;
            EnsureDialogueRunner();
            RegisterCallbacks();

            // Catch up when dialogue was already started before callback registration.
            if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
            {
                PauseGame();
            }
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            ResumeGame();
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
            ResumeGame();
        }

        private void PauseGame()
        {
            StoryPausePolicy policy = StoryPauseRuntime.EffectivePolicy;

            switch (policy)
            {
                case StoryPausePolicy.None:
                    return;

                case StoryPausePolicy.GameplayOnly:
                    PauseGameplay();
                    return;

                case StoryPausePolicy.TimeScaleZero:
                    PauseGameplay();
                    if (!timeScalePaused)
                    {
                        timeScalePaused = true;
                        previousTimeScale = Time.timeScale;
                        Time.timeScale = 0f;
                    }
                    return;

                default:
                    return;
            }
        }

        private void ResumeGame()
        {
            if (timeScalePaused)
            {
                timeScalePaused = false;
                Time.timeScale = previousTimeScale;
            }

            ResumeGameplay();
        }

        private void PauseGameplay()
        {
            if (gameplayPaused)
            {
                return;
            }

            gameplayPaused = true;
            pausedBehaviours.Clear();

            pausedPlayerInput = FindFirstObjectByType<PlayerInput>();
            if (pausedPlayerInput != null)
            {
                previousPlayerInputEnabled = pausedPlayerInput.enabled;
                pausedPlayerInput.enabled = false;
            }

            GameObject player = ResolvePlayerObject();
            if (player == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = player.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                if (!ShouldPauseBehaviour(behaviour.GetType().Name))
                {
                    continue;
                }

                behaviour.enabled = false;
                pausedBehaviours.Add(behaviour);
            }

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void ResumeGameplay()
        {
            if (!gameplayPaused)
            {
                return;
            }

            gameplayPaused = false;

            if (pausedPlayerInput != null)
            {
                pausedPlayerInput.enabled = previousPlayerInputEnabled;
            }

            for (int i = 0; i < pausedBehaviours.Count; i++)
            {
                if (pausedBehaviours[i] != null)
                {
                    pausedBehaviours[i].enabled = true;
                }
            }

            pausedBehaviours.Clear();
        }

        private static bool ShouldPauseBehaviour(string typeName)
        {
            for (int i = 0; i < PlayerControlBehaviourNames.Length; i++)
            {
                if (PlayerControlBehaviourNames[i] == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject ResolvePlayerObject()
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }

            global::PlayerController playerController = FindFirstObjectByType<global::PlayerController>();
            return playerController != null ? playerController.gameObject : null;
        }

        private void EnsureDialogueRunner()
        {
            if (dialogueRunner != null)
            {
                return;
            }

            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
            if (dialogueRunner == null)
            {
                Debug.LogWarning("[DialogueGamePauser] DialogueRunner not found.");
            }
        }

        private void RegisterCallbacks()
        {
            if (callbacksRegistered || dialogueRunner == null)
            {
                return;
            }

            dialogueRunner.onDialogueStart?.AddListener(PauseGame);
            dialogueRunner.onDialogueComplete?.AddListener(ResumeGame);
            callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!callbacksRegistered)
            {
                return;
            }

            if (dialogueRunner != null)
            {
                dialogueRunner.onDialogueStart?.RemoveListener(PauseGame);
                dialogueRunner.onDialogueComplete?.RemoveListener(ResumeGame);
            }

            callbacksRegistered = false;
        }
    }
}
