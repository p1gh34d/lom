using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using System;

public class SongList : MonoBehaviour
{
    [SerializeField] private Transform contentPanel;
    [SerializeField] private GameObject songButtonTemplate;
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private Transform difficultyContentPanel;
    [SerializeField] private GameObject difficultyButtonTemplate;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Sprite defaultCover;
    [SerializeField] private GameObject songInfoPanel;
    [SerializeField] private GameObject songInfoCoverMask;
    [SerializeField] private Image songInfoCover;
    [SerializeField] private Text songInfoGroup;
    [SerializeField] private Text songInfoSong;
    [SerializeField] private Text songInfoCharter;
    [SerializeField] private AudioSource previewAudioSource;

    [SerializeField] private GameObject expertProgressText;
    [SerializeField] private GameObject hardProgressText;
    [SerializeField] private GameObject mediumProgressText;
    [SerializeField] private GameObject easyProgressText;
    private string selectedSongName;
    private string lastSelectedDifficulty;
    private readonly string[] supportedExtensions = { ".png", ".jpg", ".jpeg" };
    private List<GameObject> songButtonObjects = new List<GameObject>();
    [SerializeField] private List<string> songFolderNames = new List<string>();
    public List<string> SongFolderNames => songFolderNames;
    private int activeSongIndex = -1;
    private float buttonHeight;
    private List<GameObject> difficultyButtonObjects = new List<GameObject>();
    private List<string> difficultyNames = new List<string>();
    private int activeDifficultyIndex = -1;
    private float difficultyButtonHeight;
    private const float shiftAmount = 30f;
    private const float shiftAmountDifficulty = 50f;
    private float scrollCooldown = 0.15f;
    private float lastScrollTime;
    private float lastKeyDownTime;
    private float lastSongSwitchTime;
    private bool isScrolling;
    private Coroutine previewDelayCoroutine;
    private Coroutine loadPreviewCoroutine;
    private Coroutine previewLoopCoroutine;
    private const float keyDownCooldown = 0.5f;
    private const float previewDelay = 0.7f;
    private const float previewDuration = 20f;
    private const float previewPause = 1f;

    void Start()
    {
        LoadSongs();
        difficultyPanel.SetActive(false);
        if (songInfoPanel != null) songInfoPanel.SetActive(false);
        UpdateShaderDimensions();

        int savedIndex = PlayerPrefs.GetInt("ActiveSongIndex", 0);
        lastSelectedDifficulty = PlayerPrefs.GetString("LastSelectedDifficulty", "ExpertSingle");
        if (songButtonObjects.Count > 0)
        {
            StartCoroutine(InitializeActiveSong(Mathf.Clamp(savedIndex, 0, songButtonObjects.Count - 1)));
        }
    }

    private IEnumerator InitializeActiveSong(int index)
    {
        yield return null;
        SetActiveSong(index);
    }

    void Update()
    {
        if (scrollRect != null)
        {
            if (!difficultyPanel.activeSelf)
            {
                if (InputManager.Instance.IsKeyDown("StrumUp") && activeSongIndex > 0)
                {
                    lastKeyDownTime = Time.time;
                    SetActiveSong(activeSongIndex - 1);
                }
                else if (InputManager.Instance.IsKeyDown("StrumDown") && activeSongIndex < songButtonObjects.Count - 1)
                {
                    lastKeyDownTime = Time.time;
                    SetActiveSong(activeSongIndex + 1);
                }
                else if (InputManager.Instance.IsKey("StrumUp") && activeSongIndex > 0 && Time.time - lastKeyDownTime > keyDownCooldown)
                {
                    HandleFastScroll(-1);
                }
                else if (InputManager.Instance.IsKey("StrumDown") && activeSongIndex < songButtonObjects.Count - 1 && Time.time - lastKeyDownTime > keyDownCooldown)
                {
                    HandleFastScroll(1);
                }

                if (InputManager.Instance.IsKeyDown("Green") && activeSongIndex >= 0)
                {
                    ShowDifficultyOptions(songFolderNames[activeSongIndex]);
                }

                if (InputManager.Instance.IsKeyDown("Red"))
                {
                    SceneManager.LoadScene("MainMenu");
                }

                if (isScrolling && !InputManager.Instance.IsKey("StrumUp") && !InputManager.Instance.IsKey("StrumDown"))
                {
                    isScrolling = false;
                    if (previewDelayCoroutine != null) StopCoroutine(previewDelayCoroutine);
                    previewDelayCoroutine = StartCoroutine(LoadPreviewAfterDelay(previewDelay));
                }
            }
            else
            {
                if (InputManager.Instance.IsKeyDown("StrumUp") && activeDifficultyIndex > 0)
                {
                    lastKeyDownTime = Time.time;
                    SetActiveDifficulty(activeDifficultyIndex - 1);
                }
                else if (InputManager.Instance.IsKeyDown("StrumDown") && activeDifficultyIndex < difficultyButtonObjects.Count - 1)
                {
                    lastKeyDownTime = Time.time;
                    SetActiveDifficulty(activeDifficultyIndex + 1);
                }
                else if (InputManager.Instance.IsKey("StrumUp") && activeDifficultyIndex > 0 && Time.time - lastKeyDownTime > keyDownCooldown)
                {
                    HandleFastDifficultyScroll(-1);
                }
                else if (InputManager.Instance.IsKey("StrumDown") && activeDifficultyIndex < difficultyButtonObjects.Count - 1 && Time.time - lastKeyDownTime > keyDownCooldown)
                {
                    HandleFastDifficultyScroll(1);
                }

                if (InputManager.Instance.IsKeyDown("Green") && activeDifficultyIndex >= 0)
                {
                    SelectSongAndDifficulty(difficultyNames[activeDifficultyIndex]);
                }

                if (InputManager.Instance.IsKeyDown("Red"))
                {
                    difficultyPanel.SetActive(false);
                    activeDifficultyIndex = -1;
                }
            }
        }
    }

    private void HandleFastScroll(int direction)
    {
        if (Time.time - lastScrollTime < scrollCooldown) return;

        isScrolling = true;
        lastScrollTime = Time.time;
        SetActiveSong(activeSongIndex + direction, skipPreview: true);
    }

    private void HandleFastDifficultyScroll(int direction)
    {
        if (Time.time - lastScrollTime < scrollCooldown) return;

        lastScrollTime = Time.time;
        SetActiveDifficulty(activeDifficultyIndex + direction);
    }

    private IEnumerator LoadPreviewAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (Time.time - lastSongSwitchTime >= delay)
        {
            PlaySongPreview(songFolderNames[activeSongIndex]);
        }
    }

    void LoadSongs()
    {
        string songsPath = Application.isEditor
            ? Path.Combine(Application.dataPath, "songs")
            : Path.Combine(Directory.GetCurrentDirectory(), "songs");

        if (!Directory.Exists(songsPath))
        {
            Debug.LogError("Папка songs не найдена!");
            return;
        }

        List<(string folder, string groupName, string songName, string songFolderName)> songList = new List<(string, string, string, string)>();

        foreach (string folder in Directory.GetDirectories(songsPath))
        {
            string songFolderName = Path.GetFileName(folder);
            string[] parts = songFolderName.Split(new[] { " - " }, System.StringSplitOptions.None);
            string groupName;
            string songName;

            if (parts.Length > 1)
            {
                groupName = parts[0];
                songName = parts[1];
            }
            else
            {
                groupName = "Unknown";
                songName = songFolderName;
            }

            songList.Add((folder, groupName, songName, songFolderName));
        }

        songList.Sort((a, b) => string.Compare(a.songName, b.songName, StringComparison.OrdinalIgnoreCase));

        foreach (var song in songList)
        {
            string folder = song.folder;
            string groupName = song.groupName;
            string songName = song.songName;
            string songFolderName = song.songFolderName;

            GameObject newButton = Instantiate(songButtonTemplate, contentPanel);
            newButton.SetActive(true);

            Transform contentTransform = newButton.transform.Find("Content");
            if (contentTransform == null)
            {
                Debug.LogError("Дочерний объект 'Content' не найден в префабе кнопки!");
                return;
            }

            Image coverImage = contentTransform.Find("CoverImage")?.GetComponent<Image>();
            Text starsText = contentTransform.Find("StarsText")?.GetComponent<Text>();
            Text groupText = contentTransform.Find("GroupText")?.GetComponent<Text>();
            Text songText = contentTransform.Find("SongText")?.GetComponent<Text>();
            Text scoreText = contentTransform.Find("ScoreText")?.GetComponent<Text>();

            if (coverImage != null)
            {
                string coverPath = GetCoverPath(folder);
                if (!string.IsNullOrEmpty(coverPath))
                {
                    byte[] bytes = File.ReadAllBytes(coverPath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(bytes);
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    coverImage.sprite = sprite;
                }
                else if (defaultCover != null)
                {
                    coverImage.sprite = defaultCover;
                }
            }

            if (groupText != null) groupText.text = groupName;
            if (songText != null) songText.text = songName;

            if (starsText != null && scoreText != null)
            {
                string[] difficulties = { "ExpertSingle", "HardSingle", "MediumSingle", "EasySingle" };
                int bestScore = 0;
                string bestStars = "-";
                float progress = 0f;

                if (UserManager.Instance != null)
                {
                    foreach (string difficulty in difficulties)
                    {
                        string keyPrefix = $"{songFolderName}_{difficulty}";
                        int currentScore = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_Score", 0);
                        if (currentScore > bestScore)
                        {
                            bestScore = currentScore;
                            bestStars = UserManager.Instance.GetUserProgressString($"{keyPrefix}_Stars", "-");
                            progress = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Progress", 0f);
                        }
                    }
                }

                starsText.text = bestScore > 0 ? (progress >= 100f ? $"<color=#ffa200>★ {bestStars}</color>" : $"★ {bestStars}") : "-";
                scoreText.text = bestScore > 0 ? $"{bestScore.ToString("N0").Replace(",", " ")}" : "0";
            }

            songButtonObjects.Add(newButton);
            songFolderNames.Add(songFolderName);

            if (buttonHeight == 0 && songButtonObjects.Count == 1)
            {
                buttonHeight = newButton.GetComponent<RectTransform>().rect.height;
            }
        }
    }

    private void SetActiveSong(int index, bool skipPreview = false)
    {
        if (index < 0 || index >= songButtonObjects.Count) return;

        if (activeSongIndex >= 0 && activeSongIndex < songButtonObjects.Count)
        {
            Transform oldContent = songButtonObjects[activeSongIndex].transform.Find("Content");
            if (oldContent != null)
            {
                Vector3 oldPos = oldContent.localPosition;
                oldContent.localPosition = new Vector3(0, oldPos.y, oldPos.z);
            }
        }

        activeSongIndex = index;
        GameObject activeButton = songButtonObjects[activeSongIndex];
        Transform activeContent = activeButton.transform.Find("Content");
        if (activeContent != null)
        {
            Vector3 currentPos = activeContent.localPosition;
            activeContent.localPosition = new Vector3(shiftAmount, currentPos.y, currentPos.z);
        }

        float totalHeight = buttonHeight * songButtonObjects.Count;
        float viewportHeight = scrollRect.GetComponent<RectTransform>().rect.height;
        float contentTop = totalHeight - viewportHeight;
        float halfViewport = viewportHeight / 2f;
        float targetCenter = activeSongIndex * buttonHeight + buttonHeight / 2f;
        float targetTop = targetCenter - halfViewport;
        targetTop = Mathf.Clamp(targetTop, 0, contentTop);
        float targetPosition = 1f - (targetTop / contentTop);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetPosition);

        PlayerPrefs.SetInt("ActiveSongIndex", activeSongIndex);
        PlayerPrefs.Save();

        if (previewAudioSource != null) previewAudioSource.Stop();
        if (loadPreviewCoroutine != null) StopCoroutine(loadPreviewCoroutine);
        if (previewDelayCoroutine != null) StopCoroutine(previewDelayCoroutine);
        if (previewLoopCoroutine != null) StopCoroutine(previewLoopCoroutine);

        UpdateSongInfo(songFolderNames[index]);

        if (!skipPreview)
        {
            lastSongSwitchTime = Time.time;
            previewDelayCoroutine = StartCoroutine(LoadPreviewAfterDelay(previewDelay));
        }
    }

    private void SetActiveDifficulty(int index)
    {
        if (index < 0 || index >= difficultyButtonObjects.Count) return;

        if (activeDifficultyIndex >= 0 && activeDifficultyIndex < difficultyButtonObjects.Count)
        {
            Transform oldContent = difficultyButtonObjects[activeDifficultyIndex].transform.Find("Content");
            Transform oldImage = difficultyButtonObjects[activeDifficultyIndex].transform.Find("Image");
            if (oldContent != null)
            {
                Vector3 oldPos = oldContent.localPosition;
                oldContent.localPosition = new Vector3(0, oldPos.y, oldPos.z);
            }
            if (oldImage != null)
            {
                oldImage.gameObject.SetActive(false);
            }
        }

        activeDifficultyIndex = index;
        GameObject activeButton = difficultyButtonObjects[activeDifficultyIndex];
        Transform activeContent = activeButton.transform.Find("Content");
        Transform activeImage = activeButton.transform.Find("Image");
        if (activeContent != null)
        {
            Vector3 currentPos = activeContent.localPosition;
            activeContent.localPosition = new Vector3(shiftAmountDifficulty, currentPos.y, currentPos.z);
        }
        if (activeImage != null)
        {
            activeImage.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Дочерний объект 'Image' не найден в активной кнопке сложности!");
        }
    }

    private string GetCoverPath(string folder)
    {
        foreach (string ext in supportedExtensions)
        {
            string path = Path.Combine(folder, $"cover{ext}");
            if (File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

void ShowDifficultyOptions(string songName)
{
    selectedSongName = songName;
    string songsPath = Application.isEditor
        ? Path.Combine(Application.dataPath, "songs")
        : Path.Combine(Directory.GetCurrentDirectory(), "songs");
    string chartPath = Path.Combine(songsPath, songName, "notes.chart");

    if (!File.Exists(chartPath))
    {
        Debug.LogError($"Файл notes.chart не найден в {chartPath}!");
        return;
    }

    List<string> availableDifficulties = new List<string>();
    string[] lines = File.ReadAllLines(chartPath);
    string currentSection = null;
    bool hasNotes = false;

    foreach (string line in lines)
    {
        string trimmedLine = line.Trim();

        // Проверяем начало новой секции
        if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
        {
            // Если предыдущая секция имела ноты и является поддерживаемой сложностью, добавляем её
            if (currentSection != null && hasNotes)
            {
                if (currentSection == "ExpertSingle" || currentSection == "HardSingle" || 
                    currentSection == "MediumSingle" || currentSection == "EasySingle")
                {
                    availableDifficulties.Add(currentSection);
                }
            }
            // Начинаем новую секцию
            currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
            hasNotes = false; // Сбрасываем флаг нот
        }
        // Проверяем ноты в текущей секции
        else if (currentSection != null && trimmedLine.Contains("= N"))
        {
            hasNotes = true; // Нашли ноту
        }
    }

    // Проверяем последнюю секцию
    if (currentSection != null && hasNotes)
    {
        if (currentSection == "ExpertSingle" || currentSection == "HardSingle" || 
            currentSection == "MediumSingle" || currentSection == "EasySingle")
        {
            availableDifficulties.Add(currentSection);
        }
    }

    if (availableDifficulties.Count == 0)
    {
        Debug.LogWarning($"В файле {chartPath} нет сложностей с нотами!");
        return;
    }

    // Очищаем существующие кнопки
    foreach (Transform child in difficultyContentPanel)
    {
        Destroy(child.gameObject);
    }
    difficultyButtonObjects.Clear();
    difficultyNames.Clear();

    string[] difficulties = { "ExpertSingle", "HardSingle", "MediumSingle", "EasySingle" };
    foreach (string difficulty in difficulties)
    {
        if (availableDifficulties.Contains(difficulty))
        {
            GameObject newButton = Instantiate(difficultyButtonTemplate, difficultyContentPanel);
            newButton.SetActive(true);
            Transform contentTransform = newButton.transform.Find("Content");
            if (contentTransform == null)
            {
                Debug.LogError("Дочерний объект 'Content' не найден в префабе кнопки сложности!");
                continue;
            }

            Text difficultyText = contentTransform.GetComponentInChildren<Text>();
            if (difficultyText != null) difficultyText.text = difficulty.Replace("Single", "").ToUpper();

            Transform imageTransform = newButton.transform.Find("Image");
            if (imageTransform != null)
            {
                imageTransform.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("Дочерний объект 'Image' не найден в кнопке сложности!");
            }

            difficultyButtonObjects.Add(newButton);
            difficultyNames.Add(difficulty);

            if (difficultyButtonHeight == 0 && difficultyButtonObjects.Count == 1)
            {
                difficultyButtonHeight = newButton.GetComponent<RectTransform>().rect.height;
            }
        }
    }

    difficultyPanel.SetActive(true);
    CanvasGroup canvasGroup = difficultyPanel.GetComponent<CanvasGroup>();
    if (canvasGroup != null)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    RectTransform rectTransform = difficultyPanel.GetComponent<RectTransform>();
    if (rectTransform != null)
    {
        rectTransform.anchoredPosition = Vector2.zero;
    }

    int defaultIndex = 0;
    for (int i = 0; i < difficultyNames.Count; i++)
    {
        if (difficultyNames[i] == lastSelectedDifficulty)
        {
            defaultIndex = i;
            break;
        }
    }
    SetActiveDifficulty(defaultIndex);
}

    void SelectSongAndDifficulty(string difficulty)
    {
        PlayerPrefs.SetInt("ActiveSongIndex", activeSongIndex);
        PlayerPrefs.SetString("SelectedSong", selectedSongName);
        PlayerPrefs.SetString("SelectedDifficulty", difficulty);
        PlayerPrefs.SetString("LastSelectedDifficulty", difficulty);

        PlayerPrefs.SetString("SongTitle", "Unknown Song");
        PlayerPrefs.SetString("BandName", "Unknown Artist");

        string songsPath = Application.isEditor
            ? Path.Combine(Application.dataPath, "songs")
            : Path.Combine(Directory.GetCurrentDirectory(), "songs");
        string chartPath = Path.Combine(songsPath, selectedSongName, "notes.chart");

        if (File.Exists(chartPath))
        {
            string[] lines = File.ReadAllLines(chartPath);
            bool inSongSection = false;
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine == "[Song]") inSongSection = true;
                else if (trimmedLine.StartsWith("[")) inSongSection = false;
                else if (inSongSection && trimmedLine.Contains("="))
                {
                    string[] parts = trimmedLine.Split(new[] { " = " }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim().Trim('"');
                        if (key == "Name") PlayerPrefs.SetString("SongTitle", value);
                        else if (key == "Artist") PlayerPrefs.SetString("BandName", value);
                        else if (key == "Charter") PlayerPrefs.SetString("SongCharter", value);
                    }
                }
            }
        }

        string songFolder = Path.Combine(songsPath, selectedSongName);
        string coverPath = GetCoverPath(songFolder);
        //Debug.Log("Saving CoverPath: " + coverPath);
        if (!string.IsNullOrEmpty(coverPath))
        {
            PlayerPrefs.SetString("CoverPath", coverPath);
        }
        else
        {
            PlayerPrefs.SetString("CoverPath", "default");
            Debug.Log("No cover found, using default");
        }

        //Debug.Log($"SelectSongAndDifficulty: SelectedSong={selectedSongName}, SongTitle={PlayerPrefs.GetString("SongTitle")}, BandName={PlayerPrefs.GetString("BandName")}, Difficulty={difficulty}, CoverPath={coverPath}");

        PlayerPrefs.Save();
        SceneManager.LoadScene("GameScene");
        difficultyPanel.SetActive(false);
    }

    private void UpdateShaderDimensions()
    {
        if (songInfoCover != null && songInfoCoverMask != null)
        {
            Material mat = songInfoCover.material;
            if (mat != null)
            {
                float maskHeight = songInfoCoverMask.GetComponent<RectTransform>().rect.height;
                float coverHeight = songInfoCover.GetComponent<RectTransform>().rect.height;
                mat.SetFloat("_MaskHeight", maskHeight);
                mat.SetFloat("_CoverHeight", coverHeight);
            }
        }
    }

    private void UpdateSongInfo(string songName)
    {
        if (songInfoPanel != null)
        {
            songInfoPanel.SetActive(true);

            string[] parts = songName.Split(new[] { " - " }, System.StringSplitOptions.None);
            string groupName = parts.Length > 0 ? parts[0] : songName;
            string songTitle = parts.Length > 1 ? parts[1] : "";

            if (songInfoGroup != null) songInfoGroup.text = groupName;
            if (songInfoSong != null) songInfoSong.text = songTitle;

            string songsPath = Application.isEditor
                ? Path.Combine(Application.dataPath, "songs")
                : Path.Combine(Directory.GetCurrentDirectory(), "songs");
            string songFolder = Path.Combine(songsPath, songName);

            if (songInfoCover != null)
            {
                string coverPath = GetCoverPath(songFolder);
                if (!string.IsNullOrEmpty(coverPath))
                {
                    byte[] bytes = File.ReadAllBytes(coverPath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(bytes);
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    songInfoCover.sprite = sprite;
                    RectTransform rectTransform = songInfoCover.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        float targetWidth = 935f;
                        float targetHeight = targetWidth;
                        rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                }
                else if (defaultCover != null)
                {
                    songInfoCover.sprite = defaultCover;
                    RectTransform rectTransform = songInfoCover.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        float targetWidth = 935f;
                        float targetHeight = targetWidth;
                        rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                }
                UpdateShaderDimensions();
            }

            string chartPath = Path.Combine(songFolder, "notes.chart");
            string charter = "Unknown Charter";
            if (File.Exists(chartPath))
            {
                string[] lines = File.ReadAllLines(chartPath);
                bool inSongSection = false;
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine == "[Song]") inSongSection = true;
                    else if (trimmedLine.StartsWith("[")) inSongSection = false;
                    else if (inSongSection && trimmedLine.Contains("="))
                    {
                        string[] charterParts = trimmedLine.Split(new[] { " = " }, StringSplitOptions.None);
                        if (charterParts.Length == 2 && charterParts[0].Trim() == "Charter")
                        {
                            charter = charterParts[1].Trim().Trim('"');
                            break;
                        }
                    }
                }
            }
            songInfoCharter.text = string.IsNullOrEmpty(charter) || charter == "Unknown Charter" ? "Unknown Charter" : $"By {charter}";

            UpdateProgressText(songName);
        }
    }

    private void UpdateProgressText(string songName)
    {
        string songsPath = Application.isEditor
            ? Path.Combine(Application.dataPath, "songs")
            : Path.Combine(Directory.GetCurrentDirectory(), "songs");
        string chartPath = Path.Combine(songsPath, songName, "notes.chart");

        if (!File.Exists(chartPath))
        {
            Debug.LogError($"notes.chart not found at {chartPath}");
            return;
        }

        List<string> availableDifficulties = new List<string>();
        string[] lines = File.ReadAllLines(chartPath);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                string section = trimmedLine.Substring(1, trimmedLine.Length - 2);
                if (section == "ExpertSingle" || section == "HardSingle" || section == "MediumSingle" || section == "EasySingle")
                {
                    availableDifficulties.Add(section);
                }
            }
        }

        string[] difficulties = { "ExpertSingle", "HardSingle", "MediumSingle", "EasySingle" };
        GameObject[] progressContainers = { expertProgressText, hardProgressText, mediumProgressText, easyProgressText };
        float startY = -465f;
        float spacing = 80f;
        float currentY = startY;

        bool useAccuracySystem = PlayerPrefs.GetInt($"AccuracySystem_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;

        for (int i = 0; i < difficulties.Length; i++)
        {
            string difficulty = difficulties[i];
            GameObject container = progressContainers[i];

            if (container == null) continue;

            bool difficultyExists = availableDifficulties.Contains(difficulty);
            container.SetActive(difficultyExists);

            if (difficultyExists)
            {
                Text scoreProgress = container.transform.Find("ScoreProgress")?.GetComponent<Text>();
                Text scorePercentage = container.transform.Find("ScorePercentage")?.GetComponent<Text>();
                Text scoreAccuracy = container.transform.Find("ScoreAccuracy")?.GetComponent<Text>();

                if (scoreProgress == null || scorePercentage == null || scoreAccuracy == null)
                {
                    Debug.LogError($"Missing child Text components in {difficulty}Progress Ridiculous™️ ProgressText!");
                    continue;
                }

                string keyPrefix = $"{songName}_{difficulty}";
                int score = 0;
                string stars = "0";
                float progress = 0f;
                float accuracy = -1f;

                if (UserManager.Instance != null)
                {
                    score = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_Score", 0);
                    stars = UserManager.Instance.GetUserProgressString($"{keyPrefix}_Stars", "0");
                    progress = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Progress", 0f);
                    accuracy = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Accuracy", -1f);
                }

                if (score > 0)
                {
                    scoreProgress.text = $"{score}";
                    scorePercentage.text = $"{progress:F0}%";
                    //Debug.Log($"SongList: keyPrefix={keyPrefix}, score={score}, accuracy={accuracy}, useAccuracySystem={useAccuracySystem}");
                    scoreAccuracy.text = (useAccuracySystem && accuracy >= 0f) ? $"{accuracy:F0}%" : "N/A";
                }
                else
                {
                    scoreProgress.text = "0";
                    scorePercentage.text = "0%";
                    scoreAccuracy.text = "N/A";
                }

                RectTransform rect = container.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, currentY);
                currentY -= spacing;
            }
        }
    }

private void PlaySongPreview(string songName)
{
    if (previewAudioSource == null)
    {
        Debug.LogError("Preview AudioSource is not assigned!");
        return;
    }

    string songsPath = Application.isEditor
        ? Path.Combine(Application.dataPath, "songs")
        : Path.Combine(Directory.GetCurrentDirectory(), "songs");
    string songFolder = Path.Combine(songsPath, songName);
    string previewMp3Path = Path.Combine(songFolder, "song.mp3");
    string previewOpusPath = Path.Combine(songFolder, "song.opus");
    string previewOggPath = Path.Combine(songFolder, "song.ogg");
    string chartPath = Path.Combine(songFolder, "notes.chart");

    float previewStartTime = 0f;
    float previewEndTime = 15f;

    if (File.Exists(chartPath))
    {
        string[] lines = File.ReadAllLines(chartPath);
        bool inSongSection = false;
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (trimmedLine == "[Song]") inSongSection = true;
            else if (trimmedLine.StartsWith("[")) inSongSection = false;
            else if (inSongSection && trimmedLine.Contains("="))
            {
                string[] parts = trimmedLine.Split(new[] { " = " }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    if (key == "PreviewStart" && float.TryParse(value, out float parsedStart))
                    {
                        previewStartTime = parsedStart;
                    }
                    else if (key == "PreviewEnd" && float.TryParse(value, out float parsedEnd))
                    {
                        previewEndTime = parsedEnd;
                    }
                }
            }
        }
    }
    else
    {
        Debug.LogWarning($"Chart file not found at {chartPath}, using default preview times");
    }

    // Проверяем валидность значений
    if (previewStartTime < 0f || previewEndTime <= previewStartTime)
    {
        previewStartTime = 3f;
        previewEndTime = 15f;
    }

    string selectedPath = File.Exists(previewOpusPath)
        ? previewOpusPath
        : (File.Exists(previewMp3Path) ? previewMp3Path : previewOggPath);
    StartPreviewPlayback(selectedPath, previewStartTime, previewEndTime);
}

private void StartPreviewPlayback(string previewPath, float startTime, float endTime)
{
    if (File.Exists(previewPath))
    {
        if (loadPreviewCoroutine != null) StopCoroutine(loadPreviewCoroutine);
        loadPreviewCoroutine = StartCoroutine(LoadAndPlayPreview(previewPath, startTime, endTime));
    }
    else
    {
        previewAudioSource.Stop();
        Debug.LogWarning($"Song file not found at {previewPath}");
    }
}

private IEnumerator LoadAndPlayPreview(string path, float startTime, float endTime)
{
    float loadStartTime = Time.time;

    // Если .opus — используем AudioLoader, если .mp3 — UnityWebRequest
    if (path.EndsWith(".opus", StringComparison.OrdinalIgnoreCase))
    {
        AudioClip clip = null;
        bool done = false;
        string folder = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);

        yield return AudioLoader.GetOrCreate().StartCoroutine(AudioLoader.Instance.LoadSongAsync(folder, name, c => { clip = c; done = true; }));
        if (!done || clip == null)
        {
            Debug.LogError($"Failed to load preview opus: {path}");
            yield break;
        }

        if (previewAudioSource != null)
        {
            previewAudioSource.clip = clip;
            StartPreviewLoop(startTime, endTime);
            //Debug.Log($"Loaded opus preview for {path} from {startTime}s to {endTime}s");
        }
    }
    else
    {
        AudioType audioType = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
            ? AudioType.OGGVORBIS
            : AudioType.MPEG;

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, audioType))
        {
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true; // Включаем потоковую загрузку
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (previewAudioSource != null)
                {
                    previewAudioSource.clip = clip;
                    StartPreviewLoop(startTime, endTime);
                    //Debug.Log($"Streaming preview for {path} from {startTime}s to {endTime}s");
                }
            }
            else
            {
                Debug.LogError($"Failed to load preview: {www.error}");
            }
        }
    }
    //Debug.Log($"LoadAndPlayPreview took {Time.time - loadStartTime} seconds");
}

    private void StartPreviewLoop(float startTime, float endTime)
    {
        if (previewAudioSource.clip == null) return;

        float clipLength = previewAudioSource.clip.length;
        startTime = Mathf.Clamp(startTime, 0f, clipLength);
        endTime = Mathf.Clamp(endTime, startTime, clipLength);

        previewAudioSource.time = startTime;
        previewAudioSource.Play();

        if (previewLoopCoroutine != null) StopCoroutine(previewLoopCoroutine);
        previewLoopCoroutine = StartCoroutine(PreviewLoop(startTime, endTime));
    }

    private IEnumerator PreviewLoop(float startTime, float endTime)
    {
        float previewDuration = endTime - startTime;

        while (true)
        {
            yield return new WaitForSeconds(previewDuration);

            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
                yield return new WaitForSeconds(previewPause);

                if (previewAudioSource != null && previewAudioSource.clip != null)
                {
                    previewAudioSource.time = startTime;
                    previewAudioSource.Play();
                }
            }
            else
            {
                yield break;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetScrollPosition()
    {
        PlayerPrefs.SetInt("ActiveSongIndex", 0);
    }

    void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey("LastSelectedDifficulty");
        PlayerPrefs.Save();
    }
}