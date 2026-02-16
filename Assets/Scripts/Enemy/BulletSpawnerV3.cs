// BulletSpawnerV3.cs
using System;
using UnityEngine;

[Serializable]
public class PatternWithCooldown
{
    public BulletPatternBase bulletPattern;
    public float attackCooldown = 2f;
    public float attackDuration = 1f;
    public int numberOfTimesToFire = 1;
    public float timeBetweenFires = 0.5f;

    [HideInInspector] public float cooldownTimer;
    [HideInInspector] public float activeTimer;
    [HideInInspector] public int currentFireCount;
    [HideInInspector] public float timeBetweenFireTimer;
    [HideInInspector] public bool isPatternActive;
    [HideInInspector] public BulletPatternRuntimeState runtimeState;
}

[Serializable]
public class PatternGroup
{
    public string name;
    public PatternWithCooldown[] patterns;
}

public class BulletSpawnerV3 : MonoBehaviour
{
    [Header("Pattern Groups (any count)")]
    public PatternGroup[] groups;

    [Header("Target")]
    [SerializeField] private Transform defaultTarget;

    private Transform target;
    private bool isFiring;
    private int activeGroupIndex = -1;

    private void Start()
    {
        target = defaultTarget != null ? defaultTarget : GameObject.FindGameObjectWithTag("Player")?.transform;

        // Optional: reset everything on start so editor playmode doesn't keep stale state
        ResetAllGroups(fireImmediately: false);
    }

    private void Update()
    {
        if (!isFiring) return;
        if (activeGroupIndex < 0 || activeGroupIndex >= groups.Length) return;

        var group = groups[activeGroupIndex];
        if (group?.patterns == null) return;

        HandlePatternFiring(group.patterns);
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null) target = newTarget;
    }

    public void PlayGroup(int index, Transform targetOverride = null, bool resetGroup = true, bool fireImmediately = true)
    {
        Debug.Log($"[Spawner] PlayGroup index={index} groups={(groups == null ? 0 : groups.Length)} reset={resetGroup} fireNow={fireImmediately}", this);

        if (index < 0 || groups == null || index >= groups.Length)
        {
            Debug.LogError("[Spawner] Invalid group index or groups not set.", this);
            return;
        }

        if (index < 0 || index >= groups.Length) return;

        if (targetOverride != null) target = targetOverride;

        activeGroupIndex = index;
        isFiring = true;

        if (resetGroup)
            ResetGroup(index, fireImmediately);
    }

    public void Stop(bool resetActiveGroup = false)
    {
        isFiring = false;

        if (resetActiveGroup && activeGroupIndex >= 0 && activeGroupIndex < groups.Length)
            ResetGroup(activeGroupIndex, fireImmediately: false);
    }

    public void StopAll(bool resetAll = false)
    {
        isFiring = false;
        activeGroupIndex = -1;

        if (resetAll)
            ResetAllGroups(fireImmediately: false);
    }

    public void ResetGroup(int index, bool fireImmediately)
    {
        if (index < 0 || index >= groups.Length) return;
        var p = groups[index].patterns;
        if (p == null) return;

        for (int i = 0; i < p.Length; i++)
            ResetPatternRuntime(p[i], fireImmediately);
    }

    public void ResetAllGroups(bool fireImmediately)
    {
        if (groups == null) return;
        for (int g = 0; g < groups.Length; g++)
            ResetGroup(g, fireImmediately);
    }

    private void ResetPatternRuntime(PatternWithCooldown pattern, bool fireImmediately)
    {
        if (pattern == null) return;

        pattern.isPatternActive = false;
        pattern.activeTimer = 0f;
        pattern.currentFireCount = 0;
        pattern.timeBetweenFireTimer = pattern.timeBetweenFires;
        pattern.cooldownTimer = fireImmediately ? 0f : pattern.attackCooldown;

        // NEW: reset spin/fireAngle runtime state back to config
        if (pattern.runtimeState == null)
            pattern.runtimeState = new BulletPatternRuntimeState();

        if (pattern.bulletPattern != null)
            pattern.runtimeState.ResetFromConfig(pattern.bulletPattern);
    }

    private void HandlePatternFiring(PatternWithCooldown[] bulletPatterns)
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < bulletPatterns.Length; i++)
        {
            var p = bulletPatterns[i];
            if (p == null || p.bulletPattern == null) continue;

            // Cooldown phase
            p.cooldownTimer -= dt;
            if (p.cooldownTimer > 0f) continue;

            int shotsTarget = Mathf.Max(1, p.numberOfTimesToFire);
            float interval = Mathf.Max(0.0001f, GetEffectiveTimeBetweenFires(p));
            bool isBurstPattern = p.bulletPattern is BurstPattern;

            // Activate pattern window once
            if (!p.isPatternActive)
            {
                p.isPatternActive = true;
                p.currentFireCount = 0;
                p.timeBetweenFireTimer = 0f; // immediate first shot

                // Ensure active window is long enough to deliver all requested shots
                p.activeTimer = Mathf.Max(p.attackDuration, GetRequiredActiveTime(p));

                if (p.runtimeState == null)
                    p.runtimeState = new BulletPatternRuntimeState();

                p.runtimeState.ResetFromConfig(p.bulletPattern);
            }

            // Active phase
            p.activeTimer -= dt;
            p.timeBetweenFireTimer -= dt;

            // For BurstPattern, fire at most once per frame (prevents hitch catch-up from stacking burst coroutines)
            if (isBurstPattern)
            {
                if (p.timeBetweenFireTimer <= 0f && p.currentFireCount < shotsTarget)
                {
                    p.bulletPattern.Fire(transform, target, this, p.runtimeState);
                    p.currentFireCount++;
                    p.timeBetweenFireTimer += interval;
                }
            }
            else
            {
                // Catch-up loop for non-burst patterns
                while (p.timeBetweenFireTimer <= 0f && p.currentFireCount < shotsTarget)
                {
                    p.bulletPattern.Fire(transform, target, this, p.runtimeState);
                    p.currentFireCount++;
                    p.timeBetweenFireTimer += interval;
                }
            }

            bool finishedByCount = p.currentFireCount >= shotsTarget;
            bool timedOut = p.activeTimer <= 0f;

            if (finishedByCount || timedOut)
            {
                p.isPatternActive = false;
                p.cooldownTimer = p.attackCooldown;

                // Reset runtime state for next activation
                if (p.runtimeState != null && p.bulletPattern != null)
                    p.runtimeState.ResetFromConfig(p.bulletPattern);
            }
        }
    }


    private float GetEffectiveTimeBetweenFires(PatternWithCooldown pattern)
    {
        float gap = Mathf.Max(0f, pattern.timeBetweenFires);

        // BurstPattern needs full burst-sequence spacing to avoid overlap (1-2-1-2 ordering)
        if (pattern?.bulletPattern is BurstPattern burst)
        {
            int arrays = Mathf.Max(1, burst.totalBulletArrays);
            int bulletsPerArray = Mathf.Max(1, burst.numberOfBulletsPerArray);
            int perLoc = Mathf.Max(1, burst.bulletsPerLocation);
            int bursts = Mathf.Max(1, burst.burstCount);

            float perBulletDelay = Mathf.Max(0f, burst.burstFireRate);
            float perBurstDelay = Mathf.Max(0f, burst.burstCountRate);

            int instancesPerBurst = arrays * bulletsPerArray * perLoc;

            // Matches current BurstPattern routine behavior (it yields per bullet and per burst)
            float oneFireDuration =
                bursts * (instancesPerBurst * perBulletDelay + perBurstDelay);

            gap = Mathf.Max(gap, oneFireDuration + 0.01f);
        }

        return gap;
    }

    private float GetRequiredActiveTime(PatternWithCooldown pattern)
    {
        int shots = Mathf.Max(1, pattern.numberOfTimesToFire);
        if (shots <= 1) return 0.01f;

        return (shots - 1) * GetEffectiveTimeBetweenFires(pattern) + 0.01f;
    }

}
