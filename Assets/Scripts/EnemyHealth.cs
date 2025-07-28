// Enhanced EnemyHealth.cs - Integrates with PowerOrbSpawner
using UnityEngine;

public class EnemyHealth : Health
{
    [Header("Enemy Specific")]
    [SerializeField] private int powerDropAmount = 1; // Power dropped when killed
    [SerializeField] private bool dropPowerOnDeath = true;
    [SerializeField] private float powerDropDelay = 0f; // Delay before dropping power
    [SerializeField] private bool useOrbSystem = true; // Use new orb system vs old direct power addition
    
    [Header("Enemy Type")]
    [SerializeField] private string enemyTypeName = "Enemy";
    
    protected override void Awake()
    {
        base.Awake();
        
        // Enemies should show health bars by default
        showHealthBar = true;
        
        // Set default values if not configured
        if (maxHealth <= 0)
        {
            maxHealth = 3; // Default enemy health
            currentHealth = maxHealth;
        }
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Ensure enemy has proper tag
        if (!CompareTag("Enemy"))
        {
            Debug.LogWarning($"EnemyHealth: {gameObject.name} doesn't have 'Enemy' tag. Setting it now.");
            gameObject.tag = "Enemy";
        }
    }
    
    protected override void Die()
    {
        if (isDead) return;
        
        // Drop power before dying
        if (dropPowerOnDeath && powerDropAmount > 0)
        {
            if (powerDropDelay > 0)
            {
                StartCoroutine(DropPowerAfterDelay());
            }
            else
            {
                DropPower();
            }
        }
        
        // Call base die (handles death effects, destruction, etc.)
        base.Die();
    }
    
    private void DropPower()
    {
        if (useOrbSystem && PowerOrbSpawner.Instance != null)
        {
            // Use new orb system
            PowerOrbSpawner.Instance.SpawnFromEnemyDeath(transform.position, powerDropAmount);
            Debug.Log($"{enemyTypeName} spawned power orb worth {powerDropAmount} power");
        }
        else if (PowerSystem.Instance != null)
        {
            // Fallback to old direct power addition
            PowerSystem.Instance.AddPowerFromEnemy(powerDropAmount);
            Debug.Log($"{enemyTypeName} dropped {powerDropAmount} power directly");
        }
    }
    
    private System.Collections.IEnumerator DropPowerAfterDelay()
    {
        yield return new WaitForSeconds(powerDropDelay);
        DropPower();
    }
    
    // Public methods specific to enemies
    public void SetPowerDropAmount(int amount)
    {
        powerDropAmount = amount;
    }
    
    public int GetPowerDropAmount() => powerDropAmount;
    
    public void SetEnemyType(string typeName)
    {
        enemyTypeName = typeName;
    }
    
    public string GetEnemyType() => enemyTypeName;
    
    public void SetUseOrbSystem(bool useOrbs)
    {
        useOrbSystem = useOrbs;
    }
    
    // Override to customize enemy-specific damage behavior
    protected override void OnDamageTaken()
    {
        base.OnDamageTaken();
        
        // You can add enemy-specific damage reactions here
        // For example, alert nearby enemies, change AI state, etc.
    }
}