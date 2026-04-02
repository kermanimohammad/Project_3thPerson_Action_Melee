using System;
using UnityEngine;

/// <summary>
/// Icon lookup for gamepad bindings.
/// - Slot icons: inspector list of (standard control + sprite); when the bound control matches that slot, that sprite is used.
/// - Path entries: optional substring rules (first match wins); use for unusual paths or to override slot icons.
/// </summary>
[CreateAssetMenu(menuName = "Game/UI/Gamepad Icon Library", fileName = "GamepadIconLibrary")]
public class GamepadIconLibrary : ScriptableObject
{
    /// <summary>
    /// Standard gamepad controls; pair each with a sprite in <see cref="slotIcons"/>.
    /// </summary>
    public enum GamepadControlSlot
    {
        LeftStick = 0,
        RightStick = 1,
        LeftStickPress = 2,
        RightStickPress = 3,
        DPadUp = 4,
        DPadDown = 5,
        DPadLeft = 6,
        DPadRight = 7,
        LeftShoulder = 8,
        RightShoulder = 9,
        LeftTrigger = 10,
        RightTrigger = 11,
        ButtonSouth = 12,
        ButtonEast = 13,
        ButtonNorth = 14,
        ButtonWest = 15,
        Start = 16,
        Select = 17,
    }

    [Serializable]
    public struct PathEntry
    {
        [Tooltip("Matched case-insensitively against the effective binding path. More specific strings should appear earlier.")]
        public string pathSubstring;
        public Sprite icon;
    }

    [Serializable]
    public struct SlotIcon
    {
        public GamepadControlSlot slot;
        public Sprite icon;
    }

    [Header("Standard controls (recommended)")]
    [Tooltip("Each row: choose a gamepad control and drag the sprite that should show when that control is bound.")]
    [SerializeField] private SlotIcon[] slotIcons = Array.Empty<SlotIcon>();

    [Header("Path substring overrides (optional)")]
    [Tooltip("Evaluated before slot icons. First match wins. Keeps compatibility with older libraries that only used this list.")]
    [SerializeField] private PathEntry[] entries = Array.Empty<PathEntry>();

    public Sprite ResolveSprite(string effectivePath)
    {
        if (string.IsNullOrEmpty(effectivePath))
            return null;

        string lower = effectivePath.ToLowerInvariant();

        Sprite fromEntry = ResolveFromPathEntries(lower);
        if (fromEntry != null)
            return fromEntry;

        if (TryDetectSlot(lower, out GamepadControlSlot slot))
            return FindIconForSlot(slot);

        return null;
    }

    private Sprite ResolveFromPathEntries(string pathLower)
    {
        if (entries == null || entries.Length == 0)
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            string key = entries[i].pathSubstring;
            if (string.IsNullOrEmpty(key) || entries[i].icon == null)
                continue;
            if (pathLower.Contains(key.ToLowerInvariant()))
                return entries[i].icon;
        }

        return null;
    }

    private Sprite FindIconForSlot(GamepadControlSlot slot)
    {
        if (slotIcons == null || slotIcons.Length == 0)
            return null;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i].slot == slot && slotIcons[i].icon != null)
                return slotIcons[i].icon;
        }

        return null;
    }

    private static bool TryDetectSlot(string pathLower, out GamepadControlSlot slot)
    {
        if (pathLower.Contains("leftstickpress") || pathLower.Contains("leftstickbutton"))
        {
            slot = GamepadControlSlot.LeftStickPress;
            return true;
        }

        if (pathLower.Contains("rightstickpress") || pathLower.Contains("rightstickbutton"))
        {
            slot = GamepadControlSlot.RightStickPress;
            return true;
        }

        if (pathLower.Contains("dpad/up") || pathLower.Contains("dpad\\up"))
        {
            slot = GamepadControlSlot.DPadUp;
            return true;
        }

        if (pathLower.Contains("dpad/down") || pathLower.Contains("dpad\\down"))
        {
            slot = GamepadControlSlot.DPadDown;
            return true;
        }

        if (pathLower.Contains("dpad/left") || pathLower.Contains("dpad\\left"))
        {
            slot = GamepadControlSlot.DPadLeft;
            return true;
        }

        if (pathLower.Contains("dpad/right") || pathLower.Contains("dpad\\right"))
        {
            slot = GamepadControlSlot.DPadRight;
            return true;
        }

        if (pathLower.Contains("lefttrigger"))
        {
            slot = GamepadControlSlot.LeftTrigger;
            return true;
        }

        if (pathLower.Contains("righttrigger"))
        {
            slot = GamepadControlSlot.RightTrigger;
            return true;
        }

        if (pathLower.Contains("leftshoulder"))
        {
            slot = GamepadControlSlot.LeftShoulder;
            return true;
        }

        if (pathLower.Contains("rightshoulder"))
        {
            slot = GamepadControlSlot.RightShoulder;
            return true;
        }

        if (pathLower.Contains("leftstick"))
        {
            slot = GamepadControlSlot.LeftStick;
            return true;
        }

        if (pathLower.Contains("rightstick"))
        {
            slot = GamepadControlSlot.RightStick;
            return true;
        }

        if (pathLower.Contains("buttonsouth"))
        {
            slot = GamepadControlSlot.ButtonSouth;
            return true;
        }

        if (pathLower.Contains("buttoneast"))
        {
            slot = GamepadControlSlot.ButtonEast;
            return true;
        }

        if (pathLower.Contains("buttonnorth"))
        {
            slot = GamepadControlSlot.ButtonNorth;
            return true;
        }

        if (pathLower.Contains("buttonwest"))
        {
            slot = GamepadControlSlot.ButtonWest;
            return true;
        }

        if (pathLower.Contains("/start") || pathLower.Contains("startbutton"))
        {
            slot = GamepadControlSlot.Start;
            return true;
        }

        if (pathLower.Contains("/select") || pathLower.Contains("selectbutton"))
        {
            slot = GamepadControlSlot.Select;
            return true;
        }

        slot = default;
        return false;
    }
}
