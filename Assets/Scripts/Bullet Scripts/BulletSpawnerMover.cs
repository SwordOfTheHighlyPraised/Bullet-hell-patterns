using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletSpawnerMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public Rigidbody2D rb;  // The Rigidbody2D component of the object.

    [SerializeField]
    private float moveSpeed = 5f;  // Speed at which the object moves.

    [SerializeField]
    private float transitionSpeed = 2f;  // Speed of transitioning between movement types.

    [Header("Toggle Movement Types")]
    public bool moveSideToSide = false;  // Toggle for side-to-side movement.
    public bool moveUpAndDown = false;  // Toggle for up-and-down movement.
    public bool moveFigureEight = false;  // Toggle for figure-eight movement.
    public bool movePendulum = false;  // Toggle for pendulum movement.
    public bool moveInCircle = false;  // Toggle for circular movement.

    private Vector2 initialPosition;  // The initial position of the object.
    private float timeElapsed = 0f;  // Time used to calculate movement patterns.
    private Vector2 currentMovement;  // The current movement position.
    private Vector2 targetMovement;  // The target movement position we want to lerp to.

    [SerializeField]
    private float circleRadius = 2f;  // The radius of the circular movement.

    void Start()
    {
        // Store the initial position to base movement patterns off of.
        initialPosition = transform.position;
        currentMovement = initialPosition;  // Start the movement at the initial position.
        targetMovement = initialPosition;  // Set the initial target to the same starting point.
    }

    void FixedUpdate()
    {
        timeElapsed += Time.fixedDeltaTime;  // Time elapsed to control smooth movement.

        // Determine the target movement based on active toggles.
        Vector2 newTargetMovement = DetermineTargetMovement();

        // Lerp from the current movement position to the target movement position.
        currentMovement = Vector2.Lerp(currentMovement, newTargetMovement, transitionSpeed * Time.fixedDeltaTime);

        // Apply the new position using Rigidbody2D's MovePosition.
        rb.MovePosition(currentMovement);
    }

    // Determine the target movement based on active toggles.
    Vector2 DetermineTargetMovement()
    {
        Vector2 newTarget = initialPosition;

        if (moveSideToSide)
        {
            newTarget += GetSideToSideMovement();
        }
        if (moveUpAndDown)
        {
            newTarget += GetUpAndDownMovement();
        }
        if (moveFigureEight)
        {
            newTarget += GetFigureEightMovement();
        }
        if (movePendulum)
        {
            newTarget += GetPendulumMovement();
        }
        if (moveInCircle)
        {
            newTarget += GetCircularMovement();  // Add circular movement if enabled.
        }

        return newTarget;
    }

    // Move the object side-to-side in a sinusoidal pattern.
    Vector2 GetSideToSideMovement()
    {
        float x = Mathf.Sin(timeElapsed * moveSpeed);  // Calculate side-to-side movement.
        return new Vector2(x, 0);  // Return the movement offset in the x-axis.
    }

    // Move the object up and down in a sinusoidal pattern.
    Vector2 GetUpAndDownMovement()
    {
        float y = Mathf.Sin(timeElapsed * moveSpeed);  // Calculate up-and-down movement.
        return new Vector2(0, y);  // Return the movement offset in the y-axis.
    }

    // Move the object in a sideways figure-eight pattern.
    Vector2 GetFigureEightMovement()
    {
        float x = Mathf.Sin(timeElapsed * moveSpeed) * 2;  // X-axis for figure-eight movement.
        float y = Mathf.Sin(timeElapsed * moveSpeed * 2);  // Y-axis for figure-eight movement.
        return new Vector2(x, y);  // Return the movement offset for figure-eight.
    }

    // Move the object in a pendulum-like swing.
    Vector2 GetPendulumMovement()
    {
        float x = Mathf.Sin(timeElapsed * moveSpeed);  // Calculate pendulum-like side-to-side swing.
        float y = -Mathf.Cos(timeElapsed * moveSpeed);  // Adds a slight vertical effect.
        return new Vector2(x, y * 0.5f);  // Return the movement offset, with reduced y motion for pendulum.
    }

    // Move the object in a circular pattern.
    Vector2 GetCircularMovement()
    {
        float x = Mathf.Cos(timeElapsed * moveSpeed) * circleRadius;  // Calculate x position for circle.
        float y = Mathf.Sin(timeElapsed * moveSpeed) * circleRadius;  // Calculate y position for circle.
        return new Vector2(x, y);  // Return the movement offset for circular movement.
    }
}
