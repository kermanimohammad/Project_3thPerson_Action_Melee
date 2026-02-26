using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class TorchSpriteAnimator : MonoBehaviour
{
    public int columns = 4;
    public int rows = 4;
    public float framesPerSecond = 15f;

    private Material mat;
    private int totalFrames;
    private int currentFrame;
    private float timer;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        timer = Random.Range(0f, 1f);
        totalFrames = columns * rows;

    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 1f / framesPerSecond)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % totalFrames;

            int column = currentFrame % columns;
            int row = currentFrame / columns;

            row = (rows - 1) - row;

            Vector2 offset = new Vector2(
                (float)column / columns,
                (float)row / rows
            );

            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetTextureOffset("_EmissionMap", offset);
        }
    }
}