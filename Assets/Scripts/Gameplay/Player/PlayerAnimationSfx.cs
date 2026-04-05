using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Unified SFX component for Animation Events on the Player.
/// Supports event names:
/// - foot_sound (walk/run clips)
/// - jump (jump clips)
/// - kickRun (KickRun clip)
/// - s_attack or S_Attack (SpacialAttack — sound + optional VFX prefab or ParticleSystem burst)
/// Attach to Player root.
/// </summary>
public sealed class PlayerAnimationSfx : MonoBehaviour
{
    [System.Serializable]
    public sealed class SAttackVfxCharacterEntry
    {
        [Tooltip("VFX prefab when this hero is the one selected in MainMenu (BattleArea loadout).")]
        public GameObject prefab;
        [Tooltip("Optional. If null, uses shared S Attack Vfx Spawn Point on PlayerAnimationSfx.")]
        public Transform spawnPoint;
        [Tooltip("Extra world-space offset (added after shared S Attack Vfx World Offset).")]
        public Vector3 worldOffset;
        [Tooltip("Extra euler degrees (added after shared S Attack Vfx Spawn Euler).")]
        public Vector3 spawnEuler;
    }

    [Header("Output (optional)")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.9f;
    [Tooltip("If assigned, footsteps only play when grounded and planar speed exceeds threshold.")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField, Min(0f)] private float minPlanarSpeedToPlay = 0.15f;
    [Tooltip("Prevents double footstep events in the same frame / tiny spacing.")]
    [SerializeField, Min(0f)] private float footstepMinIntervalSeconds = 0.04f;

    [Header("Jump")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
    [Tooltip("Prevents multiple jump events firing too close together.")]
    [SerializeField, Min(0f)] private float jumpMinIntervalSeconds = 0.05f;

    [Header("KickRun")]
    [SerializeField] private AudioClip kickRunClip;
    [SerializeField, Range(0f, 1f)] private float kickRunVolume = 1f;
    [Tooltip("Prevents multiple kickRun events firing too close together.")]
    [SerializeField, Min(0f)] private float kickRunMinIntervalSeconds = 0.05f;

    [Header("Special attack (S_Attack)")]
    [SerializeField] private AudioClip sAttackClip;
    [SerializeField, Range(0f, 1f)] private float sAttackVolume = 1f;
    [Tooltip("Which rig this component belongs to: 0 = Paladin, 1 = Erika (match MainMenu / BattleAreaLoadoutApplier). When both heroes exist in the scene, only the selected one should run s_attack VFX/audio; set 0 on Paladin root and 1 on Erika root. Use -1 to disable filtering (not recommended if two Player roots are active).")]
    [SerializeField] private int battleCharacterSlotIndex = -1;
    [Tooltip("Index 0 = Paladin, 1 = Erika. Prefab/spawn used for the hero chosen in MainMenu (PlayerPrefs). If empty, falls back to S Attack Vfx Prefab (Legacy).")]
    [SerializeField] private SAttackVfxCharacterEntry[] sAttackVfxBySelectedCharacter;
    [Tooltip("Legacy single prefab when sAttackVfxBySelectedCharacter is not used or has no prefab for the current hero.")]
    [SerializeField] private GameObject sAttackVfxPrefab;
    [Tooltip("World position for VFX: only this transform's position is used (rotation of character is ignored). If null, uses player root position.")]
    [SerializeField] private Transform sAttackVfxSpawnPoint;
    [Tooltip("Added in world space to the spawn point position (does not follow character rotation).")]
    [SerializeField] private Vector3 sAttackVfxWorldOffset;
    [Tooltip("World rotation for the spawned prefab root (degrees). Tune X/Y/Z if the effect looks rotated vs placing it manually under a parent — manual placement inherits parent rotation; code spawn uses only this + prefab defaults.")]
    [SerializeField] private Vector3 sAttackVfxSpawnEuler;
    [Tooltip("If 0, destroy time is estimated from ParticleSystem modules on the prefab.")]
    [SerializeField, Min(0f)] private float sAttackVfxDestroyAfterSeconds;
    [Tooltip("Particle(s) played on the same Animation Event as s_attack / S_Attack (e.g. under the character).")]
    [SerializeField] private ParticleSystem[] sAttackParticles;
    [Tooltip("If true, a second s_attack while the same ParticleSystem is still playing will NOT Stop/Clear it (fixes cut-off when the clip has multiple s_attack events).")]
    [SerializeField] private bool sAttackParticlesDontInterruptIfPlaying = true;
    [Tooltip("Clear/stop emission before Play. Turn OFF if you have multiple s_attack events in one clip, or if the burst gets cut in half.")]
    [SerializeField] private bool sAttackParticlesClearBeforePlay = false;
    [Tooltip("Prevents multiple S_Attack events firing too close together (sound + logic).")]
    [SerializeField, Min(0f)] private float sAttackMinIntervalSeconds = 0.05f;
    [Tooltip("When a VFX prefab is used, enforces at least this many seconds between spawns (in addition to S Attack Min Interval). Stops double spawns if S Attack Min Interval is 0.")]
    [SerializeField, Min(0f)] private float sAttackVfxPrefabMinIntervalSeconds = 0.12f;

    private AudioSource _source;
    private float _nextAllowedFootstepTime;
    private float _nextAllowedJumpTime;
    private float _nextAllowedKickRunTime;
    private float _nextAllowedSAttackTime;
    private int _lastSAttackFrameExecuted = -1;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;
    }

    // Animation Event (exact name expected by clips)
    public void foot_sound() => PlayFootstep();
    public void foot_sound(AnimationEvent _) => PlayFootstep();

    // Animation Event (exact name expected by clips)
    public void jump() => PlayJump();
    public void jump(AnimationEvent _) => PlayJump();

    // Animation Event (exact name expected by clips)
    public void kickRun() => PlayKickRun();
    public void kickRun(AnimationEvent _) => PlayKickRun();

    /// <summary>Animation Event — function name <c>s_attack</c> or <c>S_Attack</c> on the clip.</summary>
    public void s_attack() => PlaySpecialAttack();
    public void s_attack(AnimationEvent _) => PlaySpecialAttack();

    public void S_Attack() => PlaySpecialAttack();
    public void S_Attack(AnimationEvent _) => PlaySpecialAttack();

    private void PlayFootstep()
    {
        if (_source == null)
            return;

        if (footstepMinIntervalSeconds > 0f && Time.unscaledTime < _nextAllowedFootstepTime)
            return;

        if (motor != null)
        {
            if (!motor.IsGrounded)
                return;
            if (motor.PlanarVelocity.magnitude < minPlanarSpeedToPlay)
                return;
        }

        if (footstepClips == null || footstepClips.Length == 0)
            return;

        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null)
            return;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;

        _source.PlayOneShot(clip, Mathf.Clamp01(footstepVolume));
        _nextAllowedFootstepTime = Time.unscaledTime + footstepMinIntervalSeconds;
    }

    private void PlayJump()
    {
        if (_source == null)
            return;

        if (jumpMinIntervalSeconds > 0f && Time.unscaledTime < _nextAllowedJumpTime)
            return;

        if (jumpClip == null)
            return;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;

        _source.PlayOneShot(jumpClip, Mathf.Clamp01(jumpVolume));
        _nextAllowedJumpTime = Time.unscaledTime + jumpMinIntervalSeconds;
    }

    private void PlayKickRun()
    {
        if (_source == null)
            return;

        if (kickRunMinIntervalSeconds > 0f && Time.unscaledTime < _nextAllowedKickRunTime)
            return;

        if (kickRunClip == null)
            return;

        if (outputGroup == null)
            outputGroup = GameAudioSettings.FindMixerGroup("SFX");
        _source.outputAudioMixerGroup = outputGroup;

        _source.PlayOneShot(kickRunClip, Mathf.Clamp01(kickRunVolume));
        _nextAllowedKickRunTime = Time.unscaledTime + kickRunMinIntervalSeconds;
    }

    private static int GetSelectedCharacterIndexForBattle()
    {
        if (BattleLoadoutPersistence.TryLoad(out var lo))
            return Mathf.Clamp(lo.CharacterIndex, 0, 1);
        return 0;
    }

    private void PlaySpecialAttack()
    {
        // Two Animation Events in the same frame (e.g. duplicate s_attack) → run once.
        if (Time.frameCount == _lastSAttackFrameExecuted)
            return;

        int selectedHero = GetSelectedCharacterIndexForBattle();
        if (battleCharacterSlotIndex >= 0 && battleCharacterSlotIndex != selectedHero)
            return;

        GameObject prefabToSpawn = null;
        Transform perHeroSpawn = null;
        Vector3 perHeroWorldOffset = Vector3.zero;
        Vector3 perHeroSpawnEuler = Vector3.zero;

        if (sAttackVfxBySelectedCharacter != null && selectedHero < sAttackVfxBySelectedCharacter.Length)
        {
            SAttackVfxCharacterEntry entry = sAttackVfxBySelectedCharacter[selectedHero];
            if (entry != null && entry.prefab != null)
            {
                prefabToSpawn = entry.prefab;
                perHeroSpawn = entry.spawnPoint;
                perHeroWorldOffset = entry.worldOffset;
                perHeroSpawnEuler = entry.spawnEuler;
            }
        }

        if (prefabToSpawn == null)
            prefabToSpawn = sAttackVfxPrefab;

        float gateSeconds = sAttackMinIntervalSeconds;
        if (prefabToSpawn != null)
            gateSeconds = Mathf.Max(gateSeconds, sAttackVfxPrefabMinIntervalSeconds);

        if (gateSeconds > 0f && Time.unscaledTime < _nextAllowedSAttackTime)
            return;

        bool didSomething = false;

        if (prefabToSpawn != null)
        {
            Transform spawnT = perHeroSpawn != null ? perHeroSpawn : sAttackVfxSpawnPoint;
            Vector3 basePos = spawnT != null ? spawnT.position : transform.position;
            Vector3 worldPos = basePos + sAttackVfxWorldOffset + perHeroWorldOffset;
            Quaternion worldRot = Quaternion.Euler(sAttackVfxSpawnEuler + perHeroSpawnEuler);
            GameObject fx = Instantiate(prefabToSpawn, worldPos, worldRot);
            float destroyAfter = sAttackVfxDestroyAfterSeconds > 0f
                ? sAttackVfxDestroyAfterSeconds
                : EstimateParticlePrefabLifetimeSeconds(fx);
            Destroy(fx, destroyAfter);
            didSomething = true;
        }
        else if (sAttackParticles != null)
        {
            for (int i = 0; i < sAttackParticles.Length; i++)
            {
                var ps = sAttackParticles[i];
                if (ps == null)
                    continue;

                if (sAttackParticlesDontInterruptIfPlaying && ps.isPlaying)
                    continue;

                if (sAttackParticlesClearBeforePlay)
                    ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);

                ps.Play(withChildren: true);
                didSomething = true;
            }
        }

        if (sAttackClip != null && _source != null)
        {
            if (outputGroup == null)
                outputGroup = GameAudioSettings.FindMixerGroup("SFX");
            _source.outputAudioMixerGroup = outputGroup;

            _source.PlayOneShot(sAttackClip, Mathf.Clamp01(sAttackVolume));
            didSomething = true;
        }

        if (didSomething)
        {
            _lastSAttackFrameExecuted = Time.frameCount;
            if (gateSeconds > 0f)
                _nextAllowedSAttackTime = Time.unscaledTime + gateSeconds;
        }
    }

    private static float EstimateParticlePrefabLifetimeSeconds(GameObject root)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        if (systems == null || systems.Length == 0)
            return 2f;

        float maxEnd = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;

            var main = ps.main;
            float dur = main.duration;
            float lifeMax = main.startLifetime.constantMax;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                lifeMax = main.startLifetime.constant;

            maxEnd = Mathf.Max(maxEnd, dur + lifeMax);
        }

        return Mathf.Max(0.1f, maxEnd + 0.05f);
    }
}

