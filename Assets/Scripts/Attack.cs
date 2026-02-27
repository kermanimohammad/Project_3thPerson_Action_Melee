using UnityEngine;

[System.Serializable]
public class Attack
{
    public string animationTrigger;
    public float damage = 10f;

    public float timeToResetCombo = 2f;

    public float lockoutTime = 0.35f;

    [Range(0f, 1f)]
    public float comboWindowStart = 0.7f;
}