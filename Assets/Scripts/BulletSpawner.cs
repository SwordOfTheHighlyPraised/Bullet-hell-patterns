using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletSpawner : MonoBehaviour
{
    [Header("Bullet Settings")]
    private Rigidbody2D rb;
    public GameObject bulletPrefab;  // Bullet prefab to instantiate
    [SerializeField, Range(1, 10)]
    private int numberOfBulletsPerArray = 5;  // Number of bullets in a single array (1-10)

    [SerializeField, Range(1, 360)]
    private float individualArraySpread = 30f;  // Spread between bullets in the same array (1-360)

    [SerializeField, Range(1, 10)]
    private int totalBulletArrays = 3;  // Total number of bullet arrays (1-10)

    [SerializeField, Range(0, 360)]
    private float totalArraySpread = 90f;  // Spread between bullet arrays (1-360)

    [Header("Spin Settings")]
    [SerializeField, Range(-360, 360)]
    private float currentSpinSpeed = 0f;  // Current spin speed in degrees (-360 to 360)

    [SerializeField, Range(-10, 10)]
    private float spinSpeedChangeRate = 0f;  // Rate at which the spin speed changes (-10 to 10)

    [SerializeField, Range(0, 360)]
    private float maxSpinSpeed = 180f;  // Maximum spin speed (0-360)

    [SerializeField]
    private bool spinReversal = false;  // Spin reversal toggle

    [SerializeField]
    private bool swapSpinSpeedPerBurst = false;  // Toggle to swap spin speed between bursts

    [Header("Fire Settings")]
    [SerializeField]
    private bool burstFireEnabled = false;  // Enable burst fire mode

    [SerializeField, Range(1, 20)]
    private int bulletsPerBurst = 14;  // Number of bullets to fire in each burst

    [SerializeField, Range(1, 10)]
    private int burstCount = 6;  // Number of bursts to fire before waiting for the global fire rate

    [SerializeField, Range(0.01f, 5f)]  // Adjusted for fractional rates
    private float burstFireRate = 0.1f;  // Time between bullets in a burst

    [SerializeField, Range(0.1f, 5f)]  // New setting
    private float burstCountRate = 0.5f;  // Time between each burst in a sequence

    [SerializeField, Range(0.1f, 19f)]  // Adjusted for fractional rates
    private float fireRate = 1f;  // Time between each burst sequence

    private float fireTimer = 0f;  // Timer to manage global firing intervals
    public bool isFiring = true;  // Boolean to control whether the spawner is firing or not

    [Header("Bullet Movement Settings")]
    [SerializeField, Range(0, 10)]
    private float bulletSpeed = 5f;  // Initial speed of the bullet (0-10)

    [SerializeField, Range(-10, 10)]
    private float bulletAcceleration = 0f;  // Acceleration over time (-10 to 10)

    [SerializeField, Range(0, 360)]
    private float sineFrequency = 1f;  // Frequency of the sine wave

    [SerializeField, Range(0, 10)]
    private float sineAmplitude = 1f;  // Amplitude of the sine wave

    [Header("Homing Settings")]
    [SerializeField]
    private bool enableHoming = false;  // Toggle for homing behavior

    [SerializeField]
    private Transform playerTransform;  // Reference to the player's transform for homing

    [Header("Spiral Settings")]
    [SerializeField]
    private bool enableSpiral = false;  // Toggle to enable spiral movement

    [SerializeField, Range(0, 360)]
    private float spiralAngleRange = 10f;  // Angle range for bullet spiraling

    [Header("Cone Fire Settings")]
    [SerializeField]
    private bool coneFireEnabled = false;  // Enable or disable cone fire mode

    [SerializeField, Range(3, 10)]
    private int coneLayers = 3;  // Number of layers in the cone. 3 means 9 bullets, 5 means 25, etc.

    [SerializeField, Range(0f, 180f)]
    private float coneSpreadAngle = 45f;  // The spread angle of the cone (in degrees)

    [Header("Offset & Size")]
    [SerializeField]
    private float objectWidth = 0.1f;  // Width of the object

    [SerializeField]
    private float objectHeight = 0.1f;  // Height of the object

    [SerializeField]
    private float xOffset = 0f;  // X Offset for bullet spawn position

    [SerializeField]
    private float yOffset = 0f;  // Y Offset for bullet spawn position

    [Header("Initial Angle Settings")]
    [SerializeField, Range(0, 360)]
    private float initialAngle = 90f;  // Initial angle for bullet spawning (0-360)
    [SerializeField, Range(0, 359)]
    private float currentAngle = 90f;  // Current angle for bullet spawning (0-359)

    private int currentBurstCount = 0;  // Tracks how many bursts have been fired
    private bool originalSpinSpeedDirection;  // Tracks the original spin speed direction
    private bool isBursting = false;  // Controls whether the burst is in progress

    void Start()
    {
        // Initialize current angle to the initial angle
        currentAngle = initialAngle;
        originalSpinSpeedDirection = currentSpinSpeed >= 0;
    }

    void Update()
    {
        // Only fire bullets if isFiring is true
        if (isFiring)
        {
            // Update the global fire timer
            fireTimer += Time.deltaTime;

            // If burst fire is enabled, handle burst fire
            if (burstFireEnabled)
            {
                // If it's time to fire based on the global fire rate
                if (fireTimer >= fireRate && !isBursting)
                {
                    fireTimer = 0f;  // Reset global fire timer
                    StartCoroutine(FireBurstSequence());  // Start the burst fire sequence
                }
            }
            else
            {
                // Normal firing logic if burst fire is not enabled
                if (fireTimer >= fireRate)
                {
                    fireTimer = 0f;  // Reset global fire timer

                    // Check if cone fire is enabled
                    if (coneFireEnabled)
                    {
                        SpawnConeBullets();  // Fire in cone shape
                    }
                    else
                    {
                        SpawnBullets();  // Fire normally
                    }
                }
            }
        }

        // Update spin speed for bullets
        UpdateSpinSpeed();
    }

    // Set bullet prefab dynamically
    public void SetBulletPrefab(GameObject newPrefab)
    {
        bulletPrefab = newPrefab;
    }

    // Set bullet size dynamically
    public void SetBulletSize(float width, float height)
    {
        objectWidth = width;
        objectHeight = height;
    }

    // Set bullet spawn offset dynamically
    public void SetBulletOffset(Vector2 offset)
    {
        xOffset = offset.x;
        yOffset = offset.y;
    }

    // Set total bullet arrays dynamically
    public void SetTotalBulletArrays(int arrays)
    {
        totalBulletArrays = arrays;
    }

    // Set individual array spread dynamically
    public void SetIndividualArraySpread(float spread)
    {
        individualArraySpread = spread;
    }

    // Enable or disable homing dynamically
    public void SetHoming(bool homing)
    {
        enableHoming = homing;
    }

    IEnumerator FireBurstSequence()
    {
        isBursting = true;  // Indicate that the burst sequence is in progress
        currentBurstCount = 0;  // Reset the current burst count

        // Loop through each burst
        for (int burst = 0; burst < burstCount; burst++)
        {
            currentBurstCount++;

            // Fire the bullets in this burst one by one
            for (int i = 0; i < bulletsPerBurst; i++)
            {
                SpawnBullets();
                yield return new WaitForSeconds(burstFireRate);  // Wait for the burst fire rate between shots
            }

            // Check if we need to swap spin speed settings after each burst
            if (swapSpinSpeedPerBurst)
            {
                currentSpinSpeed = -currentSpinSpeed;  // Reverse spin speed
            }

            // Wait between burst counts before firing the next burst
            yield return new WaitForSeconds(burstCountRate);  // New delay between bursts
        }

        isBursting = false;  // Indicate that the burst sequence is finished
    }

    void SpawnConeBullets()
    {
        // Loop through each layer in the cone
        for (int layer = 1; layer <= coneLayers; layer++)
        {
            // The radius of the current layer (distance from the spawner)
            float layerRadius = layer * 0.5f;  // Adjust this multiplier to control the spacing between layers.

            // Loop through the bullets in this layer
            for (int bulletIndex = 0; bulletIndex < coneLayers; bulletIndex++)
            {
                // Calculate the angle for this bullet within the cone spread
                float angleOffset = (float)bulletIndex / (coneLayers - 1);  // Spread bullets evenly across the layer
                float bulletAngle = Mathf.Lerp(-coneSpreadAngle / 2, coneSpreadAngle / 2, angleOffset);

                // Convert the bullet angle into a position offset (using trigonometry)
                float x = Mathf.Cos(bulletAngle * Mathf.Deg2Rad) * layerRadius;
                float y = Mathf.Sin(bulletAngle * Mathf.Deg2Rad) * layerRadius;

                // Adjust the spawn position based on the x and y offsets
                Vector3 spawnPosition = new Vector3(transform.position.x + x, transform.position.y + y, 0f);

                // Instantiate bullet and set its rotation
                GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.Euler(0f, 0f, bulletAngle));

                // Set the size of the bullet using objectWidth and objectHeight
                bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f);

                // Access the Rigidbody2D component on the bullet prefab
                Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

                if (bulletRb != null)
                {
                    // Start the bullet with an initial speed based on bulletSpeed
                    bulletRb.velocity = new Vector2(
                        Mathf.Cos(bulletAngle * Mathf.Deg2Rad) * bulletSpeed,
                        Mathf.Sin(bulletAngle * Mathf.Deg2Rad) * bulletSpeed
                    );
                }
            }
        }
    }

    public void SpawnBullets()
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
                float finalRotation = currentAngle + arrayRotationOffset + bulletAngle;

                // Adjust the spawn position based on objectWidth and objectHeight
                Vector3 spawnPosition = new Vector3(transform.position.x + xOffset, transform.position.y + yOffset, 0f);

                // Instantiate bullet and set its rotation
                GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.Euler(0f, 0f, finalRotation));

                // Set the size of the bullet using objectWidth and objectHeight
                bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f);

                // Start the appropriate movement behavior based on settings
                if (enableHoming)
                {
                    StartCoroutine(HomingBullet(bullet.transform, finalRotation));
                }
                else if (enableSpiral)
                {
                    StartCoroutine(SpiralBullet(bullet.transform, finalRotation));
                }
                else
                {
                    StartCoroutine(MoveBulletWithSineWave(bullet.transform, finalRotation));

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
        }

        // Update the angle for the next spawn, adding the current spin speed to create the difference for the next bullet
        currentAngle += currentSpinSpeed;
    }

    // Method to change the fire angle dynamically
    public void SetFireAngle(float newAngle)
    {
        // Clamp the angle between 0 and 359 degrees
        currentAngle = Mathf.Clamp(newAngle, 0f, 359f);
    }

    // Coroutine to move the bullet with a sine wave adjustment and acceleration
    IEnumerator MoveBulletWithSineWave(Transform bulletTransform, float initialRotation)
    {
        float elapsedTime = 0f;
        float currentBulletSpeed = bulletSpeed;  // Start with initial bullet speed

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;

            // Get the sine wave offset based on time
            float sineWaveOffset = Mathf.Sin(elapsedTime * sineFrequency) * sineAmplitude;

            // Adjust the bullet's position in the Y-axis using the sine wave
            Vector3 direction = new Vector3(
                Mathf.Cos(initialRotation * Mathf.Deg2Rad),
                Mathf.Sin(initialRotation * Mathf.Deg2Rad) + sineWaveOffset,
                0f
            ).normalized;

            // Apply acceleration to the bullet's speed over time
            currentBulletSpeed += bulletAcceleration * Time.deltaTime;

            // Update the bullet's position based on the new speed
            bulletTransform.position += direction * currentBulletSpeed * Time.deltaTime;

            yield return null;  // Wait for the next frame
        }
    }

    // Coroutine for homing behavior
    IEnumerator HomingBullet(Transform bulletTransform, float initialRotation)
    {
        // 1. Move normally for the first second
        float elapsedTime = 0f;
        Vector3 direction = new Vector3(
            Mathf.Cos(initialRotation * Mathf.Deg2Rad),
            Mathf.Sin(initialRotation * Mathf.Deg2Rad),
            0f
        ).normalized;
        float currentBulletSpeed = bulletSpeed;

        while (elapsedTime < 1f)
        {
            // Check if bullet or player still exists
            if (bulletTransform == null || playerTransform == null) yield break;

            elapsedTime += Time.deltaTime;
            bulletTransform.position += direction * currentBulletSpeed * Time.deltaTime;
            yield return null;
        }

        // 2. Stop the bullet for a moment
        if (bulletTransform == null) yield break;
        bulletTransform.GetComponent<Rigidbody2D>().velocity = Vector2.zero;

        // 3. Home in towards the player for 0.5 seconds
        elapsedTime = 0f;
        while (elapsedTime < 0.5f)
        {
            // Check if bullet or player still exists
            if (bulletTransform == null || playerTransform == null) yield break;

            elapsedTime += Time.deltaTime;

            // Calculate direction towards the player
            Vector3 targetDirection = (playerTransform.position - bulletTransform.position).normalized;
            bulletTransform.position += targetDirection * currentBulletSpeed * Time.deltaTime;

            yield return null;
        }

        // 4. Continue moving in the homed direction with speed set to 10
        if (bulletTransform == null || playerTransform == null) yield break;
        currentBulletSpeed = 10f;  // Set speed to 10 after homing
        direction = (playerTransform.position - bulletTransform.position).normalized;  // Final homed direction
        while (bulletTransform != null)
        {
            currentBulletSpeed += bulletAcceleration * Time.deltaTime;  // Apply acceleration
            bulletTransform.position += direction * currentBulletSpeed * Time.deltaTime;
            yield return null;
        }
    }

    // Coroutine to make the bullet spiral outwards, independent of the spawner's movement
    IEnumerator SpiralBullet(Transform bulletTransform, float initialRotation)
    {
        float elapsedTime = 0f;
        float angle = initialRotation;
        float radius = 0f;

        // Capture the bullet's initial position when it's instantiated
        Vector3 bulletStartPosition = bulletTransform.position;

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;

            // Increase the radius and angle to make the bullet spiral outwards
            radius += bulletSpeed * Time.deltaTime;
            angle += spiralAngleRange * Time.deltaTime;  // Increase the angle over time

            // Convert polar coordinates (radius, angle) to Cartesian coordinates (x, y)
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float y = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;

            // Update the bullet's position relative to its initial position
            bulletTransform.position = new Vector3(bulletStartPosition.x + x, bulletStartPosition.y + y, bulletTransform.position.z);

            yield return null;  // Wait for the next frame
        }
    }

    void UpdateSpinSpeed()
    {
        if (spinSpeedChangeRate != 0f)
        {
            // Update spin speed with a rate that is applied per second (using Time.deltaTime)
            currentSpinSpeed += spinSpeedChangeRate * Time.deltaTime;

            // Clamp the current spin speed to ensure it doesn't exceed the maxSpinSpeed limits
            currentSpinSpeed = Mathf.Clamp(currentSpinSpeed, -maxSpinSpeed, maxSpinSpeed);

            // Check for reversal
            if (spinReversal && (currentSpinSpeed == maxSpinSpeed || currentSpinSpeed == -maxSpinSpeed))
            {
                spinSpeedChangeRate *= -1;
            }
        }
    }
}
