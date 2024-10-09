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

        for (int i = 0; i < burstCount; i++)
        {
            // Flip the spiral direction for each burst (e.g., clockwise for even bursts, counter-clockwise for odd)
            bool currentSpiralDirection = (i % 2 == 0) ? true : false;

            // Fire bulletsPerLocation bullets from the same offset position
            for (int j = 0; j < bulletsPerLocation; j++)
            {
                // Calculate the offset position
                Vector3 offsetPosition = firePoint.position + (Vector3)fireOffsets[currentOffsetIndex];

                // Instantiate a bullet at the offset position
                GameObject bullet = Instantiate(bulletPrefab, offsetPosition, Quaternion.identity);

                // Get the direction to the player or use the angle based on the movement settings
                Vector3 directionToPlayer = GetInitialBulletDirection(firePoint, player);

                // Initialize the bullet to follow the movement settings (sine, spiral, etc.)
                InitializeBurstBullet(bullet, directionToPlayer, currentSpiralDirection, firePoint, player);

                yield return new WaitForSeconds(burstFireRate);  // Wait between bullets in the same burst
            }

            // Switch to the next offset position
            currentOffsetIndex = (currentOffsetIndex + 1) % fireOffsets.Length;

            // Wait between burst sequences
            yield return new WaitForSeconds(burstCountRate);
        }
    }

    // Initialize the bullet and follow base movement settings (sine wave, spiral, etc.)
    private void InitializeBurstBullet(GameObject bullet, Vector3 directionToPlayer, bool spiralClockwise, Transform firePoint, Transform player)
    {
        // Temporarily set the spiral direction for this bullet
        BulletPatternBase patternBase = this;
        patternBase.spiralClockwise = spiralClockwise;

        // Initialize the bullet using the base class method with movement settings
        InitializeBullet(bullet, firePoint, player, directionToPlayer);  // Pass directionToPlayer
    }
}
