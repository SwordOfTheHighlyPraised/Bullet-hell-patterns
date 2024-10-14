using UnityEngine;

public class BulletCollision : MonoBehaviour
{
    [Header("Collision Settings")]
    public LayerMask collisionLayers;  // Define which layers the bullet can collide with
    public bool destroyOnImpact = true;  // Whether the bullet should be destroyed on impact
    public bool destroyOnWorldEdge = true; // Destroy bullet if it hits the world edge
    public int damageAmount = 10;  // Damage dealt by the bullet on impact

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the bullet collided with an object on the specified collision layers
        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            // Handle collision logic (e.g., deal damage, effects, etc.)
            HandleCollision(other);

            // Destroy the bullet if required
            if (destroyOnImpact)
            {
                Destroy(gameObject);
            }
        }

        // Check if the bullet collided with an object tagged "World Edge"
        if (destroyOnWorldEdge && other.CompareTag("World Edge"))
        {
            DestroyBullet();
        }
    }

    private void DestroyBullet()
    {
        Destroy(gameObject);  // Destroy the bullet
    }
    private void HandleCollision(Collider2D other)
    {
        // Check if the object has a Health component to apply damage
        Health healthComponent = other.GetComponent<Health>();
        if (healthComponent != null)
        {
            // Apply damage to the object
                healthComponent.TakeDamage(damageAmount);
        }
    }
}
