using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("Attack Settings")]
    public BulletSpawner bulletSpawner;  // Reference to the BulletSpawner script.
    public Transform player;  // Reference to the player.

    [SerializeField]
    private bool isFiring = false;  // Control if the enemy is firing or not.

    [SerializeField]
    private float fireDuration = 2f;  // How long the enemy should fire bullets.

    private float fireTimer = 0f;
    [SerializeField] private float attackRange = 10f;  // Range within which the enemy will attack the player
    [SerializeField] private float attackDelay = 2f;  // Time between attacks.
    private float attackTimer = 0f;  // Timer to manage attack delay.

    void Start()
    {
        // Find player object by tag
        player = GameObject.FindWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogError("Player not found! Make sure the Player GameObject has the correct tag.");
        }
    }

    void Update()
    {
        // Ensure player reference is valid
        if (player == null) return;

        // Calculate distance to player
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Debug.Log("Distance to player: " + distanceToPlayer);  // Debug distance

        // Attack the player if within range and attack delay has passed
        attackTimer += Time.deltaTime;
        if (distanceToPlayer <= attackRange && attackTimer >= attackDelay)
        {
            Debug.Log("Player in range. Attacking...");
            AttackPlayer();  // Call the bullet spawner to attack the player
            attackTimer = 0f;  // Reset the attack timer
        }

        // Check if enemy should be firing
        if (isFiring)
        {
            fireTimer += Time.deltaTime;

            // Fire bullets
            AttackPlayer();

            // Stop firing after the specified duration
            if (fireTimer >= fireDuration)
            {
                StopFiring();
            }
        }
    }

    // Start firing bullets at the player
    public void StartFiring()
    {
        isFiring = true;
        fireTimer = 0f;  // Reset the fire timer
    }

    // Stop firing bullets
    public void StopFiring()
    {
        isFiring = false;
    }

    // This method is called to make the enemy attack the player
    void AttackPlayer()
    {
        // Calculate the direction from the enemy to the player
        Vector2 directionToPlayer = (player.position - transform.position).normalized;

        // Calculate the angle to the player in degrees
        float angleToPlayer = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;

        // Set the firing angle in the bullet spawner to face the player
        bulletSpawner.SetFireAngle(angleToPlayer);

        // Fire the bullets
        bulletSpawner.SpawnBullets();
    }
}
