// PlayerHealth.cs
using UnityEngine;

public class PlayerHealth : Health
{
    [Header("Player Specific")]
    [SerializeField] private float invulnerabilityDuration = 1f;
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private Vector3 respawnPosition;
    
    private bool isInvulnerable = false;
    private float invulnerabilityEndTime;
    
    protected override void Awake()
    {
        base.Awake();
        respawnPosition = transform.position;
    }
    
    public override void TakeDamage(int damage)
    {
        if (isInvulnerable) return;
        
        base.TakeDamage(damage);
        
        if (!isDead && invulnerabilityDuration > 0)
        {
            StartInvulnerability();
        }
    }
    
    private void StartInvulnerability()
    {
        isInvulnerable = true;
        invulnerabilityEndTime = Time.time + invulnerabilityDuration;
        StartCoroutine(InvulnerabilityEffect());
    }
    
    private System.Collections.IEnumerator InvulnerabilityEffect()
    {
        float blinkInterval = 0.1f;
        
        while (Time.time < invulnerabilityEndTime)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
            }
            yield return new WaitForSeconds(blinkInterval);
        }
        
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
        isInvulnerable = false;
    }
    
    protected override void Die()
    {
        base.Die();
        
        if (respawnOnDeath)
        {
            Invoke(nameof(Respawn), 2f);
        }
    }
    
    private void Respawn()
    {
        transform.position = respawnPosition;
        Revive();
    }
}