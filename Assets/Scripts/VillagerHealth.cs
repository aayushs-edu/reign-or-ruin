// VillagerHealth.cs
using UnityEngine;

public class VillagerHealth : Health
{
    [Header("Villager Specific")]
    [SerializeField] private int powerDropOnDeath = 5;
    [SerializeField] private bool canRebel = true;
    [SerializeField] private float rebelHealthMultiplier = 1.5f;
    
    private bool isRebel = false;
    private VillagerAI villagerAI;
    
    protected override void Awake()
    {
        base.Awake();
        villagerAI = GetComponent<VillagerAI>();
    }
    
    protected override void Die()
    {
        // Drop power if killed
        if (PowerSystem.Instance != null && powerDropOnDeath > 0)
        {
            PowerSystem.Instance.AddPowerFromEnemy(powerDropOnDeath);
        }
        
        base.Die();
    }
    
    public void ConvertToRebel()
    {
        if (!canRebel || isRebel) return;
        
        isRebel = true;
        gameObject.tag = "Enemy"; // Change tag so enemies don't attack rebels
        
        // Increase health as rebel
        int newMaxHealth = Mathf.RoundToInt(maxHealth * rebelHealthMultiplier);
        SetMaxHealth(newMaxHealth, true);
        
        // Visual indicator
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.5f, 0.5f); // Reddish tint
        }
        
        // Change AI behavior
        if (villagerAI != null)
        {
            villagerAI.SetRebel(true);
        }
    }
    
    public bool IsRebel() => isRebel;
}
