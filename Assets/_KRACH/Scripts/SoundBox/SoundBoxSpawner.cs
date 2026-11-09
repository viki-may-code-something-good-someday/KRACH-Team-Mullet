using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using Mirror;
using Sirenix.OdinInspector;

/// <summary>
/// Spawns SoundBoxes in waves. Runs exclusively on the server: SoundBox is a NetworkBehaviour
/// and must be created via NetworkServer.Spawn – previously every client instantiated its own,
/// non-networked boxes that hit Rpcs never reached.
///
/// Note: MainLevel_Level1 currently uses hand-placed boxes + GameManager.trackedSoundBoxes,
/// not a spawner.
/// </summary>
public class SoundBoxSpawner : MonoBehaviour
{
    public static SoundBoxSpawner Instance { get; private set; }

    [Header("References")]
    [SerializeField] private List<SoundBoxWave> soundBoxWaves = new List<SoundBoxWave>();
    [SerializeField] private Transform spawnParent;
    [SerializeField] private List<SoundBoxSpawnPoint> spawnPoints = new List<SoundBoxSpawnPoint>();

    [Header("Settings")]
    [SerializeField] private float waveTransitionDelay;

    [SerializeField, Sirenix.OdinInspector.ReadOnly] private int currentWaveIndex = 0;

    private readonly List<SoundBox> activeInstances = new List<SoundBox>();
    private bool gameWon = false;
    private bool isTransitioningWave = false;
    private bool running;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!NetworkServer.active) return; // clients receive the boxes through Mirror

        ResetWaveData();
        running = true;
        SpawnCurrentWave();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!running || isTransitioningWave || gameWon) return;

        if (activeInstances.Count == 0)
        {
            isTransitioningWave = true;
            StartCoroutine(TransitionToNextWave());
        }
    }

    private void ResetWaveData()
    {
        currentWaveIndex = 0;
        gameWon = false;
        isTransitioningWave = false;
        activeInstances.Clear();
    }

    private void SpawnCurrentWave()
    {
        if (currentWaveIndex >= soundBoxWaves.Count)
        {
            WinGame();
            return;
        }

        SoundBoxWave wave = soundBoxWaves[currentWaveIndex];

        if (wave == null || wave.Boxes == null || wave.Boxes.Count == 0)
        {
            Debug.LogWarning($"[SoundBoxSpawner] Wave {currentWaveIndex} has no boxes.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[SoundBoxSpawner] No spawn points assigned.");
            return;
        }

        activeInstances.Clear();

        for (int i = 0; i < wave.Boxes.Count; i++)
        {
            SoundBox box = wave.Boxes[i];
            if (box == null) continue;

            int spawnIndex = i < wave.SpawnPosNumbers.Count ? wave.SpawnPosNumbers[i] : i;
            if (spawnIndex < 0 || spawnIndex >= spawnPoints.Count || spawnPoints[spawnIndex] == null)
            {
                Debug.LogWarning($"[SoundBoxSpawner] Wave {currentWaveIndex}: invalid spawn point index {spawnIndex} – box skipped.");
                continue;
            }

            SoundBox spawned = Instantiate(box, spawnPoints[spawnIndex].transform.position, Quaternion.identity, spawnParent);
            NetworkServer.Spawn(spawned.gameObject);
            activeInstances.Add(spawned);
        }

        if (SoundManager.Instance != null) SoundManager.Instance.InitializeSoundboxEmitters();

        Debug.Log($"[SoundBoxSpawner] Wave {currentWaveIndex} spawned with {activeInstances.Count} boxes.");
    }

    /// <summary>Called by SoundBox when it was destroyed. The box removes itself.</summary>
    public void NotifySoundBoxDestroyed(SoundBox _soundBox)
    {
        if (_soundBox == null) return;

        if (!activeInstances.Remove(_soundBox))
            Debug.LogWarning($"[SoundBoxSpawner] '{_soundBox.name}' not found in active wave.");
        // Wave completion is detected in Update()
    }

    private IEnumerator TransitionToNextWave()
    {
        RuntimeManager.PlayOneShot("event:/SFX/AllSpeakersDestroyed");

        yield return new WaitForSeconds(waveTransitionDelay);

        currentWaveIndex++;
        isTransitioningWave = false;

        SpawnCurrentWave();
    }

    private void WinGame()
    {
        if (gameWon) return;
        gameWon = true;

        Debug.Log("[SoundBoxSpawner] All waves destroyed – vandalists win.");
        if (GameManager.Instance != null) GameManager.Instance.EndGame(WinningSide.Vandalist);
    }
}
