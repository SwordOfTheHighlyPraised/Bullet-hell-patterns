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
        // Loop through each bullet array
        for (int arrayIndex = 0; arrayIndex < totalBulletArrays; arrayIndex++)
        {
            // Offset for different bullet arrays (rotates around a central point)
            float arrayRotationOffset = (arrayIndex - (totalBulletArrays / 2f)) * totalArraySpread;

            // Calculate the base direction based on the firing angle or a predefined direction
            Vector3 directionToFire = GetDirectionFromAngle(fireAngle + arrayRotationOffset, firePoint, player);

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
                    // Calculate the angle for each bullet
                    float currentAngle = startAngle + (bulletIndex * angleStep);

                    // Calculate the spread direction relative to the layer's angle
                    Vector3 bulletDirection = Quaternion.Euler(0, 0, currentAngle) * directionToFire;

                    // Adjust the spawn position based on the layer distance
                    Vector3 spawnPosition = firePoint.position + bulletDirection * layerDistance;

                    // Instantiate and initialize the bullet with the direction
                    GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
                    InitializeBullet(bullet, firePoint, player, bulletDirection); // Pass bulletDirection as the required parameter

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
