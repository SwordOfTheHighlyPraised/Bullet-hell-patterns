using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDetection : MonoBehaviour
{
    public float detectionRadius = 10f;
    public LayerMask playerLayer;
    public Transform firePoint;
    public BulletPatternController bulletController;

    void Update()
    {
        Collider2D player = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (player != null)
        {
            bulletController.FirePattern(0); // Fires the first pattern at the player
        }
    }
}

