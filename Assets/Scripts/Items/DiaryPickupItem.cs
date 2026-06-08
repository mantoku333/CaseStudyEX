using System.Collections;
using System.Collections.Generic;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

public class DiaryPickupItem : MonoBehaviour, ISaveDataModule
{
    [SerializeField] private DiaryEntryData diaryEntryData;
    [Header("Pickup Event Name Override")]
    [SerializeField] private string pickupEventName = "";

    private bool isPickedUp = false;

    public int Priority => 253;

    private void Awake()
    {
        if (diaryEntryData != null)
        {
            return;
        }

        Debug.LogWarning("DiaryEntryData is not assigned. DiaryPickupItem will be disabled.", this);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        SaveManager.UnregisterModule(this);
    }

    private void Start()
    {
        if (diaryEntryData == null)
        {
            Debug.LogError("DiaryEntryData is not assigned.", this);
            gameObject.SetActive(false);
            return;
        }

        string flagKey = diaryEntryData.GetProgressFlagKey();

        if (GameProgressFlags.Get(flagKey))
        {
            Destroy(gameObject);
        }
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickedUp){ return; }

        if (diaryEntryData == null){ return; }

        if (!other.CompareTag("Player")){ return; }

        string flagKey = diaryEntryData.GetProgressFlagKey();

        if (GameProgressFlags.Get(flagKey)){ return; }

        GameProgressFlags.Set(flagKey, true);

        Debug.Log($"Diary picked up: {diaryEntryData.GetTitle()}");

        TryPlayPickupEvent();

        CompletePickup();
    }

    private void TryPlayPickupEvent()
    {
        string eventName = ResolvePickupEventName();
        if (string.IsNullOrWhiteSpace(eventName)){ return; }

        DiaryPickupEventPlayer.Play(eventName, transform.position, name);
    }

    private string ResolvePickupEventName()
    {
        if (!string.IsNullOrWhiteSpace(pickupEventName))
        {
            return pickupEventName.Trim();
        }

        return diaryEntryData != null ? diaryEntryData.GetPickupEventName() : string.Empty;
    }

    private void CompletePickup()
    {
        isPickedUp = true;

        ItemEffectController effectController = GetComponent<ItemEffectController>();
        if (effectController != null && effectController.PlayPickupEffectAndDestroy())
        {
            return;
        }

        Destroy(gameObject);
    }

    public void Capture(SaveGameData saveData)
    {
    }

    public void Restore(SaveGameData saveData)
    {
        if (diaryEntryData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (GameProgressFlags.Get(diaryEntryData.GetProgressFlagKey()))
        {
            Destroy(gameObject);
        }
    }

    private sealed class DiaryPickupEventPlayer : MonoBehaviour
    {
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
        private GameObject dialogueTargetObject;
        private DialogueManager dialogueManager;
        private PlayerInput pausedPlayerInput;
        private bool previousPlayerInputEnabled;
        private bool waitingDialogueCompletion;
        private bool gameplayPaused;
        private bool cleaningUp;

        public static void Play(string dialogueNodeName, Vector3 pickupPosition, string sourceName)
        {
            GameObject playerObject = new GameObject("[DiaryPickupEventPlayer]");
            DiaryPickupEventPlayer player = playerObject.AddComponent<DiaryPickupEventPlayer>();
            player.StartCoroutine(player.PlayRoutine(dialogueNodeName.Trim(), pickupPosition, sourceName));
        }

        private IEnumerator PlayRoutine(string dialogueNodeName, Vector3 pickupPosition, string sourceName)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>();
            if (dialogueManager == null || dialogueManager.Runner == null)
            {
                Debug.LogWarning($"Diary pickup dialogue manager not found: {dialogueNodeName}");
                Cleanup();
                yield break;
            }

            if (dialogueManager.Runner.Dialogue == null || !dialogueManager.Runner.Dialogue.NodeExists(dialogueNodeName))
            {
                Debug.LogWarning($"Diary pickup dialogue node not found: {dialogueNodeName}");
                Cleanup();
                yield break;
            }

            dialogueTargetObject = new GameObject($"[DiaryPickupDialogueTarget] {sourceName}");
            dialogueTargetObject.transform.position = pickupPosition;

            PausePlayerControl();

            yield return StoryOverlayFader.Instance.FadeTo(1f, 0.5f, Color.black);
            yield return StoryOverlayFader.Instance.FadeTo(0f, 0.5f, Color.black);

            waitingDialogueCompletion = true;
            dialogueManager.StartConversation(dialogueNodeName, DialogueStyle.Bubble, dialogueTargetObject.transform);
            dialogueManager.Runner.onDialogueComplete?.AddListener(OnDialogueComplete);

            while (waitingDialogueCompletion)
            {
                yield return null;
            }

            Cleanup();
        }

        private void PausePlayerControl()
        {
            if (gameplayPaused)
            {
                return;
            }

            gameplayPaused = true;
            pausedBehaviours.Clear();

            GameObject player = ResolvePlayerObject();
            if (player == null)
            {
                return;
            }

            pausedPlayerInput = player.GetComponentInChildren<PlayerInput>(includeInactive: true);
            if (pausedPlayerInput != null)
            {
                previousPlayerInputEnabled = pausedPlayerInput.enabled;
                pausedPlayerInput.enabled = false;
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

        private void ResumePlayerControl()
        {
            if (!gameplayPaused)
            {
                return;
            }

            gameplayPaused = false;

            if (pausedPlayerInput != null)
            {
                pausedPlayerInput.enabled = previousPlayerInputEnabled;
                pausedPlayerInput = null;
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

        private static GameObject ResolvePlayerObject()
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }

            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            return playerController != null ? playerController.gameObject : null;
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

        private void OnDialogueComplete()
        {
            waitingDialogueCompletion = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (cleaningUp)
            {
                return;
            }

            cleaningUp = true;

            if (dialogueManager != null && dialogueManager.Runner != null)
            {
                dialogueManager.Runner.onDialogueComplete?.RemoveListener(OnDialogueComplete);
            }

            ResumePlayerControl();

            if (dialogueTargetObject != null)
            {
                Destroy(dialogueTargetObject);
                dialogueTargetObject = null;
            }

            if (this != null)
            {
                Destroy(gameObject);
            }
        }
    }
}
