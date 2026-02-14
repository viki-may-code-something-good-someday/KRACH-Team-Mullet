using System.Collections;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;
public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    Sequence,
    GameOver
}

public class GameManager : MonoBehaviour
{
    private float currentPlaytime;
    public int maxScore = 1000;
    private GameState currentState;

    public Camera_FirstPerson playerCameraController;
    public Camera playerCamera;

    public CharacterController_FirstPerson playerController;

    [SerializeField] public RoomObj[] rooms;
     
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
            rooms[1].SetEnemeyIsInThisRoom(true);

        StartGame();
    }

    public 

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

        UI_GameOver.Instance.SetGameOverScreenWithScore(finalScore);
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

    public void SetSequence()
        {
        currentState = GameState.Sequence;
    }

    public void EndSequence()
    {
        ResumeGame();
    }

    private void UpdateInternalTimer()
    {
        currentPlaytime += Time.deltaTime;
    }

    private int CalculateScore()
    {
        return Mathf.FloorToInt(maxScore - currentPlaytime);
    }

    public void WallWasDestroyed(Wall_Data wall)
    {
        for (int i = 0; i < rooms.Length; i++)
        {
            for(int j = 0; j < rooms[i].wallsInThisRoom.Length; j++)
            {
                if (rooms[i].wallsInThisRoom[j] == wall)
                {
                    rooms[i].RemoveWallFromRoomArray(wall);
                    if (rooms[i].isEnemyInThisRoom)
                    {
                        Debug.Log("Enemy was in this room!");
                        StartCoroutine(EndSequence(rooms[i]));
                        return;
                    }
                    else
                    {
                        rooms[i].wallsInThisRoom[j] = null;
                    }
                    Debug.Log("Enemy wasnt in this room");
                }
                    
            }

        }
    }

    IEnumerator EndSequence(RoomObj room)
    {
        Debug.Log("Starting end sequence for room: " + room.name);
        currentState = GameState.Sequence;

        playerController.walkSpeed = 1f;
        

        yield return new WaitForSeconds(1f);

        StartCoroutine(RotateCameraToTarget(room.transform, 4f));

        yield return new WaitForSeconds(4f);

        // Danach Spielende auslösen
        GameOver(false);
    }

    private IEnumerator RotateCameraToTarget(Transform target, float duration)
    {
        Debug.Log("Starting Camera Rotation to Target: " + target.name);
        playerCameraController.enabled = false; // Deaktiviere die Spielersteuerung während der Kamerafahrt

        if (playerCamera == null || target == null)
            yield break;

        Transform camT = playerCamera.transform;
        Quaternion startRot = camT.rotation;
        Vector3 direction = target.position - camT.position;
        if (direction.sqrMagnitude <= 0f)
            yield break;

        Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            camT.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        camT.rotation = targetRot;
    }
}
