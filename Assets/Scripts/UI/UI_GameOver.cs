using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UI_GameOver : MonoBehaviour
{
    public TextMeshProUGUI scoreValue;
    public TextMeshProUGUI resultText;
    public Dictionary<string, int> resultScorePairs = new Dictionary<string, int>()
    {
        { "Alles richtig gemacht!", 100 },
        { "Beeindruckend!", 80 },
        { "Stabil!", 50 },
        { "Hat wohl schnell aufgegeben...", 0 }
    };

    public static UI_GameOver Instance { get; private set; }

    void Start()
    {
        Instance = this;
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    public void SetGameOverScreenWithScore(int score)
    {
        scoreValue.text = score.ToString();

        float scorePercentage = score / GameManager.Instance.maxScore * 100f;
        
        resultText.text = GetResultTextForScore(score);
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
