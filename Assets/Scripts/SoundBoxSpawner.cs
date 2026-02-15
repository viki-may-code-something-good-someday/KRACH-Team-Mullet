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
    [SerializeField] private List<SoundBoxSpawnPoint> soundBoxSpawnPoints = new List<SoundBoxSpawnPoint>();
    [SerializeField] private Transform spawnParent;

    [SerializeField] private float waveSpawnDelay = 10f;   // delay between clearing a wave and spawning the next one

    private SoundManager soundManager;

    private int currentWaveIndex = 0;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        soundManager = FindFirstObjectByType<SoundManager>();
        if (soundManager == null)
        {
            Debug.LogWarning("SoundBoxSpawner: SoundManager not found in scene.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // If there are no configured waves -> win
        if (soundBoxWaves == null || soundBoxWaves.Count == 0)
        {
            WinGame();
            return;
        }

        // Guard current index
        if (currentWaveIndex >= soundBoxWaves.Count)
            return;

        SoundBoxWave currentWave = soundBoxWaves[currentWaveIndex];

        // Spawn the wave once
        if (!currentWave.hasSpawned)
        {
            SpawnSoundBoxWave(currentWave);
            currentWave.hasSpawned = true;
            return;
        }

        // If spawned and all instances are gone -> remove wave (next wave will occupy the same index)
        if (currentWave.hasSpawned && currentWave.activeInstances.Count == 0)
        {
            soundBoxWaves.RemoveAt(currentWaveIndex);
            // do not increment index; next wave (if any) is now at currentWaveIndex
        }
    }

    private void SpawnSoundBoxWave(SoundBoxWave wave)
    {
        if (wave == null || wave.prefabs == null || wave.prefabs.Count == 0)
            return;

        if (soundBoxSpawnPoints == null || soundBoxSpawnPoints.Count == 0)
        {
            Debug.LogWarning("SoundBoxSpawner: no spawn points assigned.");
            return;
        }

        wave.activeInstances.Clear();

        for (int i = 0; i < wave.prefabs.Count; i++)
        {
            SoundBox prefab = wave.prefabs[i];
            if (prefab == null) continue;

            SoundBoxSpawnPoint spawnPoint = soundBoxSpawnPoints[UnityEngine.Random.Range(0, soundBoxSpawnPoints.Count)];
            Vector3 pos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;

            // Instantiate and keep the SoundBox component reference
            SoundBox spawned = Instantiate(prefab, pos, Quaternion.identity, spawnParent);
            wave.activeInstances.Add(spawned);
        }
    }

    private void WinGame()
    {
        //Win Game Logic
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



[Serializable]
public class SoundBoxWave
{
    [Tooltip("SoundBox prefab references to spawn for this wave.")]
    public List<SoundBox> prefabs = new List<SoundBox>();

    [HideInInspector] public List<SoundBox> activeInstances = new List<SoundBox>();
    [HideInInspector] public bool hasSpawned = false;
}
