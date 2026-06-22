using UnityEngine;

[CreateAssetMenu(
    fileName = "UIButtonSfxSettings",
    menuName = "Game/UI/Button SFX Settings")]
public sealed class UIButtonSfxSettings : ScriptableObject
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip panelOpenClip;
    [SerializeField] private AudioClip diaryOpenClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    public AudioClip ClickClip => clickClip;
    public AudioClip HoverClip => hoverClip != null ? hoverClip : clickClip;
    public AudioClip PanelOpenClip => panelOpenClip;
    public AudioClip DiaryOpenClip => diaryOpenClip;
    public float Volume => volume;
}
