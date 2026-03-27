using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroShowcaseCameraController : MonoBehaviour
{
    [Header("Render Camera (3D)")]
    [SerializeField] private Transform renderTextureCamera;

    [Header("Characters")]
    [SerializeField] private Transform character1;
    [SerializeField] private Transform character2;

    [Header("Optional Camera Poses")]
    [Tooltip("If assigned, camera will move to these transforms (position) and look at lookAt transforms.")]
    [SerializeField] private Transform cameraPose1;
    [SerializeField] private Transform cameraPose2;

    [Tooltip("If null, lookAt will use character positions.")]
    [SerializeField] private Transform lookAt1;
    [SerializeField] private Transform lookAt2;

    [Header("Fallback Offsets (used if cameraPose is null)")]
    [SerializeField] private Vector3 cameraOffset1 = new Vector3(0f, 1.6f, -3.5f);
    [SerializeField] private Vector3 cameraOffset2 = new Vector3(0f, 1.6f, -3.5f);

    [Header("UI Buttons (always visible; interactable by state)")]
    [SerializeField] private GameObject leftButton; // Previous
    [SerializeField] private GameObject rightButton; // Next

    [Header("Character name (TextMeshPro)")]
    [Tooltip("Optional: shows Paladin / Erika (or custom names below) when the camera focuses each hero.")]
    [SerializeField] private TextMeshProUGUI characterNameLabel;
    [SerializeField] private string character1DisplayName = "Paladin";
    [SerializeField] private string character2DisplayName = "Erika";

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 0.35f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsTransitioning => transitionRoutine != null;

    /// <summary>0 = first character, 1 = second (matches SelectPrevious / SelectNext flow).</summary>
    public int SelectedCharacterIndex => selectedIndex;

    private int selectedIndex;
    private Coroutine transitionRoutine;

    private void Start()
    {
        selectedIndex = 0;
        ApplyUIState();
        SnapToIndex(selectedIndex);
    }

    public void SelectNext()
    {
        RequestIndex(1);
    }

    public void SelectPrevious()
    {
        RequestIndex(0);
    }

    private void RequestIndex(int index)
    {
        if (index < 0 || index > 1) return;
        if (selectedIndex == index) return;
        if (IsTransitioning) return;

        selectedIndex = index;
        ApplyUIState();
        transitionRoutine = StartCoroutine(MoveCameraToIndex(index));
    }

    private void ApplyUIState()
    {
        // Keep both arrows visible; only disable invalid direction.
        if (leftButton != null)
        {
            leftButton.SetActive(true);
            var leftBtn = leftButton.GetComponent<Button>();
            if (leftBtn != null) leftBtn.interactable = (selectedIndex == 1);
        }

        if (rightButton != null)
        {
            rightButton.SetActive(true);
            var rightBtn = rightButton.GetComponent<Button>();
            if (rightBtn != null) rightBtn.interactable = (selectedIndex == 0);
        }

        ApplyCharacterNameLabel();
    }

    private void ApplyCharacterNameLabel()
    {
        if (characterNameLabel == null) return;
        characterNameLabel.text = selectedIndex == 0 ? character1DisplayName : character2DisplayName;
    }

    private void SnapToIndex(int index)
    {
        if (renderTextureCamera == null) return;

        if (index == 0)
        {
            MoveCameraImmediate(character1, cameraPose1, lookAt1, cameraOffset1);
        }
        else
        {
            MoveCameraImmediate(character2, cameraPose2, lookAt2, cameraOffset2);
        }
    }

    private void MoveCameraImmediate(Transform character, Transform cameraPose, Transform lookAt, Vector3 fallbackOffset)
    {
        if (renderTextureCamera == null) return;
        if (character == null && cameraPose == null) return;

        Transform targetLookAt = lookAt != null ? lookAt : character;

        Vector3 targetPos;
        if (cameraPose != null)
        {
            targetPos = cameraPose.position;
        }
        else
        {
            targetPos = character.position + fallbackOffset;
        }

        renderTextureCamera.position = targetPos;

        if (targetLookAt != null)
        {
            renderTextureCamera.rotation = Quaternion.LookRotation(targetLookAt.position - targetPos);
        }
    }

    private IEnumerator MoveCameraToIndex(int index)
    {
        if (renderTextureCamera == null) yield break;

        Transform character = index == 0 ? character1 : character2;
        Transform cameraPose = index == 0 ? cameraPose1 : cameraPose2;
        Transform lookAt = index == 0 ? lookAt1 : lookAt2;
        Vector3 fallbackOffset = index == 0 ? cameraOffset1 : cameraOffset2;

        if (character == null && cameraPose == null) yield break;

        Transform targetLookAt = lookAt != null ? lookAt : character;

        Vector3 startPos = renderTextureCamera.position;
        Quaternion startRot = renderTextureCamera.rotation;

        Vector3 targetPos = cameraPose != null ? cameraPose.position : character.position + fallbackOffset;
        Quaternion targetRot = startRot;
        if (targetLookAt != null)
        {
            targetRot = Quaternion.LookRotation(targetLookAt.position - targetPos);
        }

        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float normalized = transitionDuration <= 0f ? 1f : Mathf.Clamp01(t / transitionDuration);
            float eased = transitionCurve != null ? transitionCurve.Evaluate(normalized) : normalized;

            renderTextureCamera.position = Vector3.Lerp(startPos, targetPos, eased);
            renderTextureCamera.rotation = Quaternion.Slerp(startRot, targetRot, eased);
            yield return null;
        }

        renderTextureCamera.position = targetPos;
        renderTextureCamera.rotation = targetRot;
        transitionRoutine = null;
    }
}

