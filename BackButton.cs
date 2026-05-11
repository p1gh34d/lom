using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    public void GoBack()
    {
        // Определяем, откуда мы вышли
        if (SceneManager.GetActiveScene().name == "SongSelect")
        {
            SceneManager.LoadScene("MainMenu"); // Возвращаемся в главное меню
        }
        else if (SceneManager.GetActiveScene().name == "Settings")
        {
            SceneManager.LoadScene("MainMenu"); // Возвращаемся в главное меню
        }
        else if (SceneManager.GetActiveScene().name == "GameScene")
        {
            SceneManager.LoadScene("SongSelect"); // Возвращаемся к выбору песен
        }
    }
}
