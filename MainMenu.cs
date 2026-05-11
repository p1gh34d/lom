using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

public class MainMenu : MonoBehaviour
{
[SerializeField] private GameObject startButton; // Кнопка "СТАРТ"
[SerializeField] private GameObject settingsButton; // Кнопка "НАСТРОЙКИ"
[SerializeField] private GameObject exitButton; // Кнопка "ВЫХОД"
[SerializeField] private GameObject exitConfirmPanel; // Панель подтверждения выхода
[SerializeField] private GameObject yesButton; // Кнопка "Yes" на панели подтверждения
[SerializeField] private GameObject noButton; // Кнопка "No" на панели подтверждения

    [SerializeField] private GameObject userSelectPanel; // Панель выбора пользователя
    [SerializeField] private GameObject createAccountPanel; // Панель создания/редактирования аккаунта
    [SerializeField] private Transform userButtonContainer; // Контейнер для кнопок пользователей
    [SerializeField] private GameObject userButtonPrefab; // Теперь это шаблон в сцене (UserButtonTemplate)
    [SerializeField] private GameObject createAccountButton; // Кнопка "Create Account"
    [SerializeField] private Text startPromptText; // Надпись "Press START to create an account"
    [SerializeField] private Text loginPromptText; // Новая надпись для случаев, когда есть аккаунты, но нет текущего пользователя
    [SerializeField] private Image createAccountAvatarImage; // Аватарка в окне создания
    [SerializeField] private InputField createAccountNameInput; // Поле ввода имени
    [SerializeField] private Sprite defaultAvatar; // Стандартная аватарка

    // Новые поля для окна выбора аватарки
    [SerializeField] private GameObject avatarSelectPanel; // Панель выбора аватарки
    [SerializeField] private Transform avatarListContent; // Контейнер Content внутри ScrollView
    [SerializeField] private GameObject avatarButtonTemplate; // Шаблон кнопки аватарки


private int activeMainButtonIndex = 0; // Индекс активной кнопки главного меню (0=Start, 1=Settings, 2=Exit)
private bool isConfirmExitOpen = false; // Флаг панели подтверждения
private int activeConfirmButtonIndex = 0; // Индекс активной кнопки подтверждения (0=Yes, 1=No)
private List<GameObject> mainButtons = new List<GameObject>(); // Список кнопок главного меню
    private bool isUserSelectOpen = false;
    private bool isCreateAccountOpen = false;
    private bool isAvatarSelectOpen = false;
    private bool isEditingProfile = false; // Флаг для режима редактирования
    private int editingProfileIndex = -1; // Индекс редактируемого профиля
    private int activeUserButtonIndex = 0;
    private float promptBlinkTimer = 0f;
    private bool promptVisible = true;
    private string pendingAvatarBase64; // Временное хранение аватарки
    private List<GameObject> userButtons = new List<GameObject>(); // Список кнопок пользователей

    // Для выбора аватарки
    private List<Sprite> availableAvatars = new List<Sprite>(); // Список доступных аватарок
    private List<string> avatarFileNames = new List<string>(); // Список имён файлов аватарок
    private List<GameObject> avatarButtons = new List<GameObject>(); // Список кнопок аватарок
    private int selectedAvatarIndex = 0;

private void Start()
{
    userSelectPanel.SetActive(false);
    createAccountPanel.SetActive(false);
    avatarSelectPanel.SetActive(false);

    if (createAccountButton != null)
    {
        createAccountButton.SetActive(true);
        if (createAccountButton.transform.parent == userButtonContainer)
        {
            createAccountButton.transform.SetParent(userButtonContainer.parent, false);
        }
    }

    if (userButtonPrefab != null)
    {
        userButtonPrefab.SetActive(false);
    }

    if (avatarButtonTemplate != null)
    {
        avatarButtonTemplate.SetActive(false);
    }

    // Инициализируем новый текст
    if (loginPromptText != null)
    {
        loginPromptText.gameObject.SetActive(false);
    }

    // Инициализируем панель подтверждения
    if (exitConfirmPanel != null)
    {
        exitConfirmPanel.SetActive(false);
    }

    // Заполняем список кнопок главного меню
    mainButtons.Clear();
    mainButtons.Add(startButton);
    mainButtons.Add(settingsButton);
    mainButtons.Add(exitButton);

    // Проверяем, что все кнопки назначены
    foreach (var button in mainButtons)
    {
        if (button == null)
        {
            Debug.LogError("Одна из кнопок главного меню (Start, Settings, Exit) не назначена в инспекторе!");
            return;
        }
    }

    LoadAvatarsFromFolder();
    UpdatePromptVisibility();
    UpdateUserButtons();

    // Устанавливаем первую кнопку активной
    SetActiveMainButton(0);
}

private void Update()
{
    var users = UserManager.Instance.GetAllUsers();
    var currentUser = UserManager.Instance.GetCurrentUser();

    // Мигающая надпись
    promptBlinkTimer += Time.deltaTime;
    if (promptBlinkTimer >= 0.5f)
    {
        promptVisible = !promptVisible;
        if (users.Count == 0)
        {
            startPromptText.gameObject.SetActive(promptVisible);
        }
        else if (currentUser == null && loginPromptText != null)
        {
            loginPromptText.gameObject.SetActive(promptVisible);
        }
        promptBlinkTimer = 0f;
    }

    // Управление вводом
    if (!isUserSelectOpen && !isCreateAccountOpen && !isAvatarSelectOpen && !isConfirmExitOpen)
    {
        // Переключение кнопок главного меню
        if (InputManager.Instance.IsKeyDown("StrumUp") && activeMainButtonIndex > 0)
        {
            SetActiveMainButton(activeMainButtonIndex - 1);
        }
        else if (InputManager.Instance.IsKeyDown("StrumDown") && activeMainButtonIndex < mainButtons.Count - 1)
        {
            SetActiveMainButton(activeMainButtonIndex + 1);
        }

    // Открытие панели выбора аккаунта на Start
    if (InputManager.Instance.IsKeyDown("Start"))
    {
        isUserSelectOpen = true;
        userSelectPanel.SetActive(true);
        UpdateUserButtons();
        SetActiveUserButton(0);
    }

        // Выбор кнопки на Green
        if (InputManager.Instance.IsKeyDown("Green"))
        {
            switch (activeMainButtonIndex)
            {
                case 0: // Start
                    StartGame();
                    break;
                case 1: // Settings
                    OpenSettings();
                    break;
                case 2: // Exit
                    OpenExitConfirmation();
                    break;
            }
        }

        // Выход на Red
        if (InputManager.Instance.IsKeyDown("Red"))
        {
            OpenExitConfirmation();
        }
    }
    else if (isConfirmExitOpen)
    {
        // Управление панелью подтверждения
        if (InputManager.Instance.IsKeyDown("StrumUp") || InputManager.Instance.IsKeyDown("StrumDown"))
        {
            activeConfirmButtonIndex = activeConfirmButtonIndex == 0 ? 1 : 0;
            SetActiveConfirmButton(activeConfirmButtonIndex);
        }

        if (InputManager.Instance.IsKeyDown("Green"))
        {
            if (activeConfirmButtonIndex == 0) // Yes
            {
                ExitGame();
            }
            else // No
            {
                CloseExitConfirmation();
            }
        }

        if (InputManager.Instance.IsKeyDown("Red"))
        {
            CloseExitConfirmation();
        }
    }

    // Оставляем остальную логику без изменений
    if (isUserSelectOpen)
    {
        int totalSelectableButtons = userButtons.Count + (createAccountButton != null ? 1 : 0);

        if (InputManager.Instance.IsKeyDown("StrumUp") && activeUserButtonIndex > 0)
        {
            SetActiveUserButton(activeUserButtonIndex - 1);
        }
        else if (InputManager.Instance.IsKeyDown("StrumDown") && activeUserButtonIndex < totalSelectableButtons - 1)
        {
            SetActiveUserButton(activeUserButtonIndex + 1);
        }

        if (InputManager.Instance.IsKeyDown("Green"))
        {
            if (activeUserButtonIndex == userButtons.Count)
            {
                OpenCreateAccount();
            }
            else
            {
                UserManager.Instance.SetCurrentUser(activeUserButtonIndex);
                CloseUserSelect();
            }
        }

        if (InputManager.Instance.IsKeyDown("Blue") && activeUserButtonIndex < userButtons.Count)
        {
            OpenEditProfile(activeUserButtonIndex);
        }

        if (InputManager.Instance.IsKeyDown("Red"))
        {
            CloseUserSelect();
        }
    }

    if (isCreateAccountOpen)
    {
        if (InputManager.Instance.IsKeyDown("Orange"))
        {
            OpenAvatarSelect();
        }

        if (InputManager.Instance.IsKeyDown("Green") && !string.IsNullOrEmpty(createAccountNameInput.text))
        {
            if (isEditingProfile)
            {
                UpdateProfile();
            }
            else
            {
                CreateAccount();
            }
        }

        if (InputManager.Instance.IsKeyDown("Red"))
        {
            CloseCreateAccount();
        }
    }

    if (isAvatarSelectOpen)
    {
        if (InputManager.Instance.IsKeyDown("StrumUp") && selectedAvatarIndex > 0)
        {
            selectedAvatarIndex--;
            UpdateAvatarSelection();
        }
        else if (InputManager.Instance.IsKeyDown("StrumDown") && selectedAvatarIndex < avatarButtons.Count - 1)
        {
            selectedAvatarIndex++;
            UpdateAvatarSelection();
        }

        if (InputManager.Instance.IsKeyDown("Green"))
        {
            SelectAvatar();
            CloseAvatarSelect();
        }

        if (InputManager.Instance.IsKeyDown("Red"))
        {
            CloseAvatarSelect();
        }
    }
}

private void SetActiveMainButton(int index)
{
    activeMainButtonIndex = Mathf.Clamp(index, 0, mainButtons.Count - 1);

    for (int i = 0; i < mainButtons.Count; i++)
    {
        Transform imageTransform = mainButtons[i].transform.Find("Image");
        Transform contentTransform = mainButtons[i].transform.Find("Content");

        if (imageTransform == null)
        {
            Debug.LogError($"Image not found in {mainButtons[i].name}!");
            continue;
        }
        if (contentTransform == null)
        {
            Debug.LogError($"Content not found in {mainButtons[i].name}!");
            continue;
        }

        // Для неактивных кнопок: скрываем Image, Content на X = 0
        if (i != activeMainButtonIndex)
        {
            imageTransform.gameObject.SetActive(false);
            contentTransform.localPosition = new Vector3(0f, contentTransform.localPosition.y, contentTransform.localPosition.z);
        }
        // Для активной кнопки: показываем Image, сдвигаем Content на X = 50
        else
        {
            imageTransform.gameObject.SetActive(true);
            contentTransform.localPosition = new Vector3(50f, contentTransform.localPosition.y, contentTransform.localPosition.z);
        }
    }
}

private void OpenExitConfirmation()
{
    isConfirmExitOpen = true;
    exitConfirmPanel.SetActive(true);
    activeConfirmButtonIndex = 0; // По умолчанию "Yes"
    SetActiveConfirmButton(activeConfirmButtonIndex);
}

private void CloseExitConfirmation()
{
    isConfirmExitOpen = false;
    exitConfirmPanel.SetActive(false);
}

private void SetActiveConfirmButton(int index)
{
    activeConfirmButtonIndex = Mathf.Clamp(index, 0, 1); // 0 = Yes, 1 = No

    List<GameObject> confirmButtons = new List<GameObject> { yesButton, noButton };
    for (int i = 0; i < confirmButtons.Count; i++)
    {
        Transform imageTransform = confirmButtons[i].transform.Find("Image");
        Transform contentTransform = confirmButtons[i].transform.Find("Content");

        if (imageTransform == null)
        {
            Debug.LogError($"Image not found in {confirmButtons[i].name}!");
            continue;
        }
        if (contentTransform == null)
        {
            Debug.LogError($"Content not found in {confirmButtons[i].name}!");
            continue;
        }

        // Для неактивных кнопок: скрываем Image, Content на X = 0
        if (i != activeConfirmButtonIndex)
        {
            imageTransform.gameObject.SetActive(false);
            contentTransform.localPosition = new Vector3(0f, contentTransform.localPosition.y, contentTransform.localPosition.z);
        }
        // Для активной кнопки: показываем Image, сдвигаем Content на X = 50
        else
        {
            imageTransform.gameObject.SetActive(true);
            contentTransform.localPosition = new Vector3(50f, contentTransform.localPosition.y, contentTransform.localPosition.z);
        }
    }
}

private void UpdatePromptVisibility()
{
    var users = UserManager.Instance.GetAllUsers();
    var currentUser = UserManager.Instance.GetCurrentUser();

    if (users.Count == 0)
    {
        // Нет аккаунтов — показываем StartPromptText
        startPromptText.gameObject.SetActive(true);
        if (loginPromptText != null) loginPromptText.gameObject.SetActive(false);
    }
    else if (currentUser == null)
    {
        // Есть аккаунты, но нет текущего пользователя — показываем LoginPromptText
        startPromptText.gameObject.SetActive(false);
        if (loginPromptText != null) loginPromptText.gameObject.SetActive(true);
    }
    else
    {
        // Есть текущий пользователь — скрываем обе надписи
        startPromptText.gameObject.SetActive(false);
        if (loginPromptText != null) loginPromptText.gameObject.SetActive(false);
    }
}

    private void UpdateUserButtons()
    {
        // Очищаем старые кнопки, кроме createAccountButton и userButtonPrefab (шаблон)
        foreach (Transform child in userButtonContainer)
        {
            if (child.gameObject != createAccountButton && child.gameObject != userButtonPrefab)
            {
                Destroy(child.gameObject);
            }
        }

        // Очищаем список кнопок
        userButtons.Clear();

        // Создаём кнопки для каждого пользователя
        var users = UserManager.Instance.GetAllUsers();
        for (int i = 0; i < users.Count; i++)
        {
            if (userButtonPrefab == null)
            {
                Debug.LogError("userButtonPrefab (UserButtonTemplate) is not assigned in MainMenu Inspector!");
                return;
            }

            GameObject button = Instantiate(userButtonPrefab, userButtonContainer);
            if (button == null)
            {
                Debug.LogError("Failed to instantiate userButtonPrefab!");
                continue;
            }

            // Включаем кнопку, так как шаблон изначально отключён
            button.SetActive(true);

            UserProfile user = users[i];
            Transform contentTransform = button.transform.Find("Content");
            if (contentTransform == null)
            {
                Debug.LogError($"Content not found in userButtonPrefab for user {user.userName}!");
                continue;
            }

            Transform avatarTransform = contentTransform.Find("AvatarImage");
            Transform nameTransform = contentTransform.Find("NameText");

            if (avatarTransform == null)
            {
                Debug.LogError($"AvatarImage not found in Content of userButtonPrefab for user {user.userName}!");
                continue;
            }
            if (nameTransform == null)
            {
                Debug.LogError($"NameText not found in Content of userButtonPrefab for user {user.userName}!");
                continue;
            }

            Image avatarImage = avatarTransform.GetComponent<Image>();
            Text nameText = nameTransform.GetComponent<Text>();

            if (avatarImage == null)
            {
                Debug.LogError($"AvatarImage component not found in userButtonPrefab for user {user.userName}!");
                continue;
            }
            if (nameText == null)
            {
                Debug.LogError($"NameText component not found in userButtonPrefab for user {user.userName}!");
                continue;
            }
            if (defaultAvatar == null)
            {
                Debug.LogError("defaultAvatar is not assigned in MainMenu Inspector!");
                return;
            }

            avatarImage.sprite = user.GetAvatarSprite(defaultAvatar);
            nameText.text = user.userName;

            // Добавляем кнопку в список
            userButtons.Add(button);
        }

        // Убедимся, что createAccountButton добавлена в конец
        if (createAccountButton != null)
        {
            createAccountButton.transform.SetParent(userButtonContainer, false);
            createAccountButton.transform.SetAsLastSibling();
        }
    }

    private void SetActiveUserButton(int index)
    {
        int totalSelectableButtons = userButtons.Count + (createAccountButton != null ? 1 : 0);
        activeUserButtonIndex = Mathf.Clamp(index, 0, totalSelectableButtons - 1);

        // Сбрасываем позицию всех кнопок
        for (int i = 0; i < userButtonContainer.childCount; i++)
        {
            Transform button = userButtonContainer.GetChild(i);
            // Пропускаем userButtonPrefab, так как он отключён
            if (button.gameObject == userButtonPrefab) continue;

            Transform content = button.Find("Content");
            Transform image = button.Find("Image"); // Предполагаем, что изображение активности здесь
            if (content != null)
            {
                content.localPosition = new Vector3(0f, content.localPosition.y, content.localPosition.z);
            }
            if (image != null)
            {
                image.gameObject.SetActive(false); // Скрываем изображение для предыдущей кнопки
            }
        }

        // Устанавливаем активную кнопку
        if (activeUserButtonIndex < userButtons.Count)
        {
            // Активная кнопка — это кнопка пользователя
            Transform content = userButtons[activeUserButtonIndex].transform.Find("Content");
            if (content != null)
            {
                content.localPosition = new Vector3(50f, content.localPosition.y, content.localPosition.z);
            }
        }
        else if (createAccountButton != null)
        {
            // Активная кнопка — это createAccountButton
            Transform content = createAccountButton.transform.Find("Content");
            Transform image = createAccountButton.transform.Find("Image");
            if (content != null)
            {
                content.localPosition = new Vector3(50f, content.localPosition.y, content.localPosition.z);
            }
            if (image != null)
            {
                image.gameObject.SetActive(true); // Показываем изображение для активной кнопки
            }
        }
    }

    private void CloseUserSelect()
    {
        isUserSelectOpen = false;
        userSelectPanel.SetActive(false);
        UpdatePromptVisibility();
    }

    private void OpenCreateAccount()
    {
        isEditingProfile = false; // Режим создания нового профиля
        editingProfileIndex = -1;

        isUserSelectOpen = false;
        userSelectPanel.SetActive(false);
        isCreateAccountOpen = true;
        createAccountPanel.SetActive(true);
        createAccountNameInput.text = "";

        // Устанавливаем случайную аватарку по умолчанию
        if (availableAvatars.Count > 0)
        {
            selectedAvatarIndex = Random.Range(0, availableAvatars.Count);
            createAccountAvatarImage.sprite = availableAvatars[selectedAvatarIndex];
            pendingAvatarBase64 = System.Convert.ToBase64String(File.ReadAllBytes(GetAvatarPath(avatarFileNames[selectedAvatarIndex])));
        }
        else
        {
            createAccountAvatarImage.sprite = defaultAvatar;
            pendingAvatarBase64 = "";
        }
    }

    private void OpenEditProfile(int profileIndex)
    {
        isEditingProfile = true; // Режим редактирования
        editingProfileIndex = profileIndex;

        var users = UserManager.Instance.GetAllUsers();
        if (profileIndex < 0 || profileIndex >= users.Count)
        {
            Debug.LogError($"Invalid profile index for editing: {profileIndex}");
            return;
        }

        UserProfile profile = users[profileIndex];

        isUserSelectOpen = false;
        userSelectPanel.SetActive(false);
        isCreateAccountOpen = true;
        createAccountPanel.SetActive(true);

        // Заполняем данные профиля
        createAccountNameInput.text = profile.userName;
        createAccountAvatarImage.sprite = profile.GetAvatarSprite(defaultAvatar);
        pendingAvatarBase64 = profile.avatarBase64;

        // Ищем индекс текущей аватарки в списке доступных
        if (!string.IsNullOrEmpty(profile.avatarBase64))
        {
            // Проверяем, есть ли аватарка профиля в списке доступных
            for (int i = 0; i < availableAvatars.Count; i++)
            {
                string base64 = System.Convert.ToBase64String(File.ReadAllBytes(GetAvatarPath(avatarFileNames[i])));
                if (base64 == profile.avatarBase64)
                {
                    selectedAvatarIndex = i;
                    break;
                }
            }
        }
        else
        {
            // Если аватарки нет, используем случайную
            selectedAvatarIndex = Random.Range(0, availableAvatars.Count);
            createAccountAvatarImage.sprite = availableAvatars[selectedAvatarIndex];
            pendingAvatarBase64 = System.Convert.ToBase64String(File.ReadAllBytes(GetAvatarPath(avatarFileNames[selectedAvatarIndex])));
        }
    }

    private void CloseCreateAccount()
    {
        isCreateAccountOpen = false;
        createAccountPanel.SetActive(false);
        isUserSelectOpen = true;
        userSelectPanel.SetActive(true);
        UpdateUserButtons();
        SetActiveUserButton(0);
    }

    private void CreateAccount()
    {
        string userName = createAccountNameInput.text;
        UserManager.Instance.AddUser(userName, pendingAvatarBase64);
        isCreateAccountOpen = false;
        createAccountPanel.SetActive(false);
        UpdatePromptVisibility();
    }

    private void UpdateProfile()
    {
        if (editingProfileIndex < 0 || editingProfileIndex >= UserManager.Instance.GetAllUsers().Count)
        {
            Debug.LogError($"Invalid profile index for updating: {editingProfileIndex}");
            return;
        }

        string newUserName = createAccountNameInput.text;
        UserManager.Instance.UpdateUser(editingProfileIndex, newUserName, pendingAvatarBase64);

        isCreateAccountOpen = false;
        createAccountPanel.SetActive(false);
        isUserSelectOpen = true;
        userSelectPanel.SetActive(true);
        UpdateUserButtons();
        SetActiveUserButton(editingProfileIndex);
    }

    private void LoadAvatarsFromFolder()
    {
        // В редакторе используем Assets/avatars, в билде — корень проекта (avatars)
        string avatarsPath = Application.isEditor
            ? Path.Combine(Application.dataPath, "avatars")
            : Path.Combine(Directory.GetCurrentDirectory(), "avatars");

        if (!Directory.Exists(avatarsPath))
        {
            Debug.LogWarning($"Avatars folder not found at {avatarsPath}. Creating folder...");
            Directory.CreateDirectory(avatarsPath);
            return;
        }

        // Загружаем все файлы
        string[] allFiles = Directory.GetFiles(avatarsPath, "*.*");
        List<string> avatarFiles = new List<string>();

        // Фильтруем файлы с расширениями .png, .jpg и .jpeg
        foreach (string file in allFiles)
        {
            string extension = Path.GetExtension(file).ToLower();
            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
            {
                avatarFiles.Add(file);
            }
        }

        availableAvatars.Clear();
        avatarFileNames.Clear();

        foreach (string filePath in avatarFiles)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(bytes))
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                availableAvatars.Add(sprite);
                avatarFileNames.Add(Path.GetFileName(filePath));
            }
            else
            {
                Debug.LogWarning($"Failed to load avatar: {filePath}");
            }
        }

        if (availableAvatars.Count == 0)
        {
            Debug.LogWarning("No avatars found in the 'avatars' folder!");
        }
    }

    private string GetAvatarPath(string fileName)
    {
        // В редакторе используем Assets/avatars, в билде — корень проекта (avatars)
        return Application.isEditor
            ? Path.Combine(Application.dataPath, "avatars", fileName)
            : Path.Combine(Directory.GetCurrentDirectory(), "avatars", fileName);
    }

    private void OpenAvatarSelect()
    {
        if (availableAvatars.Count == 0)
        {
            Debug.LogWarning("No avatars available to select!");
            return;
        }

        isCreateAccountOpen = false;
        isAvatarSelectOpen = true;
        avatarSelectPanel.SetActive(true);
        selectedAvatarIndex = 0;

        // Создаём кнопки для аватарок
        UpdateAvatarList();
        UpdateAvatarSelection();
    }

    private void CloseAvatarSelect()
    {
        isAvatarSelectOpen = false;
        avatarSelectPanel.SetActive(false);
        isCreateAccountOpen = true;
        createAccountPanel.SetActive(true);
    }

    private void UpdateAvatarList()
    {
        // Очищаем старые кнопки, кроме шаблона
        foreach (Transform child in avatarListContent)
        {
            if (child.gameObject != avatarButtonTemplate)
            {
                Destroy(child.gameObject);
            }
        }

        // Очищаем список кнопок
        avatarButtons.Clear();

        // Создаём кнопки для каждой аватарки
        for (int i = 0; i < availableAvatars.Count; i++)
        {
            int index = i; // Захватываем индекс для замыкания
            GameObject button = Instantiate(avatarButtonTemplate, avatarListContent);
            button.SetActive(true);

            // Настраиваем аватарку и имя
            Transform avatarImageTransform = button.transform.Find("AvatarImage");
            Transform avatarNameTransform = button.transform.Find("AvatarName");

            if (avatarImageTransform == null || avatarNameTransform == null)
            {
                Debug.LogError($"AvatarImage or AvatarName not found in AvatarButtonTemplate for avatar {avatarFileNames[index]}!");
                continue;
            }

            Image avatarImage = avatarImageTransform.GetComponent<Image>();
            Text avatarName = avatarNameTransform.GetComponent<Text>();

            if (avatarImage == null || avatarName == null)
            {
                Debug.LogError($"AvatarImage or AvatarName component not found in AvatarButtonTemplate for avatar {avatarFileNames[index]}!");
                continue;
            }

            avatarImage.sprite = availableAvatars[index];
            avatarName.text = avatarFileNames[index];

            avatarButtons.Add(button);
        }
    }

    private void UpdateAvatarSelection()
    {
        // Сбрасываем выделение всех кнопок
        for (int i = 0; i < avatarButtons.Count; i++)
        {
            Image buttonImage = avatarButtons[i].GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = i == selectedAvatarIndex ? Color.yellow : Color.white; // Подсвечиваем выбранную кнопку
            }
        }
    }

    private void SelectAvatar()
    {
        if (selectedAvatarIndex >= 0 && selectedAvatarIndex < availableAvatars.Count)
        {
            createAccountAvatarImage.sprite = availableAvatars[selectedAvatarIndex];
            pendingAvatarBase64 = System.Convert.ToBase64String(File.ReadAllBytes(GetAvatarPath(avatarFileNames[selectedAvatarIndex])));
        }
    }

    public void StartGame()
    {
        if (UserManager.Instance.GetCurrentUser() != null)
        {
            SceneManager.LoadScene("SongSelect");
        }
        else
        {
            Debug.LogWarning("Please select or create a user before starting the game!");
        }
    }

    public void OpenSettings()
    {
        if (UserManager.Instance.GetCurrentUser() != null)
        {
            SceneManager.LoadScene("Settings");
        }
        else
        {
            Debug.LogWarning("Please select or create a user before opening settings!");
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}