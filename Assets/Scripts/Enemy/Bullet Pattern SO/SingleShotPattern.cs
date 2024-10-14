using UnityEngine;

[CreateAssetMenu(fileName = "SingleShotPattern", menuName = "Bullet Patterns/Single Shot")]
public class SingleShotPattern : BulletPatternBase
{
    public override void Fire(Transform firePoint, Transform player = null)
    {
        if (player == null) return;

        // Calculate spin and update fireAngle before firing any bullets
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

        // Loop through each bullet array
        for (int arrayIndex = 0; arrayIndex < totalBulletArrays; arrayIndex++)
        {
            // Offset for different bullet arrays (rotates around a central point)
            float arrayRotationOffset = (arrayIndex - (totalBulletArrays / 2f)) * totalArraySpread;

            // Loop through each bullet in the array
            for (int bulletIndex = 0; bulletIndex < numberOfBulletsPerArray; bulletIndex++)
            {
                float bulletAngle;

                // If there is only one bullet, no need for spread
                if (numberOfBulletsPerArray == 1)
                {
                    bulletAngle = 0f;
                }
                else
                {
                    // Calculate the exact angle for each bullet in the array
                    bulletAngle = bulletIndex * (individualArraySpread / (numberOfBulletsPerArray - 1));
                }

                // Calculate the final rotation for the bullet
                float finalRotation = fireAngle + arrayRotationOffset + bulletAngle;

                // Get the direction to fire in, based on the final rotation
                Vector3 directionToFire = GetDirectionFromAngle(finalRotation, firePoint, player);

                // Adjust direction to point towards the player if moveToPlayer is enabled
                if (moveToPlayer)
                {
                    // Calculate the direction towards the player
                    Vector3 directionToPlayer = (player.position - firePoint.position).normalized;

                    // Adjust the calculated directionToFire by rotating it by the finalRotation angle
                    directionToFire = Quaternion.Euler(0, 0, finalRotation) * directionToPlayer;
                }

                // Calculate the spawn position with the X and Y offsets
                Vector3 spawnPosition = new Vector3(
                    firePoint.position.x + xOffset, // Apply the X offset
                    firePoint.position.y + yOffset, // Apply the Y offset
                    firePoint.position.z
                );

                // Instantiate the bullet at the adjusted spawn position
                GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

                // Adjust the size of the bullet
                bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f); // Set the bullet size

                // Adjust the CircleCollider2D to match the bullet's size
                CircleCollider2D collider = bullet.GetComponent<CircleCollider2D>();
                if (collider != null)
                {
                    // Set the collider radius based on the bullet's size (adjust to fit width/height)
                    collider.radius = Mathf.Min(objectWidth, objectHeight);
                }
                else
                {
                    Debug.LogWarning("Bullet prefab does not have a CircleCollider2D component.");
                }

                // Initialize the bullet with the calculated direction
                InitializeBullet(bullet, firePoint, player, directionToFire);

                // Destroy the bullet after the lifespan duration
                Destroy(bullet, bulletLifespan); // Ensure that the bullet is destroyed after its lifespan
            }
        }
    }
}
