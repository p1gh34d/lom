using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

[System.Serializable]
public class NoteData
{
    public float time; // В секундах
    public int tick; // Исходный тик из чарта
    public int midi;
    public float duration;
    public bool forced;
    public bool isOpen;
    public bool isChord; // Принадлежит ли нота аккорду
    public bool isStarPower; // Является ли нота Star Power
}

[System.Serializable]
public class TimeSignature
{
    public int ticks;
    public List<int> timeSignature;
}

[System.Serializable]
public class TempoData
{
    public float bpm;
    public int ticks;
}

[System.Serializable]
public class Header
{
    public int ppq;
    public List<TempoData> tempos;
    public List<TimeSignature> timeSignatures;
}

[System.Serializable]
public class Track
{
    public string name;
    public List<NoteData> notes;
    public float endOfTrackTicks;
}

[System.Serializable]
public class SongData
{
    public Header header;
    public List<Track> tracks;
}

public class NoteSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] notePrefabs;
    [SerializeField] private GameObject[] forcedNotePrefabs;
    [SerializeField] private GameObject[] noteStickPrefabs;
    [SerializeField] private GameObject[] openNotePrefabs; // Префабы для коротких открытых нот
    [SerializeField] private GameObject[] openNoteStickPrefabs; // Префабы для длинных открытых нот

[SerializeField] private GameObject[] starPowerNotePrefabs; // Префабы для обычных Star Power нот
[SerializeField] private GameObject[] starPowerForcedNotePrefabs; // Префабы для форсированных Star Power нот
[SerializeField] private GameObject[] starPowerOpenNotePrefabs; // Префабы для коротких открытых Star Power нот

    [SerializeField] private GameObject noteStickBasePrefab; // Префаб подложки для длинных нот
    [SerializeField] private GameObject linePrefab;
    [SerializeField] public  Transform noteParent;
    [SerializeField] private Transform lineParent;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip animationSoundClip; // Клип для анимации (например, guitar_rise.wav)
    [SerializeField] private GameObject guitarObject; // Ссылка на модель гитары (если есть)
    [SerializeField] private GameObject hitZoneObject; // Хитзона
    [SerializeField] private GameObject stringPrefab; // Модель струны
    [SerializeField] private Transform stringParent; // Родитель для струн (опционально)
    [SerializeField] private GameObject leftSideModel; // Модель слева
    [SerializeField] private GameObject rightSideModel; // Модель справа
    [SerializeField] private GameObject starPowerBar; // Модель справа
    
    [SerializeField] private Image progressFill; // Поле для заполнения полоски
    [SerializeField] private GameObject pausePanel; // Панель паузы

    public AudioSource AudioSource => audioSource;
    private AudioSource animationAudioSource; // Программный AudioSource для анимации
    private List<GameObject> spawnedStrings = new List<GameObject>(); // Для хранения струн
    private List<GameObject> spawnedObjects = new List<GameObject>();
    public List<GameObject> SpawnedObjects => spawnedObjects;
    private SongData currentSongData;
    public SongData CurrentSongData => currentSongData;
    private float startTime;
    public float StartTime => startTime;
    private float speed;
    public float Speed => speed;
    private float baseSpawnLeadTime = 2f; // Базовое время спавна
    public float BaseSpawnLeadTime => baseSpawnLeadTime;
    private float guitarWidth = 5f; // Ширина гитары (4 или 5)
    private float guitarHalfSize = 7f; // Половина размера гитары (твоё значение)
    public float GuitarHalfSize => guitarHalfSize; // Публичный доступ к guitarHalfSize
    private bool musicPlaying = false; // Флаг начала музыки
    public bool MusicPlaying => musicPlaying; // Публичный доступ
    private Coroutine cameraAnimationCoroutine; // Для отслеживания корутины анимации

    private bool leftyFlip; // Переворот нот из PlayerPrefs
    private float currentTime = 0f;

    private const float HeadTiltAngleX = 5f; // Наклон головы ноты по оси X

    private int activePauseButtonIndex = 0; // Индекс активной кнопки в меню паузы
    private List<GameObject> pauseButtons = new List<GameObject>(); // Список кнопок паузы
    private float shiftAmount = 50f; // Сдвиг контента активной кнопки

    private bool isPaused = false;
    private bool songFinished = false; // Новый флаг

    private float noteOffset; // Поле для оффсета (уже есть, но уточняю)
public Dictionary<GameObject, float> baseObjectTimes = new Dictionary<GameObject, float>();
public Dictionary<GameObject, Coroutine> baseObjectCoroutines = new Dictionary<GameObject, Coroutine>();
private void Start()
{
        // Очищаем старый AudioSource, если он существует
        AudioSource[] existingAudioSources = GetComponents<AudioSource>();
        foreach (var source in existingAudioSources)
        {
            if (source != audioSource) // Не трогаем audioSource для песни
            {
                Destroy(source);
                Debug.Log("Destroyed residual AudioSource.");
            }
        }

    pausePanel.SetActive(false); // Скрываем панель при старте
    SetGuitarSize(); // Устанавливаем размер гитары
    SpawnStrings(); // Спавним струны
    LoadSettings();
    LoadSongData();

        // Устанавливаем начальный угол камеры
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 currentRotation = mainCamera.transform.eulerAngles;
            mainCamera.transform.eulerAngles = new Vector3(-15f, currentRotation.y, currentRotation.z);
        }
        else
        {
            Debug.LogError("Main Camera not found! Please ensure a camera with MainCamera tag exists.");
        }

        // Создаём AudioSource для анимации
        animationAudioSource = gameObject.AddComponent<AudioSource>();
        animationAudioSource.playOnAwake = false;
        animationAudioSource.volume = 0.7f;

        // Проверяем Time.timeScale
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
            Debug.Log("Reset Time.timeScale to 1.");
        }

    StartCoroutine(LoadAndPlaySong());

    // Проверяем progressFill
    if (progressFill == null)
    {
        Debug.LogError("ProgressFill не назначен в NoteSpawner!");
        return;
    }
    else
    {
        progressFill.rectTransform.sizeDelta = new Vector2(0f, progressFill.rectTransform.sizeDelta.y);
    }

    // Находим PauseContent
    Transform pauseContent = pausePanel.transform.Find("PauseContent");
    if (pauseContent == null)
    {
        Debug.LogError("PauseContent не найден в PausePanel!");
        return;
    }

    // Инициализация кнопок паузы
    pauseButtons.Clear();
    pauseButtons.Add(pauseContent.Find("ContinueButton")?.gameObject);
    pauseButtons.Add(pauseContent.Find("RestartButton")?.gameObject);
    pauseButtons.Add(pauseContent.Find("NewSongButton")?.gameObject);
    pauseButtons.Add(pauseContent.Find("MainMenuButton")?.gameObject);

    // Проверяем, что все кнопки найдены
    for (int i = 0; i < pauseButtons.Count; i++)
    {
        if (pauseButtons[i] == null)
        {
            Debug.LogError($"Кнопка паузы с индексом {i} не найдена! Проверь имена: ContinueButton, RestartButton, NewSongButton.");
            return;
        }
        else
        {
            // Проверяем наличие Content и Image
            Transform content = pauseButtons[i].transform.Find("Content");
            Transform image = pauseButtons[i].transform.Find("Image");
            if (content == null) Debug.LogError($"Content не найден в кнопке {pauseButtons[i].name}!");
            if (image == null) Debug.LogError($"Image не найден в кнопке {pauseButtons[i].name}!");
            else image.gameObject.SetActive(false); // Изначально скрываем Image
        }
    }

    // Устанавливаем первую кнопку активной
    SetActivePauseButton(0);
}

    private void ApplyHeadTilt(Transform noteRoot)
    {
        if (noteRoot == null) return;

        // Создаём контейнер для головы
        GameObject headTiltObj = new GameObject("HeadTilt");
        Transform headTilt = headTiltObj.transform;
        headTilt.SetParent(noteRoot, false);
        headTilt.localPosition = Vector3.zero;
        headTilt.localRotation = Quaternion.identity;
        headTilt.localScale = Vector3.one;

        // Переносим всех детей, кроме палочки, в контейнер головы
        List<Transform> originalChildren = new List<Transform>();
        for (int i = 0; i < noteRoot.childCount; i++)
        {
            originalChildren.Add(noteRoot.GetChild(i));
        }
        foreach (Transform child in originalChildren)
        {
            if (child == null) continue;
            if (child.name.StartsWith("NoteStick")) continue; // оставляем палочку на корне
            child.SetParent(headTilt, true);
        }

        // Наклоняем только контейнер головы
        Vector3 euler = headTilt.localEulerAngles;
        euler.x += HeadTiltAngleX;
        headTilt.localEulerAngles = euler;
    }

    private void SetGuitarSize()
    {
        string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        bool useFourFrets = PlayerPrefs.GetInt("UseFourFrets", 0) == 1 && 
                           (selectedDifficulty == "EasySingle" || selectedDifficulty == "MediumSingle");
        guitarWidth = useFourFrets ? 4.5f : 5f;

        if (guitarObject != null)
        {
            Vector3 currentScale = guitarObject.transform.localScale;
            guitarObject.transform.localScale = new Vector3(guitarWidth, currentScale.y, currentScale.z);
        }

        if (hitZoneObject != null)
        {
            Vector3 currentScale = hitZoneObject.transform.localScale;
            if (guitarWidth == 4.5f)
            {
                hitZoneObject.transform.localScale = new Vector3(0.91f, currentScale.y, currentScale.z);
            }
            else
            {
                hitZoneObject.transform.localScale = new Vector3(1f, currentScale.y, currentScale.z);
            }
        }

        if (starPowerBar != null)
        {
            if (guitarWidth == 4.5f)
            {
                Vector3 currentScale = starPowerBar.transform.localScale;
                starPowerBar.transform.localScale = new Vector3(0.91f, currentScale.y, currentScale.z);
            }
            else
            {
                Vector3 currentScale = starPowerBar.transform.localScale;
                starPowerBar.transform.localScale = new Vector3(1f, currentScale.y, currentScale.z);
            }
        }

        if (leftSideModel != null && rightSideModel != null)
        {
            if (guitarWidth == 4.5f)
            {
                Vector3 leftPos = leftSideModel.transform.localPosition;
                Vector3 rightPos = rightSideModel.transform.localPosition;
                leftSideModel.transform.localPosition = new Vector3(-2.32f, leftPos.y, leftPos.z); // Половина от 4.5
                rightSideModel.transform.localPosition = new Vector3(2.32f, rightPos.y, rightPos.z);
            }
            else
            {
                Vector3 leftPos = leftSideModel.transform.localPosition;
                Vector3 rightPos = rightSideModel.transform.localPosition;
                leftSideModel.transform.localPosition = new Vector3(-2.57f, leftPos.y, leftPos.z); // Половина от 5
                rightSideModel.transform.localPosition = new Vector3(2.57f, rightPos.y, rightPos.z);
            }
        }
    }

    private void SpawnStrings()
    {
        foreach (var str in spawnedStrings)
        {
            if (str != null) Destroy(str);
        }
        spawnedStrings.Clear();

        string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        bool useFourFrets = PlayerPrefs.GetInt("UseFourFrets", 0) == 1 && 
                           (selectedDifficulty == "EasySingle" || selectedDifficulty == "MediumSingle");
        float totalStringWidth = useFourFrets ? 3.2f : 4f;
        int stringCount = useFourFrets ? 4 : 5;
        float stringSpacing = totalStringWidth / (stringCount - 0.9f);

        for (int i = 0; i < stringCount; i++)
        {
            float xPosition = (i * stringSpacing) - (totalStringWidth / 2.05f);
            Vector3 position = new Vector3(xPosition, 0.52f, 6.6f); 
            GameObject stringObj = Instantiate(stringPrefab, position, Quaternion.identity, stringParent != null ? stringParent : transform);
            spawnedStrings.Add(stringObj);
        }
    }

private void Update()
{
    // Оптимизация: кэшируем InputManager.Instance в одной переменной для многократных вызовов
    var inputManager = InputManager.Instance;
    
    if (inputManager.IsKeyDown("Start"))
    {
        TogglePause();
    }

    if (isPaused)
    {
        if (inputManager.IsKeyDown("StrumUp"))
        {
            if (activePauseButtonIndex > 0)
            {
                SetActivePauseButton(activePauseButtonIndex - 1);
            }
        }
        else if (inputManager.IsKeyDown("StrumDown"))
        {
            if (activePauseButtonIndex < pauseButtons.Count - 1)
            {
                SetActivePauseButton(activePauseButtonIndex + 1);
            }
        }

        if (inputManager.IsKeyDown("Red"))
        {
            TogglePause();
        }

        if (inputManager.IsKeyDown("Green"))
        {
            switch (activePauseButtonIndex)
            {
                case 0:
                    TogglePause();
                    break;
                case 1:
                    Time.timeScale = 1f;
                    audioSource.Stop();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    break;
                case 2:
                    Time.timeScale = 1f;
                    audioSource.Stop();
                    SceneManager.LoadScene("SongSelect");
                    break;
                case 3:
                    Time.timeScale = 1f;
                    audioSource.Stop();
                    SceneManager.LoadScene("MainMenu");
                    break;
            }
        }
    }

    if (musicPlaying && !isPaused && !songFinished)
    {
        float progress = audioSource.time / audioSource.clip.length;
        if (progress >= 1f)
        {
            songFinished = true;
            progressFill.rectTransform.sizeDelta = new Vector2(400f, progressFill.rectTransform.sizeDelta.y);
        }
        else if (Time.frameCount % 10 == 0) // Оптимизация: обновляем раз в ~0.16с вместо 0.1с
        {
            progressFill.rectTransform.sizeDelta = new Vector2(400f * progress, progressFill.rectTransform.sizeDelta.y);
        }
    }
}

private void TogglePause()
{
    isPaused = !isPaused;
    Time.timeScale = isPaused ? 0f : 1f; // Останавливаем или возобновляем время
    if (isPaused)
    {
        audioSource.Pause(); // Приостанавливаем музыку
    }
    else
    {
        audioSource.UnPause(); // Возобновляем музыку
    }
    pausePanel.SetActive(isPaused);

    // Устанавливаем первую кнопку активной при открытии меню
    if (isPaused)
    {
        SetActivePauseButton(0);
    }
}

private void SetActivePauseButton(int index)
{
    if (index < 0 || index >= pauseButtons.Count)
    {
        Debug.LogWarning($"Invalid pause button index: {index}");
        return;
    }

    // Сбрасываем предыдущую активную кнопку
    if (activePauseButtonIndex >= 0 && activePauseButtonIndex < pauseButtons.Count)
    {
        Transform oldContent = pauseButtons[activePauseButtonIndex].transform.Find("Content");
        Transform oldImage = pauseButtons[activePauseButtonIndex].transform.Find("Image");
        if (oldContent != null)
        {
            Vector3 oldPos = oldContent.localPosition;
            oldContent.localPosition = new Vector3(0, oldPos.y, oldPos.z);
        }
        else
        {
            Debug.LogError($"Content not found in button {activePauseButtonIndex}!");
        }
        if (oldImage != null)
        {
            oldImage.gameObject.SetActive(false);
        }
    }

    // Устанавливаем новую активную кнопку
    activePauseButtonIndex = index;
    Transform activeContent = pauseButtons[activePauseButtonIndex].transform.Find("Content");
    Transform activeImage = pauseButtons[activePauseButtonIndex].transform.Find("Image");
    if (activeContent != null)
    {
        Vector3 currentPos = activeContent.localPosition;
        activeContent.localPosition = new Vector3(shiftAmount, currentPos.y, currentPos.z);
    }
    else
    {
        Debug.LogError($"Content not found in active button {index}!");
    }
    if (activeImage != null)
    {
        activeImage.gameObject.SetActive(true);
    }
    else
    {
        Debug.LogError($"Image not found in active button {index}!");
    }
}

private void LoadSettings()
{
    speed = PlayerPrefs.GetFloat("NoteSpeed", 5f);
    leftyFlip = PlayerPrefs.GetInt("LeftyFlip", 0) == 1;
    noteOffset = UserManager.Instance.GetCurrentUser()?.audioOffset ?? 0f;

        // Принудительно устанавливаем ExpertSingle для калибровки
        if (PlayerPrefs.GetString("SelectedSong", "") == "calibration")
        {
            PlayerPrefs.SetString("SelectedDifficulty", "ExpertSingle");
            PlayerPrefs.Save();
            Debug.Log("Set SelectedDifficulty to ExpertSingle for calibration.");
        }
}
private void LoadSongData()
{
    string songName = PlayerPrefs.GetString("SelectedSong", "");
    string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
    if (string.IsNullOrEmpty(songName)) return;

    // Парсим chart-файл
    currentSongData = ChartParser.ParseChartFile(songName, selectedDifficulty);
    if (currentSongData == null)
    {
        Debug.LogError("Не удалось загрузить chart-файл!");
        return;
    }

    // Проверяем, есть ли трек для выбранной сложности
    var track = currentSongData.tracks.Find(t => t.name == selectedDifficulty);
    if (track == null)
    {
        currentSongData.tracks.Add(new Track { name = selectedDifficulty, notes = new List<NoteData>() });
    }

}

private IEnumerator LoadAndPlaySong()
{
    string songName = PlayerPrefs.GetString("SelectedSong", "");
    if (string.IsNullOrEmpty(songName))
    {
        Debug.LogError("Имя песни не выбрано!");
        yield break;
    }

    AudioClip songClip = null;
    float loadStartTime = Time.time;

    if (songName == "calibration")
    {
        if (Application.isEditor)
        {
            string audioPath = Path.Combine(Application.dataPath, "Resources/Sounds/calibration/song.mp3");
            using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load audio: {www.error}");
                    yield break;
                }
                songClip = DownloadHandlerAudioClip.GetContent(www);
            }
        }
        else
        {
            string audioResourcePath = "Sounds/calibration/song";
            songClip = Resources.Load<AudioClip>(audioResourcePath);
            if (songClip == null)
            {
                Debug.LogError($"Audio not found in Resources: {audioResourcePath}. Ensure 'song.mp3' (or add support for calibration .ogg/.opus if needed) is in Assets/Resources/Sounds/calibration/");
                yield break;
            }
        }
    }
    else
    {
        // Обычные песни — поддержка .opus и .mp3
        string baseFolder = Application.isEditor
            ? Path.Combine(Application.dataPath, "songs", songName)
            : Path.Combine(Directory.GetCurrentDirectory(), "songs", songName);

        songClip = null;
        bool done = false;
        yield return AudioLoader.GetOrCreate().StartCoroutine(AudioLoader.Instance.LoadSongAsync(baseFolder, "song", c => { songClip = c; done = true; }));
        if (!done || songClip == null)
        {
            Debug.LogError($"Не удалось загрузить аудио для песни '{songName}' (ожидались song.opus или song.mp3)");
            yield break;
        }
    }

    // Проверяем songClip
    if (songClip == null)
    {
        Debug.LogError("SongClip is null!");
        yield break;
    }

    // Проверяем AudioSource
    if (audioSource == null)
    {
        Debug.LogError("AudioSource is null!");
        yield break;
    }

    // Ждём полной загрузки только для обычных песен
    if (songName != "calibration")
    {
        yield return new WaitUntil(() => songClip.loadState == AudioDataLoadState.Loaded);
    }

    // Устанавливаем startTime и musicPlaying
    audioSource.clip = songClip;
    startTime = Time.time;
    audioSource.Play();
    musicPlaying = true;
    yield return new WaitUntil(() => audioSource.isPlaying);

    // Запускаем спавн нот и линий
    StartCoroutine(SpawnNotesOverTime());
    StartCoroutine(SpawnLinesOverTime());

    // Анимация камеры
    Camera mainCamera = Camera.main;
    if (mainCamera != null)
    {
        cameraAnimationCoroutine = StartCoroutine(AnimateCameraRotation(mainCamera, -15f, 18f, 0.8f));
    }
    else
    {
        Debug.LogError("Main Camera not found for animation!");
    }
}
    private IEnumerator AnimateCameraRotation(Camera camera, float startX, float endX, float duration)
    {
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
            Debug.Log("Reset Time.timeScale to 1 for animation.");
        }

        float elapsed = 0f;
        Quaternion startRotation = Quaternion.Euler(startX, camera.transform.eulerAngles.y, camera.transform.eulerAngles.z);
        Quaternion endRotation = Quaternion.Euler(endX, camera.transform.eulerAngles.y, camera.transform.eulerAngles.z);

        // Проигрываем звук анимации
        if (animationAudioSource != null && animationSoundClip != null)
        {
            animationAudioSource.Stop(); // Останавливаем, если что-то играет
            animationAudioSource.clip = animationSoundClip;
            animationAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("Animation AudioSource or AudioClip not set up properly!");
        }

        float startRealTime = Time.realtimeSinceStartup;
        while (elapsed < duration)
        {
            elapsed = Time.realtimeSinceStartup - startRealTime; // Используем реальное время
            float t = Mathf.Clamp01(elapsed / duration);

            // EaseInOutCubic
            float easedT = t * t * t * (t * (6f * t - 15f) + 10.1f);

            // Интерполяция поворота
            Quaternion baseRotation = Quaternion.Slerp(startRotation, endRotation, easedT);

            // Колебание по Y
            float oscillation = Mathf.Sin(elapsed * Mathf.PI * 4f) * 0.4f;
            Quaternion oscillationRotation = Quaternion.Euler(baseRotation.eulerAngles.x, baseRotation.eulerAngles.y + oscillation, baseRotation.eulerAngles.z);

            camera.transform.rotation = oscillationRotation;

            yield return null;
        }

        // Финальный угол
        camera.transform.rotation = endRotation;
        // Останавливаем звук
        if (animationAudioSource != null && animationAudioSource.isPlaying)
        {
            animationAudioSource.Stop();
        }

        cameraAnimationCoroutine = null; // Очищаем корутину
    }

private IEnumerator SpawnNotesOverTime()
{
    string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
    var track = currentSongData.tracks.Find(t => t.name == selectedDifficulty);
    if (track == null)
    {
        Debug.LogError($"Трек для сложности {selectedDifficulty} не найден!");
        yield break;
    }

    bool useFourFrets = PlayerPrefs.GetInt("UseFourFrets", 0) == 1 && 
                       (selectedDifficulty == "MediumSingle" || selectedDifficulty == "EasySingle");
    float totalNoteWidth = useFourFrets ? 3.2f : 4f;
    int noteCount = useFourFrets ? 4 : 5;

    bool enableOpenNotes = PlayerPrefs.GetInt($"OpenNotesEnabled_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
    bool openNotesFiveString = PlayerPrefs.GetInt($"OpenNotesFiveString_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
    bool openNotesOneString = PlayerPrefs.GetInt($"OpenNotesOneString_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;

    var notesByTime = track.notes.GroupBy(n => n.time).OrderBy(g => g.Key).ToList();

    foreach (var noteGroup in notesByTime)
    {
        float spawnLeadTime = baseSpawnLeadTime;
        float adjustedNoteTime = noteGroup.Key + noteOffset;
        float timeUntilSpawn = adjustedNoteTime - (Time.time - startTime) - spawnLeadTime;
        if (timeUntilSpawn > 0) yield return new WaitForSeconds(timeUntilSpawn);

        float noteSpacing = totalNoteWidth / (noteCount - 1);
        float zPosition = CalculateStartZ(adjustedNoteTime);

        // Если только openNotesOneString включена и есть несколько открытых нот на одном тике
        if (enableOpenNotes && openNotesOneString && !openNotesFiveString && noteGroup.Count(n => n.isOpen && n.midi != 103) > 1)
        {
            float xPosition = 0f;
            float noteWidthScale = 0.20f * totalNoteWidth;
            int trackIndex = 5; // Индекс для N 7
            bool isStarPower = noteGroup.Any(n => n.isStarPower);

            if (trackIndex >= openNotePrefabs.Length)
            {
                Debug.LogError($"trackIndex={trackIndex} выходит за пределы openNotePrefabs (длина={openNotePrefabs.Length})");
                continue;
            }

            Vector3 openNotePosition = new Vector3(xPosition, 0.65f, zPosition);
            GameObject noteObj = Instantiate(
                isStarPower ? starPowerOpenNotePrefabs[trackIndex] : openNotePrefabs[trackIndex], 
                openNotePosition, 
                Quaternion.identity, 
                noteParent
            );
            ApplyHeadTilt(noteObj.transform);
            noteObj.transform.localScale = new Vector3(0.47f * noteWidthScale, 0.55f, 0.3f);

            NoteController noteController = noteObj.AddComponent<NoteController>();
            noteController.Midi = 103;
            noteController.IsForced = false;
            noteController.Duration = noteGroup.First().duration;
            noteController.IsOpen = true;
            noteController.IsStarPower = isStarPower;

            spawnedObjects.Add(noteObj);
            baseObjectTimes.Add(noteObj, adjustedNoteTime);
            //Debug.Log($"Spawned open note {noteObj.name} at time={adjustedNoteTime:F2}s, isStarPower={isStarPower}");

            float shortNoteZ = noteObj.transform.localScale.z * 1.5f;
            if (noteController.Duration > 0.1f)
            {
                float fullLength = noteController.Duration * speed;
                float stickLength = fullLength - shortNoteZ;
                Vector3 stickPosition = openNotePosition + new Vector3(0, 0, stickLength / 2f);
                if (trackIndex >= openNoteStickPrefabs.Length)
                {
                    Debug.LogWarning($"trackIndex={trackIndex} выходит за пределы openNoteStickPrefabs (длина={openNoteStickPrefabs.Length})");
                    continue;
                }
                GameObject stickObj = Instantiate(
                    openNoteStickPrefabs[trackIndex], 
                    stickPosition, 
                    Quaternion.identity, 
                    noteObj.transform
                );
                Vector3 parentScale = noteObj.transform.lossyScale;
                stickObj.transform.localScale = new Vector3(
                    totalNoteWidth / parentScale.x,
                    0.1f / parentScale.y,
                    stickLength / parentScale.z
                );

                if (noteStickBasePrefab != null)
                {
                    Vector3 basePosition = stickPosition + new Vector3(0, -0.13f, 0);
                    GameObject baseObj = Instantiate(noteStickBasePrefab, basePosition, Quaternion.identity, noteParent);
                    baseObj.transform.localScale = new Vector3(
                        totalNoteWidth,
                        0.01f,
                        stickLength
                    );
                    spawnedObjects.Add(baseObj);
                    baseObjectTimes.Add(baseObj, adjustedNoteTime);
                    baseObjectCoroutines.Add(baseObj, StartCoroutine(MoveObjectDown(baseObj, adjustedNoteTime)));
                    if (noteController != null)
                    {
                        noteController.SetBaseTransform(baseObj.transform);
                    }
                }
                else
                {
                    Debug.LogWarning("noteStickBasePrefab не назначен!");
                }
            }

            noteController.Initialize(this, FindObjectOfType<NoteInputManager>(), adjustedNoteTime, isStarPower);
            baseObjectCoroutines.Add(noteObj, StartCoroutine(MoveObjectDown(noteObj, adjustedNoteTime)));
            continue;
        }

        foreach (var note in noteGroup)
        {
            bool isSeventhNote = note.midi == 103;
            bool isOpenNote = note.isOpen;
            int trackIndex = isSeventhNote ? 5 : note.midi - 96;

            if (!isSeventhNote && (trackIndex < 0 || trackIndex >= notePrefabs.Length))
            {
                Debug.LogWarning($"Некорректный trackIndex: {trackIndex} для MIDI {note.midi}");
                continue;
            }
            if (useFourFrets && trackIndex == 4 && !isSeventhNote) continue;

            float xPosition;
            float noteWidthScale;

            bool shouldUseOpenNote = enableOpenNotes && (
                (isSeventhNote && openNotesOneString) ||
                (!isSeventhNote && isOpenNote && openNotesFiveString)
            );

            bool replaceWithOpenN7 = enableOpenNotes && openNotesOneString && !openNotesFiveString && isOpenNote && !isSeventhNote;
            bool replaceWithOpenN0 = enableOpenNotes && openNotesFiveString && !openNotesOneString && isSeventhNote;

            if (shouldUseOpenNote || replaceWithOpenN7 || replaceWithOpenN0)
            {
                int openTrackIndex = replaceWithOpenN7 ? 5 : (replaceWithOpenN0 ? 0 : trackIndex);
                int openMidi = replaceWithOpenN7 ? 103 : (replaceWithOpenN0 ? 96 : note.midi);

                if (openTrackIndex >= openNotePrefabs.Length)
                {
                    Debug.LogWarning($"trackIndex={openTrackIndex} выходит за пределы openNotePrefabs (длина={openNotePrefabs.Length})");
                    continue;
                }

                if (openMidi == 103)
                {
                    xPosition = 0f;
                    noteWidthScale = 0.20f * totalNoteWidth;
                }
                else
                {
                    int positionIndex = leftyFlip ? ((useFourFrets ? 99 : 100) - openMidi) : openMidi - 96;
                    xPosition = (positionIndex * noteSpacing) - (totalNoteWidth / 2f);
                    noteWidthScale = 1f;
                }

                Vector3 openNotePosition = new Vector3(xPosition, 0.65f, zPosition);
                GameObject noteObj = Instantiate(
                    note.isStarPower ? starPowerOpenNotePrefabs[openTrackIndex] : openNotePrefabs[openTrackIndex], 
                    openNotePosition, 
                    Quaternion.identity, 
                    noteParent
                );
                ApplyHeadTilt(noteObj.transform);
                noteObj.transform.localScale = new Vector3(0.47f * noteWidthScale, openMidi == 103 ? 0.55f : 0.45f, 0.3f);

                NoteController noteController = noteObj.AddComponent<NoteController>();
                noteController.Midi = openMidi;
                noteController.IsForced = false;
                noteController.Duration = note.duration;
                noteController.IsOpen = true;
                noteController.IsStarPower = note.isStarPower;

                spawnedObjects.Add(noteObj);
                baseObjectTimes.Add(noteObj, adjustedNoteTime);
                //Debug.Log($"Spawned open note {noteObj.name} at time={adjustedNoteTime:F2}s, isStarPower={note.isStarPower}");

                float shortNoteZ = noteObj.transform.localScale.z * 1.5f;
                if (note.duration > 0.1f)
                {
                    float fullLength = note.duration * speed;
                    float stickLength = fullLength - shortNoteZ;
                    Vector3 stickPosition = openNotePosition + new Vector3(0, 0, stickLength / 2f);
                    if (openTrackIndex >= openNoteStickPrefabs.Length)
                    {
                        Debug.LogWarning($"trackIndex={openTrackIndex} выходит за пределы openNoteStickPrefabs (длина={openNoteStickPrefabs.Length})");
                        continue;
                    }
                    GameObject stickObj = Instantiate(
                        openNoteStickPrefabs[openTrackIndex], 
                        stickPosition, 
                        Quaternion.identity, 
                        noteObj.transform
                    );
                    Vector3 parentScale = noteObj.transform.lossyScale;
                    stickObj.transform.localScale = new Vector3(
                        (openMidi == 103 ? totalNoteWidth - 0.5f : 0.25f) / parentScale.x,
                        (openMidi == 103 ? 0.2f : 0.2f) / parentScale.y,
                        stickLength / parentScale.z
                    );

                    if (noteStickBasePrefab != null)
                    {
                        Vector3 basePosition = stickPosition + new Vector3(0, -0.13f, 0);
                        GameObject baseObj = Instantiate(noteStickBasePrefab, basePosition, Quaternion.identity, noteParent);
                        baseObj.transform.localScale = new Vector3(
                            openMidi == 103 ? totalNoteWidth - 0.5f : 0.25f,
                            0.01f,
                            stickLength
                        );
                        spawnedObjects.Add(baseObj);
                        baseObjectTimes.Add(baseObj, adjustedNoteTime);
                        baseObjectCoroutines.Add(baseObj, StartCoroutine(MoveObjectDown(baseObj, adjustedNoteTime)));
                        if (noteController != null)
                        {
                            noteController.SetBaseTransform(baseObj.transform);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("noteStickBasePrefab не назначен!");
                    }
                }

                noteController.Initialize(this, FindObjectOfType<NoteInputManager>(), adjustedNoteTime, note.isStarPower);
                baseObjectCoroutines.Add(noteObj, StartCoroutine(MoveObjectDown(noteObj, adjustedNoteTime)));
            }
            else
            {
                int replacementMidi = isSeventhNote ? 96 : note.midi;
                trackIndex = replacementMidi - 96;
                if (trackIndex < 0 || trackIndex >= notePrefabs.Length)
                {
                    Debug.LogWarning($"Некорректный replacement trackIndex: {trackIndex} для MIDI {replacementMidi}");
                    continue;
                }

                if (isSeventhNote)
                {
                    if (useFourFrets)
                    {
                        xPosition = leftyFlip ? 1.6f : -1.6f;
                    }
                    else
                    {
                        xPosition = leftyFlip ? 2f : -2f;
                    }
                    noteWidthScale = 1f;
                }
                else
                {
                    int positionIndex = leftyFlip ? ((useFourFrets ? 99 : 100) - note.midi) : note.midi - 96;
                    xPosition = (positionIndex * noteSpacing) - (totalNoteWidth / 2f);
                    noteWidthScale = 1f;
                }

                Vector3 position = new Vector3(xPosition, 0.65f, zPosition);
                bool isForced = note.forced;
                GameObject[] prefabArray = isForced ? (note.isStarPower ? starPowerForcedNotePrefabs : forcedNotePrefabs) : 
                                                    (note.isStarPower ? starPowerNotePrefabs : notePrefabs);
                GameObject noteObj = Instantiate(prefabArray[trackIndex], position, Quaternion.identity, noteParent);
                ApplyHeadTilt(noteObj.transform);
                noteObj.transform.localScale = new Vector3(0.47f * noteWidthScale, 0.55f, 0.3f);

                NoteController noteController = noteObj.AddComponent<NoteController>();
                noteController.Midi = replacementMidi;
                noteController.IsForced = isForced;
                noteController.Duration = note.duration;
                noteController.IsOpen = false;
                noteController.IsStarPower = note.isStarPower;

                spawnedObjects.Add(noteObj);
                baseObjectTimes.Add(noteObj, adjustedNoteTime);
                //Debug.Log($"Spawned note {noteObj.name} at time={adjustedNoteTime:F2}s, isStarPower={note.isStarPower}, duration={note.duration:F2}s");

                float shortNoteZ = noteObj.transform.localScale.z * 1.5f;
                if (note.duration > 0.1f)
                {
                    float fullLength = note.duration * speed;
                    float stickLength = fullLength - shortNoteZ;
                    Vector3 stickPosition = position + new Vector3(0, 0, stickLength / 2f);
                    if (trackIndex >= noteStickPrefabs.Length)
                    {
                        Debug.LogWarning($"trackIndex={trackIndex} выходит за пределы noteStickPrefabs (длина={noteStickPrefabs.Length})");
                        continue;
                    }
                    GameObject stickObj = Instantiate(noteStickPrefabs[trackIndex], stickPosition, Quaternion.identity, noteObj.transform);
                    Vector3 parentScale = noteObj.transform.lossyScale;
                    stickObj.transform.localScale = new Vector3(
                        0.25f / parentScale.x,
                        0.2f / parentScale.y,
                        stickLength / parentScale.z
                    );
                    stickObj.name = $"NoteStick{replacementMidi}"; // Устанавливаем имя для последующего поиска

                    if (noteStickBasePrefab != null)
                    {
                        Vector3 basePosition = stickPosition + new Vector3(0, -0.13f, 0);
                        GameObject baseObj = Instantiate(noteStickBasePrefab, basePosition, Quaternion.identity, noteParent);
                        baseObj.transform.localScale = new Vector3(
                            0.25f,
                            0.01f,
                            stickLength
                        );
                        spawnedObjects.Add(baseObj);
                        baseObjectTimes.Add(baseObj, adjustedNoteTime);
                        baseObjectCoroutines.Add(baseObj, StartCoroutine(MoveObjectDown(baseObj, adjustedNoteTime)));
                        if (noteController != null)
                        {
                            noteController.SetBaseTransform(baseObj.transform);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("noteStickBasePrefab не назначен!");
                    }
                }

                noteController.Initialize(this, FindObjectOfType<NoteInputManager>(), adjustedNoteTime, note.isStarPower);
                baseObjectCoroutines.Add(noteObj, StartCoroutine(MoveObjectDown(noteObj, adjustedNoteTime)));
            }
        }
    }
}

public IEnumerator ReplaceStarPowerNotesInSection(int sectionIndex)
{
    //Debug.Log($"Starting ReplaceStarPowerNotesInSection for sectionIndex={sectionIndex}, SpawnedObjects count={SpawnedObjects.Count}");
    if (sectionIndex < 0 || sectionIndex >= ChartParser.StarPowerSections.Count)
    {
        Debug.LogWarning($"Invalid sectionIndex={sectionIndex}, StarPowerSections count={ChartParser.StarPowerSections.Count}");
        yield break;
    }

    var spSection = ChartParser.StarPowerSections[sectionIndex];
    //Debug.Log($"Replacing notes in Star Power section: startTime={spSection.startTime:F2}s, endTime={spSection.endTime:F2}s");

    // Обновляем SongData для будущих нот
    string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
    var track = currentSongData.tracks.Find(t => t.name == selectedDifficulty);
    if (track != null)
    {
        foreach (var note in track.notes)
        {
            if (note.time >= spSection.startTime && note.time <= spSection.endTime && note.isStarPower)
            {
                note.isStarPower = false;
                //Debug.Log($"Updated SongData: Note at time={note.time:F2}s, midi={note.midi} is no longer Star Power");
            }
        }
    }
    else
    {
        Debug.LogWarning($"Track for difficulty {selectedDifficulty} not found in SongData");
    }

    // Параметры для масштаба палочки, как в SpawnNotesOverTime
    bool useFourFrets = PlayerPrefs.GetInt("UseFourFrets", 0) == 1 && 
                        (selectedDifficulty == "MediumSingle" || selectedDifficulty == "EasySingle");
    float totalNoteWidth = useFourFrets ? 3.2f : 4f;
    int noteCount = useFourFrets ? 4 : 5;

    // Обрабатываем уже заспавненные ноты
    foreach (var noteObj in SpawnedObjects.ToList())
    {
        if (noteObj == null) continue;
        NoteController note = noteObj.GetComponent<NoteController>();
        if (note != null && note.IsStarPower)
        {
            if (baseObjectTimes.ContainsKey(noteObj))
            {
                float noteTime = baseObjectTimes[noteObj];
                float relativeNoteTime = noteTime - noteOffset; // учём глобальный аудио-оффсет
                if (relativeNoteTime >= spSection.startTime && relativeNoteTime <= spSection.endTime + 0.0001f) // небольшая дельта для защиты от округления
                {
                    int trackIndex = note.Midi == 103 ? 5 : note.Midi - 96;
                    if (trackIndex < 0)
                    {
                        Debug.LogWarning($"Invalid trackIndex={trackIndex} for note {noteObj.name}, Midi={note.Midi}");
                        continue;
                    }

                    GameObject newPrefab;
                    if (note.IsOpen)
                    {
                        if (trackIndex >= openNotePrefabs.Length)
                        {
                            Debug.LogWarning($"Invalid trackIndex={trackIndex} for openNotePrefabs, length={openNotePrefabs.Length}");
                            continue;
                        }
                        newPrefab = openNotePrefabs[trackIndex];
                    }
                    else
                    {
                        if (trackIndex >= notePrefabs.Length)
                        {
                            Debug.LogWarning($"Invalid trackIndex={trackIndex} for notePrefabs, length={notePrefabs.Length}");
                            continue;
                        }
                        newPrefab = note.IsForced ? forcedNotePrefabs[trackIndex] : notePrefabs[trackIndex];
                    }

                    if (newPrefab == null)
                    {
                        Debug.LogError($"No prefab found for trackIndex={trackIndex}, isOpen={note.IsOpen}, isForced={note.IsForced}");
                        continue;
                    }

                    //Debug.Log($"Replacing Star Power note {noteObj.name} at time={noteTime:F2}s with normal note, Midi={note.Midi}, isOpen={note.IsOpen}, isForced={note.IsForced}, duration={note.Duration:F2}s");
                    GameObject newNoteObj = Instantiate(newPrefab, noteObj.transform.position, noteObj.transform.rotation, noteParent);
                    ApplyHeadTilt(newNoteObj.transform);
                    newNoteObj.transform.localScale = noteObj.transform.localScale;

                    NoteController newNoteController = newNoteObj.AddComponent<NoteController>();
                    newNoteController.Midi = note.Midi;
                    newNoteController.IsForced = note.IsForced;
                    newNoteController.Duration = note.Duration;
                    newNoteController.IsOpen = note.IsOpen;
                    newNoteController.IsStarPower = false;

                    // Поиск палочки среди всех детей (учёт возможного дочернего контейнера HeadTilt)
                    Transform stickChild = null;
                    for (int ci = 0; ci < noteObj.transform.childCount; ci++)
                    {
                        Transform candidate = noteObj.transform.GetChild(ci);
                        if (candidate != null && candidate.name.StartsWith("NoteStick"))
                        {
                            stickChild = candidate;
                            break;
                        }
                    }

                    if (stickChild != null)
                    {
                        stickChild.SetParent(newNoteObj.transform, false);
                        stickChild.name = note.Midi == 103 ? "NoteStickOpen7" : $"NoteStick{note.Midi}";
                        //Debug.Log($"Transferred stick {stickChild.name} to new note {newNoteObj.name}");
                    }
                    else
                    {
                        // Если палочка не найдена, создаём новую для длинных нот
                        if (note.Duration > 0.1f)
                        {
                            float shortNoteZ = newNoteObj.transform.localScale.z * 1.5f;
                            float fullLength = note.Duration * speed;
                            float stickLength = fullLength - shortNoteZ;
                            Vector3 stickPosition = newNoteObj.transform.position + new Vector3(0, 0, stickLength / 2f);
                            GameObject stickPrefab = note.IsOpen ? openNoteStickPrefabs[trackIndex] : noteStickPrefabs[trackIndex];
                            if (trackIndex < (note.IsOpen ? openNoteStickPrefabs.Length : noteStickPrefabs.Length) && stickPrefab != null)
                            {
                                GameObject stickObj = Instantiate(stickPrefab, stickPosition, Quaternion.identity, newNoteObj.transform);
                                Vector3 parentScale = newNoteObj.transform.lossyScale;
                                stickObj.transform.localScale = new Vector3(
                                    note.Midi == 103 ? (totalNoteWidth - 0.5f) / parentScale.x : 0.25f / parentScale.x,
                                    0.2f / parentScale.y,
                                    stickLength / parentScale.z
                                );
                                stickObj.name = note.Midi == 103 ? "NoteStickOpen7" : $"NoteStick{note.Midi}";
                                //Debug.Log($"Created new stick {stickObj.name} for replaced note {newNoteObj.name}, isOpen={note.IsOpen}, prefab={(note.IsOpen ? (note.Midi == 103 ? "openNoteStickPrefabs[5] (7th)" : "openNoteStickPrefabs") : "noteStickPrefabs")}, scaleX={(note.Midi == 103 ? totalNoteWidth - 0.5f : 0.25f)}");
                            }
                        }
                        else
                        {
                            Debug.Log($"No stick found for note {noteObj.name}, duration={note.Duration:F2}s (not a sustain note)");
                        }
                    }

                    // Перенос подложки
                    if (note.BaseTransform != null)
                    {
                        newNoteController.SetBaseTransform(note.BaseTransform);
                        //Debug.Log($"Transferred base transform to new note {newNoteObj.name}");
                    }
                    else if (note.Duration > 0.1f)
                    {
                        // Если подложка не найдена, создаём новую для длинных нот
                        float shortNoteZ = newNoteObj.transform.localScale.z * 1.5f;
                        float fullLength = note.Duration * speed;
                        float stickLength = fullLength - shortNoteZ;
                        Vector3 basePosition = newNoteObj.transform.position + new Vector3(0, -0.13f, stickLength / 2f);
                        if (noteStickBasePrefab != null)
                        {
                            GameObject baseObj = Instantiate(noteStickBasePrefab, basePosition, Quaternion.identity, noteParent);
                            baseObj.transform.localScale = new Vector3(
                                note.Midi == 103 ? totalNoteWidth - 0.5f : 0.25f,
                                0.01f,
                                stickLength
                            );
                            newNoteController.SetBaseTransform(baseObj.transform);
                            SpawnedObjects.Add(baseObj);
                            baseObjectTimes.Add(baseObj, noteTime);
                            baseObjectCoroutines.Add(baseObj, StartCoroutine(MoveObjectDown(baseObj, noteTime)));
                            //Debug.Log($"Created new base transform for replaced note {newNoteObj.name}");
                        }
                        else
                        {
                            Debug.LogWarning($"noteStickBasePrefab not assigned for replaced note {newNoteObj.name}");
                        }
                    }

                    newNoteController.Initialize(this, FindObjectOfType<NoteInputManager>(), noteTime);

                    if (baseObjectTimes.ContainsKey(noteObj) && baseObjectCoroutines.ContainsKey(noteObj))
                    {
                        baseObjectTimes[newNoteObj] = baseObjectTimes[noteObj];
                        baseObjectCoroutines[newNoteObj] = StartCoroutine(MoveObjectDown(newNoteObj, baseObjectTimes[noteObj]));
                        baseObjectTimes.Remove(noteObj);
                        baseObjectCoroutines.Remove(noteObj);
                    }

                    SpawnedObjects.Remove(noteObj);
                    Destroy(noteObj);
                    SpawnedObjects.Add(newNoteObj);
                }
            }
            else
            {
                Debug.LogWarning($"Note {noteObj.name} not found in baseObjectTimes during replacement");
            }
        }
    }
    yield return null;
}

private IEnumerator SpawnLinesOverTime()
{
    int ppq = currentSongData.header.ppq;
    if (ppq == 0)
    {
        Debug.LogError("PPQ не найден в chart!");
        yield break;
    }

    List<TempoData> tempos = currentSongData.header.tempos;
    tempos.Sort((a, b) => a.ticks.CompareTo(b.ticks));

    List<TimeSignature> timeSignatures = currentSongData.header.timeSignatures;
    timeSignatures.Sort((a, b) => a.ticks.CompareTo(b.ticks));

    float currentTime = 0f;
    float lastTick = 0;
    float currentBPM = tempos.Count > 0 ? tempos[0].bpm : 120f;
    int currentNumerator = 4;
    int currentDenominator = 4;

    // Инициализируем ticksPerMeasure
    float ticksPerMeasure = ppq * currentNumerator * (4f / currentDenominator);

    int signatureIndex = 0;
    int tempoIndex = 0;
    float endOfTrackTime = audioSource.clip.length;

    yield return new WaitUntil(() => musicPlaying);

    float tick = 0;
    while (currentTime < endOfTrackTime)
    {
        while (signatureIndex < timeSignatures.Count && timeSignatures[signatureIndex].ticks <= tick)
        {
            currentNumerator = timeSignatures[signatureIndex].timeSignature[0];
            currentDenominator = timeSignatures[signatureIndex].timeSignature[1];
            ticksPerMeasure = ppq * currentNumerator * (4f / currentDenominator);
            signatureIndex++;
            //Debug.Log($"Updated time signature at tick {tick}: {currentNumerator}/{currentDenominator}, ticksPerMeasure={ticksPerMeasure}");
        }

        float timeInSeconds = CalculateTimeForTick(tick, tempos, ref tempoIndex, ref currentBPM, ref lastTick, ppq);
        float spawnLeadTime = (baseSpawnLeadTime * guitarHalfSize) / speed;
        float adjustedTime = timeInSeconds + noteOffset;
        float timeUntilSpawn = adjustedTime - (Time.time - startTime) - spawnLeadTime;

        if (timeUntilSpawn > 0) yield return new WaitForSeconds(timeUntilSpawn);

        float zPosition = CalculateStartZ(adjustedTime);
        Vector3 position = new Vector3(0, 0.6f, zPosition);
        GameObject newLine = Instantiate(linePrefab, position, Quaternion.identity, lineParent);
        newLine.transform.localScale = new Vector3(guitarWidth / 5f, 1f, 1.3f); // Устанавливаем ширину линии
        spawnedObjects.Add(newLine);
        StartCoroutine(MoveObjectDown(newLine, adjustedTime));

        // Вычисляем количество долей в такте
        int beatsPerMeasure = currentNumerator;
        float ticksPerBeat = ticksPerMeasure / beatsPerMeasure;
        int subDivisions = 1; // Всегда 1, чтобы линии ставились по целым долям

        for (int i = 1; i < beatsPerMeasure * subDivisions; i++)
        {
            float subTick = tick + (i * (ticksPerMeasure / (beatsPerMeasure * subDivisions)));
            float subTime = CalculateTimeForTick(subTick, tempos, ref tempoIndex, ref currentBPM, ref lastTick, ppq);
            float subAdjustedTime = subTime + noteOffset;
            float subTimeUntilSpawn = subAdjustedTime - (Time.time - startTime) - spawnLeadTime;

            if (subTimeUntilSpawn > 0) yield return new WaitForSeconds(subTimeUntilSpawn);

            float subZPosition = CalculateStartZ(subAdjustedTime);
            Vector3 subPosition = new Vector3(0, 0.6f, subZPosition);
            GameObject newHalfLine = Instantiate(linePrefab, subPosition, Quaternion.identity, lineParent);
            newHalfLine.transform.localScale = new Vector3(guitarWidth / 5f, 0.5f, 1f); // Устанавливаем ширину полудолей
            spawnedObjects.Add(newHalfLine);
            StartCoroutine(MoveObjectDown(newHalfLine, subAdjustedTime));
        }

        tick += ticksPerMeasure;
        currentTime = CalculateTimeForTick(tick, tempos, ref tempoIndex, ref currentBPM, ref lastTick, ppq);
    }
}

private float CalculateTimeForTick(float targetTick, List<TempoData> tempos, ref int tempoIndex, ref float currentBPM, ref float lastTick, int ppq)
{
    float time = currentTime;
    float tempLastTick = lastTick;
    float tempBPM = currentBPM;

    while (tempoIndex < tempos.Count && tempos[tempoIndex].ticks <= targetTick)
    {
        float deltaTicks = tempos[tempoIndex].ticks - tempLastTick;
        time += (deltaTicks / (float)ppq) * (60f / tempBPM);
        tempLastTick = tempos[tempoIndex].ticks;
        tempBPM = tempos[tempoIndex].bpm;
        tempoIndex++;
    }

    float remainingTicks = targetTick - tempLastTick;
    time += (remainingTicks / (float)ppq) * (60f / tempBPM);

    lastTick = targetTick;
    currentBPM = tempBPM;
    currentTime = time;

    return time;
}

private float CalculateStartZ(float noteTime)
{
    float timeUntilNote = noteTime - (Time.time - startTime);
    float zPosition = timeUntilNote * speed;
    return Mathf.Max(zPosition, 0f);
}

public IEnumerator MoveObjectDown(GameObject obj, float targetTime)
{
    if (obj == null)
    {
        spawnedObjects.Remove(obj);
        yield break;
    }

    yield return new WaitUntil(() => musicPlaying);

    float removalZ = -3f;

    NoteController noteController = obj.GetComponent<NoteController>();
    Transform stickTransform = null;
    bool isBase = obj.name.Contains("NoteStickBase");

    if (noteController != null)
    {
        foreach (Transform child in obj.transform)
        {
            if (child != null && child.name.StartsWith("NoteStick"))
            {
                stickTransform = child;
                break;
            }
        }
    }

    while (obj != null)
    {
        float zPos = obj.transform.position.z;

        if (isBase)
        {
            // Для подложки проверяем конец по Z
            float baseEndZ = zPos + obj.transform.lossyScale.z;
            if (baseEndZ <= removalZ)
            {
                spawnedObjects.Remove(obj);
                baseObjectTimes.Remove(obj);
                baseObjectCoroutines.Remove(obj);
                Destroy(obj);
                yield break;
            }
        }
        else if (noteController != null)
        {
            // Логика для нот с палочкой
            if (stickTransform != null && stickTransform.gameObject != null)
            {
                if (!noteController.IsSustained && zPos <= removalZ)
                {
                    spawnedObjects.Remove(obj);
                    Destroy(obj);
                    yield break;
                }
            }
            // Логика для нот без палочки
            else if (zPos <= removalZ)
            {
                spawnedObjects.Remove(obj);
                Destroy(obj);
                yield break;
            }
        }
        else
        {
            // Для линий или других объектов
            if (zPos <= removalZ)
            {
                spawnedObjects.Remove(obj);
                Destroy(obj);
                yield break;
            }
        }

        // Двигаем объект с использованием speed из NoteSpawner
        if (noteController == null || !noteController.IsSustained)
        {
            obj.transform.Translate(Vector3.back * speed * Time.deltaTime);
        }

        yield return null;
    }
}
}