using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private Toggle leftyFlipToggle;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private Text speedValueText;
    [SerializeField] private Slider fpsSlider;
    [SerializeField] private Text fpsValueText;
    [SerializeField] private Toggle accuracySystemToggle; // Новый чекбокс Accuracy System
    [SerializeField] private Toggle showAccuracyToggle;   // Переименованный чекбокс (был accuracyToggle)
    [SerializeField] private Toggle useFourFretsToggle; // Новый toggle
    [SerializeField] private Toggle extendedForcedNotesToggle; // Чекбокс для расширенного диапазона forced нот
    [SerializeField] private Toggle missForcedToggle; // Чекбокс Miss Forced
    [SerializeField] private Toggle arrowKeyErrorsToggle; // Чекбокс для ошибок при нажатии стрелок

    [SerializeField] private Transform fretboardContentPanel;
    [SerializeField] private GameObject fretboardButtonTemplate;

    // Поля для настройки клавиш
    [SerializeField] private Text greenKeyText;
    [SerializeField] private Button greenKeyButton;
    [SerializeField] private Text redKeyText;
    [SerializeField] private Button redKeyButton;
    [SerializeField] private Text yellowKeyText;
    [SerializeField] private Button yellowKeyButton;
    [SerializeField] private Text blueKeyText;
    [SerializeField] private Button blueKeyButton;
    [SerializeField] private Text orangeKeyText;
    [SerializeField] private Button orangeKeyButton;
    [SerializeField] private Text strumUpKeyText;
    [SerializeField] private Button strumUpKeyButton;
    [SerializeField] private Text strumDownKeyText;
    [SerializeField] private Button strumDownKeyButton;
    [SerializeField] private Text starPowerKeyText;
    [SerializeField] private Button starPowerKeyButton;
    [SerializeField] private Text startKeyText;
    [SerializeField] private Button startKeyButton;
    [SerializeField] private Text whammyKeyText;
    [SerializeField] private Button whammyKeyButton;

[SerializeField] private Toggle enableOpenNotesToggle;
[SerializeField] private Toggle openNotesFiveStringToggle;
[SerializeField] private Toggle openNotesOneStringToggle;

    [SerializeField] private Button clearSongProgressButton;
    [SerializeField] private Button resetSettingsButton;
    [SerializeField] private Button deleteAccountButton;
    [SerializeField] private Button clearAllButton;

[SerializeField] private Slider offsetSlider; // Слайдер для audioOffset
[SerializeField] private Text offsetValueText; // Текст для значения оффсета
[SerializeField] private Button calibrationButton; // Кнопка калибровки

    private KeyCode? previousKey; // Хранит предыдущую клавишу перед изменением

    private bool isWaitingForKey = false;
    private string currentKeyToChange;

    private void Start()
    {
        leftyFlipToggle.isOn = PlayerPrefs.GetInt("LeftyFlip", 0) == 1;
        speedSlider.value = PlayerPrefs.GetFloat("NoteSpeed", 5f);
        fpsSlider.value = PlayerPrefs.GetInt($"TargetFPS_{UserManager.Instance.GetCurrentUser()?.userIndex}", 250);
        offsetSlider.value = PlayerPrefs.GetFloat("AudioOffset", 0f);
        accuracySystemToggle.isOn = PlayerPrefs.GetInt($"AccuracySystem_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
        showAccuracyToggle.isOn = PlayerPrefs.GetInt($"ShowAccuracy_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
        useFourFretsToggle.isOn = PlayerPrefs.GetInt("UseFourFrets", 0) == 1;
        enableOpenNotesToggle.isOn = PlayerPrefs.GetInt($"OpenNotesEnabled_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
        openNotesFiveStringToggle.isOn = PlayerPrefs.GetInt($"OpenNotesFiveString_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
        openNotesOneStringToggle.isOn = PlayerPrefs.GetInt($"OpenNotesOneString_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1;
    extendedForcedNotesToggle.isOn = PlayerPrefs.GetInt($"ExtendedForcedNotes_{UserManager.Instance.GetCurrentUser()?.userIndex}", 0) == 1; // Новое
        missForcedToggle.isOn = PlayerPrefs.GetInt($"MissForced_{UserManager.Instance.GetCurrentUser()?.userIndex}", 1) == 1; // Новое
        arrowKeyErrorsToggle.isOn = PlayerPrefs.GetInt($"ArrowKeyErrors_{UserManager.Instance.GetCurrentUser()?.userIndex}", 0) == 1; // Новое

        UpdateSpeedText(speedSlider.value);;
UpdateFPSText(fpsSlider.value);
        UpdateOffsetText(offsetSlider.value); // Обновляем текст оффсета
        UpdateAccuracyTogglesVisibility();
        UpdateOpenNotesTogglesVisibility();

        // Слайдер загружен из PlayerPrefs, но движок до этого мог не получить лимит (старые билды / только что залогинились).
        if (UserManager.Instance.GetCurrentUser() != null)
            UserManager.ApplyTargetFps(Mathf.RoundToInt(fpsSlider.value));

        leftyFlipToggle.onValueChanged.AddListener(SetLeftyFlip);
        speedSlider.onValueChanged.AddListener(SetNoteSpeed);
fpsSlider.onValueChanged.AddListener(SetTargetFPS);
        offsetSlider.onValueChanged.AddListener(SetAudioOffset);
        calibrationButton.onClick.AddListener(StartCalibration);
        accuracySystemToggle.onValueChanged.AddListener(SetAccuracySystem);
        showAccuracyToggle.onValueChanged.AddListener(SetShowAccuracy);
        useFourFretsToggle.onValueChanged.AddListener(SetUseFourFrets);
        enableOpenNotesToggle.onValueChanged.AddListener(SetOpenNotesEnabled);
        openNotesFiveStringToggle.onValueChanged.AddListener(SetOpenNotesFiveString);
        openNotesOneStringToggle.onValueChanged.AddListener(SetOpenNotesOneString);
    extendedForcedNotesToggle.onValueChanged.AddListener(SetExtendedForcedNotes); // Новое
        missForcedToggle.onValueChanged.AddListener(SetMissForced); // Новое
        arrowKeyErrorsToggle.onValueChanged.AddListener(SetArrowKeyErrors); // Новое

        LoadFretboards();
        LoadKeyBindings();

        clearSongProgressButton.onClick.AddListener(ClearSongProgress);
        resetSettingsButton.onClick.AddListener(ResetSettings);
        deleteAccountButton.onClick.AddListener(DeleteAccount);
        clearAllButton.onClick.AddListener(ClearAll);
    }

private void StartCalibration()
{
    string songName = "Calibration";
    string artist = "Messiah Flesh";
    string coverPath = "default";

    // Читаем чарт-файл
    string chartText = null;
    if (Application.isEditor)
    {
        string chartPath = Path.Combine(Application.dataPath, "Resources/Sounds/calibration/notes.chart");
        if (File.Exists(chartPath))
        {
            try
            {
                chartText = File.ReadAllText(chartPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error reading chart file: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"Chart file not found at: {chartPath}");
        }
    }
    else
    {
        string chartResourcePath = "Sounds/calibration/notes";
        TextAsset chartAsset = Resources.Load<TextAsset>(chartResourcePath);
        if (chartAsset != null)
        {
            chartText = chartAsset.text;
        }
    }

    if (chartText != null)
    {
        try
        {
            string[] lines = chartText.Split('\n');
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("Name ="))
                {
                    songName = trimmedLine.Split('=')[1].Trim();
                }
                else if (trimmedLine.StartsWith("Artist ="))
                {
                    artist = trimmedLine.Split('=')[1].Trim();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parsing chart file: {e.Message}");
        }
    }

    // Ищем кавер
    if (Application.isEditor)
    {
        string[] possibleExtensions = { ".png", ".jpg", ".jpeg" };
        foreach (string ext in possibleExtensions)
        {
            string potentialCoverPath = Path.Combine(Application.dataPath, "Resources/Sounds/calibration/cover" + ext);
            if (File.Exists(potentialCoverPath))
            {
                coverPath = potentialCoverPath;
                break;
            }
        }
    }
    else
    {
        string coverResourcePath = "Sounds/calibration/cover";
        Texture2D coverTexture = Resources.Load<Texture2D>(coverResourcePath);
        if (coverTexture != null)
        {
            coverPath = coverResourcePath; // Сохраняем относительный путь для билда
        }
    }

    // Устанавливаем PlayerPrefs
    PlayerPrefs.SetString("SongTitle", songName);
    PlayerPrefs.SetString("BandName", artist);
    PlayerPrefs.SetString("CoverPath", coverPath);
    PlayerPrefs.SetString("SelectedDifficulty", "ExpertSingle");
    PlayerPrefs.SetInt("CalibrationStage", 1);
    PlayerPrefs.SetString("SelectedSong", "calibration");
    PlayerPrefs.Save();

    CalibrationManager.Instance.StartCalibration();
}

private void UpdateOffsetText(float offset)
{
    int offsetInt = Mathf.RoundToInt(offset * 1000f); // Переводим секунды в миллисекунды и округляем
    string offsetText = offsetInt == 0 ? "0" : (offsetInt > 0 ? $"+{offsetInt}" : $"{offsetInt}");
    offsetValueText.text = offsetText;
}

    private void SetAudioOffset(float offset)
    {
        UserManager.Instance.UpdateAudioOffset(offset);
        UpdateOffsetText(offset);
    }

    private void Update()
    {
        if (isWaitingForKey)
        {
            // Проверяем нажатие Esc для отмены
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log($"Key assignment cancelled for {currentKeyToChange}, reverting to previous key.");
                UpdateKeyText(currentKeyToChange); // Возвращаем старый текст
                isWaitingForKey = false;
                previousKey = null;
                return;
            }

            // Оптимизированная проверка только разумных клавиш для игры
            // вместо итерации через все значения enum KeyCode
            KeyCode[] gameKeys = {
                // Буквы (основные для игры)
                KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F, KeyCode.G, KeyCode.H,
                KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P,
                KeyCode.Q, KeyCode.R, KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
                KeyCode.Y, KeyCode.Z,
                // Цифры (для быстрого доступа)
                KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
                KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
                // Стрелки (основные для страм)
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                // Модификаторы (часто используются)
                KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
                // Специальные клавиши (основные для игры)
                KeyCode.Space, KeyCode.Return, KeyCode.Tab,
                // Функциональные клавиши (могут пригодиться)
                KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
                // Дополнительные полезные клавиши
                KeyCode.Comma, KeyCode.Period, KeyCode.Semicolon, KeyCode.Quote,
                KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Backslash, KeyCode.Slash
            };
            
            for (int i = 0; i < gameKeys.Length; i++)
            {
                KeyCode key = gameKeys[i];
                if (Input.GetKeyDown(key))
                {
                    // Проверяем, используется ли эта клавиша в других привязках
                    if (IsKeyAlreadyAssigned(key, currentKeyToChange))
                    {
                        GetTextForKey(currentKeyToChange).text = "This key is busy";
                        StartCoroutine(ResetKeyTextAfterDelay(currentKeyToChange));
                        return;
                    }

                    // Сохраняем новую клавишу через InputManager
                    InputManager.Instance.UpdateKeyBinding(currentKeyToChange, key);
                    UpdateKeyText(currentKeyToChange);
                    isWaitingForKey = false;
                    previousKey = null;
                    break;
                }
            }
        }
    }

    private void SetLeftyFlip(bool isLefty)
    {
        UserManager.Instance.UpdateLeftyFlip(isLefty);
    }

    private void SetNoteSpeed(float speed)
    {
        UserManager.Instance.UpdateNoteSpeed(speed);
        UpdateSpeedText(speed);
    }

    private void SetAccuracySystem(bool enabled)
    {
        UserManager.Instance.UpdateAccuracySystem(enabled);
        UpdateAccuracyTogglesVisibility();
        if (!enabled)
        {
            showAccuracyToggle.isOn = false;
            UserManager.Instance.UpdateShowAccuracy(false);
        }
    }

    private void SetShowAccuracy(bool enabled)
    {
        UserManager.Instance.UpdateShowAccuracy(enabled);
    }

    private void UpdateAccuracyTogglesVisibility()
    {
        bool isEnabled = accuracySystemToggle.isOn;
        showAccuracyToggle.gameObject.SetActive(isEnabled);
    }

    private void SetUseFourFrets(bool useFourFrets)
    {
        UserManager.Instance.UpdateUseFourFrets(useFourFrets);
    }

    private void UpdateSpeedText(float speed)
    {
        speedValueText.text = speed.ToString("F1");
    }

private void SetTargetFPS(float fps)
{
    int fpsInt = Mathf.RoundToInt(fps);
    UserManager.Instance.UpdateTargetFPS(fpsInt);
    UpdateFPSText(fpsInt);
    UserManager.ApplyTargetFps(fpsInt);
}

private void UpdateFPSText(float fps)
{
    fpsValueText.text = Mathf.RoundToInt(fps).ToString();
}

    private void LoadFretboards()
    {
        string fretboardsPath = Application.isEditor
            ? Path.Combine(Application.dataPath, "fretboards")
            : Path.Combine(Directory.GetCurrentDirectory(), "fretboards");

        if (!Directory.Exists(fretboardsPath))
        {
            Debug.LogError("Папка fretboards не найдена!");
            return;
        }

        foreach (string file in Directory.GetFiles(fretboardsPath, "*.png"))
        {
            string textureName = Path.GetFileNameWithoutExtension(file);
            byte[] bytes = File.ReadAllBytes(file);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);

            GameObject newButton = Instantiate(fretboardButtonTemplate, fretboardContentPanel);
            newButton.SetActive(true);

            RawImage buttonImage = newButton.GetComponentInChildren<RawImage>();
            if (buttonImage != null)
            {
                buttonImage.texture = texture;
            }

            Button button = newButton.GetComponent<Button>();
            button.onClick.AddListener(() => SelectFretboard(textureName));
        }
    }

    private void SelectFretboard(string textureName)
    {
        UserManager.Instance.UpdateSelectedFretboard(textureName);
        Debug.Log($"Selected fretboard: {textureName}");
    }

    private void LoadKeyBindings()
    {
        // Загружаем значения через InputManager
        greenKeyText.text = $"{InputManager.Instance.GetKey("Green")}";
        redKeyText.text = $"{InputManager.Instance.GetKey("Red")}";
        yellowKeyText.text = $"{InputManager.Instance.GetKey("Yellow")}";
        blueKeyText.text = $"{InputManager.Instance.GetKey("Blue")}";
        orangeKeyText.text = $"{InputManager.Instance.GetKey("Orange")}";
        strumUpKeyText.text = $"{InputManager.Instance.GetKey("StrumUp")}";
        strumDownKeyText.text = $"{InputManager.Instance.GetKey("StrumDown")}";
        starPowerKeyText.text = $"{InputManager.Instance.GetKey("StarPower")}";
        startKeyText.text = $"{InputManager.Instance.GetKey("Start")}";
        whammyKeyText.text = $"{InputManager.Instance.GetKey("Whammy")}";

        // Привязываем кнопки
        greenKeyButton.onClick.AddListener(() => StartKeyChange("Green"));
        redKeyButton.onClick.AddListener(() => StartKeyChange("Red"));
        yellowKeyButton.onClick.AddListener(() => StartKeyChange("Yellow"));
        blueKeyButton.onClick.AddListener(() => StartKeyChange("Blue"));
        orangeKeyButton.onClick.AddListener(() => StartKeyChange("Orange"));
        strumUpKeyButton.onClick.AddListener(() => StartKeyChange("StrumUp"));
        strumDownKeyButton.onClick.AddListener(() => StartKeyChange("StrumDown"));
        starPowerKeyButton.onClick.AddListener(() => StartKeyChange("StarPower"));
        startKeyButton.onClick.AddListener(() => StartKeyChange("Start"));
        whammyKeyButton.onClick.AddListener(() => StartKeyChange("Whammy"));
    }

    private void StartKeyChange(string keyName)
    {
        isWaitingForKey = true;
        currentKeyToChange = keyName;
        previousKey = InputManager.Instance.GetKey(keyName); // Сохраняем текущую клавишу
        GetTextForKey(keyName).text = "Press a key...";
    }

    private void UpdateKeyText(string keyName)
    {
        Text keyText = GetTextForKey(keyName);
        keyText.text = $"{InputManager.Instance.GetKey(keyName)}";
    }

    private Text GetTextForKey(string keyName)
    {
        switch (keyName)
        {
            case "Green": return greenKeyText;
            case "Red": return redKeyText;
            case "Yellow": return yellowKeyText;
            case "Blue": return blueKeyText;
            case "Orange": return orangeKeyText;
            case "StrumUp": return strumUpKeyText;
            case "StrumDown": return strumDownKeyText;
            case "StarPower": return starPowerKeyText;
            case "Start": return startKeyText;
            case "Whammy": return whammyKeyText;
            default: return null;
        }
    }

    private bool IsKeyAlreadyAssigned(KeyCode key, string currentKeyName)
    {
        string[] keyNames = { "Green", "Red", "Yellow", "Blue", "Orange", "StrumUp", "StrumDown", "StarPower", "Start", "Whammy" };
        foreach (string keyName in keyNames)
        {
            if (keyName == currentKeyName) continue; // Пропускаем текущую кнопку
            if (InputManager.Instance.GetKey(keyName) == key)
            {
                Debug.Log($"Key {key} is already assigned to {keyName}");
                return true;
            }
        }
        return false;
    }

    private IEnumerator ResetKeyTextAfterDelay(string keyName)
    {
        yield return new WaitForSeconds(1.5f); // Показываем сообщение 1.5 секунды
        if (isWaitingForKey) // Проверяем, что мы всё ещё в режиме ожидания
        {
            GetTextForKey(keyName).text = "Press a key..."; // Возвращаем текст "Press a key..."
        }
    }

private void UpdateOpenNotesTogglesVisibility()
{
    bool isEnabled = enableOpenNotesToggle.isOn;
    openNotesFiveStringToggle.gameObject.SetActive(isEnabled);
    openNotesOneStringToggle.gameObject.SetActive(isEnabled);
}

private void SetOpenNotesEnabled(bool enabled)
{
    UserManager.Instance.UpdateOpenNotesSettings(enabled, openNotesFiveStringToggle.isOn, openNotesOneStringToggle.isOn);
    UpdateOpenNotesTogglesVisibility();
}

    private void SetOpenNotesFiveString(bool enabled)
    {
        // Обновляем настройки
        UserManager.Instance.UpdateOpenNotesSettings(enableOpenNotesToggle.isOn, enabled, openNotesOneStringToggle.isOn);

        // Если обе дочерние настройки выключены, выключаем openNotes
        if (!enabled && !openNotesOneStringToggle.isOn)
        {
            enableOpenNotesToggle.isOn = false;
            UserManager.Instance.UpdateOpenNotesSettings(false, false, false);
        }

        UpdateOpenNotesTogglesVisibility();
    }

    private void SetOpenNotesOneString(bool enabled)
    {
        // Обновляем настройки
        UserManager.Instance.UpdateOpenNotesSettings(enableOpenNotesToggle.isOn, openNotesFiveStringToggle.isOn, enabled);

        // Если обе дочерние настройки выключены, выключаем openNotes
        if (!enabled && !openNotesFiveStringToggle.isOn)
        {
            enableOpenNotesToggle.isOn = false;
            UserManager.Instance.UpdateOpenNotesSettings(false, false, false);
        }

        UpdateOpenNotesTogglesVisibility();
    }

private void SetExtendedForcedNotes(bool enabled)
{
    UserManager.Instance.UpdateExtendedForcedNotes(enabled);
}

private void SetMissForced(bool enabled)
{
    UserManager.Instance.UpdateMissForced(enabled);
}

private void SetArrowKeyErrors(bool enabled)
{
    UserManager.Instance.UpdateArrowKeyErrors(enabled);
}

private void ClearSongProgress()
{
    UserProfile currentUser = UserManager.Instance.GetCurrentUser();
    if (currentUser == null) return;

    string songsPath = Application.isEditor
        ? Path.Combine(Application.dataPath, "songs")
        : Path.Combine(Directory.GetCurrentDirectory(), "songs");

    if (!Directory.Exists(songsPath))
    {
        Debug.LogError("Папка songs не найдена!");
        return;
    }

    string[] difficulties = { "EasySingle", "MediumSingle", "HardSingle", "ExpertSingle" };
    string[] songFolders = Directory.GetDirectories(songsPath);
    foreach (string folder in songFolders)
    {
        string songName = Path.GetFileName(folder);
        for (int i = 0; i < difficulties.Length; i++)
        {
            string keyPrefix = $"{currentUser.userIndex}_{songName}_{difficulties[i]}";
            PlayerPrefs.DeleteKey($"{keyPrefix}_Score");
            PlayerPrefs.DeleteKey($"{keyPrefix}_Progress");
            PlayerPrefs.DeleteKey($"{keyPrefix}_Accuracy");
            PlayerPrefs.DeleteKey($"{keyPrefix}_Stars");
            PlayerPrefs.DeleteKey($"{keyPrefix}_BestRating");
        }
    }

    PlayerPrefs.Save();
    // Пересчитываем рейтинг и обновляем UI
    UserManager.Instance.CalculateUserRating(); // Пересчитываем рейтинг (он обнулится, так как данные удалены)
    UserManager.Instance.UpdateUserDisplay();   // Обновляем отображение в UI
    Debug.Log("Song progress cleared for current user.");
}

private void ResetSettings()
{
    UserProfile currentUser = UserManager.Instance.GetCurrentUser();
    if (currentUser == null) return;

    // Сбрасываем настройки
    UserManager.Instance.UpdateLeftyFlip(false);
    UserManager.Instance.UpdateNoteSpeed(5f);
    UserManager.Instance.UpdateTargetFPS(60);
    UserManager.ApplyTargetFps(60);
    UserManager.Instance.UpdateAccuracySystem(true); // По умолчанию включено
    UserManager.Instance.UpdateShowAccuracy(true);   // По умолчанию включено
    UserManager.Instance.UpdateSelectedFretboard("default"); // Пустое значение для стандартного fretboard
    UserManager.Instance.UpdateUseFourFrets(false);
    UserManager.Instance.UpdateAudioOffset(0f); // Сбрасываем оффсет
    UserManager.Instance.UpdateExtendedForcedNotes(false); // Новое
    UserManager.Instance.UpdateMissForced(true); // По умолчанию включено
    UserManager.Instance.UpdateArrowKeyErrors(false); // По умолчанию выключено

    // Сбрасываем привязки клавиш
    Dictionary<string, KeyCode> defaultBindings = new Dictionary<string, KeyCode>
    {
        { "Green", KeyCode.V },
        { "Red", KeyCode.C },
        { "Yellow", KeyCode.X },
        { "Blue", KeyCode.Z },
        { "Orange", KeyCode.LeftShift },
        { "StrumUp", KeyCode.UpArrow },
        { "StrumDown", KeyCode.DownArrow },
        { "StarPower", KeyCode.Space },
        { "Start", KeyCode.Return },
        { "Whammy", KeyCode.RightControl }
    };
    foreach (var binding in defaultBindings)
    {
        UserManager.Instance.UpdateKeyBinding(binding.Key, binding.Value);
    }
    // Сбрасываем настройки открытых нот
    UserManager.Instance.UpdateOpenNotesSettings(true, true, true);
    enableOpenNotesToggle.isOn = true;
    openNotesFiveStringToggle.isOn = true;
    openNotesOneStringToggle.isOn = true;
    UpdateOpenNotesTogglesVisibility();

    // Обновляем UI
    leftyFlipToggle.isOn = false;
    speedSlider.value = 5f;
fpsSlider.value = 60;
    accuracySystemToggle.isOn = true;
    showAccuracyToggle.isOn = true;
    UpdateAccuracyTogglesVisibility();
    useFourFretsToggle.isOn = false;
    offsetSlider.value = 0f; // Сбрасываем слайдер
    extendedForcedNotesToggle.isOn = false; // Новое
    missForcedToggle.isOn = true; // По умолчанию включено
    arrowKeyErrorsToggle.isOn = false; // По умолчанию выключено
    LoadKeyBindings();

    Debug.Log("User settings reset to default.");
}

private void DeleteAccount()
{
    UserProfile currentUser = UserManager.Instance.GetCurrentUser();
    if (currentUser == null) return;

    List<UserProfile> users = UserManager.Instance.GetAllUsers();
    int currentIndex = users.IndexOf(currentUser);
    if (currentIndex >= 0)
    {
        users.RemoveAt(currentIndex);
        UserManager.Instance.SaveUsers();
        UserManager.Instance.SetCurrentUser(-1); // Сбрасываем текущего пользователя
        SceneManager.LoadScene("MainMenu"); // Переходим в главное меню
        Debug.Log("Current account deleted.");
    }
}

private void ClearAll()
{
    // Удаляем всех пользователей
    UserManager.Instance.GetAllUsers().Clear();
    UserManager.Instance.SaveUsers();
    UserManager.Instance.SetCurrentUser(-1);

    // Очищаем все ключи в PlayerPrefs
    PlayerPrefs.DeleteAll();
    PlayerPrefs.Save();

    // Переходим в главное меню
    SceneManager.LoadScene("MainMenu");
    Debug.Log("All data cleared.");
}
}