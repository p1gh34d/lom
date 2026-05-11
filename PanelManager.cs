using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PanelManager : MonoBehaviour
{
    [SerializeField] private GameObject calibrationPanel;
    [SerializeField] private GameObject stage1Text;
    [SerializeField] private GameObject stage2Text;
    [SerializeField] private GameObject endStageText;

    private int currentCalibrationStage;
    private float audioOffset; // Для хранения оффсета с первого этапа

    private void Start()
    {
        // Загружаем текущий этап калибровки
        currentCalibrationStage = PlayerPrefs.GetInt("CalibrationStage", 1);
        audioOffset = PlayerPrefs.GetFloat("AudioOffset", 0f); // Загружаем сохранённый оффсет
        Debug.Log($"CalibrationPanelManager Start: CalibrationStage={currentCalibrationStage}, AudioOffset={audioOffset:F3} ms");

        // Активируем панель и нужный текстовый блок
        if (calibrationPanel != null)
        {
            calibrationPanel.SetActive(true);
            ActivateTextBlock();
        }
        else
        {
            Debug.LogError("CalibrationPanel is not assigned!");
        }
    }

    private void Update()
    {
        HandleInput();
    }

private void ActivateTextBlock()
{
    if (stage1Text == null || stage2Text == null || endStageText == null)
    {
        Debug.LogError("One or more text blocks (Stage1, Stage2, EndStage) are not assigned!");
        return;
    }

    // Деактивируем все блоки
    stage1Text.SetActive(false);
    stage2Text.SetActive(false);
    endStageText.SetActive(false);

    // Форматируем оффсет в -n/0/+n
    int offsetInt = Mathf.RoundToInt(audioOffset * 1000f); // Переводим секунды в миллисекунды и округляем
    string offsetText = offsetInt == 0 ? "0" : (offsetInt > 0 ? $"+{offsetInt}" : $"{offsetInt}");

    // Активируем нужный блок и обновляем StageText
    switch (currentCalibrationStage)
    {
        case 1:
            stage1Text.SetActive(true);
            Debug.Log("Activated 1Stage text block");
            break;
        case 2:
            stage2Text.SetActive(true);
            // Находим StageText внутри 2Stage
            Text stage2TextComponent = stage2Text.transform.Find("StageText")?.GetComponent<Text>();
            if (stage2TextComponent != null)
            {
                stage2TextComponent.text = stage2TextComponent.text.Replace("{offset}", offsetText);
                Debug.Log($"Updated 2Stage StageText with offset: {offsetText}");
            }
            else
            {
                Debug.LogError("StageText not found in 2Stage!");
            }
            Debug.Log("Activated 2Stage text block");
            break;
        default: // После второго этапа (stage 0 или любой другой)
            endStageText.SetActive(true);
            // Находим StageText внутри EndStage
            Text endStageTextComponent = endStageText.transform.Find("StageText")?.GetComponent<Text>();
            if (endStageTextComponent != null)
            {
                endStageTextComponent.text = endStageTextComponent.text.Replace("{offset}", offsetText);
                Debug.Log($"Updated EndStage StageText with offset: {offsetText}");
            }
            else
            {
                Debug.LogError("StageText not found in EndStage!");
            }
            Debug.Log("Activated EndStage text block");
            break;
    }
}

    private void HandleInput()
    {
        if (currentCalibrationStage == 1)
        {
            // Первый этап: Green - начать калибровку, Red - в Settings
            if (InputManager.Instance.IsKeyDown("Green"))
            {
                Debug.Log("Green pressed, starting first calibration stage");
                calibrationPanel.SetActive(false);
                SceneManager.LoadScene("GameScene");
            }
            else if (InputManager.Instance.IsKeyDown("Red"))
            {
                Debug.Log("Red pressed, returning to Settings");
                PlayerPrefs.SetInt("CalibrationStage", 0);
                PlayerPrefs.DeleteKey("SelectedSong");
                PlayerPrefs.Save();
                SceneManager.LoadScene("Settings");
            }
        }
        else if (currentCalibrationStage == 2)
        {
            // Второй этап: Green - начать калибровку, Red - в Settings, Yellow - на первый этап
            if (InputManager.Instance.IsKeyDown("Green"))
            {
                Debug.Log("Green pressed, starting second calibration stage");
                calibrationPanel.SetActive(false);
                SceneManager.LoadScene("GameScene");
            }
            else if (InputManager.Instance.IsKeyDown("Red"))
            {
                Debug.Log("Red pressed, returning to Settings");
                PlayerPrefs.SetInt("CalibrationStage", 0);
                PlayerPrefs.DeleteKey("SelectedSong");
                PlayerPrefs.Save();
                SceneManager.LoadScene("Settings");
            }
            else if (InputManager.Instance.IsKeyDown("Yellow"))
            {
                Debug.Log("Yellow pressed, restarting first calibration stage with saved offset");
                PlayerPrefs.SetInt("CalibrationStage", 1);
                PlayerPrefs.SetString("SelectedSong", "calibration");
                PlayerPrefs.Save();
                SceneManager.LoadScene("GameScene");
            }
        }
        else // После второго этапа
        {
            // EndStage: Green - в Settings, Red - неактивна, Yellow - на первый этап
            if (InputManager.Instance.IsKeyDown("Green"))
            {
                Debug.Log("Green pressed, returning to Settings");
                PlayerPrefs.SetInt("CalibrationStage", 0);
                PlayerPrefs.DeleteKey("SelectedSong");
                PlayerPrefs.Save();
                SceneManager.LoadScene("Settings");
            }
            else if (InputManager.Instance.IsKeyDown("Yellow"))
            {
                Debug.Log("Yellow pressed, restarting first calibration stage with saved offset");
                PlayerPrefs.SetInt("CalibrationStage", 1);
                PlayerPrefs.SetString("SelectedSong", "calibration");
                PlayerPrefs.Save();
                SceneManager.LoadScene("GameScene");
            }
            // Red неактивна, ничего не делаем
        }
    }
}