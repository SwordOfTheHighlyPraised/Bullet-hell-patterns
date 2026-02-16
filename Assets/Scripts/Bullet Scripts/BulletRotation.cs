using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletRotation : MonoBehaviour
{
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (rb != null && rb.linearVelocity.magnitude > 0.1f)  // Only rotate if the bullet is moving
        {
            // Get the angle of movement
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;

            // Rotate the bullet so that 'transform.up' (the top of the bullet) points in the direction of movement
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);  // Adjust the angle by -90 degrees to align 'up' with the movement direction
        }
    }
}
