using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/// <summary>
/// Loads the menu scene right after the bootstrap scene has started.
/// </summary>
public class BootstrapLoader : MonoBehaviour
{
    [Scene]
    [SerializeField] private string menuSceneName;

    private void Start()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}