using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public GameManager gameManager;
    public void Play()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    public void Quit()
    {
        Application.Quit();
    }

    public void Volver()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
    }

    public void cambiarEscena(int nivel)
    {
        SceneManager.LoadScene(nivel);
    }

    public void OnSelectMode1v1()
    {
        gameManager.SetTwoPlayersPerTeam(false);
    }

    public void OnSelectMode2v2()
    {
        gameManager.SetTwoPlayersPerTeam(true);
    }

}
