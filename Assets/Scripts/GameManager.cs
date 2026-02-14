using UnityEngine;

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}

public class GameManager : MonoBehaviour
{
    private float currentPlaytime;
    public int maxScore = 1000;
    private GameState currentState;
    
    public static GameManager Instance { get; private set; }

    void Start()
    {
        Instance = this;
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

    }

    void Update()
    {
        if(currentState == GameState.Playing) UpdateInternalTimer();
    }

    public void StartGame()
    {
        currentPlaytime = 0f;

        ResumeGame();
    }

    public void GameOver(bool won)
    {         
        currentState = GameState.GameOver;
        int finalScore = CalculateScore();
        Debug.Log($"Game Over! Final Score: {finalScore} - You {(won ? "won" : "lost")}!");
    }

    public void PauseGame()
    {
        currentState = GameState.Paused;

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        currentState = GameState.Playing;

        Time.timeScale = 1f;
    }

    private void UpdateInternalTimer()
    {
        currentPlaytime += Time.deltaTime;
    }

    private int CalculateScore()
    {
        return Mathf.FloorToInt(maxScore - currentPlaytime);
    }
}
