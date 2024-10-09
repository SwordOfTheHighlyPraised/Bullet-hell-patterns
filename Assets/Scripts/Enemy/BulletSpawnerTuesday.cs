using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletSpawnerTuesday : MonoBehaviour
{
    public BulletPatternBase[] bulletPatterns;  // Assign different patterns via the inspector
    private Transform player;

    void Start()
    {
        // Find the player by tag and store their transform
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    // This method will be called to fire bullet patterns
    public void FirePattern()
    {
        // Loop through all assigned bullet patterns
        foreach (var pattern in bulletPatterns)
        {
            if (pattern != null)  // Ensure the pattern is assigned
            {
                pattern.Fire(transform, player);  // Fire the pattern aimed at the player's position
            }
        }
    }
}
