using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_GameOver : MonoBehaviour
{
    public TextMeshProUGUI reasonText;
    public TextMeshProUGUI scoreValue;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI highestScore;
    public Dictionary<string, int> resultScorePairs = new Dictionary<string, int>()
    {
        { "Alles richtig gemacht!", 100 },
        { "Beeindruckend!", 80 },
        { "Stabil!", 50 },
        { "Hat wohl schnell aufgegeben...", 0 }
    };

    [Description("0: default: player destroyed wall of room with enemy in it, or player ran out of time; 1: player won")]
    public string[] reasonForGameOverString;

    public static UI_GameOver Instance { get; private set; }

    public TextMeshProUGUI buttonText;
    public string[] buttonTextRestart;

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        gameObject.SetActive(false);


    }

    public void SetGameOverScreenWithScore(int score, int reasonForGameOver)
    {

        buttonText.text = buttonTextRestart[Random.Range(0, buttonTextRestart.Length - 1)];
        switch (reasonForGameOver)
        {
            case 0: // default: player destroyed wall of room with enemy in it, or player ran out of time
                Debug.Log("This should be called with final score: " + score);
                gameObject.SetActive(true);

                scoreValue.text = score.ToString();

                float scorePercentage = score / GameManager.Instance.maxScore * 100f;

                resultText.text = GetResultTextForScore(Mathf.RoundToInt(scorePercentage));

                reasonText.text = reasonForGameOverString[0];
                break;
            case 1: // player won
                Debug.Log("This should be called with final score: " + score);
                gameObject.SetActive(true);

                scoreValue.text = score.ToString();

                float scorePercentage2 = score / GameManager.Instance.maxScore * 100f;

                resultText.text = GetResultTextForScore(Mathf.RoundToInt(scorePercentage2));

                reasonText.text = reasonForGameOverString[1];
                break;
            case 2: // player lost because they destroyed a wall when RMF was low
                Debug.Log("This should be called with final score: " + score);
                gameObject.SetActive(true);

                scoreValue.text = score.ToString();

                float scorePercentage3 = score / GameManager.Instance.maxScore * 100f;

                resultText.text = GetResultTextForScore(Mathf.RoundToInt(scorePercentage3));

                reasonText.text = reasonForGameOverString[2];
                break;
            case 3: //player ran out of time
                Debug.Log("This should be called with final score: " + score);
                gameObject.SetActive(true);

                scoreValue.text = score.ToString();

                float scorePercentage4 = score / GameManager.Instance.maxScore * 100f;

                resultText.text = GetResultTextForScore(Mathf.RoundToInt(scorePercentage4));

                reasonText.text = reasonForGameOverString[3];
                break;
        }
        
    }

    public string GetResultTextForScore(int score)
    {
        float scorePercentage = (float)score / GameManager.Instance.maxScore * 100f;

        // Verwende die Dictionary-Werte als Schwellen (absteigend)
        foreach (var kv in resultScorePairs.OrderByDescending(kv => kv.Value))
        {
            if (scorePercentage >= kv.Value)
                return kv.Key;
        }

        // Fallback
        return "Miss";
    }
}
