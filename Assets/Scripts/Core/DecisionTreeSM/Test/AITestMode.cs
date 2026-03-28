using UnityEngine;

public class AITestMode : MonoBehaviour
{
    public static AITestMode Instance { get; private set; }

    [Header("Global AI Mode")]
    [SerializeField] private bool testMode = true;
    [SerializeField] private bool verboseLogs = true;

    public bool TestMode => testMode;
    public bool VerboseLogs => verboseLogs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}