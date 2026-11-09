using FMOD.Studio;
using FMODUnity;
using TMPro;
using UnityEngine;

public class UI_GameOver : MonoBehaviour
{
    public TextMeshProUGUI reasonText;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI buttonText;

    // [0] = lost, [1] = won
    public string[] reasonForGameOverString;
    public string[] buttonTextRestart;

    private EventInstance gameOverSound;

    public static UI_GameOver Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);
    }

    /// <param name="_won">Whether the LOCAL player's side won.</param>
    public void SetGameOverScreen(bool _won)
    {
        gameObject.SetActive(true);

        if (buttonText != null && buttonTextRestart != null && buttonTextRestart.Length > 0)
            buttonText.text = buttonTextRestart[Random.Range(0, buttonTextRestart.Length)];

        int reasonIndex = _won ? 1 : 0;
        if (reasonText != null && reasonForGameOverString != null && reasonIndex < reasonForGameOverString.Length)
            reasonText.text = reasonForGameOverString[reasonIndex];

        if (resultText != null)
            resultText.text = _won ? "You won!" : "You lost!";

        if (!_won)
        {
            //gameOverSound = RuntimeManager.CreateInstance("event:/SFX/GameOver");
            if (gameOverSound.isValid())
            {
                gameOverSound.start();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (gameOverSound.isValid())
        {
            gameOverSound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            gameOverSound.release();
        }
    }
}
