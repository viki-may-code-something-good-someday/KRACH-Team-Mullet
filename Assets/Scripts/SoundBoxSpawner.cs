using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SoundBoxSpawner : MonoBehaviour
{
    public static SoundBoxSpawner Instance { get; private set; }

    [SerializeField] private List<SoundBoxWave> soundBoxWaves = new List<SoundBoxWave>();
    [SerializeField] private Transform spawnParent;
    [SerializeField] private List<SoundBoxSpawnPoint> spawnPoints = new List<SoundBoxSpawnPoint>();

    [SerializeField] private float waveSpawnDelay = 10f;   // delay between clearing a wave and spawning the next one

    private SoundManager soundManager;
    private bool wonGame = false;

    [SerializeField] private int currentWaveIndex = 0;


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
        soundManager = FindFirstObjectByType<SoundManager>();
        if (soundManager == null)
        {
            Debug.LogWarning("SoundBoxSpawner: SoundManager not found in scene.");
        }

        for (int i = soundBoxWaves.Count - 1; i >= 0 ; i--)
        {
            soundBoxWaves[i].hasSpawned = false;
            soundBoxWaves[i].activeInstances.Clear();
        }
        currentWaveIndex = 0;
        wonGame = false;

    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
{
    if (currentWaveIndex >= soundBoxWaves.Count)
    {
        WinGame();
        return;
    }

    SoundBoxWave currentWave = soundBoxWaves[currentWaveIndex];

    if (!currentWave.hasSpawned)
    {
        SpawnSoundBoxWave(currentWave);
        currentWave.hasSpawned = true;

        SoundManager.Instance.InitializeSoundboxEmitters();

        return;
    }

    // IMPORTANT: only check clear if it had instances
    if (currentWave.hasSpawned &&
        currentWave.activeInstances.Count == 0 &&
        currentWave.boxes.Count > 0)
    {
        Debug.Log("Wave " + currentWaveIndex + " cleared");

        currentWaveIndex++;
    }
}


    private void SpawnSoundBoxWave(SoundBoxWave wave)
    {
        if (wave == null || wave.boxes == null || wave.boxes.Count == 0)
        {
            Debug.LogWarning("SoundBoxSpawner: wave has no boxes to spawn.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("SoundBoxSpawner: no spawn points assigned.");
            return;
        }

        wave.activeInstances.Clear();

        for (int i = 0; i < wave.boxes.Count; i++)
        {
            SoundBox thisBox = wave.boxes[i];
            if (thisBox == null) continue;

            SoundBoxSpawnPoint spawnPoint = spawnPoints[wave.spawnPosNumbers[i]];

            // Instantiate and keep the SoundBox component reference
            SoundBox spawned = Instantiate(thisBox, spawnPoint.transform.position, Quaternion.identity, spawnParent);
            wave.activeInstances.Add(spawned);
        }

        Debug.Log("Spawning wave " + currentWaveIndex + " with " + wave.boxes.Count + " boxes.!!!!!!!!!!!!!!!!!!!!!!");

        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!Spawned instances: " + wave.activeInstances.Count);

    }

    private void WinGame()
    {
        if(wonGame) return;

        wonGame = true;

        Debug.Log("All waves cleared! You win!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        GameManager.Instance.WinGame();
    }

    public void DestroyingSoundBox(SoundBox soundBox)
    {
        if (soundBox == null) return;

        for (int i = 0; i < soundBoxWaves.Count; i++)
        {
            SoundBoxWave wave = soundBoxWaves[i];
            if (wave.activeInstances.Remove(soundBox))
            {
                // wave completion handled in Update
                return;
            }
        }

        Destroy(soundBox.gameObject);

        Debug.LogWarning("DestroyedSoundBox: instance not found in any active wave.");
    }

    private void ClearWave()
    {
        RuntimeManager.PlayOneShot("event:/SFX/AllSpeakersDestroyed");    // sound on all destroyed soundboxes in the wave

        // Start timer before next wave
        StartCoroutine(NextWaveSpawnDelay(waveSpawnDelay));
    }

    private IEnumerator NextWaveSpawnDelay(float delay)
    {
        soundManager.PlayClassicMusic();    // classic music
        yield return new WaitForSeconds(delay/0.5f);
        soundManager.StopClassicMusic();
        RuntimeManager.PlayOneShot("event:/SFX/NextWave");    // sound for next wave incoming
        // BLACKOUT SCREEN HERE (Elevator Transition)
        yield return new WaitForSeconds(delay/0.5f);
        soundManager.PlayRemixMusic();
    }
}
