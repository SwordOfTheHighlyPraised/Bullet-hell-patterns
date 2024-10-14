using System.Collections;
using UnityEngine;

public class SummonableEntity : MonoBehaviour
{
    public BulletSpawnerSunday bulletSpawner; // BulletSpawner attached to the summonable entity
    public float summonDuration = 10f;  // Time before the entity is destroyed
    public int health = 5;              // Health of the summonable entity
    public bool isHealthBasedDestruction = false; // Toggle to destroy based on health

    private Transform player;           // Reference to the player

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        // Start firing Pattern 1 at the player, or other patterns based on logic
        if (bulletSpawner != null)
        {
            bulletSpawner.StartFiringPattern1();
        }

        // If it's not health-based destruction, destroy after summonDuration
        if (!isHealthBasedDestruction)
        {
            StartCoroutine(DestroyAfterDuration());
        }
    }

    void Update()
    {
        // Continuously fire bullets at the player based on the current patterns
        if (bulletSpawner != null)
        {
            // The firing logic is handled in BulletSpawnerSunday via cooldowns, no need to manually fire here
        }
    }

    private IEnumerator DestroyAfterDuration()
    {
        yield return new WaitForSeconds(summonDuration);
        DestroyEntity();
    }

    public void TakeDamage(int damage)
    {
        if (isHealthBasedDestruction)
        {
            health -= damage;

            if (health <= 0)
            {
                DestroyEntity();
            }
        }
    }

    public void DestroyEntity()
    {
        // Optional: Trigger destruction effects, such as animations or sounds
        if (bulletSpawner != null)
        {
            bulletSpawner.StopAllFiring(); // Stop firing bullets
        }
        Destroy(gameObject); // Destroy the summonable entity
    }
}