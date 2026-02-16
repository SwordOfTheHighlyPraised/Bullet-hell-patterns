using UnityEngine;

public class BossBulletAttack : BossAttack
{
    [SerializeField] private BulletSpawnerV3 spawner;

    // Runtime-configured per AttackEntry
    private int groupIndex = 0;
    private bool resetOnBegin = true;
    private bool fireImmediately = true;
    private bool resetOnEnd = true;

    public void Configure(int groupIndex, bool resetOnBegin, bool fireImmediately, bool resetOnEnd)
    {
        this.groupIndex = groupIndex;
        this.resetOnBegin = resetOnBegin;
        this.fireImmediately = fireImmediately;
        this.resetOnEnd = resetOnEnd;
    }

    public override void Begin(BossController boss)
    {
        if (spawner == null) spawner = GetComponent<BulletSpawnerV3>();
        if (spawner == null) return;

        spawner.PlayGroup(groupIndex, boss.PlayerTransform, resetOnBegin, fireImmediately);
    }

    public override void End(BossController boss, bool reset)
    {
        if (spawner == null) return;

        // If reset==true (Lost/Alert transitions), you typically DO want to reset.
        bool doReset = reset && resetOnEnd;
        spawner.Stop(resetActiveGroup: doReset);
    }
}
