using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rotates this transform (camera pivot) from the Player action map Look action — same idea as
/// Starter Assets <c>ThirdPersonController.CameraRotation</c>, for use with Cinemachine Virtual Camera
/// + 3rd Person Follow instead of FreeLook.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerCameraPivot : MonoBehaviour
{
    [Header("Pitch limits (degrees)")]
    [SerializeField] private float topClamp = 85f;
    [SerializeField] private float bottomClamp = -55f;

    [Header("Sensitivity")]
    [Tooltip("Mouse delta × this value (Look uses mouse delta from the Input System).")]
    [SerializeField] private float mouseSensitivity = 0.012f;

    [Tooltip("Caps raw mouse delta per axis before sensitivity (reduces fast spins / bad frames).")]
    [SerializeField] private float maxMouseDeltaPerFrame = 80f;

    [Tooltip("Gamepad right stick: degrees per second at full deflection.")]
    [SerializeField] private float gamepadDegreesPerSecond = 72f;

    [SerializeField] private bool invertLookY;

    [Header("Follow (independent of body yaw)")]
    [Tooltip("Player root with CharacterController. Pivot stays at body position + world offset so turning the character "
             + "with WASD does not orbit the camera attachment.")]
    [SerializeField] private Transform characterBody;

    [Tooltip("Added to character body world position each frame (typically Y = eye/chest height). World axes.")]
    [SerializeField] private Vector3 worldSpaceOffsetFromCharacter = new Vector3(0f, 1.117f, 0f);

    private PlayerInputActions _actions;
    private float _yaw;
    private float _pitch;

    private void Awake()
    {
        _actions = new PlayerInputActions();
        TryResolveCharacterBody();
    }

    private void OnEnable()
    {
        if (_actions == null)
            _actions = new PlayerInputActions();

        TryResolveCharacterBody();
        if (_actions.asset != null)
        {
            InputRebindPersistence.LoadAndApply(_actions.asset);
            InputBindingRuntimeSync.Register(_actions.asset);
        }
        _actions.Player.Enable();
    }

    private void OnDisable()
    {
        if (_actions != null)
        {
            _actions.Player.Disable();
            if (_actions.asset != null)
                InputBindingRuntimeSync.Unregister(_actions.asset);
        }
    }

    private void OnDestroy()
    {
        if (_actions != null)
        {
            _actions.Dispose();
            _actions = null;
        }
    }

    private void Start()
    {
        TryResolveCharacterBody();
        var e = transform.rotation.eulerAngles;
        _yaw = e.y;
        _pitch = NormalizePitch(e.x);
    }

    /// <summary>Clears stale Unity missing references and picks the parent that has CharacterController (Player root).</summary>
    private void TryResolveCharacterBody()
    {
        if (characterBody != null && characterBody)
            return;

        characterBody = null;
        var cc = GetComponentInParent<CharacterController>();
        if (cc != null)
            characterBody = cc.transform;
    }

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f)
            return;

        TryResolveCharacterBody();

        Transform body = characterBody;
        if (body != null && body)
            transform.position = body.position + worldSpaceOffsetFromCharacter;

        Vector2 look = _actions.Player.Look.ReadValue<Vector2>();

        if (look.sqrMagnitude >= 1e-8f)
        {
            float lookY = invertLookY ? -look.y : look.y;

            // Right stick must use deg/sec scaling; mouse delta must NOT (mis-detecting mouse as stick
            // makes yaw jump because pixel deltas are huge compared to -1..1 stick values).
            Vector2 stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
            bool useGamepadLook = stick.sqrMagnitude > 0.12f;

            if (!useGamepadLook)
            {
                float mx = Mathf.Clamp(look.x, -maxMouseDeltaPerFrame, maxMouseDeltaPerFrame);
                float my = Mathf.Clamp(lookY, -maxMouseDeltaPerFrame, maxMouseDeltaPerFrame);
                _yaw += mx * mouseSensitivity;
                _pitch += my * mouseSensitivity;
            }
            else
            {
                float s = gamepadDegreesPerSecond * Time.deltaTime;
                _yaw += look.x * s;
                _pitch += lookY * s;
            }
        }

        _yaw = ClampAngle(_yaw, float.MinValue, float.MaxValue);
        _pitch = ClampAngle(_pitch, bottomClamp, topClamp);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private static float NormalizePitch(float x)
    {
        if (x > 180f) x -= 360f;
        return x;
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
