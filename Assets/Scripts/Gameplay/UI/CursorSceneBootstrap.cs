using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Global cursor policy to avoid getting "stuck" locked/hidden after scene transitions.
/// Creates a small runtime object so we can enforce cursor state across a few frames (some systems
/// may re-lock on focus or late initialization).
/// </summary>
public sealed class CursorSceneBootstrap : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private static CursorSceneBootstrap _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_instance != null)
            return;

        var go = new GameObject("CursorSceneBootstrap (Runtime)");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CursorSceneBootstrap>();
        _instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid())
            return;

        if (scene.name == MainMenuSceneName)
            StartCoroutine(EnforceMainMenuCursorForAWhile());
    }

    private static void ApplyMainMenuCursorNow()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // StarterAssets can re-lock cursor on focus; make sure its flag is off in MainMenu.
        // (If the type isn't present in the project, this compiles out naturally.)
        var starterInputs = FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < starterInputs.Length; i++)
        {
            if (starterInputs[i] != null)
                starterInputs[i].cursorLocked = false;
        }
    }

    private IEnumerator EnforceMainMenuCursorForAWhile()
    {
        // Apply immediately, then again over a few frames to win races against late init/focus callbacks.
        ApplyMainMenuCursorNow();
        yield return null;
        ApplyMainMenuCursorNow();
        yield return null;
        ApplyMainMenuCursorNow();

        // One more after a tiny delay (unscaled) to cover edge cases.
        float t = 0.1f;
        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        ApplyMainMenuCursorNow();
    }
}

