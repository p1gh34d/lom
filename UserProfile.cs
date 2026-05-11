using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class UserProfile
{
    public string userName;
    public string avatarBase64; // Аватарка в формате Base64
    public int userIndex; // Уникальный индекс пользователя
    public float audioOffset; // Новое поле для оффсета

    // Настройки пользователя
    public bool leftyFlip;
    public float noteSpeed;
    public int targetFPS;
    public bool useAccuracySystem; // Новое поле для Accuracy System
    public bool showAccuracy;      // Переименовано из useAccuracySystem
    public string selectedFretboard;
    public Dictionary<string, KeyCode> keyBindings; // Привязки клавиш
    public bool useFourFrets; // Новое поле

    public bool enableOpenNotes; // Включение открытых нот
    public bool openNotesFiveString; // Открытые ноты на 5 струнах
    public bool openNotesOneString; // Открытые ноты на 1 струне
    public bool extendedForcedNotes; // Новое поле для расширенного диапазона forced нот
    public bool missForced; // Настройка Miss Forced
    public bool arrowKeyErrors; // Настройка ошибок при нажатии стрелок

    public UserProfile(string name, string avatar, int index)
    {
        userName = name;
        avatarBase64 = avatar;
        userIndex = index;

        audioOffset = 0f; // По умолчанию 0
        // Устанавливаем значения по умолчанию для настроек
        leftyFlip = false;
        noteSpeed = 5f;
        targetFPS = 250;
        useAccuracySystem = true; // По умолчанию включено
        showAccuracy = true;      // По умолчанию включено
        useFourFrets = false; // По умолчанию 5 кнопок
        selectedFretboard = "default";

        enableOpenNotes = true; // По умолчанию включено
        openNotesFiveString = true; // По умолчанию включено
        openNotesOneString = true; // По умолчанию включено
        extendedForcedNotes = false; // По умолчанию выключено
        missForced = true; // По умолчанию включено
        arrowKeyErrors = false; // По умолчанию выключено

        InitializeKeyBindings();
    }

    // Метод для инициализации keyBindings
    private void InitializeKeyBindings()
    {
        if (keyBindings == null)
        {
            keyBindings = new Dictionary<string, KeyCode>
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
    }

    // Вызываем после десериализации
    [System.Runtime.Serialization.OnDeserialized]
    private void OnDeserialized(System.Runtime.Serialization.StreamingContext context)
    {
        InitializeKeyBindings();
    }

    // Получение аватарки как Sprite
    public Sprite GetAvatarSprite(Sprite defaultAvatar)
    {
        if (string.IsNullOrEmpty(avatarBase64))
            return defaultAvatar;

        byte[] bytes = Convert.FromBase64String(avatarBase64);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}