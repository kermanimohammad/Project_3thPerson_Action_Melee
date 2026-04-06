using UnityEngine;

/// <summary>
/// BattleArea-side loader for the MainMenu loadout (character + equipment indices).
/// Either toggles pre-placed <see cref="characterRoots"/> in the scene, or spawns the selected hero from prefabs.
/// Assumes all weapons are already under right hand, shields under left hand, helmets under head;
/// we only toggle GameObjects by index.
/// </summary>
public class BattleAreaLoadoutApplier : MonoBehaviour
{
    [Header("Characters in scene (toggle mode)")]
    [Tooltip("0 = Paladin, 1 = Erika. Used when Spawn Selected Character From Prefab is OFF.")]
    [SerializeField] private GameObject[] characterRoots = new GameObject[2];

    [Header("Character spawn (prefab mode)")]
    [Tooltip("If ON: disables all Character Roots, destroys any previous spawn, then Instantiate the prefab matching MainMenu selection at Character Spawn Anchor.")]
    [SerializeField] private bool spawnSelectedCharacterFromPrefab;
    [Tooltip("Position/rotation (and optional parent) for the spawned character. Required when spawn mode is on.")]
    [SerializeField] private Transform characterSpawnAnchor;
    [Tooltip("0 = Paladin prefab, 1 = Erika prefab.")]
    [SerializeField] private GameObject[] characterPrefabs = new GameObject[2];

    [Header("Camera (spawn mode)")]
    [Tooltip("After spawning: disable this camera (whole GameObject) and use the first Camera under the spawned character as MainCamera. If null, uses Camera.main at spawn time.")]
    [SerializeField] private Camera sceneDefaultCamera;
    [Tooltip("If false, leaves scene and player cameras as configured on the prefab.")]
    [SerializeField] private bool swapMainCameraAfterSpawn = true;

    [Header("Equipment (per character)")]
    [Tooltip("Used in toggle mode, or in spawn mode if the spawned instance has no CharacterEquipmentSlots.")]
    [SerializeField] private EquipmentSet[] equipmentByCharacter = new EquipmentSet[2];

    [Header("HUD (optional)")]
    [Tooltip("If assigned, the HUD will bind to the selected character's Health. If null, the first PlayerHealthUI in scene is used.")]
    [SerializeField] private PlayerHealthUI playerHealthUi;

    [Header("Behaviour")]
    [SerializeField] private bool applyOnStart = true;

    private GameObject _spawnedCharacterInstance;

    [System.Serializable]
    public class EquipmentSet
    {
        public GameObject[] weaponsRightHand;
        public GameObject[] shieldsLeftHand;
        public GameObject[] helmetsHead;
    }

    private void Start()
    {
        if (!applyOnStart)
            return;
        ApplyFromSavedLoadout();
    }

    private void OnDestroy()
    {
        if (_spawnedCharacterInstance != null)
        {
            Destroy(_spawnedCharacterInstance);
            _spawnedCharacterInstance = null;
        }
    }

    [ContextMenu("Apply From Saved Loadout")]
    public void ApplyFromSavedLoadout()
    {
        BattleMenuLoadout loadout;
        if (!BattleLoadoutPersistence.TryLoad(out loadout))
            loadout = default;

        Apply(loadout);
    }

    public void Apply(in BattleMenuLoadout loadout)
    {
        int characterIndex = Mathf.Clamp(loadout.CharacterIndex, 0, 1);

        GameObject activeCharacterRoot;

        if (spawnSelectedCharacterFromPrefab)
        {
            activeCharacterRoot = SpawnOrReplaceCharacter(characterIndex);
            if (activeCharacterRoot == null)
            {
                Debug.LogError($"{nameof(BattleAreaLoadoutApplier)}: spawn mode enabled but no prefab for character index {characterIndex}.", this);
                return;
            }

            if (swapMainCameraAfterSpawn)
                SwapMainCameraToSpawnedPlayer(activeCharacterRoot);
        }
        else
        {
            if (characterRoots == null || characterRoots.Length == 0)
            {
                Debug.LogError($"{nameof(BattleAreaLoadoutApplier)}: no character roots assigned.", this);
                return;
            }

            characterIndex = Mathf.Clamp(characterIndex, 0, characterRoots.Length - 1);

            for (int i = 0; i < characterRoots.Length; i++)
            {
                if (characterRoots[i] != null)
                    characterRoots[i].SetActive(i == characterIndex);
            }

            activeCharacterRoot = characterRoots[characterIndex];
        }

        BindHealthUi(activeCharacterRoot);
        ApplyEquipment(activeCharacterRoot, characterIndex, loadout);
    }

    private GameObject SpawnOrReplaceCharacter(int characterIndex)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
            return null;

        characterIndex = Mathf.Clamp(characterIndex, 0, characterPrefabs.Length - 1);
        GameObject prefab = characterPrefabs[characterIndex];
        if (prefab == null)
            return null;

        if (characterSpawnAnchor == null)
        {
            Debug.LogError($"{nameof(BattleAreaLoadoutApplier)}: {nameof(characterSpawnAnchor)} must be assigned when spawn mode is on.", this);
            return null;
        }

        if (characterRoots != null)
        {
            for (int i = 0; i < characterRoots.Length; i++)
            {
                if (characterRoots[i] != null)
                    characterRoots[i].SetActive(false);
            }
        }

        if (_spawnedCharacterInstance != null)
        {
            Destroy(_spawnedCharacterInstance);
            _spawnedCharacterInstance = null;
        }

        _spawnedCharacterInstance = Instantiate(
            prefab,
            characterSpawnAnchor.position,
            characterSpawnAnchor.rotation,
            characterSpawnAnchor);

        return _spawnedCharacterInstance;
    }

    private void SwapMainCameraToSpawnedPlayer(GameObject spawned)
    {
        if (spawned == null)
            return;

        Camera playerCamera = spawned.GetComponentInChildren<Camera>(includeInactive: true);
        if (playerCamera == null)
        {
            Debug.LogWarning($"{nameof(BattleAreaLoadoutApplier)}: spawned character has no Camera in children; cannot swap MainCamera.", spawned);
            return;
        }

        Camera sceneCam = sceneDefaultCamera != null ? sceneDefaultCamera : Camera.main;

        if (sceneCam != null && sceneCam != playerCamera)
        {
            sceneCam.gameObject.tag = "Untagged";
            sceneCam.enabled = false;
            sceneCam.gameObject.SetActive(false);
        }

        playerCamera.gameObject.SetActive(true);
        playerCamera.enabled = true;
        if (!playerCamera.CompareTag("MainCamera"))
            playerCamera.gameObject.tag = "MainCamera";
    }

    private void BindHealthUi(GameObject activeCharacterRoot)
    {
        if (activeCharacterRoot == null)
            return;

        Health h = activeCharacterRoot.GetComponentInChildren<Health>(includeInactive: false);
        PlayerHealthUI ui = playerHealthUi != null ? playerHealthUi : Object.FindFirstObjectByType<PlayerHealthUI>();
        if (ui != null)
            ui.SetTargetHealth(h);
    }

    private void ApplyEquipment(GameObject activeCharacterRoot, int characterIndex, in BattleMenuLoadout loadout)
    {
        CharacterEquipmentSlots slots = activeCharacterRoot != null
            ? activeCharacterRoot.GetComponent<CharacterEquipmentSlots>()
            : null;

        if (slots != null)
        {
            SetActiveByIndex(slots.weaponsRightHand, loadout.WeaponIndex);
            SetActiveByIndex(slots.helmetsHead, loadout.HelmetIndex);
            SetActiveByIndex(slots.shieldsLeftHand, loadout.ShieldIndex);
            return;
        }

        if (equipmentByCharacter == null || equipmentByCharacter.Length == 0)
            return;

        if (characterIndex < 0 || characterIndex >= equipmentByCharacter.Length)
            return;

        EquipmentSet set = equipmentByCharacter[characterIndex];
        SetActiveByIndex(set.weaponsRightHand, loadout.WeaponIndex);
        SetActiveByIndex(set.helmetsHead, loadout.HelmetIndex);
        SetActiveByIndex(set.shieldsLeftHand, loadout.ShieldIndex);
    }

    private static void SetActiveByIndex(GameObject[] items, int index)
    {
        if (items == null || items.Length == 0)
            return;

        int clamped = Mathf.Clamp(index, -1, items.Length - 1);

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                items[i].SetActive(i == clamped);
        }
    }
}
