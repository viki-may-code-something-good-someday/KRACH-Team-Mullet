using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class TestDummySpawner : NetworkBehaviour
{
    [System.Serializable]
    public struct DummySpawnGroup
    {
        public PlayerRole role;
        public int count;
    }

    [Header("Dummy Settings")]
    [Tooltip("Prefab with NetworkIdentity + PlayerObjectController + PlayerRoleSetup + CapsuleCollider." +
             " PlayerRoleSetup is needed for model visibility AND catch/respawn behavior.")]
    [SerializeField] private GameObject dummyPrefab;

    [Tooltip("Which roles get spawned with how many dummies. This way e.g. vandalist and hunter " +
             "dummies can be created at the same time for different test scenarios.")]
    [SerializeField]
    private DummySpawnGroup[] spawnGroups = new DummySpawnGroup[]
    {
        new DummySpawnGroup { role = PlayerRole.Vandalist, count = 2 }
    };

    [Tooltip("Offset so dummies don't stand exactly on the spawn point.")]
    [SerializeField] private float spawnSpread = 1.5f;

    [Tooltip("Place dummies on the ground via raycast. Spawn points are set up for players," +
             " who fall down after spawning – a dummy without physics would otherwise stay in the" +
             " air and be out of reach for a hunter on the ground.")]
    [SerializeField] private bool dropToGround = true;
    [Tooltip("Layers that count as ground. Should match the groundMask of PlayerMovement.")]
    [SerializeField] private LayerMask groundMask = 8;

    private readonly List<GameObject> spawnedDummies = new List<GameObject>();

    // ── Server Start ──────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        CustomNetworkManager mgr = NetworkManager.singleton as CustomNetworkManager;

        if (mgr == null || !mgr.IsTestMode)
        {
            Debug.Log("[TestDummySpawner] Test mode not active – dummy spawn skipped.");
            return;
        }

        if (dummyPrefab == null)
        {
            Debug.LogError("[TestDummySpawner] dummyPrefab is not assigned!");
            return;
        }

        // Wait briefly until the LevelManager is definitely loaded
        StartCoroutine(SpawnAfterDelay());
    }

    public override void OnStopServer()
    {
        // Prevents ghost dummies during repeated play tests in the editor (esp. with domain reload disabled).
        foreach (GameObject dummy in spawnedDummies)
        {
            if (dummy != null)
                NetworkServer.Destroy(dummy);
        }

        spawnedDummies.Clear();
    }

    private System.Collections.IEnumerator SpawnAfterDelay()
    {
        // Wait one frame so Awake/Start of the LevelManager have finished
        yield return null;

        if (LevelManager.Instance == null)
        {
            Debug.LogError("[TestDummySpawner] LevelManager not found – dummies could not be spawned.");
            yield break;
        }

        foreach (DummySpawnGroup group in spawnGroups)
        {
            SpawnDummies(group.role, group.count);
        }
    }

    // ── Spawn Logic ───────────────────────────────────────────────────────────

    private void SpawnDummies(PlayerRole role, int count)
    {
        Transform[] spawnPoints = role == PlayerRole.Hunter
            ? LevelManager.Instance.hunterSpawnPositions
            : LevelManager.Instance.vandalistSpawnPositions;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[TestDummySpawner] No spawn positions configured in the LevelManager for role {role}!");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            // Use spawn points cyclically, small offset so dummies don't stand on top of each other
            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];
            Vector3 offset = new Vector3(
                Random.Range(-spawnSpread, spawnSpread),
                0f,
                Random.Range(-spawnSpread, spawnSpread)
            );
            Vector3 spawnPos = spawnPoint.position + offset;

            if (dropToGround && dummyPrefab.TryGetComponent(out CapsuleCollider body))
                spawnPos = SpawnPlacement.DropToGround(spawnPos, body, groundMask);

            GameObject dummy = Instantiate(dummyPrefab, spawnPos, spawnPoint.rotation);

            // Set the role directly on the server before spawning → sent along with the spawn message
            PlayerObjectController poc = dummy.GetComponent<PlayerObjectController>();
            if (poc != null)
            {
                poc.playerRole = role;
            }
            else
            {
                Debug.LogWarning("[TestDummySpawner] Dummy prefab has no PlayerObjectController!");
            }

            if (dummy.GetComponent<PlayerRoleSetup>() == null)
            {
                Debug.LogWarning("[TestDummySpawner] Dummy prefab has no PlayerRoleSetup – " +
                                  "the model stays invisible and catch/respawn won't work for this dummy!");
            }

            NetworkServer.Spawn(dummy);
            spawnedDummies.Add(dummy);

            Debug.Log($"[TestDummySpawner] {role} dummy #{i + 1} spawned at {spawnPos}");
        }
    }
}
