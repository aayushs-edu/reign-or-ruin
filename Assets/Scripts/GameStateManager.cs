using UnityEngine;
using System.Collections;

public enum GameState
{
    MainMenu,
    Loading,
    Day,
    DayToNight,
    Night,
    NightToDay,
    GameOver,
    Paused
}

/// <summary>
/// Manages the overall game state and coordinates between different systems.
/// Handles the day/night cycle, wave progression, and UI state management.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float dayDuration = 30f;
    [SerializeField] private int maxWaves = 10; // Day 10 win condition
    [SerializeField] private bool startWithDay = false;
    [SerializeField] private bool debugStateChanges = true;
    
    [Header("System References")]
    [SerializeField] private DayNightCycleManager dayNightManager;
    [SerializeField] private VillageMenu villageUI;
    [SerializeField] private EnemyWaveSystem waveSystem;
    [SerializeField] private VillageManager villageManager;
    
    // State tracking
    private GameState currentState = GameState.Loading;
    private GameState previousState = GameState.Loading;
    private int currentWave = 1;
    private bool isGameRunning = false;
    
    // Events
    public System.Action<GameState, GameState> OnStateChanged;
    public System.Action<int> OnWaveChanged;
    public System.Action OnGameWon;
    public System.Action OnGameLost;
    
    public static GameStateManager Instance { get; private set; }
    
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
        InitializeGame();
    }
    
    private void InitializeGame()
    {
        SetupReferences();
        SetupEventListeners();
        
        // Start the game
        if (startWithDay)
        {
            ChangeState(GameState.Day);
        }
        else
        {
            ChangeState(GameState.Night);
        }
        
        isGameRunning = true;
    }
    
    private void SetupReferences()
    {
        // Auto-find references if not assigned
        if (dayNightManager == null)
            dayNightManager = FindObjectOfType<DayNightCycleManager>();
        if (villageUI == null)
            villageUI = FindObjectOfType<VillageMenu>();
        if (waveSystem == null)
            waveSystem = FindObjectOfType<EnemyWaveSystem>();
        if (villageManager == null)
            villageManager = FindObjectOfType<VillageManager>();
            
        // Configure day duration
        if (dayNightManager != null)
            dayNightManager.SetDayDuration(dayDuration);
    }
    
    private void SetupEventListeners()
    {
        // Day/Night cycle events
        if (dayNightManager != null)
        {
            dayNightManager.OnDayStarted += HandleDayStarted;
            dayNightManager.OnNightStarted += HandleNightStarted;
            dayNightManager.OnDayNightTransition += HandleDayNightTransition;
        }
        
        // Wave system events
        if (waveSystem != null)
        {
            waveSystem.OnWaveStart += HandleWaveStart;
            waveSystem.OnWaveComplete += HandleWaveComplete;
        }
        
        // Village events
        if (villageManager != null)
        {
            villageManager.OnVillagerRebel += HandleVillagerRebel;
            villageManager.OnMassRebellion += HandleMassRebellion;
        }
    }
    
    #region State Management
    
    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;
        
        previousState = currentState;
        currentState = newState;
        
        if (debugStateChanges)
            Debug.Log($"GameState changed: {previousState} -> {currentState}");
        
        HandleStateChange(previousState, currentState);
        OnStateChanged?.Invoke(previousState, currentState);
    }
    
    private void HandleStateChange(GameState from, GameState to)
    {
        // Exit previous state
        switch (from)
        {
            case GameState.Day:
                OnExitDay();
                break;
            case GameState.Night:
                OnExitNight();
                break;
        }
        
        // Enter new state
        switch (to)
        {
            case GameState.Day:
                OnEnterDay();
                break;
            case GameState.DayToNight:
                OnEnterDayToNight();
                break;
            case GameState.Night:
                OnEnterNight();
                break;
            case GameState.NightToDay:
                OnEnterNightToDay();
                break;
            case GameState.GameOver:
                OnEnterGameOver();
                break;
            case GameState.Paused:
                OnEnterPaused();
                break;
        }
    }
    
    #endregion
    
    #region State Handlers
    
    private void OnEnterDay()
    {
        if (debugStateChanges)
            Debug.Log("Entered Day state - Power allocation phase");
        
        // Enable power allocation UI
        // if (villageUI != null)
        //     villageUI.RefreshAllUI();
        
        // Process village recovery and food distribution
        if (villageManager != null)
            villageManager.ProcessDayCycle();
    }
    
    private void OnExitDay()
    {
        if (debugStateChanges)
            Debug.Log("Exiting Day state");
        
        // Apply all power allocations
        ApplyPowerAllocations();
    }
    
    private void OnEnterDayToNight()
    {
        if (debugStateChanges)
            Debug.Log("Transitioning from Day to Night");
        
        // Start transition animation
        if (dayNightManager != null)
            dayNightManager.StartNightTransition();
    }
    
    private void OnEnterNight()
    {
        if (debugStateChanges)
            Debug.Log("Entered Night state - Combat phase");
        
        // Start the next wave
        if (waveSystem != null && isGameRunning)
        {
            // The wave system should handle starting the appropriate wave
            currentWave = dayNightManager != null ? dayNightManager.GetCurrentWave() : currentWave;
            OnWaveChanged?.Invoke(currentWave);
            
            // Check win condition
            if (currentWave > maxWaves)
            {
                WinGame();
                return;
            }
        }
        
        // Process night cycle effects
        if (villageManager != null)
            villageManager.ProcessNightCycle();
    }
    
    private void OnExitNight()
    {
        if (debugStateChanges)
            Debug.Log("Exiting Night state");
    }
    
    private void OnEnterNightToDay()
    {
        if (debugStateChanges)
            Debug.Log("Transitioning from Night to Day");
        
        // Start transition animation
        if (dayNightManager != null)
            dayNightManager.StartDayTransition();
    }
    
    private void OnEnterGameOver()
    {
        if (debugStateChanges)
            Debug.Log("Game Over");
        
        isGameRunning = false;
        
        // Stop all systems
        if (waveSystem != null)
            waveSystem.enabled = false;
        if (dayNightManager != null)
            dayNightManager.enabled = false;
    }
    
    private void OnEnterPaused()
    {
        Time.timeScale = 0f;
    }
    
    private void OnExitPaused()
    {
        Time.timeScale = 1f;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandleDayStarted()
    {
        if (currentState != GameState.Day)
            ChangeState(GameState.Day);
    }
    
    private void HandleNightStarted()
    {
        if (currentState != GameState.Night)
            ChangeState(GameState.Night);
    }
    
    private void HandleDayNightTransition(bool isDay)
    {
        if (isDay && currentState != GameState.Day && currentState != GameState.NightToDay)
        {
            ChangeState(GameState.NightToDay);
        }
        else if (!isDay && currentState != GameState.Night && currentState != GameState.DayToNight)
        {
            ChangeState(GameState.DayToNight);
        }
    }
    
    private void HandleWaveStart(int waveNumber)
    {
        currentWave = waveNumber;
        OnWaveChanged?.Invoke(currentWave);
        
        if (debugStateChanges)
            Debug.Log($"Wave {waveNumber} started");
    }
    
    private void HandleWaveComplete(int waveNumber)
    {
        if (debugStateChanges)
            Debug.Log($"Wave {waveNumber} completed");
        
        // Check if we should transition to day
        if (currentState == GameState.Night)
        {
            ChangeState(GameState.NightToDay);
        }
    }
    
    private void HandleVillagerRebel(Villager rebel)
    {
        if (debugStateChanges)
            Debug.Log($"Villager rebelled: {rebel.GetRole()}");
        
        // Check if too many rebels (game over condition)
        CheckGameOverConditions();
    }
    
    private void HandleMassRebellion(System.Collections.Generic.List<Villager> rebels)
    {
        if (debugStateChanges)
            Debug.Log($"Mass rebellion: {rebels.Count} villagers rebelled!");
        
        // Immediate game over on mass rebellion
        LoseGame("Mass rebellion occurred!");
    }
    
    #endregion
    
    #region Game Flow Control
    
    public void PauseGame()
    {
        if (currentState != GameState.Paused && currentState != GameState.GameOver)
        {
            ChangeState(GameState.Paused);
        }
    }
    
    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            ChangeState(previousState);
        }
    }
    
    public void RestartGame()
    {
        // Reset all systems
        currentWave = 1;
        isGameRunning = true;
        
        // Reset wave system
        if (waveSystem != null)
            waveSystem.enabled = true;
        
        // Reset day/night manager
        if (dayNightManager != null)
            dayNightManager.enabled = true;
        
        // Start over
        if (startWithDay)
            ChangeState(GameState.Day);
        else
            ChangeState(GameState.Night);
    }
    
    public void WinGame()
    {
        if (debugStateChanges)
            Debug.Log("Player Won! Survived all waves!");
        
        ChangeState(GameState.GameOver);
        OnGameWon?.Invoke();
    }
    
    public void LoseGame(string reason = "")
    {
        if (debugStateChanges)
            Debug.Log($"Player Lost! Reason: {reason}");
        
        ChangeState(GameState.GameOver);
        OnGameLost?.Invoke();
    }
    
    #endregion
    
    #region Power Allocation
    
    private void ApplyPowerAllocations()
    {
        // This should finalize all power allocations made during the day
        if (villageUI != null && villageManager != null)
        {
            // Apply power changes and calculate discontent
            if (debugStateChanges)
                Debug.Log("Applied power allocations for the night");
        }
    }
    
    #endregion
    
    #region Game Over Conditions
    
    private void CheckGameOverConditions()
    {
        if (!isGameRunning) return;
        
        // Check player death (this should be connected to player health system)
        // if (playerHealth <= 0) LoseGame("Player died");
        
        // Check village destruction (if all villagers are rebels or dead)
        if (villageManager != null)
        {
            var villagers = FindObjectsOfType<Villager>();
            int totalVillagers = villagers.Length;
            int rebels = 0;
            int dead = 0;
            
            foreach (var villager in villagers)
            {
                if (!villager.IsLoyal()) rebels++;
                // Add death check: if (!villager.IsAlive()) dead++;
            }
            
            // Game over if too many rebels (>50% of village)
            if (rebels > totalVillagers / 2)
            {
                LoseGame($"Too many rebels: {rebels}/{totalVillagers}");
                return;
            }
            
            // Game over if all villagers are dead
            if (dead >= totalVillagers)
            {
                LoseGame("All villagers are dead");
                return;
            }
        }
    }
    
    #endregion
    
    #region Public Interface
    
    public GameState GetCurrentState() => currentState;
    public GameState GetPreviousState() => previousState;
    public int GetCurrentWave() => currentWave;
    public bool IsGameRunning() => isGameRunning;
    public float GetDayDuration() => dayDuration;
    
    public void SetDayDuration(float duration)
    {
        dayDuration = duration;
        if (dayNightManager != null)
            dayNightManager.SetDayDuration(duration);
    }
    
    public void ForceNextPhase()
    {
        switch (currentState)
        {
            case GameState.Day:
                ChangeState(GameState.DayToNight);
                break;
            case GameState.Night:
                ChangeState(GameState.NightToDay);
                break;
        }
    }
    
    #endregion
    
    #region Unity Events
    
    private void Update()
    {
        // Handle input
        HandleInput();
        
        // Update game over conditions periodically
        if (isGameRunning && Time.frameCount % 60 == 0) // Every second at 60 FPS
        {
            CheckGameOverConditions();
        }
    }
    
    private void HandleInput()
    {
        // ESC to pause/unpause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Paused)
                ResumeGame();
            else
                PauseGame();
        }
        
        // R to restart (debug)
        if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
        {
            RestartGame();
        }
        
        // Skip phase (debug)
        if (Input.GetKeyDown(KeyCode.N) && Input.GetKey(KeyCode.LeftControl))
        {
            ForceNextPhase();
        }
    }
    
    private void OnDestroy()
    {
        // Clean up event listeners
        if (dayNightManager != null)
        {
            dayNightManager.OnDayStarted -= HandleDayStarted;
            dayNightManager.OnNightStarted -= HandleNightStarted;
            dayNightManager.OnDayNightTransition -= HandleDayNightTransition;
        }
        
        if (waveSystem != null)
        {
            waveSystem.OnWaveStart -= HandleWaveStart;
            waveSystem.OnWaveComplete -= HandleWaveComplete;
        }
        
        if (villageManager != null)
        {
            villageManager.OnVillagerRebel -= HandleVillagerRebel;
            villageManager.OnMassRebellion -= HandleMassRebellion;
        }
    }
    
    #endregion
    
    #if UNITY_EDITOR
    [ContextMenu("Force Day")]
    private void ForceDayState()
    {
        if (Application.isPlaying)
            ChangeState(GameState.Day);
    }
    
    [ContextMenu("Force Night")]
    private void ForceNightState()
    {
        if (Application.isPlaying)
            ChangeState(GameState.Night);
    }
    
    [ContextMenu("Next Wave")]
    private void ForceNextWave()
    {
        if (Application.isPlaying)
        {
            currentWave++;
            OnWaveChanged?.Invoke(currentWave);
        }
    }
    
    [ContextMenu("Win Game")]
    private void ForceWin()
    {
        if (Application.isPlaying)
            WinGame();
    }
    
    [ContextMenu("Lose Game")]
    private void ForceLose()
    {
        if (Application.isPlaying)
            LoseGame("Debug forced loss");
    }
        #endif
}