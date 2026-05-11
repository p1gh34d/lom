using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using System;

public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance { get; private set; }

    private NoteInputManager noteInputManager;
    private NoteSpawner noteSpawner;
    private Text calibrationHintText;
    private List<float> hitTimings = new List<float>();
    private const float VALID_HIT_THRESHOLD = 0.5f;
    private const float EXPECTED_INTERVAL = 1f;

    private ScoreManager scoreManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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
        if (scene.name == "GameScene")
        {
            InitializeReferences();
            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager != null)
            {
                Debug.Log($"ScoreManager found, instance ID={scoreManager.GetInstanceID()}, GameObject={scoreManager.gameObject.name}");
            }
            else
            {
                Debug.LogError("ScoreManager not found in GameScene! Check if ScoreManager component exists in scene.");
            }
        }
        UpdateHintText();
    }

    private void InitializeReferences()
    {
        noteInputManager = FindObjectOfType<NoteInputManager>();
        if (noteInputManager == null)
        {
            Debug.LogError("CalibrationManager: NoteInputManager not found in GameScene!");
        }

        noteSpawner = FindObjectOfType<NoteSpawner>();
        if (noteSpawner == null)
        {
            Debug.LogError("CalibrationManager: NoteSpawner not found in GameScene!");
        }

        Debug.Log($"CalibrationManager: References initialized. NoteInputManager={(noteInputManager != null ? "Found" : "Null")}, NoteSpawner={(noteSpawner != null ? "Found" : "Null")}, CalibrationHintText={(calibrationHintText != null ? "Found" : "Null")}");
    }

    public void StartCalibration()
    {
        Debug.Log("PlayerPrefs cleared for testing");
        PlayerPrefs.SetInt("CalibrationStage", 1);
        PlayerPrefs.SetString("SelectedSong", "calibration");
        int userIndex = UserManager.Instance.GetCurrentUser()?.userIndex ?? 0;
        PlayerPrefs.SetInt($"ShowAccuracy_{userIndex}", 1);
        PlayerPrefs.Save();
    Debug.Log($"Starting calibration: CalibrationStage=1, SelectedSong=calibration, ShowAccuracy_{userIndex}={PlayerPrefs.GetInt($"ShowAccuracy_{userIndex}")}");
    SceneManager.LoadScene("PanelScene"); // Переходим в PanelScene
    }

private void Update()
{
    if (!IsCalibrationActive() || !IsFirstStage() || noteInputManager == null || noteSpawner == null)
    {
        return;
    }

    bool enableOpenNotes = PlayerPrefs.GetInt($"OpenNotesEnabled_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
    bool strumPressed = InputManager.Instance.IsKeyDown("StrumUp") || InputManager.Instance.IsKeyDown("StrumDown");
    bool validInput;

    if (enableOpenNotes)
    {
        // Для открытых нот: только Strum без клавиш
        validInput = noteInputManager.GetPressedKeys().Count == 0 && strumPressed;
    }
    else
    {
        // Для обычных нот: любая клавиша + Strum
        validInput = noteInputManager.GetPressedKeys().Count > 0 && strumPressed;
    }

    if (validInput)
    {
        float hitTime = Time.time - noteSpawner.StartTime;
        hitTimings.Add(hitTime);
        Debug.Log($"Strum pressed at {hitTime:F3} s");

        float expectedNoteTime = Mathf.Round(hitTime / EXPECTED_INTERVAL) * EXPECTED_INTERVAL;
        float offset = hitTime - expectedNoteTime;
        float offsetMs = offset * 1000f;
        Debug.Log($"Offset calculated: {offsetMs:F2} ms");

        if (scoreManager != null)
        {
            scoreManager.TriggerCalibrationHit(offsetMs);
        }
        else
        {
            Debug.LogError("ScoreManager is null, cannot trigger CalibrationHit!");
        }
    }
}

    public void HandleSongEnd()
    {
        if (!IsCalibrationActive()) return;

        if (IsFirstStage())
        {
            Debug.Log("Конец калибровочного трека: первый этап");
            CalculateAndSaveAudioOffset();
            StartCoroutine(TransitionToSecondStage());
        }
        else
        {
            Debug.Log("Конец калибровочного трека: второй этап");
            StartCoroutine(TransitionToSettings());
        }
    }

private void CalculateAndSaveAudioOffset()
{
    Debug.Log($"CalculateAndSaveAudioOffset вызван, hitTimings.Count={hitTimings.Count}");
    if (hitTimings.Count == 0)
    {
        Debug.LogWarning("No hit timings recorded, offset unchanged");
        return;
    }

    float totalOffset = 0f;
    int validHits = 0;

    foreach (var hitTime in hitTimings)
    {
        float expectedNoteTime = Mathf.Round(hitTime / EXPECTED_INTERVAL) * EXPECTED_INTERVAL;
        float offset = hitTime - expectedNoteTime;
        Debug.Log($"Hit: {hitTime:F3} s, Expected: {expectedNoteTime:F3} s, Offset: {offset:F3} s");
        if (Mathf.Abs(offset) < VALID_HIT_THRESHOLD)
        {
            totalOffset += offset;
            validHits++;
        }
    }

    if (validHits > 0)
    {
            float averageOffset = totalOffset / validHits;
            UserManager.Instance.UpdateAudioOffset(averageOffset);
            Debug.Log($"Calculated audioOffset: {averageOffset:F3} s, saved");
    }
    else
    {
        Debug.LogWarning("No valid hits for calibration");
    }

    hitTimings.Clear();
}

    private IEnumerator TransitionToSecondStage()
    {
        PlayerPrefs.SetInt("CalibrationStage", 2);
        PlayerPrefs.Save();
        SceneManager.LoadScene("PanelScene"); // Переходим в PanelScene
        yield return null;
    }

    private IEnumerator TransitionToSettings()
    {
        PlayerPrefs.SetInt("CalibrationStage", 0);
        PlayerPrefs.DeleteKey("SongTitle");
        PlayerPrefs.DeleteKey("BandName");
        PlayerPrefs.DeleteKey("CoverPath");
        PlayerPrefs.DeleteKey("SelectedSong");
        PlayerPrefs.Save();
        SceneManager.LoadScene("PanelScene"); // Переходим в PanelScene
        yield return null;
    }

    private bool IsCalibrationActive()
    {
        return PlayerPrefs.GetString("SelectedSong", "") == "calibration";
    }

    private bool IsFirstStage()
    {
        return PlayerPrefs.GetInt("CalibrationStage", 1) == 1;
    }

    private void UpdateHintText()
    {
        if (calibrationHintText != null)
        {
            calibrationHintText.text = IsCalibrationActive() && IsFirstStage() ? "Жми на слух!" : "";
        }
    }
}