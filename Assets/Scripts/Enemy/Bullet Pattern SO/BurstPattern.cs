using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "BurstPattern", menuName = "Bullet Patterns/Burst")]
public class BurstPattern : BulletPatternBase
{
    [Header("Burst Settings")]
    public int bulletsPerLocation = 2;  // Number of bullets to fire from the same location before switching
    public int burstCount = 6;          // Total number of burst sequences to fire
    public float burstFireRate = 0.1f;  // Time between bullets in a burst
    public float burstCountRate = 0.5f; // Time between each burst sequence

    [Header("Offset Settings")]
    public Vector2[] fireOffsets;  // Array of offsets for alternating burst positions

    public override void Fire(Transform firePoint, Transform player = null)
    {
        if (player == null) return;

        MonoBehaviour monoBehaviour = firePoint.GetComponent<MonoBehaviour>();
        if (monoBehaviour != null)
        {
            monoBehaviour.StartCoroutine(FireBurst(firePoint, player));
        }
    }

    private IEnumerator FireBurst(Transform firePoint, Transform player)
    {
        int currentOffsetIndex = 0;

        // Loop through each burst sequence
        for (int i = 0; i < burstCount; i++)
        {
            // Calculate spin and update fireAngle before firing bullets in the burst
            if (enableSpin)
            {
                // Update the fireAngle based on the current spin speed
                fireAngle += currentSpinSpeed;

                // Update the spin speed based on the spin change rate
                currentSpinSpeed += spinSpeedChangeRate;

                // Clamp the spin speed to the max spin speed
                currentSpinSpeed = Mathf.Clamp(currentSpinSpeed, -maxSpinSpeed, maxSpinSpeed);

                // Reverse the spin direction if it reaches the max or min spin speed
                if (spinReversal && (Mathf.Abs(currentSpinSpeed) >= maxSpinSpeed))
                {
                    spinSpeedChangeRate = -spinSpeedChangeRate; // Reverse spin direction
                }

                // Normalize the fireAngle to stay within 0-359 degrees
                fireAngle = fireAngle % 360f;
                if (fireAngle < 0f)
                {
                    fireAngle += 360f;
                }
            }

            // Fire for each array in the current burst
            for (int arrayIndex = 0; arrayIndex < totalBulletArrays; arrayIndex++)
            {
                // Offset for different bullet arrays (rotates around a central point)
                float arrayRotationOffset = (arrayIndex - (totalBulletArrays / 2f)) * totalArraySpread;

                // Calculate directionToPlayer if moveToPlayer is enabled
                Vector3 directionToFire;
                if (moveToPlayer && player != null)
                {
                    // Get direction to the player for the entire array
                    directionToFire = (player.position - firePoint.position).normalized;
                }
                else
                {
                    // Use fireAngle + array offset if not moving to the player
                    directionToFire = GetDirectionFromAngle(fireAngle + arrayRotationOffset, firePoint, player);
                }

                // Fire bulletsPerLocation bullets from the same offset position
                for (int j = 0; j < bulletsPerLocation; j++)
                {
                    // Calculate the offset position based on fireOffsets array and apply the X and Y offsets
                    Vector3 offsetPosition = new Vector3(
                        firePoint.position.x + xOffset + fireOffsets[currentOffsetIndex].x, // Apply X offset
                        firePoint.position.y + yOffset + fireOffsets[currentOffsetIndex].y, // Apply Y offset
                        firePoint.position.z
                    );

                    // Instantiate a bullet at the offset position
                    GameObject bullet = Instantiate(bulletPrefab, offsetPosition, Quaternion.identity);

                    // Adjust the bullet size
                    bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f); // Set the bullet size

                    // Adjust the direction slightly for the array spread
                    Vector3 finalDirection = Quaternion.Euler(0, 0, arrayRotationOffset) * directionToFire;

                    // Initialize the bullet to follow the movement settings (sine, spiral, etc.)
                    InitializeBurstBullet(bullet, finalDirection, i % 2 == 0, firePoint, player);

                    // Destroy the bullet after the specified lifespan
                    Destroy(bullet, bulletLifespan);  // Destroy the bullet after its lifespan expires

                    yield return new WaitForSeconds(burstFireRate);  // Wait between bullets in the same burst
                }
            }

            // Switch to the next offset position
            currentOffsetIndex = (currentOffsetIndex + 1) % fireOffsets.Length;

            // Wait between burst sequences
            yield return new WaitForSeconds(burstCountRate);
        }
    }

    // Initialize the bullet and follow base movement settings (sine wave, spiral, etc.)
    private void InitializeBurstBullet(GameObject bullet, Vector3 directionToFire, bool spiralClockwise, Transform firePoint, Transform player)
    {
        // Temporarily set the spiral direction for this bullet
        BulletPatternBase patternBase = this;
        patternBase.spiralClockwise = spiralClockwise;

        // Initialize the bullet using the base class method with movement settings
        InitializeBullet(bullet, firePoint, player, directionToFire);  // Pass directionToFire
    }
}
