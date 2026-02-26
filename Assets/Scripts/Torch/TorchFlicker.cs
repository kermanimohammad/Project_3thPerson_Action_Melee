using UnityEngine;

[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Intensity")]
    public float baseIntensity = 7f;
    public float intensityVariation = 1.2f;

    [Header("Range")]
    public float baseRange = 5f;
    public float rangeVariation = 0.5f;

    [Header("Movement")]
    public float positionAmount = 0.04f;
    public float movementSpeed = 2f;

    [Header("General")]
    public float flickerSpeed = 3f;

    private Light torchLight;
    private Vector3 initialPosition;
    private float noiseSeed;

    private Color baseColor;   // 👈 رنگ اصلی ذخیره می‌شود

    void Start()
    {
        torchLight = GetComponent<Light>();
        initialPosition = transform.localPosition;

        baseColor = torchLight.color;   // 👈 ذخیره رنگ Inspector
        noiseSeed = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time;

        float noise = Mathf.PerlinNoise(t * flickerSpeed, noiseSeed);
        float moveX = Mathf.PerlinNoise(t * movementSpeed, noiseSeed + 10f);
        float moveZ = Mathf.PerlinNoise(t * movementSpeed, noiseSeed + 20f);

        // Intensity
        torchLight.intensity =
            baseIntensity + (noise - 0.5f) * intensityVariation;

        // Range
        torchLight.range =
            baseRange + (noise - 0.5f) * rangeVariation;

        // Position jitter (خیلی ملایم)
        Vector3 offset = new Vector3(
            (moveX - 0.5f) * positionAmount,
            (noise - 0.5f) * positionAmount,
            (moveZ - 0.5f) * positionAmount
        );

        transform.localPosition = initialPosition + offset;

        // 👇 فقط brightness را تغییر می‌دهیم، نه رنگ را
        float brightnessFactor = 0.9f + noise * 0.2f;
        torchLight.color = baseColor * brightnessFactor;
    }
}