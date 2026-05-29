using UnityEngine;

[CreateAssetMenu(
    fileName = "GameCursorBootstrapSettings",
    menuName = "Game/UI/Game Cursor Bootstrap Settings")]
public sealed class GameCursorBootstrapSettings : ScriptableObject
{
    [SerializeField] private GameObject cursorPrefab;

    public GameObject CursorPrefab => cursorPrefab;
}
