using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RetryGame : MonoBehaviour
{
    public Button retryButton;

    void Start()
    {
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RestartScene);
        }
    }

    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
