using System;
using UnityEngine;

public sealed class FullscreenDebugMessageOverlay : MonoBehaviour
{
    private const float DefaultDurationSeconds = 1.6f;
    private const int FontSize = 42;

    private static FullscreenDebugMessageOverlay instance;

    private string currentMessage = string.Empty;
    private float hideAtRealtime;
    private GUIStyle messageStyle;
    private GUIStyle shadowStyle;

    public static void Show(string message, float durationSeconds = DefaultDurationSeconds)
    {
        if (!Application.isPlaying || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureInstance();
        instance.currentMessage = message;
        instance.hideAtRealtime = Time.realtimeSinceStartup + Mathf.Max(0.1f, durationSeconds);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(nameof(FullscreenDebugMessageOverlay));
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<FullscreenDebugMessageOverlay>();
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(currentMessage))
        {
            return;
        }

        if (Time.realtimeSinceStartup > hideAtRealtime)
        {
            currentMessage = string.Empty;
            return;
        }

        EnsureStyles();

        Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.38f);
        GUI.DrawTexture(full, Texture2D.whiteTexture);
        GUI.color = previousColor;

        Rect labelRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Rect shadowRect = labelRect;
        shadowRect.x += 2f;
        shadowRect.y += 2f;
        GUI.Label(shadowRect, currentMessage, shadowStyle);
        GUI.Label(labelRect, currentMessage, messageStyle);
    }

    private void EnsureStyles()
    {
        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = FontSize,
                fontStyle = FontStyle.Bold
            };
            messageStyle.normal.textColor = Color.white;
        }

        if (shadowStyle == null)
        {
            shadowStyle = new GUIStyle(messageStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.72f);
        }
    }
}
