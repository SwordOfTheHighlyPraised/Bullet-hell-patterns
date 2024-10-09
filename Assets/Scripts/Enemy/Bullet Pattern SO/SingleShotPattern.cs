using UnityEngine;

[CreateAssetMenu(fileName = "SingleShotPattern", menuName = "Bullet Patterns/Single Shot")]
public class SingleShotPattern : BulletPatternBase
{
    public override void Fire(Transform firePoint, Transform player = null)
    {
        if (player == null) return;

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

                // Instantiate and initialize the bullet
                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

                // Initialize the bullet with the calculated direction
                InitializeBullet(bullet, firePoint, player, directionToFire);
            }
        }
    }
}
