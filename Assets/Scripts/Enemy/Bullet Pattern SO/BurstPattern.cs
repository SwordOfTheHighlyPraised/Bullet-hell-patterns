// BurstPattern.cs
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "BurstPattern", menuName = "Bullet Patterns/Burst")]
public class BurstPattern : BulletPatternBase
{
    [Header("Burst Settings")]
    public int bulletsPerLocation = 2;
    public int burstCount = 6;
    public float burstFireRate = 0.1f;
    public float burstCountRate = 0.5f;

    [Header("Offset Settings")]
    public Vector2[] fireOffsets;

    public enum SpinAdvanceMode
    {
        PerBurst,
        PerArray,
        PerDirection,
        PerBulletInstance
    }

    [Header("Spin Settings (Burst)")]
    // Default as requested: currentSpinSpeed advances per burst
    public SpinAdvanceMode spinAdvanceMode = SpinAdvanceMode.PerBurst;

    // Used by BulletSpawnerV3 to prevent overlapping Fire() calls for this pattern
    public float GetRecommendedMinRefireDelay()
    {
        int arrays = Mathf.Max(1, totalBulletArrays);
        int bulletsPerArray = Mathf.Max(1, numberOfBulletsPerArray);
        int perLoc = Mathf.Max(1, bulletsPerLocation);
        int bursts = Mathf.Max(1, burstCount);

        int shotsPerBurst = arrays * bulletsPerArray * perLoc;

        // Waits between shots inside a burst (no trailing wait after final shot)
        float intraBurst = Mathf.Max(0, shotsPerBurst - 1) * Mathf.Max(0f, burstFireRate);

        // Waits between bursts (no trailing wait after final burst)
        float interBurst = Mathf.Max(0, bursts - 1) * Mathf.Max(0f, burstCountRate);

        return (bursts * intraBurst) + interBurst;
    }

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

        if (runner == null)
        {
            Debug.LogWarning($"[BurstPattern] No coroutine runner found for pattern '{name}'.", this);
            return;
        }

        if (!enableSpin)
            state.fireAngle = fireAngle;

        bool aimMode = moveToPlayer && player != null;
        runner.StartCoroutine(FireBurstRoutine(firePoint, player, runner, state, aimMode));
    }

    private IEnumerator FireBurstRoutine(
        Transform firePoint,
        Transform player,
        MonoBehaviour runner,
        BulletPatternRuntimeState state,
        bool aimMode)
    {
        int arrays = Mathf.Max(1, totalBulletArrays);
        int bulletsPerArray = Mathf.Max(1, numberOfBulletsPerArray);
        int perLoc = Mathf.Max(1, bulletsPerLocation);
        int totalBursts = Mathf.Max(1, burstCount);

        int offsetCount = (fireOffsets != null) ? fireOffsets.Length : 0;
        int offsetIndex = 0;

        WaitForSeconds waitPerBullet = burstFireRate > 0f ? new WaitForSeconds(burstFireRate) : null;
        WaitForSeconds waitPerBurst = burstCountRate > 0f ? new WaitForSeconds(burstCountRate) : null;

        bool stepPerBurst = spinAdvanceMode == SpinAdvanceMode.PerBurst;
        bool stepPerArray = spinAdvanceMode == SpinAdvanceMode.PerArray;
        bool stepPerDir = spinAdvanceMode == SpinAdvanceMode.PerDirection;
        bool stepPerInstance = spinAdvanceMode == SpinAdvanceMode.PerBulletInstance;

        int shotsPerBurst = arrays * bulletsPerArray * perLoc;

        for (int burst = 0; burst < totalBursts; burst++)
        {
            if (firePoint == null) yield break;

            Vector2 off = (offsetCount > 0) ? fireOffsets[offsetIndex] : Vector2.zero;
            int emittedThisBurst = 0;

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

                    for (int j = 0; j < perLoc; j++)
                    {
                        if (firePoint == null) yield break;

                        float baseAngle = ComputeBurstBaseAngle(firePoint, player, state, aimMode);
                        float castAngle = NormalizeAngle(baseAngle + arrayRotationOffset + bulletOffset);
                        Vector3 finalDirection = GetDirectionFromAngle(castAngle, firePoint, null);

                        Vector3 spawnPosition = new Vector3(
                            firePoint.position.x + xOffset + off.x,
                            firePoint.position.y + yOffset + off.y,
                            firePoint.position.z
                        );

                        GameObject bullet = CreateBullet(spawnPosition);
                        bool spiralCW = (burst % 2 == 0);

                        InitializeBullet(
                            bullet,
                            firePoint,
                            player,
                            finalDirection,
                            runner,
                            state,
                            spiralClockwiseOverride: spiralCW
                        );

                        Destroy(bullet, bulletLifespan);

                        emittedThisBurst++;

                        if (stepPerInstance)
                            StepBurstSpin(state);

                        // no trailing bullet wait after last shot in burst
                        if (waitPerBullet != null && emittedThisBurst < shotsPerBurst)
                            yield return waitPerBullet;
                    }

                    if (stepPerDir)
                        StepBurstSpin(state);
                }

                if (stepPerArray)
                    StepBurstSpin(state);
            }

            if (offsetCount > 0)
                offsetIndex = (offsetIndex + 1) % offsetCount;

            if (stepPerBurst)
                StepBurstSpin(state);

            // no trailing burst wait after final burst
            if (waitPerBurst != null && burst < totalBursts - 1)
                yield return waitPerBurst;
        }
    }

    private float ComputeBurstBaseAngle(
        Transform firePoint,
        Transform player,
        BulletPatternRuntimeState state,
        bool aimMode)
    {
        if (aimMode && player != null)
        {
            Vector2 toPlayer = (Vector2)(player.position - firePoint.position);
            float playerAngle = (toPlayer.sqrMagnitude > 0.0001f)
                ? Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg
                : fireAngle;

            float spinOffset = Mathf.DeltaAngle(fireAngle, state.fireAngle);
            return NormalizeAngle(playerAngle + spinOffset);
        }

        return state.fireAngle;
    }

    private void StepBurstSpin(BulletPatternRuntimeState state)
    {
        if (!enableSpin || state == null) return;

        state.fireAngle = NormalizeAngle(state.fireAngle + state.currentSpinSpeed);

        state.currentSpinSpeed += state.spinSpeedChangeRate;
        state.currentSpinSpeed = Mathf.Clamp(state.currentSpinSpeed, -maxSpinSpeed, maxSpinSpeed);

        if (spinReversal && Mathf.Abs(state.currentSpinSpeed) >= maxSpinSpeed)
            state.spinSpeedChangeRate = -state.spinSpeedChangeRate;
    }
}
