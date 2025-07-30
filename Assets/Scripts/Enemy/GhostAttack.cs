using UnityEngine;

// Projectile-based attack system for Ghost enemies
public class GhostAttack : BaseEnemyAttack
{
    [Header("Fireball Configuration")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform fireballSpawnPoint;
    [SerializeField] private float fireballSpeed = 10f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip fireballLaunchSound;
    
    private AudioSource audioSource;
    
    protected override void InitializeComponents()
    {
        base.InitializeComponents();
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Auto-create spawn point if not assigned
        if (fireballSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("FireballSpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = new Vector3(0.5f, 0.5f, 0f);
            fireballSpawnPoint = spawnObj.transform;
            Debug.LogWarning($"GhostFireballAttack: Created fireballSpawnPoint for {gameObject.name}");
        }
    }
    
    protected override void ValidateConfiguration()
    {
        base.ValidateConfiguration();
        
        if (fireballPrefab == null)
        {
            Debug.LogError($"GhostFireballAttack on {gameObject.name}: No fireball prefab assigned!");
        }
    }
    
    protected override void ExecuteAttack()
    {
        SpawnFireball();
    }
    
    private void SpawnFireball()
    {
        if (fireballPrefab == null || fireballSpawnPoint == null || currentTarget == null) return;
        
        GameObject fireball = Instantiate(fireballPrefab, fireballSpawnPoint.position, Quaternion.identity);
        
        // Set up fireball component
        Fireball fireballScript = fireball.GetComponent<Fireball>();
        if (fireballScript == null)
        {
            fireballScript = fireball.AddComponent<Fireball>();
        }
        
        // Calculate direction to target
        Vector3 direction = (currentTarget.position - fireballSpawnPoint.position).normalized;
        
        // Initialize fireball
        fireballScript.Initialize(direction, fireballSpeed, damage, gameObject);
        
        // Set fireball rotation to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        fireball.transform.rotation = Quaternion.Euler(0, 0, angle);
        
        // Play launch sound
        if (fireballLaunchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireballLaunchSound);
        }
        
        if (debugTargeting)
        {
            Debug.Log($"{gameObject.name} fired fireball at {currentTarget.name}");
        }
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Draw spawn point
        if (fireballSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(fireballSpawnPoint.position, 0.2f);
        }
    }
}