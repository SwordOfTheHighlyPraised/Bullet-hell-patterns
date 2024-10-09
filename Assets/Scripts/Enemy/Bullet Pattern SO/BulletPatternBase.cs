using System.Collections;
using UnityEngine;

public abstract class BulletPatternBase : ScriptableObject
{
    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 5f;              // Initial bullet speed from ScriptableObject
    public float acceleration = 0f;             // Rate of acceleration

    [Header("Movement Settings")]
    public bool moveToPlayer = true;            // Toggle to decide if the bullet should move towards the player
    [Range(0f, 359f)] public float fireAngle = 0f; // Angle (0-359) to fire in a specific direction if not moving to the player

    [Header("Bullet Array Settings")]
    [SerializeField, Range(1, 10)]
    public int numberOfBulletsPerArray = 5;  // Number of bullets in a single array (1-10)

    [SerializeField, Range(1, 360)]
    public float individualArraySpread = 30f;  // Spread between bullets in the same array (1-360)

    [SerializeField, Range(1, 10)]
    public int totalBulletArrays = 3;  // Total number of bullet arrays (1-10)

    [SerializeField, Range(0, 360)]
    public float totalArraySpread = 90f;  // Spread between bullet arrays (1-360)

    [Header("Sine Wave Settings")]
    [Range(-5f, 5f)] public float sineAmplitude = 1f;   // Sine wave amplitude (range: -5 to 5)
    [Range(-10f, 10f)] public float sineFrequency = 1f;  // Sine wave frequency (range: -10 to 10)
    public bool enableSineWave = false;                // Enable sine wave movement

    [Header("Spiral Settings")]
    public bool enableSpiral = false;                  // Enable spiral movement
    [Range(0f, 360f)] public float spiralSpeed = 45f;  // Speed of the spiral rotation (degrees per second)
    public bool spiralClockwise = true;                // Toggle for spiral direction: clockwise or anti-clockwise

    [Header("Homing Settings")]
    public bool enableHoming = false;                  // Enable or disable homing behavior
    public int maxStops = 1;                           // Number of times the bullet can stop and redirect
    public float stopDuration = 1f;                    // Time the bullet stops before redirecting
    public float initialMovementTime = 1f;             // Time to move in the initial direction before stopping
    public float curveDuration = 0.5f;                 // Time it takes to curve to the new direction

    private float currentAngle = 0f;  // Used to track the angle for spinning or spiraling

    public void SetTotalBulletArrays(int arrays)
    {
        totalBulletArrays = arrays;
    }

    public void SetIndividualArraySpread(float spread)
    {
        individualArraySpread = spread;
    }

    public virtual void Fire(Transform firePoint, Transform player = null)
    {
        SpawnBullets(firePoint, player);
    }

    public void SpawnBullets(Transform firePoint, Transform player)
    {
        // Loop through each array of bullets
        for (int arrayIndex = 0; arrayIndex < totalBulletArrays; arrayIndex++)
        {
            // Offset for different bullet arrays
            float arrayRotationOffset = (arrayIndex - (totalBulletArrays / 2f)) * totalArraySpread;

            // Loop through each bullet in the array
            for (int bulletIndex = 0; bulletIndex < numberOfBulletsPerArray; bulletIndex++)
            {
                float bulletAngle;

                if (numberOfBulletsPerArray == 1)
                {
                    // If there is only 1 bullet, it should be at 0 degrees relative to the array
                    bulletAngle = 0f;
                }
                else
                {
                    // Calculate the exact angle for each bullet in the array
                    bulletAngle = bulletIndex * (individualArraySpread / (numberOfBulletsPerArray - 1));
                }

                // Calculate the final rotation for each bullet
                float finalRotation = fireAngle + arrayRotationOffset + bulletAngle;

                // Get the direction to fire in, based on the final rotation
                Vector3 directionToFire = GetDirectionFromAngle(finalRotation, firePoint, player);

                // Instantiate and initialize the bullet
                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

                // Initialize the bullet with the calculated direction
                InitializeBullet(bullet, firePoint, player, directionToFire);

                // Access the Rigidbody2D component on the bullet prefab
                Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

                if (bulletRb != null)
                {
                    // Start the bullet with an initial speed based on bulletSpeed
                    bulletRb.velocity = new Vector2(
                        Mathf.Cos(finalRotation * Mathf.Deg2Rad) * bulletSpeed,
                        Mathf.Sin(finalRotation * Mathf.Deg2Rad) * bulletSpeed
                    );
                }
            }
        }

        // Update the angle for the next spawn, adding the current spin speed to create the difference for the next bullet
        currentAngle += fireAngle;
    }

    // Method to get the direction based on a specific angle (in degrees) relative to the firing point or player
    protected Vector3 GetDirectionFromAngle(float angle, Transform firePoint, Transform player)
    {
        // If the bullet is set to move towards the player, use the player's position
        if (moveToPlayer && player != null)
        {
            return (player.position - firePoint.position).normalized; // Move towards the player
        }
        else
        {
            // Calculate the direction based on the specified angle (convert angle to direction vector)
            float angleInRadians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angleInRadians), Mathf.Sin(angleInRadians), 0).normalized;
        }
    }

    // Initialize the bullet with a custom direction
    protected void InitializeBullet(GameObject bullet, Transform firePoint, Transform player, Vector3 direction)
    {
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            // Set initial velocity for the bullet instance using the initial speed
            rb.velocity = direction * bulletSpeed;

            MonoBehaviour monoBehaviour = bullet.GetComponent<MonoBehaviour>();
            if (monoBehaviour != null)
            {
                // Start acceleration if needed
                monoBehaviour.StartCoroutine(AccelerateBullet(rb, direction, bulletSpeed));

                // Start sine wave movement if enabled for this bullet
                if (enableSineWave)
                {
                    monoBehaviour.StartCoroutine(MoveBulletWithSineWave(bullet.transform, direction));
                }

                // Start spiral movement if enabled for this bullet
                if (enableSpiral)
                {
                    Vector3 origin = bullet.transform.position;
                    monoBehaviour.StartCoroutine(SpiralBulletOutward(bullet.transform, origin, direction, bulletSpeed, spiralSpeed, spiralClockwise));
                }

                // Start homing behavior if enabled
                if (enableHoming)
                {
                    monoBehaviour.StartCoroutine(HomingRoutine(bullet.transform, firePoint, player, rb));
                }
            }
        }
    }

    // Get the initial direction based on moveToPlayer or fireAngle settings
    protected Vector3 GetInitialBulletDirection(Transform firePoint, Transform player)
    {
        if (moveToPlayer && player != null)
        {
            // Move towards the player
            return (player.position - firePoint.position).normalized;
        }
        else
        {
            // Move in the direction of the specified fire angle (convert angle to a direction vector)
            float angleInRadians = fireAngle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angleInRadians), Mathf.Sin(angleInRadians), 0).normalized;
        }
    }

    // Coroutine for homing behavior with stops and redirects
    protected IEnumerator HomingRoutine(Transform bulletTransform, Transform firePoint, Transform player, Rigidbody2D rb)
    {
        int currentStopCount = 0;
        float currentBulletSpeed = bulletSpeed;

        // 1. Initial movement in the initial direction for the configured time
        Vector3 direction = GetInitialBulletDirection(firePoint, player);
        float elapsedTime = 0f;

        while (elapsedTime < initialMovementTime)
        {
            if (bulletTransform == null) yield break;

            elapsedTime += Time.deltaTime;
            currentBulletSpeed += acceleration * Time.deltaTime;  // Apply acceleration
            rb.velocity = direction * currentBulletSpeed;         // Update bullet velocity
            yield return null;
        }

        // 2. Stop and redirect behavior
        while (currentStopCount < maxStops)
        {
            if (bulletTransform == null || player == null) yield break;

            // Stop the bullet
            rb.velocity = Vector2.zero;
            yield return new WaitForSeconds(stopDuration);  // Wait for the stop duration

            // Calculate new direction towards the player
            Vector3 newDirection = (player.position - bulletTransform.position).normalized;

            // Curve smoothly into the new direction
            yield return CurveBulletDirection(bulletTransform, direction, newDirection, currentBulletSpeed, rb);

            // Update the direction
            direction = newDirection;

            // Move in the new direction after the stop
            elapsedTime = 0f;
            while (elapsedTime < initialMovementTime && bulletTransform != null)
            {
                elapsedTime += Time.deltaTime;
                currentBulletSpeed += acceleration * Time.deltaTime;  // Apply acceleration
                rb.velocity = direction * currentBulletSpeed;         // Update bullet velocity
                yield return null;
            }

            currentStopCount++;
        }

        // 3. Continue moving indefinitely in the final direction
        while (bulletTransform != null)
        {
            currentBulletSpeed += acceleration * Time.deltaTime;  // Continue accelerating
            rb.velocity = direction * currentBulletSpeed;         // Move in final direction
            yield return null;
        }
    }

    // Coroutine to smoothly curve the bullet's direction between two vectors
    protected IEnumerator CurveBulletDirection(Transform bulletTransform, Vector3 oldDirection, Vector3 newDirection, float currentBulletSpeed, Rigidbody2D rb)
    {
        float curveElapsedTime = 0f;

        while (curveElapsedTime < curveDuration)
        {
            if (bulletTransform == null) yield break;

            curveElapsedTime += Time.deltaTime;

            // Lerp between the old direction and the new direction
            Vector3 curvedDirection = Vector3.Lerp(oldDirection, newDirection, curveElapsedTime / curveDuration).normalized;

            // Apply acceleration
            currentBulletSpeed += acceleration * Time.deltaTime;

            // Move the bullet in the curved direction using velocity
            rb.velocity = curvedDirection * currentBulletSpeed;
            yield return null;
        }
    }

    // Coroutine for accelerating the bullet instance over time
    protected IEnumerator AccelerateBullet(Rigidbody2D rb, Vector3 moveDirection, float currentBulletSpeed)
    {
        while (rb != null)
        {
            currentBulletSpeed += acceleration * Time.deltaTime;
            rb.velocity = moveDirection * currentBulletSpeed;
            yield return null;
        }
    }

    // Sine wave movement coroutine
    protected IEnumerator MoveBulletWithSineWave(Transform bulletTransform, Vector3 moveDirection)
    {
        float elapsedTime = 0f;

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;

            // Apply sine wave movement on top of forward movement
            float sineWaveOffset = Mathf.Sin(elapsedTime * sineFrequency) * sineAmplitude;

            // Perpendicular direction for sine wave movement
            Vector3 perpendicularDirection = new Vector3(-moveDirection.y, moveDirection.x, 0f);

            // Apply sine wave movement
            Vector3 sineWaveMovement = perpendicularDirection * sineWaveOffset;

            // Update the bullet's position with sine wave
            bulletTransform.position += sineWaveMovement * Time.deltaTime;

            yield return null;
        }
    }

    // Spiral movement coroutine
    protected IEnumerator SpiralBulletOutward(Transform bulletTransform, Vector3 origin, Vector3 directionToPlayer, float currentBulletSpeed, float spiralSpeed, bool spiralClockwise)
    {
        float elapsedTime = 0f;
        float currentAngle = 0f;

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;
            currentAngle += spiralClockwise ? spiralSpeed * Time.deltaTime : -spiralSpeed * Time.deltaTime;
            float radius = currentBulletSpeed * elapsedTime;
            Vector3 rotatedDirection = Quaternion.Euler(0, 0, currentAngle) * directionToPlayer;
            float x = rotatedDirection.x * radius;
            float y = rotatedDirection.y * radius;
            bulletTransform.position = new Vector3(origin.x + x, origin.y + y, bulletTransform.position.z);
            yield return null;
        }
    }
}
