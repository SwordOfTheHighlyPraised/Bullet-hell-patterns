using UnityEngine;

[CreateAssetMenu(fileName = "SingleShotPattern", menuName = "Bullet Patterns/Single Shot")]
public class SingleShotPattern : BulletPatternBase
{
    public override void Fire(Transform firePoint, Transform player, MonoBehaviour runner, BulletPatternRuntimeState state)
    {
        if (firePoint == null || bulletPrefab == null) return;
        if (moveToPlayer && player == null) return;

        if (state == null)
        {
            state = new BulletPatternRuntimeState();
            state.ResetFromConfig(this);
        }

        if (runner == null)
            runner = firePoint.GetComponentInParent<MonoBehaviour>();

        int arrays = Mathf.Max(1, totalBulletArrays);
        int bulletsPerArray = Mathf.Max(1, numberOfBulletsPerArray);

        bool aimMode = moveToPlayer && player != null;

        // Slider is authoritative when spin is OFF
        if (!enableSpin)
            state.fireAngle = fireAngle;

        // Aim+Spin: seed only first shot from player
        if (aimMode && enableSpin && !state.hasSeededFromPlayer)
        {
            Vector2 toPlayerSeed = (player.position - firePoint.position);
            state.fireAngle = NormalizeAngle(Mathf.Atan2(toPlayerSeed.y, toPlayerSeed.x) * Mathf.Rad2Deg);
            state.hasSeededFromPlayer = true;
        }

        // Seed spin center once
        if (enableSpin && !state.spinCenterSeeded)
        {
            state.spinCenterAngle = state.fireAngle;
            state.spinOffset = 0f;
            state.spinStep = Mathf.Abs(currentSpinSpeed);
            if (state.spinStep < 0.0001f) state.spinStep = 1f;
            state.spinDirection = (spinSpeedChangeRate < 0f) ? -1 : 1;
            state.spinCenterSeeded = true;
        }

        // Base angle per shot:
        // - aimMode + spin ON => frozen seeded/spun angle
        // - aimMode + spin OFF => live player tracking
        // - manual mode => state.fireAngle
        float baseAngle;
        if (aimMode)
        {
            if (enableSpin)
            {
                baseAngle = state.fireAngle;
            }
            else
            {
                Vector2 toPlayerNow = (player.position - firePoint.position);
                baseAngle = NormalizeAngle(Mathf.Atan2(toPlayerNow.y, toPlayerNow.x) * Mathf.Rad2Deg);
            }
        }
        else
        {
            baseAngle = state.fireAngle;
        }

        for (int arrayIndex = 0; arrayIndex < arrays; arrayIndex++)
        {
            float arrayRotationOffset = GetAnchoredArrayOffset(arrayIndex, arrays, totalArraySpread);

            for (int bulletIndex = 0; bulletIndex < bulletsPerArray; bulletIndex++)
            {
                float bulletOffset = GetBulletOffsetWithinArray(
                    bulletIndex,
                    bulletsPerArray,
                    individualArraySpread
                );

                float spreadOffset = arrayRotationOffset + bulletOffset;
                float finalRotation = baseAngle + spreadOffset;

                Vector3 directionToFire = GetDirectionFromAngle(finalRotation, firePoint, null);

                Vector3 spawnPosition = new Vector3(
                    firePoint.position.x + xOffset,
                    firePoint.position.y + yOffset,
                    firePoint.position.z
                );

                GameObject bullet = CreateBullet(spawnPosition);
                InitializeBullet(bullet, firePoint, player, directionToFire, runner, state);
                Destroy(bullet, bulletLifespan);
            }
        }

        // Advance spin for next shot
        if (enableSpin)
            ApplySpinPerShot(state);
    }

}
