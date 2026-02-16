using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BeamPattern", menuName = "Bullet Patterns/Beam")]
public class BeamPattern : BulletPatternBase
{
    public enum BeamFanOrderMode
    {
        ArrayMajor,   // A0B0, A0B1, A0B2, A1B0, A1B1...
        BulletMajor   // A0B0, A1B0, A2B0, A0B1, A1B1...
    }

    public enum BeamDespawnMode
    {
        AllAtOnce_VolleyEnd,     // default: all beams vanish together when last would end
        AllAtOnce_FirstBeamEnd,  // all beams vanish at first beam's natural end
        PerBeam_Individual       // old behavior: each beam ends by its own timer
    }

    [Header("Telegraph")]
    public GameObject beamTelegraphPrefab;
    public float telegraphDuration = 1f;

    [Header("Beam (Tiled Segments)")]
    [Tooltip("Prefab that has StraightLaserBeam2D + BoxCollider2D (trigger). If null, we create one at runtime.")]
    public GameObject beamRootPrefab;

    [Tooltip("Small sprite prefab used as a repeating segment. Should NOT have a collider.")]
    public GameObject beamSegmentPrefab;

    public float beamDuration = 2f;
    public float beamWidth = 1f;
    public float beamRange = 10f;
    public float yOffsetBeam = 0.5f;
    public float growthDuration = 0.5f;

    [Header("Pivot Settings")]
    public Vector3 pivotOffset = new Vector3(-0.5f, 0f, 0f);

    [Header("Behavior")]
    public bool resetAngleAfterBeam = true;

    [Tooltip("When Move To Player is ON, sample target once at telegraph spawn.")]
    public bool trackPlayerDuringTelegraph = true;

    [Tooltip("If true, beam keeps telegraph's final angle. If false, re-samples target at beam spawn.")]
    public bool lockBeamToTelegraphFinalAngle = true;

    [Header("Beam Burst Timing")]
    [Tooltip("If ON, beam casts start sequentially with interBeamDelay. If OFF, all casts start at once.")]
    public bool useInterBeamDelay = false;
    [Min(0f)] public float interBeamDelay = 0.05f;
    public BeamFanOrderMode interBeamOrder = BeamFanOrderMode.ArrayMajor;

    [Tooltip("Only relevant when Use Inter Beam Delay is ON.\nON = each delayed beam spins independently.\nOFF = delayed beams share one spin phase.")]
    public bool delayedBeamsSpinIndependently = true;

    [Header("Beam End Behavior")]
    public BeamDespawnMode despawnMode = BeamDespawnMode.AllAtOnce_VolleyEnd;

    private struct BeamCastData
    {
        public float castAngle;      // absolute angle incl. spread
        public float spreadOffset;   // relative spread around base angle
        public float localSpinSpeed;
        public float localSpinChange;
    }

    private sealed class SharedSpinClock
    {
        public bool started;
        public float angleOffset;
        public float spinSpeed;
        public float spinChangeRate;
    }

    public override void Fire(Transform firePoint, Transform player, MonoBehaviour runner, BulletPatternRuntimeState state)
    {
        if (firePoint == null) return;
        if (beamTelegraphPrefab == null) return;
        if (beamSegmentPrefab == null)
        {
            Debug.LogWarning($"[BeamPattern] '{name}' missing beamSegmentPrefab.", this);
            return;
        }

        bool aimMode = moveToPlayer && player != null;
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
            Debug.LogWarning($"[BeamPattern] No coroutine runner found for '{name}'.", this);
            return;
        }

        float originalAngle = state.fireAngle;

        // Keep slider authoritative when spin is OFF.
        if (!enableSpin)
            state.fireAngle = fireAngle;

        int arrays = Mathf.Max(1, totalBulletArrays);
        int bulletsPerArray = Mathf.Max(1, numberOfBulletsPerArray);

        Vector3 origin = GetBeamStart(firePoint);
        float baseAngle = aimMode
            ? AngleToTarget(origin, player.position, state.fireAngle)
            : state.fireAngle;

        List<BeamCastData> casts = BuildCastList(arrays, bulletsPerArray, baseAngle, state);

        // -----------------------------
        // batchDelay + timing belong HERE (inside Fire orchestration)
        // -----------------------------
        float batchDelay = (useInterBeamDelay && interBeamDelay > 0f)
            ? interBeamDelay * Mathf.Max(0, casts.Count - 1)
            : 0f;

        float firstBeamEndOffset = telegraphDuration + growthDuration + beamDuration;
        float fullBatchDuration = firstBeamEndOffset + batchDelay;

        float now = Time.time;
        float firstBeamEndTime = now + firstBeamEndOffset;
        float volleyEndTime = now + fullBatchDuration;

        float globalEndOffset = (despawnMode == BeamDespawnMode.AllAtOnce_FirstBeamEnd)
            ? firstBeamEndOffset
            : fullBatchDuration;

        SharedSpinClock sharedClock = null;
        bool useSharedSpinClock = useInterBeamDelay
                                  && interBeamDelay > 0f
                                  && enableSpin
                                  && !delayedBeamsSpinIndependently;

        if (useSharedSpinClock)
        {
            float lastBeamStartDelay = interBeamDelay * Mathf.Max(0, casts.Count - 1);

            // Start shared spin AFTER all beams have spawned (after last telegraph completes)
            float sharedSpinStartDelay = lastBeamStartDelay + telegraphDuration + growthDuration;
            float sharedSpinRunDuration = Mathf.Max(0f, globalEndOffset - sharedSpinStartDelay);

            sharedClock = new SharedSpinClock
            {
                started = false,
                angleOffset = 0f,
                spinSpeed = state.currentSpinSpeed,
                spinChangeRate = state.spinSpeedChangeRate
            };

            runner.StartCoroutine(RunSharedSpinClock(sharedClock, sharedSpinStartDelay, sharedSpinRunDuration));
        }

        if (resetAngleAfterBeam)
            runner.StartCoroutine(ResetAngleAfterDelay(state, originalAngle, globalEndOffset));

        // Start casts
        if (useInterBeamDelay && interBeamDelay > 0f)
        {
            runner.StartCoroutine(FireBeamBatchWithDelay(
                firePoint, player, aimMode, casts, runner,
                sharedClock, firstBeamEndTime, volleyEndTime));
        }
        else
        {
            for (int i = 0; i < casts.Count; i++)
            {
                BeamCastData c = casts[i];
                runner.StartCoroutine(FireBeamSequence(
                    firePoint, player,
                    c.castAngle, c.spreadOffset,
                    aimMode,
                    c.localSpinSpeed, c.localSpinChange,
                    sharedClock,
                    firstBeamEndTime, volleyEndTime
                ));
            }
        }

        // Advance runtime once per Fire() call.
        ApplySpinPerShot(state);
    }

    private List<BeamCastData> BuildCastList(int arrays, int bulletsPerArray, float baseAngle, BulletPatternRuntimeState state)
    {
        var casts = new List<BeamCastData>(arrays * bulletsPerArray);

        if (interBeamOrder == BeamFanOrderMode.ArrayMajor)
        {
            for (int arrayIndex = 0; arrayIndex < arrays; arrayIndex++)
            {
                float arrayOffset = GetAnchoredArrayOffset(arrayIndex, arrays, totalArraySpread);

                for (int bulletIndex = 0; bulletIndex < bulletsPerArray; bulletIndex++)
                {
                    float bulletOffset = GetBulletOffsetWithinArray(
                        bulletIndex, bulletsPerArray, individualArraySpread);

                    float spreadOffset = arrayOffset + bulletOffset;

                    casts.Add(new BeamCastData
                    {
                        castAngle = NormalizeAngle(baseAngle + spreadOffset),
                        spreadOffset = spreadOffset,
                        localSpinSpeed = state.currentSpinSpeed,
                        localSpinChange = state.spinSpeedChangeRate
                    });
                }
            }
        }
        else // BulletMajor
        {
            for (int bulletIndex = 0; bulletIndex < bulletsPerArray; bulletIndex++)
            {
                float bulletOffset = GetBulletOffsetWithinArray(
                    bulletIndex, bulletsPerArray, individualArraySpread);

                for (int arrayIndex = 0; arrayIndex < arrays; arrayIndex++)
                {
                    float arrayOffset = GetAnchoredArrayOffset(arrayIndex, arrays, totalArraySpread);
                    float spreadOffset = arrayOffset + bulletOffset;

                    casts.Add(new BeamCastData
                    {
                        castAngle = NormalizeAngle(baseAngle + spreadOffset),
                        spreadOffset = spreadOffset,
                        localSpinSpeed = state.currentSpinSpeed,
                        localSpinChange = state.spinSpeedChangeRate
                    });
                }
            }
        }

        return casts;
    }

    // -----------------------------
    // firebeambatchwithdelay goes here: batching orchestrator
    // -----------------------------
    private IEnumerator FireBeamBatchWithDelay(
        Transform firePoint,
        Transform player,
        bool aimMode,
        List<BeamCastData> casts,
        MonoBehaviour runner,
        SharedSpinClock sharedSpinClock,
        float firstBeamEndTime,
        float volleyEndTime)
    {
        WaitForSeconds wait = new WaitForSeconds(interBeamDelay);

        for (int i = 0; i < casts.Count; i++)
        {
            if (firePoint == null) yield break;

            BeamCastData c = casts[i];
            runner.StartCoroutine(FireBeamSequence(
                firePoint, player,
                c.castAngle, c.spreadOffset,
                aimMode,
                c.localSpinSpeed, c.localSpinChange,
                sharedSpinClock,
                firstBeamEndTime, volleyEndTime
            ));

            if (i < casts.Count - 1)
                yield return wait;
        }
    }

    // -----------------------------
    // firebeamsequence goes here: per-beam lifecycle
    // -----------------------------
    private IEnumerator FireBeamSequence(
        Transform firePoint,
        Transform player,
        float castBaseAngle,
        float spreadOffset,
        bool aimMode,
        float localSpinSpeed,
        float localSpinChange,
        SharedSpinClock sharedSpinClock,
        float firstBeamEndTime,
        float volleyEndTime)
    {
        if (firePoint == null) yield break;

        Vector3 BeamStart() => GetBeamStart(firePoint);

        // 1) Telegraph (STATIC)
        Vector3 telegraphStart = BeamStart();
        float angleNow = castBaseAngle;

        if (aimMode && player != null && trackPlayerDuringTelegraph)
        {
            float baseAim = AngleToTarget(telegraphStart, player.position, castBaseAngle - spreadOffset);
            angleNow = NormalizeAngle(baseAim + spreadOffset);
        }

        GameObject telegraph = Instantiate(
            beamTelegraphPrefab,
            telegraphStart,
            Quaternion.Euler(0f, 0f, angleNow)
        );
        telegraph.transform.localScale = new Vector3(beamRange, 0.1f, 1f);

        float tTele = 0f;
        while (tTele < telegraphDuration)
        {
            if (firePoint == null)
            {
                if (telegraph != null) Destroy(telegraph);
                yield break;
            }

            tTele += Time.deltaTime;
            yield return null;
        }

        if (telegraph != null) Destroy(telegraph);
        if (firePoint == null) yield break;

        // 2) Spawn beam root
        Vector3 spawnStart = BeamStart();

        if (aimMode && player != null && !lockBeamToTelegraphFinalAngle)
        {
            float baseAimAtSpawn = AngleToTarget(spawnStart, player.position, angleNow - spreadOffset);
            angleNow = NormalizeAngle(baseAimAtSpawn + spreadOffset);
        }

        // 2) Beam root + renderer
        GameObject beamRoot = beamRootPrefab != null
            ? Instantiate(beamRootPrefab, spawnStart, Quaternion.Euler(0f, 0f, angleNow))
            : new GameObject("BeamRoot");

        beamRoot.transform.position = spawnStart;
        beamRoot.transform.rotation = Quaternion.Euler(0f, 0f, angleNow);

        StraightLaserBeam2D tiled = beamRoot.GetComponent<StraightLaserBeam2D>();
        if (tiled == null) tiled = beamRoot.AddComponent<StraightLaserBeam2D>();
        tiled.Configure(beamSegmentPrefab, beamWidth);


        // 3) Grow
        float tGrow = 0f;
        while (tGrow < growthDuration)
        {
            if (beamRoot == null || firePoint == null) yield break;

            tGrow += Time.deltaTime;
            float p = Mathf.Clamp01(tGrow / Mathf.Max(0.0001f, growthDuration));

            beamRoot.transform.position = BeamStart();
            beamRoot.transform.rotation = Quaternion.Euler(0f, 0f, angleNow); // fixed during growth

            tiled.SetLength(beamRange * p); // or lShape.SetLengths(...) if using L
            yield return null;
        }

        // 4) Active (SPIN STARTS HERE)

        float endTime = ResolveBeamEndTime(firstBeamEndTime, volleyEndTime);
        while (Time.time < endTime)
        {
            if (beamRoot == null || firePoint == null) yield break;

            float dt = Time.deltaTime;

            if (enableSpin)
            {
                if (sharedSpinClock != null)
                    angleNow = GetSharedAngle(castBaseAngle, sharedSpinClock);
                else
                    StepBeamSpin(ref angleNow, ref localSpinSpeed, ref localSpinChange, dt);
            }

            beamRoot.transform.position = BeamStart();
            beamRoot.transform.rotation = Quaternion.Euler(0f, 0f, angleNow);

            yield return null;
        }

        if (beamRoot != null) Destroy(beamRoot);
    }

    private float ResolveBeamEndTime(float firstBeamEndTime, float volleyEndTime)
    {
        switch (despawnMode)
        {
            case BeamDespawnMode.AllAtOnce_FirstBeamEnd:
                return firstBeamEndTime;

            case BeamDespawnMode.PerBeam_Individual:
                return Time.time + beamDuration;

            case BeamDespawnMode.AllAtOnce_VolleyEnd:
            default:
                return volleyEndTime;
        }
    }

    private IEnumerator RunSharedSpinClock(SharedSpinClock clock, float startDelay, float runDuration)
    {
        if (clock == null) yield break;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        clock.started = true;

        float t = 0f;
        while (t < runDuration && clock != null)
        {
            float dt = Time.deltaTime;
            t += dt;

            clock.angleOffset = NormalizeAngle(clock.angleOffset + clock.spinSpeed * dt);

            clock.spinSpeed += clock.spinChangeRate * dt;
            clock.spinSpeed = Mathf.Clamp(clock.spinSpeed, -maxSpinSpeed, maxSpinSpeed);

            if (spinReversal && Mathf.Abs(clock.spinSpeed) >= maxSpinSpeed)
                clock.spinChangeRate = -clock.spinChangeRate;

            yield return null;
        }
    }

    private float GetSharedAngle(float castBaseAngle, SharedSpinClock clock)
    {
        if (clock == null || !clock.started)
            return NormalizeAngle(castBaseAngle);

        return NormalizeAngle(castBaseAngle + clock.angleOffset);
    }

    private IEnumerator ResetAngleAfterDelay(BulletPatternRuntimeState state, float angle, float delay)
    {
        if (state == null) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        state.fireAngle = angle;
    }

    private void StepBeamSpin(ref float angle, ref float spinSpeed, ref float spinChangeRate, float dt)
    {
        angle = NormalizeAngle(angle + spinSpeed * dt);

        spinSpeed += spinChangeRate * dt;
        spinSpeed = Mathf.Clamp(spinSpeed, -maxSpinSpeed, maxSpinSpeed);

        if (spinReversal && Mathf.Abs(spinSpeed) >= maxSpinSpeed)
            spinChangeRate = -spinChangeRate;
    }

    private float AngleToTarget(Vector3 from, Vector3 to, float fallback)
    {
        Vector2 d = (Vector2)(to - from);
        if (d.sqrMagnitude <= 0.0001f) return fallback;
        return NormalizeAngle(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private Vector3 GetBeamStart(Transform firePoint)
    {
        if (firePoint == null) return Vector3.zero;
        return firePoint.position + new Vector3(xOffset, yOffsetBeam, 0f) + pivotOffset;
    }
}
