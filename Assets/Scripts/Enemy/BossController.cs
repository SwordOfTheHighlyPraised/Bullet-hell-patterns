using UnityEngine;

public enum BossState
{
    Idle,
    Attack,
    Retreat
}

public class BossController : MonoBehaviour
{
    public BossState currentState;
    public Transform player;  // The player's position
    public float attackDistance = 10f;  // Distance at which the boss will start attacking
    public float idleDuration = 2f;  // Time the boss stays idle before attacking

    private float idleTimer;
    private BulletSpawnerTuesday bulletSpawner;  // Reference to the bullet spawner

    void Start()
    {
        currentState = BossState.Idle;
        bulletSpawner = GetComponent<BulletSpawnerTuesday>();  // Assuming the BulletSpawner is on the same GameObject
    }

    void Update()
    {
        switch (currentState)
        {
            case BossState.Idle:
                HandleIdleState();
                break;

            case BossState.Attack:
                HandleAttackState();
                break;

            case BossState.Retreat:
                HandleRetreatState();
                break;
        }
    }

    void HandleIdleState()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleDuration && Vector2.Distance(transform.position, player.position) < attackDistance)
        {
            idleTimer = 0f;
            ChangeState(BossState.Attack);  // Switch to attack after idle duration
        }
    }

    void HandleAttackState()
    {
        bulletSpawner.FirePattern();  // Fire bullet patterns
        ChangeState(BossState.Idle);  // After attack, go back to idle for a cooldown
    }

    void HandleRetreatState()
    {
        // Logic for retreat, if needed
    }

    void ChangeState(BossState newState)
    {
        currentState = newState;
    }
}
