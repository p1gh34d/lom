using UnityEngine;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    private static InputManager instance;

    private Dictionary<string, KeyCode> keyBindings;

    public static InputManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("InputManager");
                instance = go.AddComponent<InputManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadKeyBindings();
    }

    private void LoadKeyBindings()
    {
        keyBindings = new Dictionary<string, KeyCode>
        {
            { "Green", (KeyCode)PlayerPrefs.GetInt("Key_Green", (int)KeyCode.V) },
            { "Red", (KeyCode)PlayerPrefs.GetInt("Key_Red", (int)KeyCode.C) },
            { "Yellow", (KeyCode)PlayerPrefs.GetInt("Key_Yellow", (int)KeyCode.X) },
            { "Blue", (KeyCode)PlayerPrefs.GetInt("Key_Blue", (int)KeyCode.Z) },
            { "Orange", (KeyCode)PlayerPrefs.GetInt("Key_Orange", (int)KeyCode.LeftShift) },
            { "StrumUp", (KeyCode)PlayerPrefs.GetInt("Key_StrumUp", (int)KeyCode.UpArrow) },
            { "StrumDown", (KeyCode)PlayerPrefs.GetInt("Key_StrumDown", (int)KeyCode.DownArrow) },
            { "StarPower", (KeyCode)PlayerPrefs.GetInt("Key_StarPower", (int)KeyCode.Space) },
            { "Start", (KeyCode)PlayerPrefs.GetInt("Key_Start", (int)KeyCode.Return) },
            { "Whammy", (KeyCode)PlayerPrefs.GetInt("Key_Whammy", (int)KeyCode.RightControl) }
        };
    }

    // Метод для перезагрузки привязок клавиш из PlayerPrefs
    public void ReloadKeyBindings()
    {
        LoadKeyBindings();
        Debug.Log("InputManager: Key bindings reloaded from PlayerPrefs.");
    }

    public void UpdateKeyBinding(string keyName, KeyCode newKey)
    {
        if (keyBindings.ContainsKey(keyName))
        {
            keyBindings[keyName] = newKey;
            // Обновляем через UserManager
            UserManager.Instance.UpdateKeyBinding(keyName, newKey);
        }
    }

    public bool IsKeyDown(string keyName)
    {
        return keyBindings.ContainsKey(keyName) && Input.GetKeyDown(keyBindings[keyName]);
    }

    public bool IsKey(string keyName)
    {
        return keyBindings.ContainsKey(keyName) && Input.GetKey(keyBindings[keyName]);
    }

    public bool IsKeyUp(string keyName)
    {
        return keyBindings.ContainsKey(keyName) && Input.GetKeyUp(keyBindings[keyName]);
    }

    public KeyCode GetKey(string keyName)
    {
        return keyBindings.ContainsKey(keyName) ? keyBindings[keyName] : KeyCode.None;
    }
}