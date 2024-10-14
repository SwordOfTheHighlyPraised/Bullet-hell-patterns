using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MovementType
{
    Pendulum,
    UpAndDown,
    Orbit
}

public enum TargetType
{
    Player,
    Boss
}

public class SummonableMovement : MonoBehaviour
{
    public MovementType movementType;         // Select the type of movement (Pendulum, UpAndDown, Orbit)
    public GameObject pointAPrefab;           // Prefab for pointA (to instantiate in the scene)
    public GameObject pointBPrefab;           // Prefab for pointB (to instantiate in the scene)
    public float speed = 2f;                  // Speed of the movement
    public float pendulumAmplitude = 1f;      // Amplitude for pendulum or up-and-down movement
    public float orbitSpeed = 0f;             // Speed for orbiting; negative for anti-clockwise
    public TargetType targetType;             // Dropdown for selecting between Player or Boss
    public float offsetDistance = 1f;         // Distance for the offset

    [Header("Offset Settings")]
    public bool offsetLeft;
    public bool offsetTop;
    public bool offsetRight;
    public bool offsetBottom;

    [Header("Initial Spawn Angle")]
    public float initialAngle = 0f;           // The starting angle for the orbit movement
    private Vector3 initialOffset;

    private GameObject target;                // The object (Player/Boss) to orbit or follow
    private GameObject pointA;                // The instantiated pointA in the scene
    private GameObject pointB;                // The instantiated pointB in the scene
    private float timeCounter = 0f;
    private Vector3 lastPosition;             // To track the last position for movement direction
    private Vector3 currentDirection;         // The direction of movement
    private Vector3 offsetVector;

    private BulletSpawnerSunday bulletSpawner; // Reference to the bullet spawner for firing bullets
    public float timeBetweenShots = 1f;       // Time delay between each shot

    private void Start()
    {
        bulletSpawner = GetComponent<BulletSpawnerSunday>();

        // Find the target based on the selected target type from the enum
        switch (targetType)
        {
            case TargetType.Player:
                target = GameObject.FindGameObjectWithTag("Player");
                break;
            case TargetType.Boss:
                target = GameObject.FindGameObjectWithTag("Boss");
                break;
        }

        // Instantiate pointA and pointB in the scene as empty objects
        pointA = new GameObject("PointA");
        pointA.transform.position = new Vector3(4, 3, 0);

        pointB = new GameObject("PointB");
        pointB.transform.position = new Vector3(-6, 3, 0);

        // Calculate the offset vector based on the selected options
        offsetVector = Vector3.zero;
        if (offsetLeft)
            offsetVector += Vector3.left * offsetDistance;
        if (offsetRight)
            offsetVector += Vector3.right * offsetDistance;
        if (offsetTop)
            offsetVector += Vector3.up * offsetDistance;
        if (offsetBottom)
            offsetVector += Vector3.down * offsetDistance;

        // Set the initial time counter to reflect the starting angle
        timeCounter = initialAngle * Mathf.Deg2Rad;  // Convert the initial angle to radians

        // Track the initial position
        lastPosition = transform.position;

        // Start firing bullets in the movement direction
        StartCoroutine(FireBulletsInPattern());
    }


    private void Update()
    {
        switch (movementType)
        {
            case MovementType.Pendulum:
                PendulumMove();
                break;
            case MovementType.UpAndDown:
                UpAndDownMove();
                break;
            case MovementType.Orbit:
                OrbitMove();
                break;
        }

        // Calculate the current movement direction
        currentDirection = (transform.position - lastPosition).normalized;
        lastPosition = transform.position;  // Update the last position
    }

    // Orbit around the target (Player or Boss)
    private void OrbitMove()
    {
        if (target == null) return;

        // Increment the angle for orbiting
        timeCounter += Time.deltaTime * orbitSpeed;

        // Calculate the orbit position around the target
        float x = Mathf.Cos(timeCounter) * offsetDistance;
        float y = Mathf.Sin(timeCounter) * offsetDistance;
        Vector3 orbitPosition = new Vector3(x, y, 0);

        // Set the new position of the summonable entity relative to the target
        transform.position = target.transform.position + orbitPosition;
    }

    // Pendulum movement logic between two points
    private void PendulumMove()
    {
        if (pointA == null || pointB == null) return;

        // Calculate the midpoint between the two points
        Vector3 midPoint = (pointA.transform.position + pointB.transform.position) / 2f;
        float distance = Vector3.Distance(pointA.transform.position, pointB.transform.position);

        // Increase time counter based on speed
        timeCounter += Time.deltaTime * speed;

        // Apply pendulum movement (sinusoidal)
        float offset = Mathf.Sin(timeCounter) * (distance / 2f) * pendulumAmplitude;
        transform.position = midPoint + (pointB.transform.position - pointA.transform.position).normalized * offset + offsetVector;
    }

    // Up and Down movement between pointA and pointB
    private void UpAndDownMove()
    {
        if (pointA == null || pointB == null) return;

        // Calculate the midpoint
        Vector3 midPoint = (pointA.transform.position + pointB.transform.position) / 2f;
        float distance = Vector3.Distance(pointA.transform.position, pointB.transform.position);

        // Increase time counter based on speed
        timeCounter += Time.deltaTime * speed;

        // Apply up-and-down movement (sinusoidal)
        float offset = Mathf.Sin(timeCounter) * (distance / 2f) * pendulumAmplitude;
        transform.position = new Vector3(transform.position.x, midPoint.y + offset) + offsetVector;
    }

    // Coroutine to fire bullets using the BulletSpawnerSunday pattern
    private IEnumerator FireBulletsInPattern()
    {
        // Wait for half a second before starting to fire
        yield return new WaitForSeconds(0.5f);

        while (true)  // Continuously loop until destroyed or stopped
        {
            if (bulletSpawner != null)
            {
                // If in Orbit mode, fire the entire bullet pattern attached to the spawner
                if (movementType == MovementType.Orbit)
                {
                    // Check if fireAngle is 0, and fire the bullet pattern in the direction of movement
                    if (bulletSpawner.bulletPatterns1.Length > 0)
                    {
                        foreach (var pattern in bulletSpawner.bulletPatterns1)
                        {
                            if (pattern.bulletPattern.fireAngle == 0)
                            {
                                // Get the current movement direction (orbit direction)
                                Vector3 currentDirection = (transform.position - target.transform.position).normalized;

                                // Set fire direction based on orbit movement
                                bulletSpawner.FireInDirection(currentDirection);
                            }
                            else
                            {
                                // Fire the normal bullet pattern logic if fireAngle is defined
                                bulletSpawner.StartFiringPattern1();  // Fire the whole pattern
                            }
                        }
                    }
                }
                // For non-Orbit modes, fire using the normal bullet pattern logic
                else
                {
                    bulletSpawner.StartFiringPattern1();  // Start firing the normal pattern
                }
            }

            // Wait for the next shot according to the bullet spawner's internal timing
            yield return new WaitForSeconds(bulletSpawner.bulletPatterns1[0].timeBetweenFires);  // Adjust this delay as necessary
        }
    }
    // Destroy pointA and pointB when this object is destroyed
    private void OnDestroy()
    {
        if (pointA != null)
        {
            Destroy(pointA);
        }

        if (pointB != null)
        {
            Destroy(pointB);
        }
    }
}