using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHealth = 100;  // Maximum health
    public int currentHealth;

    public bool isPlayer;        // Toggle to indicate if this is the player

    void Start()
    {
        currentHealth = maxHealth;  // Set the health to the maximum value at the start
    }

    // Method to apply damage
    public void TakeDamage(int damageAmount)
    {
        currentHealth -= damageAmount;  // Subtract the damage from the current health

        if (currentHealth <= 0)
        {
            Die();  // Call the Die function if health drops to zero or below
        }
    }

    // Method to heal
    public void Heal(int healAmount)
    {
        currentHealth += healAmount;  // Add health

        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;  // Clamp health to the maxHealth value
        }
    }

    // Function to handle death
    void Die()
    {
        if (isPlayer)
        {
            Debug.Log("Player has died!");
            // Add player-specific death logic here, such as showing game over screen
        }
        else
        {
            Debug.Log("Enemy has died!");
            Destroy(gameObject);  // Destroy enemy GameObject when they die
        }
    }

    // Optional: Expose the current health
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
}
