using UnityEngine;
using System.IO;

public class FretboardManager : MonoBehaviour
{
    [SerializeField] private Renderer guitarRenderer;
    private NoteSpawner noteSpawner;

    private Material fretboardMaterial;
    private float scrollOffset = 0f;
    private float noteSpeed; // Кэшируем
    
    // Оптимизация: кэшируем вычисления для FretboardManager.Update
    private float cachedScrollSpeed;
    private bool isCacheValid = false;

    private void Start()
    {
        noteSpawner = FindObjectOfType<NoteSpawner>();
        if (noteSpawner == null)
        {
            Debug.LogError("NoteSpawner not found in scene!");
            return;
        }

        noteSpeed = PlayerPrefs.GetFloat("NoteSpeed", 5f);
        
        // Предвычисляем scrollSpeed для оптимизации
        RecalculateScrollSpeed();

        ApplyFretboardTexture();
    }
    
    private void RecalculateScrollSpeed()
    {
        if (noteSpawner != null)
        {
            float guitarLength = noteSpawner.GuitarHalfSize * 2;
            cachedScrollSpeed = noteSpeed / guitarLength;
            isCacheValid = true;
        }
    }

private void Update()
{
    // Оптимизация: используем кэшированные значения и минимизируем вычисления
    if (fretboardMaterial != null && noteSpawner.MusicPlaying && isCacheValid)
    {
        scrollOffset -= cachedScrollSpeed * Time.deltaTime;
        scrollOffset %= 1f;
        if (scrollOffset < 0) scrollOffset += 1f;
        fretboardMaterial.SetFloat("_ScrollOffset", scrollOffset);
    }
}

    private void ApplyFretboardTexture()
    {
        string selectedFretboard = PlayerPrefs.GetString("SelectedFretboard", "default");
        string fretboardsPath;

        if (Application.isEditor)
        {
            fretboardsPath = Path.Combine(Application.dataPath, "fretboards");
        }
        else
        {
            fretboardsPath = Path.Combine(Directory.GetCurrentDirectory(), "fretboards");
        }

        string texturePath = Path.Combine(fretboardsPath, $"{selectedFretboard}.png");

        if (!File.Exists(texturePath))
        {
            Debug.LogError($"Текстура {texturePath} не найдена!");
            return;
        }

        byte[] bytes = File.ReadAllBytes(texturePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);

        Texture2D flippedTexture = FlipTexture(texture);
        Destroy(texture);

        if (guitarRenderer != null)
        {
            fretboardMaterial = new Material(Shader.Find("Custom/GuitarFade"));
            fretboardMaterial.SetTexture("_MainTex", flippedTexture);
            guitarRenderer.material = fretboardMaterial;
        }
        else
        {
            Debug.LogError("Renderer гитары не настроен!");
        }
    }

    private Texture2D FlipTexture(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D flipped = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flipped.SetPixel(width - 1 - x, height - 1 - y, original.GetPixel(x, y));
            }
        }
        flipped.Apply();
        return flipped;
    }
}