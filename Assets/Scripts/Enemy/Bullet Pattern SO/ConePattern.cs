using UnityEngine;

[CreateAssetMenu(fileName = "ConePattern", menuName = "Bullet Patterns/Cone")]
public class ConePattern : BulletPatternBase
{
    [Header("Cone Settings")]
    public int coneLayers = 4;                // Number of layers in the cone
    public int bulletsPerLayer = 6;           // Number of bullets in each layer (same across all layers)
    public float tipSpreadAngle = 15f;        // The spread angle at the tip (top/narrower)
    public float baseSpreadAngle = 45f;       // The spread angle at the base (bottom/wider)
    public float tipDistance = 1f;            // Distance of the tip layer from the fire point
    public float baseDistance = 3f;           // Distance of the base layer from the fire point

    public override void Fire(Transform firePoint, Transform player = null)
    {
        if (player == null && moveToPlayer) return; // Ensure player is available if aiming at player

        // Apply spin logic before firing if spin is enabled
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

            // Calculate the base direction towards the player or based on fireAngle
            Vector3 directionToPlayer = GetInitialBulletDirection(firePoint, player);
            Vector3 directionToFire = Quaternion.Euler(0, 0, fireAngle + arrayRotationOffset) * directionToPlayer;

            // Loop through each layer, ensuring bullets are aligned from tip (closer) to base (further out)
            for (int layer = 0; layer < coneLayers; layer++)
            {
                // Calculate the current layer's position factor, from 0 (tip) to 1 (base)
                float layerPositionFactor = (float)layer / (coneLayers - 1);

                // Interpolate the current spread angle for this layer
                float currentSpreadAngle = Mathf.Lerp(tipSpreadAngle, baseSpreadAngle, layerPositionFactor);

                // Calculate the distance for the current layer (closer for tip, further for base)
                float layerDistance = Mathf.Lerp(tipDistance, baseDistance, layerPositionFactor);

                // Calculate the starting angle for the current layer
                float startAngle = -currentSpreadAngle / 2f;
                float angleStep = currentSpreadAngle / (bulletsPerLayer - 1);

                // Loop through each bullet in this layer
                for (int bulletIndex = 0; bulletIndex < bulletsPerLayer; bulletIndex++)
                {
                    // Calculate the angle for each bullet relative to the player or fire direction
                    float currentAngle = startAngle + (bulletIndex * angleStep);

                    // Calculate the spread direction relative to the layer's angle
                    Vector3 bulletDirection = Quaternion.Euler(0, 0, currentAngle) * directionToFire;

                    // Adjust the spawn position based on the layer distance and apply the X and Y offsets
                    Vector3 spawnPosition = new Vector3(
                        firePoint.position.x + xOffset + (bulletDirection * layerDistance).x, // Apply X offset
                        firePoint.position.y + yOffset + (bulletDirection * layerDistance).y, // Apply Y offset
                        firePoint.position.z
                    );

                    // Instantiate and initialize the bullet with the direction
                    GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

                    // Adjust the size of the bullet
                    bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f); // Set the bullet size

                    // Initialize the bullet with the calculated direction
                    InitializeBullet(bullet, firePoint, player, bulletDirection);

                    // Destroy the bullet after the specified lifespan
                    Destroy(bullet, bulletLifespan); // Destroy the bullet after the lifespan has passed

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
}
