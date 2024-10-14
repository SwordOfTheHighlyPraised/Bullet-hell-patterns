using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SummonablePatternWithCooldown
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

public class BulletSpawnerSunday : MonoBehaviour
{
    public SummonablePatternWithCooldown[] bulletPatterns1;  // Array of patterns for Attack Pattern 1
    private Transform player;
    public bool isFiringPattern1 = false;  // Track if Pattern 1 is firing

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        // Initialize cooldown timers for all patterns
        InitializePatternCooldowns(bulletPatterns1);
    }

    void Update()
    {
        if (isFiringPattern1)
        {
            HandlePatternFiring(bulletPatterns1);
        }
    }

    // Method to fire in a specific direction
    public void FireInDirection(Vector3 direction)
    {
        foreach (var pattern in bulletPatterns1)
        {
            if (pattern.bulletPattern != null)
            {
                // Check if fireAngle is 0, then fire in the direction of movement
                if (pattern.bulletPattern.fireAngle == 0)
                {
                    pattern.bulletPattern.FireInDirection(transform, direction);
                }
                else
                {
                    // Fire using default logic
                    pattern.bulletPattern.Fire(transform, player);
                }
            }
        }
    }

    // Common method to handle the firing logic for each pattern
    void HandlePatternFiring(SummonablePatternWithCooldown[] bulletPatterns)
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

    // Method to initialize cooldown timers for each pattern
    void InitializePatternCooldowns(SummonablePatternWithCooldown[] bulletPatterns)
    {
        foreach (var pattern in bulletPatterns)
        {
            pattern.cooldownTimer = pattern.attackCooldown;
        }
    }

    // Methods to start and stop the firing sequences
    public void StartFiringPattern1() { isFiringPattern1 = true; }

    public void StopFiringPattern1() { isFiringPattern1 = false; }


    public void StopAllFiring()
    {
        isFiringPattern1 = false;
    }
}