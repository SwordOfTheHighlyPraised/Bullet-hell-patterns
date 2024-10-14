using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PatternWithCooldown
{
    public BulletPatternBase bulletPattern;  // The bullet pattern to fire
    public float attackCooldown = 2f;        // Time between attacks for this pattern
    public float attackDuration = 1f;        // Time for how long this pattern stays active
    public int numberOfTimesToFire = 1;      // How many times the pattern should fire during attack
    public float timeBetweenFires = 0.5f;    // Time delay between each fire
    public float cooldownTimer = 0f;         // Tracks time until the next attack
    [HideInInspector]
    public float activeTimer = 0f;           // Tracks how long the pattern stays active
    [HideInInspector]
    public int currentFireCount = 0;         // Tracks how many times the pattern has fired during attack
    [HideInInspector]
    public float timeBetweenFireTimer = 0f;  // Tracks the time between each fire
    [HideInInspector]
    public bool firstAttackFired = false;    // Tracks whether the initial attack has been fired
    [HideInInspector]
    public bool isPatternActive = false;     // Tracks if the pattern is actively firing
}

public class BulletSpawnerTuesday : MonoBehaviour
{
    public PatternWithCooldown[] bulletPatterns1;  // Array of patterns for Attack Pattern 1
    public PatternWithCooldown[] bulletPatterns2;  // Array of patterns for Attack Pattern 2
    public PatternWithCooldown[] bulletPatterns3;
    private Transform player;
    public bool isFiringPattern1 = false;  // Track if Pattern 1 is firing
    public bool isFiringPattern2 = false;  // Track if Pattern 2 is firing
    public bool isFiringPattern3 = false;  // Track if Pattern 2 is firing

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        // Initialize timers for both patterns
        foreach (var pattern in bulletPatterns1)
        {
            pattern.cooldownTimer = 2f;
        }

        foreach (var pattern in bulletPatterns2)
        {
            pattern.cooldownTimer = 2f;
        }
    }

    void Update()
    {
        if (isFiringPattern1)
        {
            HandlePatternFiring(bulletPatterns1);
        }

        if (isFiringPattern2)
        {
            HandlePatternFiring(bulletPatterns2);
        }

        if (isFiringPattern3)
        {
            HandlePatternFiring(bulletPatterns3);
        }


        // Common method to handle the firing logic for each pattern
        void HandlePatternFiring(PatternWithCooldown[] bulletPatterns)
        {
            foreach (var pattern in bulletPatterns)
            {
                if (pattern.bulletPattern == null) continue; // Skip if no pattern assigned

                // Decrease the cooldown timer
                pattern.cooldownTimer -= Time.deltaTime;

                // If the cooldown timer reaches 0, handle the firing
                if (pattern.cooldownTimer <= 0f)
                {
                    if (!pattern.isPatternActive)
                    {
                        // Start firing the pattern
                        pattern.isPatternActive = true;
                        pattern.activeTimer = pattern.attackDuration;
                        pattern.currentFireCount = 0;
                        pattern.timeBetweenFireTimer = pattern.timeBetweenFires;
                    }

                    // Continue firing while the pattern is active
                    if (pattern.activeTimer > 0f)
                    {
                        // Decrease the timeBetweenFireTimer
                        pattern.timeBetweenFireTimer -= Time.deltaTime;

                        // Fire the bullets if the timer reaches zero
                        if (pattern.timeBetweenFireTimer <= 0f && pattern.currentFireCount < pattern.numberOfTimesToFire)
                        {
                            pattern.bulletPattern.Fire(transform, player);
                            pattern.currentFireCount++;
                            pattern.timeBetweenFireTimer = pattern.timeBetweenFires;
                        }

                        // Decrease the active timer
                        pattern.activeTimer -= Time.deltaTime;
                    }
                    else
                    {
                        // Reset the pattern after the attack duration is over
                        pattern.isPatternActive = false;
                        pattern.cooldownTimer = pattern.attackCooldown;
                    }
                }
            }
        }
    }

    // Method to start a pattern's firing sequence
    // Start firing for Pattern 1
    public void StartFiringPattern1()
    {
        isFiringPattern1 = true;
    }

    // Start firing for Pattern 2
    public void StartFiringPattern2()
    {
    isFiringPattern2 = true;
    }

    public void StartFiringPattern3()
    {
        isFiringPattern3 = true;
    }

    // Stop firing for Pattern 1
    public void StopFiringPattern1()
    {
        isFiringPattern1 = false;
    }

    // Stop firing for Pattern 2
    public void StopFiringPattern2()
    {
        isFiringPattern2 = false;
    }

    public void StopFiringPattern3()
    {
        isFiringPattern3 = false;
    }

    // Stop all firing
    public void StopAllFiring()
    {
        isFiringPattern1 = false;
        isFiringPattern2 = false;
        isFiringPattern3 = false;
    }
}

