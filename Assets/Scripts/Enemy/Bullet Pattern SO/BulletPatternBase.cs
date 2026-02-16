using System.Collections;
using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// Runtime state for a bullet pattern. This must be per-spawner/per-slot.
/// Do NOT store mutable runtime variables on the ScriptableObject asset itself.
/// </summary>
[System.Serializable]
public class BulletPatternRuntimeState
{
    public float fireAngle;
    public float currentSpinSpeed;
    public float spinSpeedChangeRate;
    public bool useSineWave = true;

    // NEW: one-shot seed for moveToPlayer
    public bool hasSeededFromPlayer;

    public float spinCenterAngle;     // fixed center angle for this activation
    public bool spinCenterSeeded;     // seed once per activation

    // NEW: ping-pong spin runtime
    public float spinOffset;          // current offset from center
    public float spinStep;            // degrees per shot
    public int spinDirection;         // +1 or -1



    public void ResetFromConfig(BulletPatternBase config)
    {
        if (config == null) return;
        fireAngle = config.fireAngle;
        currentSpinSpeed = config.currentSpinSpeed;
        spinSpeedChangeRate = config.spinSpeedChangeRate;
        useSineWave = true;

        hasSeededFromPlayer = false;

        spinCenterAngle = config.fireAngle;
        spinCenterSeeded = false;

        spinOffset = 0f;
        spinStep = Mathf.Abs(config.currentSpinSpeed);
        if (spinStep < 0.0001f) spinStep = 1f;
        spinDirection = (config.spinSpeedChangeRate < 0f) ? -1 : 1;


    }
}

public abstract class BulletPatternBase : ScriptableObject
{
    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    [InspectorName("Linear Bullet Speed")] public float bulletSpeed = 5f;              // Initial bullet speed from ScriptableObject
    [InspectorName("Linear Acceleration")] public float acceleration = 0f;             // Rate of acceleration
    public float bulletLifespan = 5f;           // Lifespan of the bullet (in seconds)


    [Header("Offset and Size")]
    [SerializeField]
    public float objectWidth = 1f;   // Width of the bullet prefab
    [SerializeField]
    public float objectHeight = 1f;  // Height of the bullet prefab
    [SerializeField]
    public float xOffset = 0f;       // X offset for bullet spawn position
    [SerializeField]
    public float yOffset = 0f;       // Y offset for bullet spawn position


    [Header("Movement Settings")]
    public bool moveToPlayer = true;            // Toggle to decide if the bullet should move towards the player
    [Range(0f, 359f)] public float fireAngle = 0f; // Angle (0-359) to fire in a specific direction if not moving to the player

    [Header("Bullet Array Settings")]
    [SerializeField, Range(1, 10)]
    public int numberOfBulletsPerArray = 5;  // Number of bullets in a single array (1-10)

    [SerializeField, Range(1, 360)]
    public float individualArraySpread = 30f;  // Spread between bullets in the same array (1-360)

    [SerializeField, Range(1, 10)]
    public int totalBulletArrays = 3;  // Total number of bullet arrays (1-10)

    [SerializeField, Range(0, 360)]
    public float totalArraySpread = 90f;  // Spread between bullet arrays (1-360)

    [Header("Spin Settings")]
    public bool enableSpin = false;                  // Enable spin functionality
    [Range(-360f, 360f)] public float currentSpinSpeed = 0f; // Current spin speed (degrees per second)
    [Range(-180f, 180f)] public float spinSpeedChangeRate = 0f; // Rate of change of spin speed
    [Range(0f, 360f)] public float maxSpinSpeed = 180f; // Maximum spin speed (0-360)
    public bool spinReversal = false;                // Reverse spin when max/min speed is reached

    [Header("Sine Wave Settings")]
    [Range(-5f, 5f)] public float sineAmplitude = 1f;   // Sine wave amplitude (range: -5 to 5)
    [Range(-10f, 10f)] public float sineFrequency = 1f;  // Sine wave frequency (range: -10 to 10)
    public bool enableSineWave = false;                // Enable sine wave movement

    [Header("Cosine Wave Settings")]
    [Range(-5f, 5f)] public float cosineAmplitude = 1f;   // Cosine wave amplitude (range: -5 to 5)
    [Range(-10f, 10f)] public float cosineFrequency = 1f;  // Cosine wave frequency (range: -10 to 10)
    public bool enableCosineWave = false;              // Enable cosine wave movement


    [Header("Spiral Settings")]
    public bool enableSpiral = false;                  // Enable spiral movement
    [InspectorName("Spiral Turn Rate (deg/s)")][Range(0f, 360f)] public float spiralSpeed = 45f;  // Speed of the spiral rotation (degrees per second)
    [InspectorName("Spiral Clockwise")] public bool spiralClockwise = true;                // Toggle for spiral direction: clockwise or anti-clockwise

    [Header("Homing Settings")]
    public bool enableHoming = false;                  // Enable or disable homing behavior
    public int maxStops = 1;                           // Number of times the bullet can stop and redirect
    public float stopDuration = 1f;                    // Time the bullet stops before redirecting
    public float initialMovementTime = 1f;             // Time to move in the initial direction before stopping
    public float curveDuration = 0.5f;                 // Time it takes to curve to the new direction
    [Tooltip("OFF (default): Sine/Cos/Spiral only during the first initial movement window. ON: keep them active in all homing phases.")]
    public bool homingStyleAllPhases = false;


    private float currentAngle = 0f;  // Used to track the angle for spinning or spiraling
    private bool useSineWave = true;  // Tracks whether the current bullet should use sine or cosine wave

    public void SetTotalBulletArrays(int arrays) => totalBulletArrays = arrays;
    public void SetIndividualArraySpread(float spread) => individualArraySpread = spread;

    /// <summary>
    /// Legacy entry point (kept for compatibility). Uses a temporary runtime state.
    /// Burst/Beam patterns that require coroutines may not behave correctly through this overload.
    /// </summary>
    public virtual void Fire(Transform firePoint, Transform player = null)
    {
        var tmp = new BulletPatternRuntimeState();
        tmp.ResetFromConfig(this);

        // Best-effort runner: try the firePoint's GameObject.
        MonoBehaviour runner = firePoint != null ? firePoint.GetComponentInParent<MonoBehaviour>() : null;
        Fire(firePoint, player, runner, tmp);
    }


    /// <summary>
    /// Preferred entry point. Provide a MonoBehaviour runner for coroutines and a per-slot runtime state.
    /// </summary>
    public virtual void Fire(Transform firePoint, Transform player, MonoBehaviour runner, BulletPatternRuntimeState state)
    {
        if (firePoint == null) return;
        if (bulletPrefab == null) return;

        if (state == null)
        {
            state = new BulletPatternRuntimeState();
            state.ResetFromConfig(this);
        }

        if (runner == null)
            runner = firePoint.GetComponentInParent<MonoBehaviour>();

        bool aimMode = moveToPlayer && player != null;

        // Keep slider authoritative whenever spin is OFF.
        if (!enableSpin)
            state.fireAngle = fireAngle;

        // NEW: only first shot in activation seeds from player
        if (aimMode && !state.hasSeededFromPlayer)
        {
            Vector2 toPlayer = (player.position - firePoint.position);
            state.fireAngle = NormalizeAngle(Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg);
            state.hasSeededFromPlayer = true;
        }

        // Seed spin center once per activation (after optional player seed)
        if (enableSpin && !state.spinCenterSeeded)
        {
            state.spinCenterAngle = state.fireAngle;
            state.spinOffset = 0f;
            state.spinStep = Mathf.Abs(currentSpinSpeed);
            if (state.spinStep < 0.0001f) state.spinStep = 1f;
            state.spinDirection = (spinSpeedChangeRate < 0f) ? -1 : 1;
            state.spinCenterSeeded = true;
        }


        SpawnBullets_DefaultArrays(firePoint, player, runner, state);

        // CHANGED: do not block spin when moveToPlayer is true
        ApplySpinPerShot(state);
    }

    protected static float GetAnchoredArrayOffset(int arrayIndex, int totalArrays, float spreadPerStep)
    {
        if (totalArrays <= 1 || arrayIndex <= 0) return 0f;

        int step = (arrayIndex + 1) / 2;                 // 1,1,2,2,3,3...
        float sign = (arrayIndex % 2 == 1) ? 1f : -1f;   // +,-,+,-...
        return sign * step * spreadPerStep;
    }

    protected static float GetBulletOffsetWithinArray(int bulletIndex, int bulletsPerArray, float totalConeSpreadDeg)
    {
        if (bulletsPerArray <= 1) return 0f;

        float spread = Mathf.Clamp(totalConeSpreadDeg, 0f, 360f);

        // Full circle: even ring, no duplicated endpoint
        if (spread >= 359.999f)
        {
            float step = 360f / bulletsPerArray;
            return bulletIndex * step; // e.g. 4 -> 0,90,180,270
        }

        // Centered bins in cone (avoids edge-touch overlaps between neighboring arrays)
        // Example: 5 in 90 => -36, -18, 0, 18, 36
        float stepCone = spread / bulletsPerArray;
        float start = -spread * 0.5f + stepCone * 0.5f;
        return start + bulletIndex * stepCone;
    }



    protected void SpawnBullets_DefaultArrays(Transform firePoint, Transform player, MonoBehaviour runner, BulletPatternRuntimeState state)
    {
        int arrays = Mathf.Max(1, totalBulletArrays);
        int bulletsPerArray = Mathf.Max(1, numberOfBulletsPerArray);


        // Optional hard no-duplicate-angle guard (per Fire call)
        const float eps = 0.01f;
        HashSet<int> usedAngleKeys = new HashSet<int>();

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

                float worldAngle = NormalizeAngle(state.fireAngle + spreadOffset);

                int key = Mathf.RoundToInt(worldAngle / eps);
                if (!usedAngleKeys.Add(key))
                    continue;

                Vector3 directionToFire = GetDirectionFromAngle(worldAngle, firePoint, null);

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
    }

    protected GameObject CreateBullet(Vector3 spawnPosition)
    {
        GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
        bullet.transform.localScale = new Vector3(objectWidth, objectHeight, 1f);

        // Optional: adjust CircleCollider2D radius to match size
        var circle = bullet.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            float radius = Mathf.Min(objectWidth, objectHeight) * 0.5f;
            circle.radius = radius;
        }

        return bullet;
    }

    protected void InitializeBullet(
        GameObject bullet,
        Transform firePoint,
        Transform player,
        Vector3 direction,
        MonoBehaviour runner,
        BulletPatternRuntimeState state,
        bool? spiralClockwiseOverride = null)
    {
        if (bullet == null) return;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        rb.linearVelocity = direction.normalized * bulletSpeed;

        // Face velocity
        float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
        rb.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Coroutine runner (ideally: always your spawner)
        if (runner == null)
            runner = bullet.GetComponent<MonoBehaviour>(); // last resort
        if (runner == null) return;

        // ✅ Gate: if homing is enabled
        bool doHoming = enableHoming && player != null;
        if (doHoming)
        {
            bool useSine = false;
            bool useCosine = false;

            if (enableSineWave && enableCosineWave)
            {
                useSine = (state == null) ? true : state.useSineWave;
                useCosine = !useSine;
                if (state != null) state.useSineWave = !state.useSineWave;
            }
            else if (enableSineWave) useSine = true;
            else if (enableCosineWave) useCosine = true;

            bool clockwise = spiralClockwiseOverride ?? spiralClockwise;

            runner.StartCoroutine(HomingRoutine(
                bullet.transform,
                firePoint,
                player,
                rb,
                useSine,
                useCosine,
                enableSpiral,
                clockwise,
                homingStyleAllPhases
            ));
            return;
        }



        // -----------------------------
        // Non-homing motion stack below
        // -----------------------------

        if (enableSpiral)
        {
            bool useSine = false;
            bool useCosine = false;

            // keep your sine/cos alternation behavior
            if (enableSineWave && enableCosineWave)
            {
                useSine = (state == null) ? true : state.useSineWave;
                useCosine = !useSine;
                if (state != null) state.useSineWave = !state.useSineWave;
            }
            else if (enableSineWave)
            {
                useSine = true;
            }
            else if (enableCosineWave)
            {
                useCosine = true;
            }

            bool clockwise = spiralClockwiseOverride ?? spiralClockwise;
            Vector3 origin = bullet.transform.position;

            runner.StartCoroutine(SpiralBulletOutwardCombined(
                bullet.transform,
                origin,
                direction.normalized,
                bulletSpeed,
                spiralSpeed,
                clockwise,
                useSine,
                useCosine
            ));
            return; // IMPORTANT: prevent starting separate wave/accel coroutines
        }

        // Non-spiral path keeps your existing behavior
        if (Mathf.Abs(acceleration) > 0.0001f)
            runner.StartCoroutine(AccelerateBullet(rb, direction.normalized, bulletSpeed));

        // Sine/Cosine selection (no double-start)
        if (enableSineWave && enableCosineWave)
        {
            if (state != null && state.useSineWave)
                runner.StartCoroutine(MoveBulletWithSineWave(bullet.transform, direction.normalized));
            else
                runner.StartCoroutine(MoveBulletWithCosineWave(bullet.transform, direction.normalized));

            if (state != null) state.useSineWave = !state.useSineWave;
        }
        else if (enableSineWave)
        {
            runner.StartCoroutine(MoveBulletWithSineWave(bullet.transform, direction.normalized));
        }
        else if (enableCosineWave)
        {
            runner.StartCoroutine(MoveBulletWithCosineWave(bullet.transform, direction.normalized));
        }
    }


    // Method to get the direction based on a specific angle (in degrees) relative to the firing point or player
    protected Vector3 GetDirectionFromAngle(float angle, Transform firePoint, Transform player)
    {
        // If the bullet is set to move towards the player, use the player's position
        if (moveToPlayer && player != null)
        {
            return (player.position - firePoint.position).normalized; // Move towards the player
        }
        else
        {
            // Calculate the direction based on the specified angle (convert angle to direction vector)
            float angleInRadians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angleInRadians), Mathf.Sin(angleInRadians), 0).normalized;
        }
    }

    // Get the initial direction based on moveToPlayer or fireAngle settings
    protected Vector3 GetInitialBulletDirection(Transform firePoint, Transform player, BulletPatternRuntimeState state)
    {
        if (moveToPlayer && player != null)
            return (player.position - firePoint.position).normalized;

        float rad = (state != null ? state.fireAngle : fireAngle) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0).normalized;
    }

    protected void ApplySpinPerShot(BulletPatternRuntimeState state)
    {
        if (!enableSpin || state == null) return;

        // Lazy init safety
        if (!state.spinCenterSeeded)
        {
            state.spinCenterAngle = state.fireAngle;
            state.spinOffset = 0f;
            state.spinStep = Mathf.Abs(currentSpinSpeed);
            if (state.spinStep < 0.0001f) state.spinStep = 1f;
            state.spinDirection = (spinSpeedChangeRate < 0f) ? -1 : 1;
            state.spinCenterSeeded = true;
        }

        // Reverse at bounds BEFORE stepping:
        // center, +step, +max, +step, center, -step, -max, -step...
        if (spinReversal)
        {
            if (state.spinDirection > 0 && state.spinOffset >= maxSpinSpeed)
                state.spinDirection = -1;
            else if (state.spinDirection < 0 && state.spinOffset <= -maxSpinSpeed)
                state.spinDirection = 1;
        }

        state.spinOffset += state.spinDirection * state.spinStep;
        state.spinOffset = Mathf.Clamp(state.spinOffset, -maxSpinSpeed, maxSpinSpeed);

        // next shot angle
        state.fireAngle = NormalizeAngle(state.spinCenterAngle + state.spinOffset);
    }



    protected void ApplySpinPerSecond(BulletPatternRuntimeState state, float dt)
    {
        if (!enableSpin || state == null) return;

        state.fireAngle = NormalizeAngle(state.fireAngle + state.currentSpinSpeed * dt);

        state.currentSpinSpeed += state.spinSpeedChangeRate * dt;
        state.currentSpinSpeed = Mathf.Clamp(state.currentSpinSpeed, -maxSpinSpeed, maxSpinSpeed);

        if (spinReversal && Mathf.Abs(state.currentSpinSpeed) >= maxSpinSpeed)
            state.spinSpeedChangeRate = -state.spinSpeedChangeRate;
    }

    protected static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    // ----- Movement Coroutines -----

    // Coroutine for homing behavior with stops and redirects
    protected IEnumerator HomingRoutine(
        Transform bulletTransform,
        Transform firePoint,
        Transform player,
        Rigidbody2D rb,
        bool useSine,
        bool useCosine,
        bool useSpiral,
        bool spiralClockwise,
        bool styleAllPhases)
    {
        int currentStopCount = 0;
        float currentBulletSpeed = bulletSpeed;
        float styleTime = 0f;

        Vector2 direction =
            (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
                ? rb.linearVelocity.normalized
                : (Vector2)GetInitialBulletDirection(firePoint, player);

        // 1) FIRST initial movement window: always styled
        float elapsedTime = 0f;
        while (elapsedTime < initialMovementTime)
        {
            if (bulletTransform == null || rb == null) yield break;

            float dt = Time.deltaTime;
            elapsedTime += dt;

            StepHomingMovement(
                bulletTransform, rb,
                ref direction, ref currentBulletSpeed,
                dt, ref styleTime,
                useSine, useCosine, useSpiral, spiralClockwise,
                applyStyle: true
            );

            yield return null;
        }

        // 2) Stops + redirects
        while (currentStopCount < maxStops)
        {
            if (bulletTransform == null || rb == null || player == null) yield break;

            SetBulletVelocity(rb, Vector2.zero);

            float stopElapsed = 0f;
            while (stopElapsed < stopDuration)
            {
                if (bulletTransform == null || rb == null) yield break;
                stopElapsed += Time.deltaTime;
                SetBulletVelocity(rb, Vector2.zero);
                yield return null;
            }

            Vector2 oldDirection = direction;
            Vector2 desiredDirection = ((Vector2)player.position - (Vector2)bulletTransform.position).normalized;

            if (curveDuration > 0f)
            {
                yield return CurveBulletDirection(
                    bulletTransform,
                    (Vector3)oldDirection,
                    (Vector3)desiredDirection,
                    currentBulletSpeed,
                    rb,
                    player
                );
            }
            else
            {
                SetBulletVelocity(rb, desiredDirection * currentBulletSpeed);
            }

            direction = desiredDirection;

            // Movement after redirect:
            // default OFF => pure homing
            // toggle ON   => styled like current behavior
            elapsedTime = 0f;
            while (elapsedTime < initialMovementTime)
            {
                if (bulletTransform == null || rb == null) yield break;

                float dt = Time.deltaTime;
                elapsedTime += dt;

                StepHomingMovement(
                    bulletTransform, rb,
                    ref direction, ref currentBulletSpeed,
                    dt, ref styleTime,
                    useSine, useCosine, useSpiral, spiralClockwise,
                    applyStyle: styleAllPhases
                );

                yield return null;
            }

            currentStopCount++;
        }

        // 3) Final continuous phase
        while (bulletTransform != null && rb != null)
        {
            float dt = Time.deltaTime;

            StepHomingMovement(
                bulletTransform, rb,
                ref direction, ref currentBulletSpeed,
                dt, ref styleTime,
                useSine, useCosine, useSpiral, spiralClockwise,
                applyStyle: styleAllPhases
            );

            yield return null;
        }
    }

    private void StepHomingMovement(
        Transform bulletTransform,
        Rigidbody2D rb,
        ref Vector2 direction,
        ref float currentBulletSpeed,
        float dt,
        ref float styleTime,
        bool useSine,
        bool useCosine,
        bool useSpiral,
        bool spiralClockwise,
        bool applyStyle)
    {
        if (applyStyle && useSpiral)
        {
            float sign = spiralClockwise ? 1f : -1f; // flip if your clockwise feels inverted
            direction = (Quaternion.Euler(0f, 0f, sign * spiralSpeed * dt) * (Vector3)direction).normalized;
        }

        currentBulletSpeed += acceleration * dt;
        Vector2 velocity = direction * currentBulletSpeed;
        SetBulletVelocity(rb, velocity);

        if (applyStyle && (useSine || useCosine))
        {
            styleTime += dt;
            float wave = useSine
                ? Mathf.Sin(styleTime * sineFrequency) * sineAmplitude
                : Mathf.Cos(styleTime * cosineFrequency) * cosineAmplitude;

            Vector2 perp = new Vector2(-direction.y, direction.x);
            bulletTransform.position += (Vector3)(perp * wave * dt);
        }

        if (velocity.sqrMagnitude > 0.0001f)
        {
            float look = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            bulletTransform.rotation = Quaternion.Euler(0f, 0f, look);
        }
    }


    // This method smoothly curves the bullet toward the new direction during stops
    // BulletPatternBase.cs
    protected void SetBulletVelocity(Rigidbody2D rb, Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
    rb.velocity = v;
#endif
    }

    // Curves direction only (speed handled elsewhere)
    protected IEnumerator CurveBulletDirection(
        Transform bulletTransform,
        Vector3 oldDirection,
        Vector3 newDirection,
        float currentBulletSpeed,
        Rigidbody2D rb,
        Transform player)
    {
        float t = 0f;

        while (t < curveDuration)
        {
            if (bulletTransform == null || rb == null) yield break;

            t += Time.deltaTime;

            // Update desired direction live during the curve
            if (player != null)
                newDirection = (player.position - bulletTransform.position).normalized;

            Vector3 curved = Vector3.Lerp(oldDirection, newDirection, t / curveDuration).normalized;
            rb.linearVelocity = (Vector2)(curved * currentBulletSpeed);

            yield return null;
        }
    }

    // ✅ Back-compat overload: lets existing code call without state
    protected Vector3 GetInitialBulletDirection(Transform firePoint, Transform player)
    {
        return GetInitialBulletDirection(firePoint, player, null);
    }

    // Coroutine for accelerating the bullet instance over time
    protected IEnumerator AccelerateBullet(Rigidbody2D rb, Vector3 moveDirection, float currentBulletSpeed)
    {
        while (rb != null)
        {
            currentBulletSpeed += acceleration * Time.deltaTime;
            rb.linearVelocity = moveDirection * currentBulletSpeed;

            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            rb.transform.rotation = Quaternion.Euler(0, 0, angle);

            yield return null;
        }
    }

    // Coroutine for sine wave movement
    protected IEnumerator MoveBulletWithSineWave(Transform bulletTransform, Vector3 moveDirection)
    {
        float elapsedTime = 0f;

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;
            float offset = Mathf.Sin(elapsedTime * sineFrequency) * sineAmplitude;
            Vector3 perp = new Vector3(-moveDirection.y, moveDirection.x, 0f);
            bulletTransform.position += perp * offset * Time.deltaTime;
            yield return null;
        }
    }


    // Cosine wave movement coroutine
    protected IEnumerator MoveBulletWithCosineWave(Transform bulletTransform, Vector3 moveDirection)
    {
        float elapsedTime = 0f;

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;
            float offset = Mathf.Cos(elapsedTime * cosineFrequency) * cosineAmplitude;
            Vector3 perp = new Vector3(-moveDirection.y, moveDirection.x, 0f);
            bulletTransform.position += perp * offset * Time.deltaTime;
            yield return null;
        }
    }

    protected IEnumerator SpiralBulletOutwardCombined(
        Transform bulletTransform,
        Vector3 origin,                 // kept for signature compatibility; not used
        Vector3 baseDirection,
        float currentBulletSpeed,
        float spiralSpeedDeg,           // turn rate only
        bool clockwise,
        bool useSine,
        bool useCosine)
    {
        float t = 0f;
        Vector3 dir = baseDirection.normalized;
        Vector3 pos = bulletTransform.position;

        // Keep old clockwise convention if needed:
        // if direction feels reversed in-game, flip this sign.
        float turnSign = clockwise ? 1f : -1f;

        while (bulletTransform != null)
        {
            float dt = Time.deltaTime;
            t += dt;

            // 1) Turn only (no speed change here)
            dir = (Quaternion.Euler(0f, 0f, turnSign * spiralSpeedDeg * dt) * dir).normalized;

            // 2) Linear speed handled ONLY here
            currentBulletSpeed += acceleration * dt;
            pos += dir * currentBulletSpeed * dt;

            // 3) Optional wave offset around current travel tangent
            if (useSine || useCosine)
            {
                float wave = useSine
                    ? Mathf.Sin(t * sineFrequency) * sineAmplitude
                    : Mathf.Cos(t * cosineFrequency) * cosineAmplitude;

                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                pos += perp * wave * dt;
            }

            bulletTransform.position = pos;

            float look = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bulletTransform.rotation = Quaternion.Euler(0f, 0f, look);

            yield return null;
        }
    }


    // Spiral movement coroutine
    protected IEnumerator SpiralBulletOutward(Transform bulletTransform, Vector3 origin, Vector3 directionToPlayer, float currentBulletSpeed, float spiralSpeedDeg, bool clockwise)
    {
        float elapsedTime = 0f;
        float angle = 0f;

        while (bulletTransform != null)
        {
            elapsedTime += Time.deltaTime;
            angle += (clockwise ? 1f : -1f) * spiralSpeedDeg * Time.deltaTime;

            float radius = currentBulletSpeed * elapsedTime;
            Vector3 rotated = Quaternion.Euler(0, 0, angle) * directionToPlayer;
            bulletTransform.position = new Vector3(origin.x + rotated.x * radius, origin.y + rotated.y * radius, bulletTransform.position.z);

            yield return null;
        }
    }

    // New method to fire bullets in a specific direction
    public virtual void FireInDirection(Transform firePoint, Vector3 direction)
    {
        if (firePoint == null || bulletPrefab == null) return;

        Vector3 spawnPosition = new Vector3(
            firePoint.position.x + xOffset,
            firePoint.position.y + yOffset,
            firePoint.position.z
        );

        GameObject bullet = CreateBullet(spawnPosition);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * bulletSpeed;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        Destroy(bullet, bulletLifespan);
    }
}