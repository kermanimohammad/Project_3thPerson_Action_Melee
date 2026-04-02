using UnityEngine;
using UnityEngine.UI;

public class SpriteSheetAnimator : MonoBehaviour
{
    public Image targetImage;
    public Texture2D spriteSheet;

    public int columns = 4;
    public int rows = 4;
    public float fps = 12f;

    private Sprite[] frames;
    private int currentFrame;
    private float timer;

    void Start()
    {
        GenerateFrames();

        currentFrame = Random.Range(0, frames.Length);

        targetImage.sprite = frames[currentFrame];
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 1f / fps)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
            targetImage.sprite = frames[currentFrame];
        }
    }

    void GenerateFrames()
    {
        int totalFrames = rows * columns;
        frames = new Sprite[totalFrames];

        int frameWidth = spriteSheet.width / columns;
        int frameHeight = spriteSheet.height / rows;

        int index = 0;

        for (int y = rows - 1; y >= 0; y--)
        {
            for (int x = 0; x < columns; x++)
            {
                Rect rect = new Rect(
                    x * frameWidth,
                    y * frameHeight,
                    frameWidth,
                    frameHeight
                );

                frames[index] = Sprite.Create(
                    spriteSheet,
                    rect,
                    new Vector2(0.5f, 0.5f)
                );

                index++;
            }
        }
    }
}