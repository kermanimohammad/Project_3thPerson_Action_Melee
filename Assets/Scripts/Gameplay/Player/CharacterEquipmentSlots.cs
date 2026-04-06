using UnityEngine;

/// <summary>
/// Wire weapon / shield / helmet children on a character prefab so <see cref="BattleAreaLoadoutApplier"/>
/// can toggle them after Instantiate (spawn mode).
/// </summary>
public sealed class CharacterEquipmentSlots : MonoBehaviour
{
    [Header("Same layout as BattleAreaLoadoutApplier EquipmentSet")]
    public GameObject[] weaponsRightHand;
    public GameObject[] shieldsLeftHand;
    public GameObject[] helmetsHead;
}
