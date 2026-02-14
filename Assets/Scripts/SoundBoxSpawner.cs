using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundBoxSpawner : MonoBehaviour
{
    public static SoundBoxSpawner Instance { get; private set; }

    [SerializeField] private List<SoundBoxWave> soundBoxWaves = new List<SoundBoxWave>();
    [SerializeField] private List<SoundBoxSpawnPoint> soundBoxSpawnPoints = new List<SoundBoxSpawnPoint>();
    [SerializeField] private Transform spawnParent;

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

        var currentWave = soundBoxWaves[currentWaveIndex];

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
            var prefab = wave.prefabs[i];
            if (prefab == null) continue;

            var spawnPoint = soundBoxSpawnPoints[UnityEngine.Random.Range(0, soundBoxSpawnPoints.Count)];
            var pos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;

            // Instantiate and keep the SoundBox component reference
            var spawned = Instantiate(prefab, pos, Quaternion.identity, spawnParent);
            wave.activeInstances.Add(spawned);
        }
    }

    private void WinGame()
    {
        //Win Game Logic
    }

    public void DestroyedSoundbox(SoundBox soundBox)
    {
        if (soundBox == null) return;

        for (int i = 0; i < soundBoxWaves.Count; i++)
        {
            var wave = soundBoxWaves[i];
            if (wave.activeInstances.Remove(soundBox))
            {
                // wave completion handled in Update
                return;
            }
        }

        Debug.LogWarning("DestroyedSoundbox: instance not found in any active wave.");
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
