using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

public class UserManager : MonoBehaviour
{
    public static UserManager Instance { get; private set; }

    [SerializeField] private Image userAvatarImage; // UI Image для аватарки
    [SerializeField] private Text userNameText; // UI Text для имени
    [SerializeField] private Sprite defaultAvatar; // Стандартная аватарка
    [SerializeField] private Text ratingText; // Текст рейтинга

    private List<UserProfile> users = new List<UserProfile>();
    private UserProfile currentUser;
    private const string USERS_KEY = "UsersList";
    private const string CURRENT_USER_KEY = "CurrentUserIndex";

    // Событие, вызываемое при переключении пользователя
    public delegate void UserChangedHandler();
    public event UserChangedHandler OnUserChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Сохраняем объект между сценами
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadUsers();
        LoadCurrentUser();
        ApplyFramerateForCurrentUser();
        UpdateUserDisplay();
    }

    /// <summary>
    /// Применяет сохранённый лимит FPS текущего пользователя. Без активного профиля — VSync в меню, чтобы GPU не гонять тысячи FPS.
    /// </summary>
    public void ApplyFramerateForCurrentUser()
    {
        if (currentUser != null)
            ApplyTargetFps(currentUser.targetFPS);
        else
            ApplyTargetFps(0);
    }

    public static void ApplyTargetFps(int fpsInt)
    {
        if (fpsInt <= 0)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            return;
        }

        if (fpsInt < 120)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fpsInt;
        }
        else
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        FindUserProfileUI(); // Ищем UI-элементы в новой сцене
        UpdateUserDisplay();
    }

    private void FindUserProfileUI()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogWarning("Canvas GameObject not found in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }

        Transform userProfileTransform = canvas.transform.Find("UserProfile");
        if (userProfileTransform == null)
        {
            Debug.LogWarning("UserProfile GameObject not found inside Canvas in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }

        GameObject userProfile = userProfileTransform.gameObject;
        Transform avatarTransform = userProfile.transform.Find("Avatar");
        Transform nameTransform = userProfile.transform.Find("Name");
        Transform ratingTransform = userProfile.transform.Find("RatingText");

        if (avatarTransform == null)
        {
            Debug.LogWarning("UserProfile found, but 'Avatar' child not found in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        else
        {
            userAvatarImage = avatarTransform.GetComponent<Image>();
            if (userAvatarImage == null)
            {
                Debug.LogWarning("UserProfile found, 'Avatar' child found, but no Image component on 'Avatar' in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }

        if (nameTransform == null)
        {
            Debug.LogWarning("UserProfile found, but 'Name' child not found in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        else
        {
            userNameText = nameTransform.GetComponent<Text>();
            if (userNameText == null)
            {
                Debug.LogWarning("UserProfile found, 'Name' child found, but no Text component on 'Name' in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }
        if (ratingTransform == null)
        {
            Debug.LogWarning("UserProfile found, but 'RatingText' child not found in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        else
        {
            ratingText = ratingTransform.GetComponent<Text>();
            if (ratingText == null)
            {
                Debug.LogWarning("UserProfile found, 'RatingText' child found, but no Text component on 'RatingText' in scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }
    }

    public void LoadUsers()
    {
        users.Clear();
        string usersJson = PlayerPrefs.GetString(USERS_KEY, "");
        if (!string.IsNullOrEmpty(usersJson))
        {
            UserListWrapper wrapper = JsonUtility.FromJson<UserListWrapper>(usersJson);
            users = wrapper.users;

            // Инициализируем keyBindings для каждого пользователя после десериализации
            foreach (var user in users)
            {
                if (user.keyBindings == null)
                {
                    user.keyBindings = new Dictionary<string, KeyCode>
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
                }
                
                // Загружаем настройки из PlayerPrefs, если они отсутствуют в JSON
                if (!user.keyBindings.ContainsKey("arrowKeyErrors"))
                {
                    user.arrowKeyErrors = PlayerPrefs.GetInt($"ArrowKeyErrors_{user.userIndex}", 0) == 1;
                }
            }
        }
    }

    public void SaveUsers()
    {
        UserListWrapper wrapper = new UserListWrapper { users = users };
        string usersJson = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(USERS_KEY, usersJson);
        PlayerPrefs.Save();
    }

    public void AddUser(string name, string avatarBase64)
    {
        int newIndex = users.Count; // Индекс нового пользователя
        UserProfile newUser = new UserProfile(name, avatarBase64, newIndex);
        users.Add(newUser);
        SetCurrentUser(newIndex);
        SaveUsers();
    }

    public void UpdateUser(int index, string newUserName, string newAvatarBase64)
    {
        if (index < 0 || index >= users.Count)
        {
            Debug.LogError($"Invalid user index for updating: {index}");
            return;
        }

        users[index].userName = newUserName;
        users[index].avatarBase64 = newAvatarBase64;
        // userIndex остаётся неизменным
        if (currentUser == users[index])
        {
            currentUser = users[index]; // Обновляем currentUser, если редактируем текущего пользователя
            UpdateUserDisplay();
        }
        SaveUsers();
    }

    public void SetCurrentUser(int index)
    {
        if (index >= 0 && index < users.Count)
        {
            currentUser = users[index];
            PlayerPrefs.SetInt(CURRENT_USER_KEY, index);
            // Загружаем настройки пользователя в PlayerPrefs
            LoadUserSettingsToPlayerPrefs();
            // Перезагружаем привязки клавиш в InputManager
            InputManager.Instance.ReloadKeyBindings();
            PlayerPrefs.Save();
            ApplyFramerateForCurrentUser();
            UpdateUserDisplay();
            // Уведомляем подписчиков о смене пользователя
            OnUserChanged?.Invoke();
        }
        else
        {
            currentUser = null;
            PlayerPrefs.SetInt(CURRENT_USER_KEY, -1);
            PlayerPrefs.Save();
            ApplyFramerateForCurrentUser();
            UpdateUserDisplay();
            // Уведомляем подписчиков о смене пользователя
            OnUserChanged?.Invoke();
        }
    }

    public UserProfile GetCurrentUser()
    {
        return currentUser;
    }

    public List<UserProfile> GetAllUsers()
    {
        return users;
    }

    private void LoadCurrentUser()
    {
        int currentIndex = PlayerPrefs.GetInt(CURRENT_USER_KEY, -1);
        if (currentIndex >= 0 && currentIndex < users.Count)
        {
            currentUser = users[currentIndex];
            // Загружаем настройки пользователя в PlayerPrefs
            LoadUserSettingsToPlayerPrefs();
            // Перезагружаем привязки клавиш в InputManager
            InputManager.Instance.ReloadKeyBindings();
        }
        else
        {
            currentUser = null;
        }
    }

    public void UpdateUserDisplay()
    {
        if (currentUser != null)
        {
            if (userAvatarImage != null)
            {
                userAvatarImage.sprite = currentUser.GetAvatarSprite(defaultAvatar);
                userAvatarImage.gameObject.SetActive(true);
            }
            if (userNameText != null)
            {
                userNameText.text = currentUser.userName;
                userNameText.gameObject.SetActive(true);
            }
            if (ratingText != null)
            {
                float rating = CalculateUserRating();
                ratingText.text = $"★ {rating:F2}";
                ratingText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (userAvatarImage != null) userAvatarImage.gameObject.SetActive(false);
            if (userNameText != null) userNameText.gameObject.SetActive(false);
            if (ratingText != null) ratingText.gameObject.SetActive(false);
        }
    }

public void UpdateOpenNotesSettings(bool enableOpenNotes, bool fiveString, bool oneString)
{
    if (currentUser != null)
    {
        currentUser.enableOpenNotes = enableOpenNotes;
        currentUser.openNotesFiveString = fiveString;
        currentUser.openNotesOneString = oneString;
        PlayerPrefs.SetInt($"OpenNotesEnabled_{currentUser.userIndex}", enableOpenNotes ? 1 : 0);
        PlayerPrefs.SetInt($"OpenNotesFiveString_{currentUser.userIndex}", fiveString ? 1 : 0);
        PlayerPrefs.SetInt($"OpenNotesOneString_{currentUser.userIndex}", oneString ? 1 : 0);
        SaveUsers();
        OnUserChanged?.Invoke();
    }
}
public void UpdateExtendedForcedNotes(bool enabled)
{
    if (currentUser != null)
    {
        currentUser.extendedForcedNotes = enabled;
        PlayerPrefs.SetInt($"ExtendedForcedNotes_{currentUser.userIndex}", enabled ? 1 : 0);
        SaveUsers();
    }
}

public void UpdateMissForced(bool enabled)
{
    if (currentUser != null)
    {
        currentUser.missForced = enabled;
        PlayerPrefs.SetInt($"MissForced_{currentUser.userIndex}", enabled ? 1 : 0);
        SaveUsers();
    }
}

public void UpdateArrowKeyErrors(bool enabled)
{
    if (currentUser != null)
    {
        currentUser.arrowKeyErrors = enabled;
        PlayerPrefs.SetInt($"ArrowKeyErrors_{currentUser.userIndex}", enabled ? 1 : 0);
        SaveUsers();
    }
}

public bool GetMissForced()
{
    if (currentUser != null)
    {
        return currentUser.missForced;
    }
    return true; // По умолчанию включено
}

public bool GetArrowKeyErrors()
{
    if (currentUser != null)
    {
        return currentUser.arrowKeyErrors;
    }
    return false; // По умолчанию выключено
}

public bool GetExtendedForcedNotes()
{
    if (currentUser != null)
    {
        return currentUser.extendedForcedNotes;
    }
    return false; // По умолчанию выключено
}
    private void LoadUserSettingsToPlayerPrefs()
    {
        if (currentUser == null) return;

        // Загружаем настройки пользователя в PlayerPrefs
        PlayerPrefs.SetInt("LeftyFlip", currentUser.leftyFlip ? 1 : 0);
        PlayerPrefs.SetFloat("NoteSpeed", currentUser.noteSpeed);
        PlayerPrefs.SetInt($"TargetFPS_{currentUser.userIndex}", currentUser.targetFPS);
        PlayerPrefs.SetInt($"AccuracySystem_{currentUser.userIndex}", currentUser.useAccuracySystem ? 1 : 0);
        PlayerPrefs.SetInt($"ShowAccuracy_{currentUser.userIndex}", currentUser.showAccuracy ? 1 : 0);
        PlayerPrefs.SetString("SelectedFretboard", currentUser.selectedFretboard);
        PlayerPrefs.SetInt("UseFourFrets", currentUser.useFourFrets ? 1 : 0);
        PlayerPrefs.SetFloat("AudioOffset", currentUser.audioOffset);
        PlayerPrefs.SetInt($"OpenNotesEnabled_{currentUser.userIndex}", currentUser.enableOpenNotes ? 1 : 0);
        PlayerPrefs.SetInt($"OpenNotesFiveString_{currentUser.userIndex}", currentUser.openNotesFiveString ? 1 : 0);
        PlayerPrefs.SetInt($"OpenNotesOneString_{currentUser.userIndex}", currentUser.openNotesOneString ? 1 : 0);
        PlayerPrefs.SetInt($"ExtendedForcedNotes_{currentUser.userIndex}", currentUser.extendedForcedNotes ? 1 : 0);
        PlayerPrefs.SetInt($"MissForced_{currentUser.userIndex}", currentUser.missForced ? 1 : 0);
        PlayerPrefs.SetInt($"ArrowKeyErrors_{currentUser.userIndex}", currentUser.arrowKeyErrors ? 1 : 0);

        // Загружаем привязки клавиш для конкретного пользователя
        LoadUserKeyBindings();
        
        // И в общие ключи для InputManager
        foreach (var binding in currentUser.keyBindings)
        {
            PlayerPrefs.SetInt($"Key_{binding.Key}", (int)binding.Value);
        }
    }
    
    // Загружаем привязки клавиш конкретного пользователя
    private void LoadUserKeyBindings()
    {
        if (currentUser == null) return;
        
        currentUser.keyBindings = new Dictionary<string, KeyCode>
        {
            { "Green", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Green", (int)KeyCode.V) },
            { "Red", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Red", (int)KeyCode.C) },
            { "Yellow", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Yellow", (int)KeyCode.X) },
            { "Blue", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Blue", (int)KeyCode.Z) },
            { "Orange", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Orange", (int)KeyCode.LeftShift) },
            { "StrumUp", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_StrumUp", (int)KeyCode.UpArrow) },
            { "StrumDown", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_StrumDown", (int)KeyCode.DownArrow) },
            { "StarPower", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_StarPower", (int)KeyCode.Space) },
            { "Start", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Start", (int)KeyCode.Return) },
            { "Whammy", (KeyCode)PlayerPrefs.GetInt($"User_{currentUser.userIndex}_Key_Whammy", (int)KeyCode.RightControl) }
        };
    }

    // Методы для обновления настроек текущего пользователя
    public void UpdateLeftyFlip(bool isLefty)
    {
        if (currentUser != null)
        {
            currentUser.leftyFlip = isLefty;
            PlayerPrefs.SetInt("LeftyFlip", isLefty ? 1 : 0);
            SaveUsers();
        }
    }

    public void UpdateNoteSpeed(float speed)
    {
        if (currentUser != null)
        {
            currentUser.noteSpeed = speed;
            PlayerPrefs.SetFloat("NoteSpeed", speed);
            SaveUsers();
        }
    }

public void UpdateTargetFPS(int fps)
{
    if (currentUser != null)
    {
        currentUser.targetFPS = fps;
        PlayerPrefs.SetInt($"TargetFPS_{currentUser.userIndex}", fps);
        SaveUsers();
    }
}

    public void UpdateAccuracySystem(bool enabled)
    {
        if (currentUser != null)
        {
            currentUser.useAccuracySystem = enabled;
            PlayerPrefs.SetInt($"AccuracySystem_{currentUser.userIndex}", enabled ? 1 : 0);
            SaveUsers();
        }
    }

    public void UpdateShowAccuracy(bool enabled)
    {
        if (currentUser != null)
        {
            currentUser.showAccuracy = enabled;
            PlayerPrefs.SetInt($"ShowAccuracy_{currentUser.userIndex}", enabled ? 1 : 0);
            SaveUsers();
        }
    }

    public void UpdateUseFourFrets(bool useFourFrets)
    {
        if (currentUser != null)
        {
            currentUser.useFourFrets = useFourFrets; // Добавим поле в UserProfile
            PlayerPrefs.SetInt("UseFourFrets", useFourFrets ? 1 : 0);
            SaveUsers();
        }
    }

    public void UpdateSelectedFretboard(string fretboardName)
    {
        if (currentUser != null)
        {
            currentUser.selectedFretboard = fretboardName;
            PlayerPrefs.SetString("SelectedFretboard", fretboardName);
            SaveUsers();
        }
    }

    public void UpdateKeyBinding(string keyName, KeyCode newKey)
    {
        if (currentUser != null)
        {
            currentUser.keyBindings[keyName] = newKey;
            // Сохраняем привязку для конкретного пользователя
            PlayerPrefs.SetInt($"User_{currentUser.userIndex}_Key_{keyName}", (int)newKey);
            // И в общий ключ для InputManager
            PlayerPrefs.SetInt($"Key_{keyName}", (int)newKey);
            PlayerPrefs.Save();
            // Перезагружаем привязки клавиш в InputManager
            InputManager.Instance.ReloadKeyBindings();
        }
    }

// Новый метод для обновления audioOffset
public void UpdateAudioOffset(float offset)
{
    if (currentUser != null)
    {
        currentUser.audioOffset = offset;
        PlayerPrefs.SetFloat("AudioOffset", offset);
        SaveUsers(); // SaveUsers уже вызывает PlayerPrefs.Save()
    }
}

    // Для сохранения прогресса с префиксом пользователя
    public void SaveUserProgress(string key, int value)
    {
        if (currentUser != null)
        {
            PlayerPrefs.SetInt($"{currentUser.userIndex}_{key}", value);
            PlayerPrefs.Save();
        }
    }

    public void SaveUserProgress(string key, float value)
    {
        if (currentUser != null)
        {
            PlayerPrefs.SetFloat($"{currentUser.userIndex}_{key}", value);
            PlayerPrefs.Save();
        }
    }

    public void SaveUserProgress(string key, string value)
    {
        if (currentUser != null)
        {
            PlayerPrefs.SetString($"{currentUser.userIndex}_{key}", value);
            PlayerPrefs.Save();
        }
    }

    public int GetUserProgressInt(string key, int defaultValue = 0)
    {
        if (currentUser != null)
        {
            return PlayerPrefs.GetInt($"{currentUser.userIndex}_{key}", defaultValue);
        }
        return defaultValue;
    }

    public float GetUserProgressFloat(string key, float defaultValue = 0f)
    {
        if (currentUser != null)
        {
            return PlayerPrefs.GetFloat($"{currentUser.userIndex}_{key}", defaultValue);
        }
        return defaultValue;
    }

    public string GetUserProgressString(string key, string defaultValue = "")
    {
        if (currentUser != null)
        {
            return PlayerPrefs.GetString($"{currentUser.userIndex}_{key}", defaultValue);
        }
        return defaultValue;
    }

public float CalculateUserRating()
{
    if (GetCurrentUser() == null) return 0f; // Если нет пользователя, рейтинг 0

    float totalRating = 0f; // Общий рейтинг
    string[] difficulties = { "EasySingle", "MediumSingle", "HardSingle", "ExpertSingle" }; // Сложности
    float[] progressWeights = { 0.10f, 0.20f, 0.30f, 0.40f }; // Веса за прогресс
    float[] accuracyWeights = { 0.10f, 0.20f, 0.30f, 0.40f }; // Базовые рейтинги для точности

    // Загружаем список песен из папки songs
    string songsPath = Application.isEditor
        ? Path.Combine(Application.dataPath, "songs")
        : Path.Combine(Directory.GetCurrentDirectory(), "songs");

    if (!Directory.Exists(songsPath))
    {
        Debug.LogError("Папка songs не найдена!");
        return 0f;
    }

    string[] songFolders = Directory.GetDirectories(songsPath);
    foreach (string folder in songFolders)
    {
        string songName = Path.GetFileName(folder);
        // Для каждой песни считаем лучший рейтинг по каждой сложности
        for (int i = 0; i < difficulties.Length; i++)
        {
            if (songName == "calibration") continue; // Пропускаем калибровку
            string keyPrefix = $"{songName}_{difficulties[i]}";
            int score = GetUserProgressInt($"{keyPrefix}_Score", -1);
            if (score == -1) continue; // Песня не сыграна на этой сложности

            // Получаем текущие прогресс, hitNotes и totalNotes из последней попытки
            float progress = GetUserProgressFloat($"{keyPrefix}_Progress", 0f) / 100f; // 0-1
            int hitNotes = GetUserProgressInt($"{keyPrefix}_HitNotes", 0);
            int totalNotes = GetUserProgressInt($"{keyPrefix}_TotalNotes", 0);

            // Считаем рейтинг для этой попытки
            float progressContribution = progressWeights[i] * progress;

            float accuracyContribution;
            if (totalNotes > 0 && hitNotes >= 0)
            {
                float hitPercentage = (float)hitNotes / totalNotes;
                float noteCountWeight = 1f / (1f + Mathf.Exp(-0.05f * (totalNotes - 50f)));
                accuracyContribution = accuracyWeights[i] * hitPercentage * noteCountWeight;
                Debug.Log($"Песня: {songName}, Сложность: {difficulties[i]}, HitNotes={hitNotes}, TotalNotes={totalNotes}, HitPercentage={hitPercentage*100:F1}%, NoteCountWeight={noteCountWeight:F3}, AccuracyContribution={accuracyContribution:F2}");
            }
            else
            {
                accuracyContribution = 0f; // Нет нот или неверные данные
                Debug.LogWarning($"Песня: {songName}, Сложность: {difficulties[i]} — TotalNotes={totalNotes}, HitNotes={hitNotes}, AccuracyContribution=0");
            }

            float currentAttemptRating = progressContribution + accuracyContribution;

            // Получаем лучший сохранённый рейтинг для этой песни и сложности
            float bestRating = GetUserProgressFloat($"{keyPrefix}_BestRating", 0f);

            // Если текущий рейтинг больше сохранённого, обновляем лучший результат
            if (currentAttemptRating > bestRating)
            {
                SaveUserProgress($"{keyPrefix}_BestRating", currentAttemptRating);
                SaveUserProgress($"{keyPrefix}_AccuracyContribution", accuracyContribution); // Для отладки
                bestRating = currentAttemptRating;
            }

            // Добавляем лучший рейтинг к общему
            totalRating += bestRating;
        }
    }

    Debug.Log($"Общий рейтинг пользователя: {totalRating:F2}");
    return totalRating;
}
}

[System.Serializable]
public class UserListWrapper
{
    public List<UserProfile> users;
}