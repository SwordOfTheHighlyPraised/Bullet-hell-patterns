// BossAttack.cs
using UnityEngine;

public abstract class BossAttack : MonoBehaviour
{
    public virtual void Begin(BossController boss) { }
    public virtual void Tick(BossController boss, float dt) { }
    public virtual void End(BossController boss, bool reset) { }
}
