using UnityEngine;

/// <summary>
/// Project asset: drag your BattleArea pause Settings UI prefab here.
/// Place this asset under a <c>Resources</c> folder as <c>BattleAreaSettingsUI</c> so the bootstrap
/// <see cref="PauseMenuController"/> can load it without an in-scene reference.
/// </summary>
[CreateAssetMenu(fileName = "BattleAreaSettingsUI", menuName = "Game UI/Battle Area Settings UI")]
public sealed class BattleAreaSettingsUIConfig : ScriptableObject
{
    [Tooltip("Prefab root: only the Settings UI you want in BattleArea (Tabs + panels). No world Fire/Torch.")]
    public GameObject settingsPanelPrefab;

    [Header("Gameplay music (BattleArea)")]
    [Tooltip("Loops during BattleArea gameplay. Pauses when the pause menu opens; resumes when you continue. Output: AudioMixer → Music.")]
    public AudioClip battleGameplayMusicClip;

    [Tooltip("If true, battle music loops while playing.")]
    public bool loopBattleGameplayMusic = true;

    [Header("Pause menu music (BattleArea)")]
    [Tooltip("Played when BattleArea is paused. Output: AudioMixer → Music (MusicVolume slider). Default: Citadel at Dusk (BattleMenu).")]
    public AudioClip pauseMenuMusicClip;

    [Tooltip("If true, music loops while the pause menu stays open.")]
    public bool loopPauseMenuMusic = true;
}
