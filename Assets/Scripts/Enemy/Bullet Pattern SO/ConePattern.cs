using UnityEngine;

[CreateAssetMenu(fileName = "ConePattern", menuName = "Bullet Patterns/Cone")]
public class ConePattern : BulletPatternBase
{
    [Header("Cone Settings")]
    public int coneLayers = 4;                // Total layers
    public int bulletsPerLayer = 6;           // Base (widest) layer bullet count
    public int tipBulletsPerLayer = 1;        // Tip (narrowest) layer bullet count
    public float tipSpreadAngle = 15f;        // Spread at tip layer
    public float baseSpreadAngle = 45f;       // Spread at base layer
    public float tipDistance = 1f;            // Distance of tip layer from fire point
    public float baseDistance = 3f;           // Distance of base layer from fire point
    public bool startFromBaseFirst = false;

    public override void Fire(Transform firePoint, Transform player, MonoBehaviour runner, BulletPatternRuntimeState state)
    {
        if (firePoint == null || bulletPrefab == null) return;

        if (state == null)
        {
            state = new BulletPatternRuntimeState();
            state.ResetFromConfig(this);
        }

        if (runner == null)
            runner = firePoint.GetComponentInParent<MonoBehaviour>();

        int arrays = Mathf.Max(1, totalBulletArrays);
        int layers = Mathf.Max(1, coneLayers);
        int baseBullets = Mathf.Max(1, bulletsPerLayer);
        int tipBullets = Mathf.Max(1, tipBulletsPerLayer);

        //// Use shared Bullet Array Settings count (same behavior as SingleShotPattern).
        //int bulletsPerArray = Mathf.Max(1, numberOfBulletsPerArray);

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

        // Base angle per shot
        float baseAngle;
        if (aimMode)
        {
            if (enableSpin)
            {
                baseAngle = state.fireAngle; // seeded/spun angle
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
            Vector3 arrayDirection = GetDirectionFromAngle(baseAngle + arrayRotationOffset, firePoint, null);


            for (int step = 0; step < layers; step++)
            {
                // Always emit in reverse draw order (far/base visually first)
                int emitLayer = layers - 1 - step;

                // Independently control cone progression direction
                int progressionIndex = startFromBaseFirst
                    ? (layers - 1 - step)  // base -> tip
                    : step;                // tip -> base

                float t = (layers == 1) ? 0f : (float)progressionIndex / (layers - 1);

                int bulletsThisLayer;
                if (tipBullets <= baseBullets)
                    bulletsThisLayer = Mathf.Min(baseBullets, tipBullets + progressionIndex); // 1,2,3,4,5,5...
                else
                    bulletsThisLayer = Mathf.Max(baseBullets, tipBullets - progressionIndex);

                float currentSpread = Mathf.Lerp(tipSpreadAngle, baseSpreadAngle, t);
                float layerDistance = Mathf.Lerp(tipDistance, baseDistance, t);

                for (int b = 0; b < bulletsThisLayer; b++)
                {
                    float bulletOffset = GetBulletOffsetWithinArray(b, bulletsThisLayer, currentSpread);
                    Vector3 bulletDirection = Quaternion.Euler(0f, 0f, bulletOffset) * arrayDirection;

                    Vector3 spawnPosition = new Vector3(
                        firePoint.position.x + xOffset + bulletDirection.x * layerDistance,
                        firePoint.position.y + yOffset + bulletDirection.y * layerDistance,
                        firePoint.position.z
                    );

                    GameObject bullet = CreateBullet(spawnPosition);
                    InitializeBullet(bullet, firePoint, player, bulletDirection, runner, state);
                    Destroy(bullet, bulletLifespan);
                }
            }
        }

        if (enableSpin)
            ApplySpinPerShot(state);
    }

    private static int GetBulletsForLayerStepToBase(int layerIndex, int tipCount, int baseCount)
    {
        tipCount = Mathf.Max(1, tipCount);
        baseCount = Mathf.Max(1, baseCount);

        if (tipCount <= baseCount)
            return Mathf.Min(baseCount, tipCount + layerIndex); // ascend then clamp
        else
            return Mathf.Max(baseCount, tipCount - layerIndex); // descend then clamp
    }

}
