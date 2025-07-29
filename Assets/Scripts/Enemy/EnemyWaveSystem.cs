using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class EnemyType
{
    public GameObject enemyPrefab;
    public string enemyName;
    public int difficultyCost = 1;      // How much "budget" this enemy costs
    public int powerValue = 1;          // Power dropped when killed
    public float spawnWeight = 1f;      // Base spawn probability weight
    public int minWaveToAppear = 1;     // Earliest wave this enemy can appear
}

[System.Serializable]
public class WaveConfiguration
{
    [Header("Wave Timing")]
    public float timeBetweenWaves = 15f;        // Seconds between waves
    public float timeBetweenSpawns = 0.8f;      // Seconds between individual enemy spawns
    public float waveTimingReduction = 0.95f;   // Multiplier to make waves faster over time
    
    [Header("Difficulty Progression")]
    public int baseBudget = 10;                 // Starting wave budget
    public float budgetGrowthRate = 1.2f;       // How much budget increases per wave
    public int maxEnemiesPerWave = 25;          // Cap on enemies per wave
    public int minEnemiesPerWave = 3;           // Minimum enemies per wave
    
    [Header("Elite Enemy Progression")]
    public AnimationCurve eliteSpawnChance;     // Curve for elite (hard) enemy chance over waves
    public int maxElitesPerWave = 3;            // Cap on elite enemies per wave
}

public class EnemyWaveSystem : MonoBehaviour
{
    [Header("Enemy Configuration")]
    [SerializeField] private EnemyType[] easyEnemies;
    [SerializeField] private EnemyType[] mediumEnemies;
    [SerializeField] private EnemyType[] hardEnemies;

    [Header("Wave Settings")]
    [SerializeField] private WaveConfiguration waveConfig;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRadius = 2f;
    [SerializeField] private Transform playerTarget; // What enemies should pathfind to

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Public Events
    public System.Action<int> OnWaveStart;      // Wave number
    public System.Action<int> OnWaveComplete;   // Wave number
    public System.Action<EnemyType> OnEnemySpawned;

    // Private variables
    private int currentWave = 0;
    private bool waveInProgress = false;
    private List<GameObject> currentWaveEnemies = new List<GameObject>();
    private Coroutine waveCoroutine;

    // Wave statistics for balancing
    private int totalEnemiesKilled = 0;
    private float averageWaveTime = 0f;
    private int playerDeaths = 0;

    private void Start()
    {
        ValidateConfiguration();
        StartNextWave();
    }

    private void ValidateConfiguration()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("EnemyWaveSystem: No spawn points assigned!");
        }

        if (easyEnemies.Length == 0)
        {
            Debug.LogError("EnemyWaveSystem: No easy enemies configured!");
        }

        // Set up elite spawn curve if not configured
        if (waveConfig.eliteSpawnChance.keys.Length == 0)
        {
            // Default curve: 0% at wave 1, 25% at wave 10, 60% at wave 20, 100% at wave 30
            AnimationCurve defaultCurve = new AnimationCurve();
            defaultCurve.AddKey(1, 0f);
            defaultCurve.AddKey(5, 0.1f);
            defaultCurve.AddKey(10, 0.25f);
            defaultCurve.AddKey(20, 0.6f);
            defaultCurve.AddKey(30, 1f);
            waveConfig.eliteSpawnChance = defaultCurve;
        }
    }

    public void StartNextWave()
    {
        if (waveInProgress) return;

        currentWave++;
        waveInProgress = true;
        currentWaveEnemies.Clear();

        OnWaveStart?.Invoke(currentWave);

        if (debugMode)
        {
            Debug.Log($"Starting Wave {currentWave}");
        }

        // Always log for debugging visibility issues
        Debug.Log($"Wave {currentWave} starting with {spawnPoints.Length} spawn points");

        waveCoroutine = StartCoroutine(SpawnWave());
    }

    private IEnumerator SpawnWave()
    {
        // Calculate wave difficulty budget
        int waveBudget = Mathf.RoundToInt(waveConfig.baseBudget * Mathf.Pow(waveConfig.budgetGrowthRate, currentWave - 1));

        // Generate enemy composition for this wave
        List<EnemyType> enemiesToSpawn = GenerateWaveComposition(waveBudget);

        if (debugMode)
        {
            Debug.Log($"Wave {currentWave}: Budget={waveBudget}, Enemies={enemiesToSpawn.Count}");
        }

        // Spawn enemies with timing
        float currentSpawnDelay = waveConfig.timeBetweenSpawns;

        foreach (EnemyType enemyType in enemiesToSpawn)
        {
            SpawnEnemy(enemyType);
            yield return new WaitForSeconds(currentSpawnDelay);

            // Slightly randomize spawn timing to feel more organic
            currentSpawnDelay = waveConfig.timeBetweenSpawns * Random.Range(0.7f, 1.3f);
        }

        // Wait for all enemies to be defeated
        yield return new WaitUntil(() => currentWaveEnemies.Count == 0);

        // Wave completed
        CompleteWave();
    }

    private List<EnemyType> GenerateWaveComposition(int budget)
    {
        List<EnemyType> composition = new List<EnemyType>();
        int remainingBudget = budget;
        int enemyCount = 0;

        // Calculate spawn weights based on current wave
        Dictionary<EnemyType, float> adjustedWeights = CalculateSpawnWeights();

        // Create weighted list of available enemies
        List<EnemyType> availableEnemies = new List<EnemyType>();
        List<float> weights = new List<float>();

        foreach (var kvp in adjustedWeights)
        {
            if (kvp.Key.minWaveToAppear <= currentWave)
            {
                availableEnemies.Add(kvp.Key);
                weights.Add(kvp.Value);
            }
        }

        // Generate enemies until budget is exhausted or max enemies reached
        while (remainingBudget > 0 && enemyCount < waveConfig.maxEnemiesPerWave)
        {
            // Select random enemy based on weights
            EnemyType selectedEnemy = SelectWeightedRandom(availableEnemies, weights);

            // Check if we can afford this enemy
            if (selectedEnemy.difficultyCost <= remainingBudget)
            {
                composition.Add(selectedEnemy);
                remainingBudget -= selectedEnemy.difficultyCost;
                enemyCount++;
            }
            else
            {
                // Try to find a cheaper enemy
                bool foundCheaper = false;
                for (int i = 0; i < availableEnemies.Count; i++)
                {
                    if (availableEnemies[i].difficultyCost <= remainingBudget)
                    {
                        composition.Add(availableEnemies[i]);
                        remainingBudget -= availableEnemies[i].difficultyCost;
                        enemyCount++;
                        foundCheaper = true;
                        break;
                    }
                }

                if (!foundCheaper) break; // No affordable enemies left
            }
        }

        // Ensure minimum enemies per wave
        while (composition.Count < waveConfig.minEnemiesPerWave && easyEnemies.Length > 0)
        {
            composition.Add(easyEnemies[Random.Range(0, easyEnemies.Length)]);
        }

        // Shuffle the composition for variety
        ShuffleList(composition);

        return composition;
    }

    private Dictionary<EnemyType, float> CalculateSpawnWeights()
    {
        Dictionary<EnemyType, float> weights = new Dictionary<EnemyType, float>();

        // Easy enemies: Start high, gradually decrease
        float easyWeight = Mathf.Lerp(3f, 1f, (float)currentWave / 20f);
        foreach (var enemy in easyEnemies)
        {
            weights[enemy] = enemy.spawnWeight * easyWeight;
        }

        // Medium enemies: Gradual increase, peak around wave 15
        float mediumWeight = Mathf.Lerp(0.5f, 2f, Mathf.Min((float)currentWave / 15f, 1f));
        foreach (var enemy in mediumEnemies)
        {
            weights[enemy] = enemy.spawnWeight * mediumWeight;
        }

        // Hard enemies: Use elite spawn curve
        float hardWeight = waveConfig.eliteSpawnChance.Evaluate(currentWave);
        foreach (var enemy in hardEnemies)
        {
            weights[enemy] = enemy.spawnWeight * hardWeight;
        }

        return weights;
    }

    private EnemyType SelectWeightedRandom(List<EnemyType> enemies, List<float> weights)
    {
        float totalWeight = 0f;
        foreach (float weight in weights)
        {
            totalWeight += weight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < enemies.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue <= currentWeight)
            {
                return enemies[i];
            }
        }

        return enemies[enemies.Count - 1]; // Fallback
    }

    private void SpawnEnemy(EnemyType enemyType)
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("No spawn points assigned!");
            return;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogError("Could not find valid spawn position on NavMesh!");
            return;
        }

        // Instantiate enemy
        GameObject enemy = Instantiate(enemyType.enemyPrefab, spawnPosition, Quaternion.identity);

        if (enemy == null)
        {
            Debug.LogError($"Failed to instantiate enemy prefab: {enemyType.enemyName}");
            return;
        }

        // Ensure enemy stays at Z=0 for 2D
        Vector3 pos = enemy.transform.position;
        pos.z = 0f;
        enemy.transform.position = pos;

        currentWaveEnemies.Add(enemy);

        // Debug logging
        Debug.Log($"Spawned {enemyType.enemyName} at position {spawnPosition}. Total enemies: {currentWaveEnemies.Count}");

        // Set up enemy pathfinding target
        var enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null && playerTarget != null)
        {
            enemyAI.SetTarget(playerTarget);
        }

        // Set up enemy death callback
        var enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += () => OnEnemyDied(enemy, enemyType);
        }

        OnEnemySpawned?.Invoke(enemyType);
    }

    private Vector3 GetValidSpawnPosition()
    {
        int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Select random spawn point from your list
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // Add some randomness to spawn position within radius
            Vector3 randomOffset = (Vector3)Random.insideUnitCircle * spawnRadius;
            Vector3 testPosition = spawnPoint.position + randomOffset;

            // Check if this position is on the NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(testPosition, out hit, spawnRadius * 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Found a valid position on NavMesh
                return hit.position;
            }
        }

        // Fallback: try spawn points directly
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPoints[i].position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return Vector3.zero; // Failed to find valid position
    }

    private void OnEnemyDied(GameObject enemy, EnemyType enemyType)
    {
        if (currentWaveEnemies.Contains(enemy))
        {
            currentWaveEnemies.Remove(enemy);
            totalEnemiesKilled++;

            Debug.Log($"Enemy {enemyType.enemyName} died. Remaining: {currentWaveEnemies.Count}");

            // Add power to the communal pool
            if (PowerSystem.Instance != null)
            {
                PowerSystem.Instance.AddPowerFromEnemy(enemyType.powerValue);
                Debug.Log($"Added {enemyType.powerValue} power from {enemyType.enemyName}");
            }
        }
        else
        {
            Debug.LogWarning($"Tried to remove enemy {enemy.name} but it wasn't in the current wave list!");
        }
    }

    private void CompleteWave()
    {
        waveInProgress = false;
        OnWaveComplete?.Invoke(currentWave);

        if (debugMode)
        {
            Debug.Log($"Wave {currentWave} completed!");
        }

        // Reduce timing for next wave (waves get faster)
        waveConfig.timeBetweenWaves *= waveConfig.waveTimingReduction;
        waveConfig.timeBetweenSpawns *= waveConfig.waveTimingReduction;

        // Start next wave after delay
        Invoke(nameof(StartNextWave), waveConfig.timeBetweenWaves);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // Public utility methods
    public int GetCurrentWave() => currentWave;
    public bool IsWaveInProgress() => waveInProgress;
    public int GetEnemiesRemaining() => currentWaveEnemies.Count;

    // For debugging and balancing
    public void ForceNextWave()
    {
        if (waveCoroutine != null)
        {
            StopCoroutine(waveCoroutine);
        }

        // Clear current enemies
        foreach (var enemy in currentWaveEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }

        CompleteWave();
    }
    
    public float GetTimeBetweenWaves() => waveConfig.timeBetweenWaves;
    public List<GameObject> GetCurrentWaveEnemies() => new List<GameObject>(currentWaveEnemies);
}