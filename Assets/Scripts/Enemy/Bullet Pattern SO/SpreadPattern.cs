using UnityEngine;

[CreateAssetMenu(fileName = "SpreadPattern", menuName = "Bullet Patterns/Spread")]
public class SpreadPattern : BulletPatternBase
{
    [Header("Spread Settings")]
    public int bulletCount = 5;              // Number of bullets to fire in the spread
    public float spreadAngle = 45f;          // Total angle of the spread (degrees)
    public float spreadDistance = 1f;        // Distance of the bullets from the fire point

    public override void Fire(Transform firePoint, Transform player = null)
    {
        // Calculate the base direction to fire in (either based on the fire angle or towards the player)
        Vector3 directionToFire = GetInitialBulletDirection(firePoint, player);

        // Calculate the starting angle for the spread (centered on the fire direction)
        float startAngle = -spreadAngle / 2f;
        float angleStep = spreadAngle / (bulletCount - 1);

        // Loop through and instantiate each bullet in the spread
        for (int i = 0; i < bulletCount; i++)
        {
            // Calculate the angle for each bullet relative to the center of the spread
            float currentAngle = startAngle + (i * angleStep);

            // Calculate the direction for each bullet
            Vector3 bulletDirection = Quaternion.Euler(0, 0, currentAngle) * directionToFire;

            // Instantiate and initialize the bullet with the direction
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            InitializeBullet(bullet, firePoint, player, bulletDirection);

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
