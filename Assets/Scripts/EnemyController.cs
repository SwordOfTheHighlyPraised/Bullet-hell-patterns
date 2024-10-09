using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;  // Speed of the enemy.
    public float jumpForce = 5f;  // Jump force for the enemy.
    private bool isGrounded = true;  // Check if the enemy is grounded.

    private Rigidbody2D rb;

    [Header("Collision Settings")]
    public LayerMask groundLayerMask;  // Layer mask to detect ground.

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();  // Get the Rigidbody2D component.
    }

    void Update()
    {
        MoveEnemy();  // Handle the platformer movement.
    }

    // Move the enemy left and right, like a platformer.
    void MoveEnemy()
    {
        // Simple AI for moving left and right.
        float moveDirection = Mathf.PingPong(Time.time * moveSpeed, 2) - 1;  // Move back and forth.
        rb.velocity = new Vector2(moveDirection * moveSpeed, rb.velocity.y);

        // Check for jumping logic (optional, e.g., for patrolling over platforms).
        if (isGrounded && Random.value < 0.01f)  // Randomly jump if on the ground.
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            isGrounded = false;
        }
    }

    // Use a collision mask to check if the enemy is grounded.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the object collided with is part of the ground layer.
        if (IsGroundLayer(collision.gameObject))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // Check if the object leaving collision is part of the ground layer.
        if (IsGroundLayer(collision.gameObject))
        {
            isGrounded = false;
        }
    }

    // Helper method to check if the collided object is on the ground layer.
    private bool IsGroundLayer(GameObject obj)
    {
        // Check if the object's layer matches the ground layer mask.
        return (groundLayerMask.value & (1 << obj.layer)) > 0;
    }
}
