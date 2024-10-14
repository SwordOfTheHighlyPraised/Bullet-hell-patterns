using UnityEngine;

[CreateAssetMenu(fileName = "SpreadPattern", menuName = "Bullet Patterns/Spread")]
public class SpreadPattern : BulletPatternBase
{
    [Header("Spread Settings")]
    public int bulletCount = 5;               // Number of bullets to fire in the spread
    public float spreadAngle = 45f;           // Total angle of the spread (degrees)
    public float spreadDistance = 1f;         // Distance of the bullets from the fire point

    public override void Fire(Transform firePoint, Transform player = null)
    {
        // Adjust fireAngle based on spin before firing any bullets
        if (enableSpin)
        {
            // Update fireAngle based on spin logic
            fireAngle += currentSpinSpeed;

            // Update the spin speed based on the spin change rate
            currentSpinSpeed += spinSpeedChangeRate;

            // Clamp the spin speed to the max spin speed
            currentSpinSpeed = Mathf.Clamp(currentSpinSpeed, -maxSpinSpeed, maxSpinSpeed);

            // Reverse the spin direction if necessary
            if (spinReversal && (Mathf.Abs(currentSpinSpeed) >= maxSpinSpeed))
            {
                spinSpeedChangeRate = -spinSpeedChangeRate; // Reverse the spin direction
            }

            // Normalize the fireAngle to keep it within the 0-359 range
            fireAngle = fireAngle % 360f;
            if (fireAngle < 0f)
            {
                fireAngle += 360f;
            }
        }

        // Loop through each bullet array
        for (int arrayIndex = 0; arrayIndex < totalBulletArrays; arrayIndex++)
        {
            // Offset for different bullet arrays (rotates around a central point)
            float arrayRotationOffset = (arrayIndex - (totalBulletArrays / 2f)) * totalArraySpread;

            // Calculate the base direction towards the player or based on fireAngle
            Vector3 baseDirection;

            // If moveToPlayer is true, adjust each array's direction to point towards the player
            if (moveToPlayer && player != null)
            {
                // Calculate direction toward the player and apply the array rotation offset
                Vector3 directionToPlayer = (player.position - firePoint.position).normalized;
                baseDirection = Quaternion.Euler(0, 0, arrayRotationOffset) * directionToPlayer;
            }
            else
            {
                // Use fireAngle for array rotation if not targeting the player
                baseDirection = GetDirectionFromAngle(fireAngle + arrayRotationOffset, firePoint, player);
            }

            // Calculate the starting angle for the spread (centered on the fire direction)
            float startAngle = -spreadAngle / 2f;
            float angleStep = spreadAngle / (bulletCount - 1);

            // Loop through and instantiate each bullet in the spread for the current array
            for (int i = 0; i < bulletCount; i++)
            {
                // Calculate the angle for each bullet relative to the center of the spread
                float currentAngle = startAngle + (i * angleStep);

                // Calculate the direction for each bullet relative to the array's direction
                Vector3 bulletDirection = Quaternion.Euler(0, 0, currentAngle) * baseDirection;

                // Adjust the spawn position based on spreadDistance and apply the X and Y offsets
                Vector3 spawnPosition = new Vector3(
                    firePoint.position.x + xOffset + (bulletDirection.normalized * spreadDistance).x, // Apply X offset
                    firePoint.position.y + yOffset + (bulletDirection.normalized * spreadDistance).y, // Apply Y offset
                    firePoint.position.z
                );

                // Instantiate the bullet at the adjusted spawn position
                GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

                // Adjust the size of the bullet
                bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f); // Set the bullet size

                // Initialize the bullet with the direction
                InitializeBullet(bullet, firePoint, player, bulletDirection);

                // Destroy the bullet after the lifespan ends
                Destroy(bullet, bulletLifespan);

                // Apply sine wave or spiral movement if enabled
                MonoBehaviour monoBehaviour = bullet.GetComponent<MonoBehaviour>();
                if (monoBehaviour != null)
                {
                    // Apply sine wave movement if enabled
                    if (enableSineWave)
                    {
                        monoBehaviour.StartCoroutine(MoveBulletWithSineWave(bullet.transform, bulletDirection));
                    }

                    // Apply spiral movement if enabled
                    if (enableSpiral)
                    {
                        Vector3 origin = bullet.transform.position;
                        monoBehaviour.StartCoroutine(SpiralBulletOutward(bullet.transform, origin, bulletDirection, bulletSpeed, spiralSpeed, spiralClockwise));
                    }
                }
            }
        }
    }
}
