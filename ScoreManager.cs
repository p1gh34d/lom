using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using System;
using System.Linq;

public class ScoreManager : MonoBehaviour
{
    private NoteInputManager noteInputManager;
    private NoteSpawner noteSpawner;
    private ChartParser chartParser; // Ссылка на ChartParser

    // Новые поля для UI песни
    [SerializeField] private Image songCoverImage; // Кавер
    [SerializeField] private Text songTitleText;   // Название песни
    [SerializeField] private Text bandNameText;    // Название группы
    [SerializeField] private Sprite defaultCover;  // Дефолтный кавер

    [SerializeField] private Text multiplierText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text streakText;

    [SerializeField] private GameObject statsPanel;
    [SerializeField] private Image statsSongCoverImage; // Кавер для statsPanel
    [SerializeField] private Text statsBandNameText;   // Название группы для statsPanel
    [SerializeField] private Text statsSongTitleText;  // Название песни для statsPanel
    [SerializeField] private Text statsSongCharterText;  // Название песни для statsPanel
    [SerializeField] private Text statsScoreText;
    [SerializeField] private Text statsProgressText;
    [SerializeField] private Text statsStarsText;
    [SerializeField] private Text statsTotalNotesText;
    [SerializeField] private Text statsMaxStreakText;
    [SerializeField] private Text statsHitNotesText;
    [SerializeField] private Text statsMissedNotesText;
    [SerializeField] private Text statsPerfectText;
    [SerializeField] private Text statsEarlyText;
    [SerializeField] private Text statsLateText;
    [SerializeField] private Text statsAccuracyText;

    [SerializeField] private Text statsPreviousScoreText;
    //[SerializeField] private Text statsPreviousProgressText;
    //[SerializeField] private Text statsPreviousStarsText;
    [SerializeField] private Text statsPreviousMaxStreakText;
    [SerializeField] private Text statsPreviousHitNotesText;
    //[SerializeField] private Text statsPreviousMissedNotesText;
    //[SerializeField] private Text statsPreviousAccuracyText;
    //[SerializeField] private Text statsPreviousPerfectText;
    //[SerializeField] private Text statsPreviousEarlyText;
    //[SerializeField] private Text statsPreviousLateText;

    // Новое поле для префаба точности
    [SerializeField] private Text accuracyText; // Уже есть, используем его
    [SerializeField] private Text offsetText;
    private Vector3 initialAccuracyPosition;
    private Vector3 initialOffsetPosition;
    private GameObject lastAccuracyInstance;
    private GameObject lastOffsetInstance;

    private int score = 0;
    private int streak = 0;
    private int maxStreak = 0;
    private int multiplier = 1;
    private int hitNotes = 0;
    private int missedNotes = 0;
    private int totalNotes = 0;
    private int perfectHits = 0;
    private int earlyHits = 0;
    private int lateHits = 0;
    private const int maxMultiplierLimit = 4;
    private const int pointsPerNote = 50;
    private float starPowerEnergy = 0f; // Уровень энергии Star Power (0–1)
    private const float starPowerDetectionWindow = 0.2f;

    [SerializeField] private GameObject starPowerModel;
    [SerializeField] private GameObject starPowerEffect;
    [SerializeField] private AudioClip starPowerEnergyAddClip; // Звук для добавления энергии Star Power
    [SerializeField] private AudioClip starPowerActivateClip;  // Звук для активации Star Power
    private Vector3 initialSPEnergyScale;
    private Vector3 initialSPEnergyPosition;
    private AudioSource starPowerAudioSource;
    private Transform spEnergyTransform;
    private Track cachedDifficultyTrack;

    private HashSet<int> processedChordsThisFrame = new HashSet<int>();
    private HashSet<int> missedChordsThisFrame = new HashSet<int>();
    private HashSet<GameObject> missedNotesSet = new HashSet<GameObject>();

private HashSet<int> processedStarPowerNotes;
private HashSet<int> processedStarPowerChordsThisFrame = new HashSet<int>();
private HashSet<int> sectionEnergyAwarded = new HashSet<int>();
private Dictionary<int, HashSet<int>> processedTicksPerSection = new Dictionary<int, HashSet<int>>();
private Dictionary<int, bool> starPowerSectionSuccess; // Отслеживание успешности секций

    // Активная удерживаемая StarPower нота (для Whammy)
    private GameObject activeSustainedSpNoteObj = null;
    private NoteController activeSustainedSpNoteController = null;
    private NoteData activeSustainedSpNoteData = null;
    private Dictionary<int, List<NoteData>> spLongNotesByMidi = null; // midi -> список длинных SP нот, отсортированных по времени


    private Coroutine whammyEnergyCoroutine = null;
    private Coroutine starPowerCoroutine = null;

    private bool canStartNewWhammyCycle = true;
    private bool isAddingEnergy = false;
    private bool isStarPowerActive = false;
    private bool useAccuracySystem;
    private bool hasStartedPlaying = false;

    public event Action<float> OnCalibrationHit;
    private float noteOffset;

    private void Start()
    {
        noteInputManager = FindObjectOfType<NoteInputManager>();
        noteSpawner = FindObjectOfType<NoteSpawner>();

        noteOffset = UserManager.Instance.GetCurrentUser()?.audioOffset ?? 0f;

        if (noteInputManager == null || noteSpawner == null)
        {
            Debug.LogError("NoteController or NoteSpawner not found in scene!");
            return;
        }

    // Один источник для Star Power sfx — не дергаем GetComponent при каждом событии
    starPowerAudioSource = gameObject.AddComponent<AudioSource>();
    starPowerAudioSource.playOnAwake = false;
    starPowerAudioSource.loop = false;
    starPowerAudioSource.volume = 0.8f; // Настрой громкость (0–1)

        if (starPowerModel != null)
        {
            spEnergyTransform = starPowerModel.transform.Find("SPEnergy");
            if (spEnergyTransform != null)
            {
                initialSPEnergyScale = spEnergyTransform.localScale;
                initialSPEnergyPosition = spEnergyTransform.localPosition;
                if (starPowerEnergy == 0f)
                {
                    spEnergyTransform.gameObject.SetActive(false);
                }
            }
        }

    if (starPowerEffect == null)
    {
        Debug.LogError("StarPower GameObject not found in scene!");
    }

        processedStarPowerNotes = new HashSet<int>();
        starPowerSectionSuccess = new Dictionary<int, bool>();

        noteInputManager.OnNoteHit += HandleNoteHit;
        noteInputManager.OnNoteSustainEnd += HandleNoteSustainEnd;
        noteInputManager.OnNoteSustainTick += HandleNoteSustainTick;
        noteInputManager.OnArrowKeyError += HandleArrowKeyError;

        OnCalibrationHit -= HandleCalibrationHit;
        OnCalibrationHit += HandleCalibrationHit;

        useAccuracySystem = PlayerPrefs.GetInt($"AccuracySystem_{UserManager.Instance.GetCurrentUser()?.userIndex ?? 0}", 1) == 1;
        if (accuracyText != null)
        {
            accuracyText.text = "";
            initialAccuracyPosition = accuracyText.transform.position;
        }
        else
        {
            Debug.LogError("AccuracyText is null in Start!");
        }

        if (offsetText != null)
        {
            offsetText.text = "";
            initialOffsetPosition = offsetText.transform.position;
            offsetText.gameObject.SetActive(IsCalibrationActive() && IsFirstStage());
        }
        else
        {
            Debug.LogError("OffsetText is null in Start!");
        }

        statsPanel.SetActive(false);
        UpdateSongInfo();
        StartCoroutine(InitializeAfterSongLoad());
        UpdateTextVisibility();
    }

    private bool IsCalibrationActive()
    {
        return PlayerPrefs.GetString("SelectedSong", "") == "calibration";
    }

    private bool IsFirstStage()
    {
        return PlayerPrefs.GetInt("CalibrationStage", 1) == 1;
    }

    public void TriggerCalibrationHit(float offsetMs)
    {
        OnCalibrationHit?.Invoke(offsetMs);
    }

    private void HandleCalibrationHit(float offsetMs)
    {
        if (!IsCalibrationActive() || !IsFirstStage())
        {
            return;
        }

        bool showAccuracy = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex ?? 0}", 1) == 1;
        if (showAccuracy)
        {
            if (offsetText == null)
            {
                Debug.LogError("offsetText is null, cannot show offset!");
                return;
            }
            StartCoroutine(ShowCalibrationOffset(offsetMs));
        }
        else
        {
            Debug.LogWarning("showAccuracy is false, offset text not shown");
        }
    }

    private void UpdateTextVisibility()
    {
        bool isCalibration = IsCalibrationActive();
        bool isFirstStage = IsFirstStage();
        if (accuracyText != null)
        {
            accuracyText.gameObject.SetActive(!isCalibration || !isFirstStage);
        }
        if (offsetText != null)
        {
            offsetText.gameObject.SetActive(isCalibration && isFirstStage);
        }
        else
        {
            Debug.LogError("offsetText is null in UpdateTextVisibility!");
        }
    }

private void UpdateSongInfo()
{
    bool isCalibration = IsCalibrationActive();

    // Название песни
    if (songTitleText != null)
    {
        songTitleText.text = PlayerPrefs.GetString("SongTitle", isCalibration ? "Calibration" : "Unknown Song");
    }

    // Название группы
    if (bandNameText != null)
    {
        bandNameText.text = PlayerPrefs.GetString("BandName", isCalibration ? "Messiah Flesh" : "Unknown Artist");
    }

    // Кавер
    if (songCoverImage != null)
    {
        string coverPath = PlayerPrefs.GetString("CoverPath", "default");
        Debug.Log($"Loading cover: CoverPath={coverPath}");

        if (coverPath != "default")
        {
            if (Application.isEditor)
            {
                // В редакторе используем полный путь
                if (File.Exists(coverPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(coverPath);
                        Texture2D texture = new Texture2D(2, 2);
                        if (texture.LoadImage(bytes))
                        {
                            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                            songCoverImage.sprite = sprite;
                            Debug.Log($"Loaded cover from file: {coverPath}");
                        }
                        else
                        {
                            Debug.LogError($"Failed to load image bytes: {coverPath}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error loading cover file: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"Cover file not found: {coverPath}");
                }
            }
            else if (isCalibration)
            {
                string resourcePath = coverPath; // Ожидаем Sounds/calibration/cover
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    songCoverImage.sprite = sprite;
                }
                else
                {
                    Debug.LogError($"Cover not found in Resources: {resourcePath}");
                }
            }
            else
            {
                if (coverPath != "default" && File.Exists(coverPath))
                {
                    byte[] bytes = File.ReadAllBytes(coverPath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(bytes);
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    songCoverImage.sprite = sprite;
                }
                else if (defaultCover != null)
                {
                    songCoverImage.sprite = defaultCover;
                }
            }
        }

        if (songCoverImage.sprite == null && defaultCover != null)
        {
            songCoverImage.sprite = defaultCover;
            Debug.Log($"Using default cover.");
        }
    }

    // Обновляем UI для statsPanel
    if (statsBandNameText != null)
    {
        statsBandNameText.text = PlayerPrefs.GetString("BandName", isCalibration ? "Messiah Flesh" : "Unknown Artist");
    }

    if (statsSongTitleText != null)
    {
        statsSongTitleText.text = PlayerPrefs.GetString("SongTitle", isCalibration ? "Calibration" : "Unknown Song");
    }

    if (statsSongCharterText != null)
    {
        string charter = PlayerPrefs.GetString("SongCharter", "Unknown Charter");
        statsSongCharterText.text = string.IsNullOrEmpty(charter) || charter == "Unknown Charter" ? "Unknown Charter" : $"By {charter}";
    }
}

    private System.Collections.IEnumerator InitializeAfterSongLoad()
    {
        yield return new WaitUntil(() => noteSpawner.CurrentSongData != null);
        CalculateTotalNotes();
        UpdateUI();
        BuildStarPowerCaches();
        yield return new WaitUntil(() => noteSpawner.AudioSource.isPlaying);
        PrewarmGameplayHitches();
        StartCoroutine(CheckSongEnd());
    }

    private void PrewarmGameplayHitches()
    {
        if (starPowerEnergyAddClip != null)
        {
            starPowerEnergyAddClip.LoadAudioData();
            starPowerAudioSource.PlayOneShot(starPowerEnergyAddClip, 0.0001f);
        }

        if (starPowerActivateClip != null)
            starPowerActivateClip.LoadAudioData();

        SongData sd = noteSpawner != null ? noteSpawner.CurrentSongData : null;
        if (sd != null && sd.header != null && sd.header.tempos != null)
            ChartParser.GetBPMAtTick(0f, sd.header.tempos, sd.header.ppq);

        UpdateStarPowerUI();
    }

    private void BuildStarPowerCaches()
    {
        // Инициализируем processedTicksPerSection для всех секций заранее
        processedTicksPerSection.Clear();
        sectionEnergyAwarded.Clear();
        starPowerSectionSuccess.Clear();
        for (int i = 0; i < ChartParser.StarPowerSections.Count; i++)
        {
            processedTicksPerSection[i] = new HashSet<int>();
        }

        // Составляем словарь длинных StarPower нот по MIDI
        spLongNotesByMidi = new Dictionary<int, List<NoteData>>();
        cachedDifficultyTrack = null;
        string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        List<Track> tracks = noteSpawner.CurrentSongData.tracks;
        Track track = null;
        if (tracks != null)
        {
            for (int ti = 0; ti < tracks.Count; ti++)
            {
                if (tracks[ti].name == selectedDifficulty)
                {
                    track = tracks[ti];
                    break;
                }
            }
        }
        cachedDifficultyTrack = track;
        if (track != null)
        {
            var groups = track.notes
                .Where(n => n.isStarPower && n.duration > 0.1f)
                .GroupBy(n => n.midi);
            foreach (var g in groups)
            {
                var list = g.OrderBy(n => n.time).ToList();
                spLongNotesByMidi[g.Key] = list;
            }
        }
        activeSustainedSpNoteObj = null;
        activeSustainedSpNoteController = null;
        activeSustainedSpNoteData = null;
    }

private void HandleNoteHit(int midi, bool isLongNote, NoteInputManager.HitAccuracy accuracy)
{
    if (IsCalibrationActive() && IsFirstStage()) return;

    int chordId = Time.frameCount;
    if (!processedChordsThisFrame.Contains(chordId))
    {
        hitNotes++;
        streak++;
        if (streak > maxStreak) maxStreak = streak;
        processedChordsThisFrame.Add(chordId);
        UpdateMultiplier();

        if (useAccuracySystem && accuracy != NoteInputManager.HitAccuracy.None)
        {
            switch (accuracy)
            {
                case NoteInputManager.HitAccuracy.Perfect:
                    perfectHits++;
                    break;
                case NoteInputManager.HitAccuracy.Early:
                    earlyHits++;
                    break;
                case NoteInputManager.HitAccuracy.Late:
                    lateHits++;
                    break;
            }

            bool showAccuracy = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
            if (showAccuracy)
            {
                StartCoroutine(ShowAccuracy(accuracy));
            }
        }
    }

    AddScore(pointsPerNote);

    NoteData note = FindClosestStarPowerChartNote(midi);
    if (cachedDifficultyTrack != null)
    {
        if (note != null)
        {
            int sectionIndex = FindStarPowerSectionIndexForTick(note.tick);
            if (sectionIndex >= 0)
            {
                if (!processedTicksPerSection.ContainsKey(sectionIndex))
                {
                    processedTicksPerSection[sectionIndex] = new HashSet<int>();
                }
                processedTicksPerSection[sectionIndex].Add(note.tick);
                starPowerSectionSuccess[sectionIndex] = true;

                // Если это длинная SP-нота — сохраняем активную ссылку для Whammy
                if (isLongNote)
                {
                    // Найдём объект ноты, который только что начал удерживаться
                    GameObject candidateObj = null;
                    NoteController candidateCtrl = null;
                    foreach (var obj in noteSpawner.SpawnedObjects)
                    {
                        if (obj == null) continue;
                        var c = obj.GetComponent<NoteController>();
                        if (c != null && c.Midi == midi && c.IsStarPower && c.IsSustained && c.Duration > 0.1f)
                        {
                            candidateObj = obj;
                            candidateCtrl = c;
                            break;
                        }
                    }
                    if (candidateObj != null)
                    {
                        activeSustainedSpNoteObj = candidateObj;
                        activeSustainedSpNoteController = candidateCtrl;
                        activeSustainedSpNoteData = note;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"No Star Power section found for note tick={note.tick}, midi={midi}");
            }
        }
    }
    else
    {
        Debug.LogWarning("Track not found for selected difficulty");
    }

    UpdateUI();
}

private NoteData FindClosestStarPowerChartNote(int midi)
{
    if (cachedDifficultyTrack?.notes == null || noteSpawner == null)
        return null;

    float playbackTime = Time.time - noteSpawner.StartTime;
    float bestDelta = starPowerDetectionWindow;
    NoteData best = null;
    List<NoteData> chartNotes = cachedDifficultyTrack.notes;
    for (int i = 0; i < chartNotes.Count; i++)
    {
        NoteData n = chartNotes[i];
        if (n.midi != midi || !n.isStarPower)
            continue;

        float delta = Mathf.Abs(n.time - playbackTime + noteOffset);
        if (delta < bestDelta)
        {
            bestDelta = delta;
            best = n;
        }
    }
    return best;
}

private static int FindStarPowerSectionIndexForTick(int tick)
{
    var sections = ChartParser.StarPowerSections;
    if (sections == null)
        return -1;

    for (int i = 0; i < sections.Count; i++)
    {
        var sp = sections[i];
        if (tick >= sp.startTick && tick <= sp.startTick + sp.duration)
            return i;
    }
    return -1;
}

private void HandleNoteSustainEnd(int midi, bool wasInterrupted)
{
    float currentTime = Time.time - noteSpawner.StartTime;
    
    // Если завершилась активная удерживаемая SP нота — проверяем, не было ли раннего отпускания
    if (activeSustainedSpNoteController != null && activeSustainedSpNoteController.Midi == midi && activeSustainedSpNoteData != null)
    {
        float naturalEnd = activeSustainedSpNoteData.time + activeSustainedSpNoteData.duration;
        
        int sectionIndex = ChartParser.StarPowerSections.FindIndex(sp =>
            activeSustainedSpNoteData.tick >= sp.startTick && activeSustainedSpNoteData.tick <= sp.startTick + sp.duration);
            
        // Секция неуспешна ТОЛЬКО при ошибках стрелок, а не при обычном отпускании
        if (wasInterrupted)
        {
            if (sectionIndex >= 0)
            {
                starPowerSectionSuccess[sectionIndex] = false;
            }
        }
        else
        {
            // Проверяем можно ли сразу дать энергию за эту секцию
            CheckSectionCompletionImmediately(sectionIndex);
        }
        activeSustainedSpNoteObj = null;
        activeSustainedSpNoteController = null;
        activeSustainedSpNoteData = null;
    }
}

// Новый метод для немедленной проверки завершения секции
private void CheckSectionCompletionImmediately(int sectionIndex)
{
    if (sectionIndex < 0 || sectionEnergyAwarded.Contains(sectionIndex)) return;
    
    // Проверяем все условия как в CheckStarPowerSections
    if (sectionIndex >= ChartParser.StarPowerSectionHasNotes.Count || !ChartParser.StarPowerSectionHasNotes[sectionIndex])
    {
        return;
    }
    
    if (!processedTicksPerSection.ContainsKey(sectionIndex))
    {
        return;
    }
    
    var uniqueTicksSet = (sectionIndex >= 0 && sectionIndex < ChartParser.StarPowerSectionUniqueTicks.Count)
        ? ChartParser.StarPowerSectionUniqueTicks[sectionIndex]
        : null;
    if (uniqueTicksSet == null || uniqueTicksSet.Count == 0)
    {
        return;
    }
    
    bool allTicksProcessed = processedTicksPerSection[sectionIndex].IsSupersetOf(uniqueTicksSet);
    bool sectionSucceeded = starPowerSectionSuccess.TryGetValue(sectionIndex, out var success) && success;
    
    if (sectionSucceeded && processedTicksPerSection[sectionIndex].Count > 0 && allTicksProcessed && !sectionEnergyAwarded.Contains(sectionIndex) && !isAddingEnergy)
    {
        StartCoroutine(SmoothAddStarPowerEnergy(0.25f, 0.2f));
        starPowerSectionSuccess.Remove(sectionIndex);
        processedTicksPerSection.Remove(sectionIndex);
        sectionEnergyAwarded.Add(sectionIndex);
    }
}

    private void HandleNoteSustainTick(int midi, int points)
    {
        AddScore(points);
        UpdateUI();
    }
    
    // Обработчик ошибок при нажатии стрелок
    private void HandleArrowKeyError()
    {
        Debug.Log("Arrow key error detected - resetting streak and multiplier");
        
        // Сбрасываем счётчик очков и стрика
        missedNotes++;
        ResetStreak();
        
        // Проверяем StarPower секции и меняем ноты на обычные
        HandleStarPowerSectionFailure();
        
        UpdateUI();
    }
    
    // Обработка провала StarPower секции при ошибках
    private void HandleStarPowerSectionFailure()
    {
        float currentTime = Time.time - noteSpawner.StartTime;
        
        // Находим активные StarPower секции
        for (int i = 0; i < ChartParser.StarPowerSections.Count; i++)
        {
            var spSection = ChartParser.StarPowerSections[i];
            
            // Проверяем, находимся ли мы в данной секции
            if (currentTime >= spSection.startTime && currentTime <= spSection.endTime)
            {
                starPowerSectionSuccess[i] = false;
                Debug.Log($"Arrow key error in Star Power section {i} at time={currentTime:F2}s. Converting StarPower notes to regular notes.");
                StartCoroutine(noteSpawner.ReplaceStarPowerNotesInSection(i));
                break; // Обрабатываем только одну секцию за раз
            }
        }
    }
// Изменить метод CheckStarPowerSections
private void CheckStarPowerSections()
{
    float currentTime = noteSpawner.AudioSource.time;
    for (int i = 0; i < ChartParser.StarPowerSections.Count; i++)
    {
        if (sectionEnergyAwarded.Contains(i)) continue;
        if (i < 0 || i >= ChartParser.StarPowerSectionHasNotes.Count) continue;
        if (!ChartParser.StarPowerSectionHasNotes[i]) continue;

        if (!processedTicksPerSection.ContainsKey(i))
        {
            processedTicksPerSection[i] = new HashSet<int>();
        }

        var uniqueTicksSet = (i >= 0 && i < ChartParser.StarPowerSectionUniqueTicks.Count)
            ? ChartParser.StarPowerSectionUniqueTicks[i]
            : null;
        if (uniqueTicksSet == null || uniqueTicksSet.Count == 0) continue;

        bool allTicksProcessed = processedTicksPerSection[i].IsSupersetOf(uniqueTicksSet);
        bool sectionSucceeded = starPowerSectionSuccess.TryGetValue(i, out var success) && success;
        float lastSpEndTime = (i >= 0 && i < ChartParser.StarPowerSectionLastEndTime.Count)
            ? ChartParser.StarPowerSectionLastEndTime[i]
            : ChartParser.StarPowerSections[i].endTime;

        if (sectionSucceeded && processedTicksPerSection[i].Count > 0 && allTicksProcessed && currentTime >= lastSpEndTime && !sectionEnergyAwarded.Contains(i) && !isAddingEnergy)
        {
            StartCoroutine(SmoothAddStarPowerEnergy(0.25f, 0.2f));
            starPowerSectionSuccess.Remove(i);
            processedTicksPerSection.Remove(i);
            sectionEnergyAwarded.Add(i);
        }
    }
}
// Заменить метод UpdateStarPowerUI
private void UpdateStarPowerUI()
{
    if (starPowerModel != null)
    {
        if (spEnergyTransform != null)
        {
            if (starPowerEnergy > 0f)
            {
                spEnergyTransform.gameObject.SetActive(true);
                float newScaleX = starPowerEnergy * 1f;
                // Смещаем позицию, чтобы левый край оставался на initialXPosition
                float newPositionX = -2.37f + (newScaleX * 2.37f);
                spEnergyTransform.localScale = new Vector3(newScaleX, initialSPEnergyScale.y, initialSPEnergyScale.z);
                spEnergyTransform.localPosition = new Vector3(newPositionX, initialSPEnergyPosition.y, initialSPEnergyPosition.z);
            }
            else
            {
                spEnergyTransform.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("SPEnergy mesh not found in SP2 model!");
        }
    }
    else
    {
        Debug.LogWarning("starPowerModel is not assigned!");
    }
}

// Добавить новый метод SmoothAddStarPowerEnergy
private IEnumerator SmoothAddStarPowerEnergy(float energyIncrement, float duration)
{
    if (isAddingEnergy)
    {
        yield return new WaitUntil(() => !isAddingEnergy);
    }
    isAddingEnergy = true;

    float startEnergy = starPowerEnergy;
    float targetEnergy = Mathf.Min(1f, starPowerEnergy + energyIncrement);
    float elapsed = 0f;

    // Проигрываем звук добавления энергии в любом случае
    if (starPowerAudioSource != null && starPowerEnergyAddClip != null)
        starPowerAudioSource.PlayOneShot(starPowerEnergyAddClip);

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        starPowerEnergy = Mathf.Lerp(startEnergy, targetEnergy, t);
        UpdateStarPowerUI();
        yield return null;
    }

    starPowerEnergy = targetEnergy;
    UpdateStarPowerUI();

    isAddingEnergy = false;
}

private IEnumerator SmoothAddWhammyEnergy(GameObject noteObj, NoteController noteController, NoteData noteData)
{
    float elapsed = 0f;
    float timeHeld = 0f;
    float startEnergy = starPowerEnergy;
    float maxDuration = 0.01f; // Минимальное время удержания Whammy для получения maxEnergyIncrement
    float maxEnergyIncrement = 0.01f; // 1% энергии за удержание
    float animationDuration = 0.1f; // Фиксированное время анимации
    bool isWhammyActive = true;

    while (elapsed < animationDuration)
    {
        if (isWhammyActive && (noteObj == null || !noteController.IsSustained || !InputManager.Instance.IsKey("Whammy") || (Time.time - noteSpawner.StartTime) > noteData.time + noteData.duration))
        {
            isWhammyActive = false;
            float finalEnergyIncrement = (timeHeld / maxDuration) * maxEnergyIncrement;
            float finalTargetEnergy = Mathf.Min(1f, startEnergy + finalEnergyIncrement);
            starPowerEnergy = Mathf.Max(starPowerEnergy, finalTargetEnergy);
            UpdateStarPowerUI();
        }

        elapsed += Time.deltaTime;
        if (isWhammyActive)
        {
            timeHeld = Mathf.Min(timeHeld + Time.deltaTime, maxDuration);
        }

        float t = elapsed / animationDuration;
       // t = t * t * (3f - 2f * t); // SmoothStep
        float energyIncrement = (timeHeld / maxDuration) * maxEnergyIncrement * t;
        float newEnergyValue = Mathf.Min(1f, startEnergy + energyIncrement);
        starPowerEnergy = Mathf.Max(starPowerEnergy, newEnergyValue);
        UpdateStarPowerUI();
        yield return null;
    }

    float completedEnergyIncrement = (timeHeld / maxDuration) * maxEnergyIncrement;
    float completedTargetEnergy = Mathf.Min(1f, startEnergy + completedEnergyIncrement);
    starPowerEnergy = Mathf.Max(starPowerEnergy, completedTargetEnergy);
    UpdateStarPowerUI();
    whammyEnergyCoroutine = null;
}
private IEnumerator SmoothConsumeStarPowerEnergy()
{
    float currentTime = noteSpawner.AudioSource.time;
    float bpm = ChartParser.GetBPMAtTick(currentTime * noteSpawner.CurrentSongData.header.ppq, noteSpawner.CurrentSongData.header.tempos, noteSpawner.CurrentSongData.header.ppq);
    float beatDuration = 60f / bpm; // Длительность одного бита в секундах
    float depletionRate = 1f / (28f * beatDuration); // 1.0 энергии за 28 битов
    float lastEnergy = starPowerEnergy;
    float elapsedBeats = 0f;

    while (starPowerEnergy > 0f)
    {
        float elapsed = Time.deltaTime;
        elapsedBeats += elapsed / beatDuration;

        if (starPowerEnergy != lastEnergy)
        {
            // Добавляем время пропорционально новой энергии
            float deltaEnergy = starPowerEnergy - lastEnergy;
            float additionalBeats = deltaEnergy * 28f;
            elapsedBeats -= additionalBeats; // Компенсируем добавленную энергию
            lastEnergy = starPowerEnergy;
        }

        starPowerEnergy = Mathf.Max(0f, starPowerEnergy - depletionRate * elapsed);
        float beatsRemaining = starPowerEnergy * 28f; // Оставшиеся биты
        UpdateStarPowerUI();
        yield return null;
    }

    starPowerEnergy = 0f;
    isStarPowerActive = false;
    starPowerCoroutine = null;
    int baseMultiplier = Mathf.Max(1, Mathf.Min(maxMultiplierLimit, 1 + streak / 10));
    multiplier = baseMultiplier;
    if (starPowerEffect != null)
    {
        starPowerEffect.SetActive(false);
    }
    UpdateMultiplier();
    UpdateStarPowerUI();
    UpdateUI();
}


private void HandleWhammyInput()
{
    float currentTime = Time.time - noteSpawner.StartTime;
    bool isWhammyHeld = InputManager.Instance.IsKey("Whammy");
    bool isWhammyReleased = InputManager.Instance.IsKeyUp("Whammy");

    if (isWhammyReleased)
    {
        canStartNewWhammyCycle = true;
        if (whammyEnergyCoroutine != null)
        {
            StopCoroutine(whammyEnergyCoroutine);
            whammyEnergyCoroutine = null;
        }
    }

    UpdateActiveSustainedSpNoteState();

    if (!isWhammyHeld)
    {
        if (whammyEnergyCoroutine != null)
        {
            StopCoroutine(whammyEnergyCoroutine);
            whammyEnergyCoroutine = null;
        }
        return;
    }

    if (activeSustainedSpNoteObj != null && activeSustainedSpNoteController != null && activeSustainedSpNoteData != null)
    {
        float noteTime = noteSpawner.baseObjectTimes.ContainsKey(activeSustainedSpNoteObj) ? noteSpawner.baseObjectTimes[activeSustainedSpNoteObj] : float.MaxValue;
        float nStart = activeSustainedSpNoteData.time;
        float nEnd = nStart + activeSustainedSpNoteData.duration;
        bool sameInstance = Math.Abs(activeSustainedSpNoteData.time - (noteTime - noteOffset)) < starPowerDetectionWindow;
        bool withinWindow = currentTime >= nStart && currentTime <= nEnd;

        if (sameInstance && withinWindow && canStartNewWhammyCycle && whammyEnergyCoroutine == null && !isAddingEnergy)
        {
            whammyEnergyCoroutine = StartCoroutine(SmoothAddWhammyEnergy(activeSustainedSpNoteObj, activeSustainedSpNoteController, activeSustainedSpNoteData));
            canStartNewWhammyCycle = false;
        }
        else if (whammyEnergyCoroutine != null && (!sameInstance || !withinWindow))
        {
            StopCoroutine(whammyEnergyCoroutine);
            whammyEnergyCoroutine = null;
        }
    }
}

private void UpdateActiveSustainedSpNoteState()
{
    if (activeSustainedSpNoteObj == null || activeSustainedSpNoteController == null)
    {
        return;
    }
    if (!noteSpawner.SpawnedObjects.Contains(activeSustainedSpNoteObj) || !activeSustainedSpNoteController.IsSustained || !activeSustainedSpNoteController.IsStarPower)
    {
        activeSustainedSpNoteObj = null;
        activeSustainedSpNoteController = null;
        activeSustainedSpNoteData = null;
    }
}

private void Update()
{
    if (noteSpawner.SpawnedObjects.Count == 0 && !statsPanel.activeSelf) return;

    processedChordsThisFrame.Clear();
    processedStarPowerChordsThisFrame.Clear();
    missedChordsThisFrame.Clear();
    bool missedChordThisFrame = false;
    
    // Оптимизированный один проход по всем нотам
    for (int i = 0; i < noteSpawner.SpawnedObjects.Count; i++)
    {
        var noteObj = noteSpawner.SpawnedObjects[i];
        if (noteObj == null) continue;
        
        NoteController note = noteObj.GetComponent<NoteController>();
        if (note == null || !note.IsActive) continue;
        
        float noteZ = note.transform.position.z;
        bool isMissed = noteZ < -noteInputManager.HitZoneTolerance;
        
        if (isMissed)
        {
            int chordId = Time.frameCount;
            if (!missedNotesSet.Contains(noteObj))
            {
                // Проверяем Miss Forced логику
                if (note.IsForced && UserManager.Instance.GetMissForced())
                {
                    noteInputManager.HandleForcedNoteMiss();
                }
                
                missedNotesSet.Add(noteObj);
                if (!missedChordsThisFrame.Contains(chordId))
                {
                    missedChordsThisFrame.Add(chordId);
                    missedChordThisFrame = true;
                }
            }
            
            // Обработка Star Power нот в том же цикле
            if (missedChordThisFrame && note.IsStarPower)
            {
                int noteId = noteObj.GetInstanceID();
                if (!processedStarPowerNotes.Contains(noteId))
                {
                    processedStarPowerNotes.Add(noteId);
                    if (noteSpawner.baseObjectTimes.ContainsKey(noteObj))
                    {
                        float noteTime = noteSpawner.baseObjectTimes[noteObj];
                        int sectionIndex = ChartParser.StarPowerSections.FindIndex(sp => noteTime >= sp.startTime && noteTime < sp.endTime);
                        if (sectionIndex >= 0)
                        {
                            starPowerSectionSuccess[sectionIndex] = false;
                            Debug.Log($"Missed Star Power note at time={noteTime:F2}s, sectionIndex={sectionIndex}. Starting ReplaceStarPowerNotesInSection.");
                            StartCoroutine(noteSpawner.ReplaceStarPowerNotesInSection(sectionIndex));
                        }
                        else
                        {
                            Debug.LogWarning($"No Star Power section found for note at time={noteTime:F2}s");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Note object {noteObj.name} (ID={noteId}) not found in baseObjectTimes");
                    }
                }
            }
        }
    }
    
    if (missedChordThisFrame)
    {
        missedNotes++;
        ResetStreak();
        UpdateUI();
    }

    // Оптимизированная очистка missedNotesSet
    if (missedNotesSet.Count > 0)
    {
        var toRemove = new List<GameObject>();
        foreach (var note in missedNotesSet)
        {
            if (note == null || !noteSpawner.SpawnedObjects.Contains(note))
            {
                toRemove.Add(note);
            }
        }
        foreach (var note in toRemove)
        {
            missedNotesSet.Remove(note);
        }
    }

    if (noteSpawner.AudioSource.isPlaying)
    {
        hasStartedPlaying = true;
    }

    if (InputManager.Instance.IsKeyDown("StarPower") && starPowerEnergy > 0.25f && starPowerCoroutine == null && !isStarPowerActive)
    {
        starPowerCoroutine = StartCoroutine(SmoothConsumeStarPowerEnergy());
        isStarPowerActive = true;
        if (starPowerEffect != null)
        {
            starPowerEffect.SetActive(true);
        }
        if (starPowerAudioSource != null && starPowerActivateClip != null)
        {
            starPowerAudioSource.PlayOneShot(starPowerActivateClip);
        }
        else if (starPowerAudioSource == null)
        {
            Debug.LogWarning("Cannot play starPowerActivateClip: AudioSource component not found!");
        }
        else if (starPowerActivateClip == null)
        {
            Debug.LogWarning("Cannot play starPowerActivateClip: clip is null!");
        }
        UpdateMultiplier();
        UpdateUI();
    }

    HandleWhammyInput();
    CheckStarPowerSections();
    if (statsPanel.activeSelf)
    {
        if (InputManager.Instance.IsKeyDown("Green"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("SongSelect");
        }
        else if (InputManager.Instance.IsKeyDown("Red"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}

private void AddScore(int points)
{
    score += points * (isStarPowerActive ? multiplier * 2 : multiplier);
}

private void UpdateMultiplier()
{
    int baseMultiplier = Mathf.Max(1, Mathf.Min(maxMultiplierLimit, 1 + streak / 10));
    multiplier = isStarPowerActive ? baseMultiplier * 2 : baseMultiplier;
}

private void ResetStreak()
{
    streak = 0;
    UpdateMultiplier();
}

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = $"{score.ToString("N0").Replace(",", " ")}";
        if (multiplierText != null) multiplierText.text = $"x{multiplier}";
        if (streakText != null) streakText.text = $"{streak}";
        UpdateStarPowerUI();
    }

    private void CalculateTotalNotes()
    {
        if (noteSpawner.CurrentSongData == null || noteSpawner.CurrentSongData.tracks == null)
        {
            Debug.LogError("Song data or tracks are null!");
            totalNotes = 0;
            return;
        }

string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");

var track = noteSpawner.CurrentSongData.tracks.Find(t => t.name == selectedDifficulty);
if (track == null || track.notes == null || track.notes.Count == 0)
{
    Debug.LogError($"Track for difficulty {selectedDifficulty} not found or has no notes!");
    totalNotes = 0;
    return;
}

        HashSet<float> chordTimes = new HashSet<float>();
        foreach (var note in track.notes)
        {
            chordTimes.Add(note.time);
        }

        totalNotes = chordTimes.Count;
    }




    private void HandleCalibrationOffset(float offsetMs)
    {
        if (!IsCalibrationActive() || !IsFirstStage())
        {
            return;
        }

        bool showAccuracy = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex ?? 0}", 1) == 1;
        if (showAccuracy)
        {
            if (offsetText == null)
            {
                Debug.LogError("offsetText is null, cannot show offset!");
                return;
            }
            StartCoroutine(ShowCalibrationOffset(offsetMs));
        }
        else
        {
            Debug.LogWarning("showAccuracy is false, offset text not shown");
        }
    }

    private IEnumerator ShowCalibrationOffset(float offsetMs)
    {
        if (lastOffsetInstance != null)
        {
            StartCoroutine(FadeOutAccuracy(lastOffsetInstance));
        }

        if (offsetText == null)
        {
            Debug.LogError("offsetText is null, cannot instantiate!");
            yield break;
        }

        GameObject offsetInstance = Instantiate(offsetText.gameObject, initialOffsetPosition, Quaternion.identity, offsetText.transform.parent);
        Text offsetTextInstance = offsetInstance.GetComponent<Text>();
        if (offsetTextInstance == null)
        {
            Destroy(offsetInstance);
            yield break;
        }

        lastOffsetInstance = offsetInstance;
        offsetTextInstance.transform.position = initialOffsetPosition;

        int offsetInt = Mathf.RoundToInt(offsetMs);
        string offsetTextStr = offsetInt == 0 ? "0" : (offsetInt > 0 ? $"+{offsetInt}" : $"{offsetInt}");
        offsetTextInstance.text = offsetTextStr;

        Vector3 startPos = initialOffsetPosition;
        Vector3 showPos = startPos + Vector3.right * 15f;
        Vector3 endPos = showPos + Vector3.right * 35f;

        float showDuration = 0.1f;
        float elapsed = 0f;
        while (elapsed < showDuration)
        {
            if (offsetInstance == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / showDuration;
            offsetTextInstance.transform.position = Vector3.Lerp(startPos, showPos, t);
            yield return null;
        }

        float slideDuration = 1.5f;
        float fadeStartTime = slideDuration - 1f;
        elapsed = 0f;
        while (elapsed < slideDuration)
        {
            if (offsetInstance == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            offsetTextInstance.transform.position = Vector3.Lerp(showPos, endPos, t);

            if (elapsed >= fadeStartTime)
            {
                float fadeT = (elapsed - fadeStartTime) / 1f;
                float alpha = 1f - fadeT;
                offsetTextInstance.color = new Color(offsetTextInstance.color.r, offsetTextInstance.color.g, offsetTextInstance.color.b, alpha);
            }
            yield return null;
        }

        if (offsetInstance != null)
        {
            Destroy(offsetInstance);
            if (lastOffsetInstance == offsetInstance) lastOffsetInstance = null;
        }
    }

private IEnumerator ShowAccuracy(NoteInputManager.HitAccuracy accuracy)
{
    // Если есть предыдущий текст, запускаем его исчезновение
    if (lastAccuracyInstance != null)
    {
        StartCoroutine(FadeOutAccuracy(lastAccuracyInstance));
    }

    // Создаём копию текста из сцены
    GameObject accuracyInstance = Instantiate(accuracyText.gameObject, initialAccuracyPosition, Quaternion.identity, accuracyText.transform.parent);
    Text accuracyTextInstance = accuracyInstance.GetComponent<Text>();

    // Сохраняем новый экземпляр как последний
    lastAccuracyInstance = accuracyInstance;

    // Сбрасываем позицию
    accuracyTextInstance.transform.position = initialAccuracyPosition;

    // Настраиваем текст и цвет
    switch (accuracy)
    {
        case NoteInputManager.HitAccuracy.Perfect:
            accuracyTextInstance.text = "PERFECT";
            //accuracyTextInstance.color = new Color(0, 1, 0, 0); // Зелёный, прозрачный изначально
            break;
        case NoteInputManager.HitAccuracy.Early:
            accuracyTextInstance.text = "EARLY";
            //accuracyTextInstance.color = new Color(1, 1, 0, 0); // Жёлтый, прозрачный изначально
            break;
        case NoteInputManager.HitAccuracy.Late:
            accuracyTextInstance.text = "LATE";
            //accuracyTextInstance.color = new Color(1, 0, 0, 0); // Красный, прозрачный изначально
            break;
    }

    // Начальная позиция текста
    Vector3 startPos = initialAccuracyPosition;
    Vector3 showPos = startPos + Vector3.right * 15f; // Выдвигается на 5 вправо
    Vector3 endPos = showPos + Vector3.right * 35f;   // Конечная позиция через 5 секунд

    // Появление (0.1 секунды)
    float showDuration = 0.1f;
    float elapsed = 0f;
    while (elapsed < showDuration)
    {
        if (accuracyInstance == null) yield break;
        elapsed += Time.deltaTime;
        float t = elapsed / showDuration;
        accuracyTextInstance.transform.position = Vector3.Lerp(startPos, showPos, t);
        accuracyTextInstance.color = new Color(accuracyTextInstance.color.r, accuracyTextInstance.color.g, accuracyTextInstance.color.b, t);
        yield return null;
    }

    float slideDuration = 1.5f; // Скольжение с исчезновением (5 секунд всего, исчезновение на последние 0.3 секунды)
    float fadeStartTime = slideDuration - 1f; // Начинаем исчезновение за 0.3 сек до конца
    elapsed = 0f;
    while (elapsed < slideDuration)
    {
        if (accuracyInstance == null) yield break;
        elapsed += Time.deltaTime;
        float t = elapsed / slideDuration;
        accuracyTextInstance.transform.position = Vector3.Lerp(showPos, endPos, t);

        // Исчезновение начинается на 4.7 секунде
        if (elapsed >= fadeStartTime)
        {
            float fadeT = (elapsed - fadeStartTime) / 1f; // Прогресс исчезновения (0 → 1 за 0.3 сек)
            float alpha = 1f - fadeT; // От 1 до 0
            accuracyTextInstance.color = new Color(accuracyTextInstance.color.r, accuracyTextInstance.color.g, accuracyTextInstance.color.b, alpha);
        }
        yield return null;
    }

    // Уничтожаем копию текста
    if (accuracyInstance != null)
    {
        Destroy(accuracyInstance);
        if (lastAccuracyInstance == accuracyInstance) lastAccuracyInstance = null;
    }
}

private IEnumerator FadeOutAccuracy(GameObject instance)
{
    if (instance == null) yield break;

    Text accuracyTextInstance = instance.GetComponent<Text>();
    float fadeDuration = 0.1f; // Быстрое исчезновение за 0.3 секунды
    float elapsed = 0f;

    while (elapsed < fadeDuration)
    {
        if (instance == null) yield break;
        elapsed += Time.deltaTime;
        float t = 1f - (elapsed / fadeDuration);
        accuracyTextInstance.color = new Color(accuracyTextInstance.color.r, accuracyTextInstance.color.g, accuracyTextInstance.color.b, t);
        yield return null;
    }

    if (instance != null) Destroy(instance);
}


private IEnumerator CheckSongEnd()
{
    yield return new WaitUntil(() => hasStartedPlaying && noteSpawner.AudioSource.isPlaying);
    yield return new WaitUntil(() => !noteSpawner.AudioSource.isPlaying && noteSpawner.SpawnedObjects.Count == 0);

    bool isCalibration = PlayerPrefs.GetString("SelectedSong", "") == "calibration";
    if (isCalibration)
    {
        CalibrationManager.Instance.HandleSongEnd();
    }
    else
    {
        ShowStats();
    }
}

    private void ShowStats()
    {
        statsPanel.SetActive(true);
        Time.timeScale = 0f;
        float hitPercentage = totalNotes > 0 ? Mathf.Floor((float)hitNotes / totalNotes * 100f) : 0f;

        statsScoreText.text = $"{score.ToString("N0").Replace(",", " ")}";
        statsProgressText.text = $"{hitPercentage}%";
        string rank = GetRank(hitPercentage);
        string starsDisplay = score > 0 ? new string('★', int.Parse(rank)) : "-";
        statsStarsText.text = score > 0 ? (hitPercentage >= 100f ? $"<color=#ffa200>{starsDisplay}</color>" : starsDisplay) : "-";
        statsTotalNotesText.text = $"{totalNotes}";
        statsMaxStreakText.text = $"{maxStreak}";
        statsHitNotesText.text = $"{hitNotes}";
        statsMissedNotesText.text = $"{missedNotes}";

        // Предыдущие параметры
        string songName = PlayerPrefs.GetString("SelectedSong", "");
        string difficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        string keyPrefix = $"{songName}_{difficulty}";
        int previousScore = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_Score", 0);
        if (previousScore > 0)
        {
            //float previousProgress = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Progress", 0f);
            //string previousRank = UserManager.Instance.GetUserProgressString($"{keyPrefix}_Stars", "0");
            int previousMaxStreak = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_MaxStreak", 0);
            int previousHitNotes = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_HitNotes", 0);
            //int previousMissedNotes = previousTotalNotes - previousHitNotes;
            //float previousAccuracy = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Accuracy", -1f);
            //int previousPerfectHits = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_PerfectHits", 0);
            //int previousEarlyHits = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_EarlyHits", 0);
            //int previousLateHits = UserManager.Instance.GetUserProgressInt($"{keyPrefix}_LateHits", 0);

            statsPreviousScoreText.text = $"{previousScore.ToString("N0").Replace(",", " ")}";
            //statsPreviousProgressText.text = $"Previous Progress: {previousProgress}%";
            //string previousStarsDisplay = new string('★', int.Parse(previousRank));
            //statsPreviousStarsText.text = previousProgress >= 100f ? $"<color=#ffa200>{previousStarsDisplay}</color>" : previousStarsDisplay;
            statsPreviousMaxStreakText.text = $"{previousMaxStreak}";
            statsPreviousHitNotesText.text = $"{previousHitNotes}";
            //statsPreviousMissedNotesText.text = $"Previous Missed: {previousMissedNotes}";
            //statsPreviousAccuracyText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Accuracy: {previousAccuracy:F0}%" : "Previous Accuracy: N/A";
            //statsPreviousPerfectText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Perfect: {previousPerfectHits}" : "Previous Perfect: N/A";
            //statsPreviousEarlyText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Early: {previousEarlyHits}" : "Previous Early: N/A";
            //statsPreviousLateText.text = useAccuracySystem && previousAccuracy >= 0f ? $"Previous Late: {previousLateHits}" : "Previous Late: N/A";
        }
        else
        {
            statsPreviousScoreText.text = "-";
            //statsPreviousProgressText.text = "Previous Progress: -";
            //statsPreviousStarsText.text = "Previous Stars: -";
            statsPreviousMaxStreakText.text = "-";
            statsPreviousHitNotesText.text = "-";
            //statsPreviousMissedNotesText.text = "Previous Missed: -";
            //statsPreviousAccuracyText.text = "Previous Accuracy: -";
            //statsPreviousPerfectText.text = "Previous Perfect: -";
            //statsPreviousEarlyText.text = "Previous Early: -";
            //statsPreviousLateText.text = "Previous Late: -";
        }

        // Обновляем UI песни на statsPanel
        if (statsBandNameText != null)
        {
            statsBandNameText.text = PlayerPrefs.GetString("BandName", "Unknown Artist");
        }

        if (statsSongTitleText != null)
        {
            statsSongTitleText.text = PlayerPrefs.GetString("SongTitle", "Unknown Song");
        }

        if (statsSongCharterText != null)
        {
            string charter = PlayerPrefs.GetString("SongCharter", "Unknown Charter");
            statsSongCharterText.text = string.IsNullOrEmpty(charter) || charter == "Unknown Charter" ? "Unknown Charter" : $"By {charter}";
        }

        if (statsSongCoverImage != null)
        {
            string coverPath = PlayerPrefs.GetString("CoverPath", "default");
            if (coverPath != "default" && File.Exists(coverPath))
            {
                byte[] bytes = File.ReadAllBytes(coverPath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                statsSongCoverImage.sprite = sprite;
            }
            else if (defaultCover != null)
            {
                statsSongCoverImage.sprite = defaultCover;
            }
        }


        float accuracyPercent = 0f;
        bool hasSavedAccuracy = UserManager.Instance.GetUserProgressFloat($"{keyPrefix}_Accuracy", -1f) >= 0f;
        if (useAccuracySystem)
        {
            accuracyPercent = hitNotes > 0 ? (perfectHits + 0.5f * (earlyHits + lateHits)) / hitNotes * 100f : 0f;
        }

        statsAccuracyText.text = useAccuracySystem ? $"{accuracyPercent:F0}%" : "N/A";
        statsPerfectText.text = useAccuracySystem ? $"Perfect: {perfectHits}" : "Perfect: N/A";
        statsEarlyText.text = useAccuracySystem ? $"Early: {earlyHits}" : "Early: N/A";
        statsLateText.text = useAccuracySystem ? $"Late: {lateHits}" : "Late: N/A";

        // Используем UserManager для сохранения прогресса
        if (score > previousScore)
        {
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_Score", score);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_Stars", rank);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_Progress", hitPercentage);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_MaxStreak", maxStreak);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_HitNotes", hitNotes);
            UserManager.Instance.SaveUserProgress($"{keyPrefix}_MissedNotes", missedNotes);
            if (useAccuracySystem)
            {
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_Accuracy", accuracyPercent);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_PerfectHits", perfectHits);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_EarlyHits", earlyHits);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_LateHits", lateHits);
            }
            else
            {
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_Accuracy", -1f);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_PerfectHits", 0);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_EarlyHits", 0);
                UserManager.Instance.SaveUserProgress($"{keyPrefix}_LateHits", 0);
            }
        }
    }

    private void OnDestroy()
    {
        if (noteInputManager != null)
        {
            noteInputManager.OnNoteHit -= HandleNoteHit;
            noteInputManager.OnNoteSustainEnd -= HandleNoteSustainEnd;
            noteInputManager.OnNoteSustainTick -= HandleNoteSustainTick;
        }
        OnCalibrationHit -= HandleCalibrationHit;
        Debug.Log("ScoreManager destroyed, OnCalibrationHit unsubscribed");
    }

    private string GetRank(float hitPercentage)
    {
        if (hitPercentage >= 90f) return "5";
        if (hitPercentage >= 70f) return "4";
        if (hitPercentage >= 50f) return "3";
        if (hitPercentage >= 30f) return "2";
        return "1";
    }
}