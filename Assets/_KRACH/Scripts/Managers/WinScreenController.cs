using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// Lives in the win screen scene. Reads on start who won (stored in the CustomNetworkManager
/// by the GameManager before the scene change) and displays it.
/// </summary>
public class WinScreenController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private GameObject hunterWinVisuals;
    [SerializeField] private GameObject vandalistWinVisuals;

    private void Start()
    {
        // Safety net: reset the round-end slow motion and the cursor lock from the game,
        // so the buttons in the win screen are usable.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        CustomNetworkManager manager = NetworkManager.singleton as CustomNetworkManager;
        if (manager == null)
        {
            Debug.LogError("[WinScreenController] No CustomNetworkManager found – can't display the winner.");
            return;
        }

        DisplayWinner(manager.LastWinner);
    }

    private void DisplayWinner(WinningSide winner)
    {
        if (winnerText != null)
        {
            winnerText.text = winner switch
            {
                WinningSide.Hunter => "The Hunters won!",
                WinningSide.Vandalist => "The Vandalists won!",
                _ => "Draw."
            };
        }

        if (hunterWinVisuals != null) hunterWinVisuals.SetActive(winner == WinningSide.Hunter);
        if (vandalistWinVisuals != null) vandalistWinVisuals.SetActive(winner == WinningSide.Vandalist);
    }
}
