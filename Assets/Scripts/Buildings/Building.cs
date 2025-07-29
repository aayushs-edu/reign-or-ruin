using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    
    [Header("UI Reference")]
    [SerializeField] private HealthBar healthBar;
    
    // Events
    public System.Action<Building> OnBuildingDestroyed;
    public System.Action<Building> OnBuildingRepaired;
    public System.Action<Building, int> OnHealthChanged;
    
    private void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }
    
    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        OnHealthChanged?.Invoke(this, currentHealth);
        UpdateHealthBar();
        
        if (currentHealth <= 0)
        {
            DestroyBuilding();
        }
    }
    
    public void Repair(int repairAmount)
    {
        int oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + repairAmount);
        
        if (oldHealth <= 0 && currentHealth > 0)
        {
            OnBuildingRepaired?.Invoke(this);
        }
        
        OnHealthChanged?.Invoke(this, currentHealth);
        UpdateHealthBar();
    }
    
    private void DestroyBuilding()
    {
        OnBuildingDestroyed?.Invoke(this);
    }
    
    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }
    }
    
    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    public bool IsDestroyed() => currentHealth <= 0;
}