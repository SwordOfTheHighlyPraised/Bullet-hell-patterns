using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public enum BossState
{
    Idle,
    Patrol,
    PlayerDetected,
    AttackPattern1,
    AttackPattern2,
    AttackPattern3,
    AttackPattern4,
    PlayerLost,
    Retreat
}

public class BossController : MonoBehaviour
{
    public BossState currentState;
    public Transform player;  // The player's position
    public float attackDistance = 10f;  // Distance at which the boss will start attacking
    public float detectionDelay = 2f;   // Delay after player is detected before attacking
    public float playerLostDelay = 2f;  // Delay after player is lost before returning to idle/patrol
    public float attackPatternDuration = 5f;  // Duration of each attack pattern
    public bool canPatrol = false;      // Whether the boss can patrol when in idle state

    private float stateTimer;           // Timer to track delays for detection and lost state
    private BulletSpawnerTuesday bulletSpawner;  // Reference to the bullet spawner
    private bool isPlayerInRange = false;  // Tracks if the player is inside the detection zone
    private bool lastPatternWas1 = false;  // Tracks if the last pattern was Pattern 1

    public GameObject summonablePrefab3; // Summonable entity for Attack Pattern 3
    public GameObject summonablePrefab4; // Summonable entity for Attack Pattern 4
    public Transform summonPoint;


    private bool hasSummonedEntityPattern3 = false;  // Flag for pattern 3 summon
    private bool hasSummonedEntityPattern4 = false;  // Flag for pattern 4 summon


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

            case BossState.Patrol:
                HandlePatrolState();
                break;

            case BossState.PlayerDetected:
                HandlePlayerDetectedState();
                break;

            case BossState.AttackPattern1:
                HandleAttackPattern1State();
                break;

            case BossState.AttackPattern2:
                HandleAttackPattern2State();
                break;

            case BossState.AttackPattern3:
                HandleAttackPattern3State();
                break;

            case BossState.AttackPattern4:
                HandleAttackPattern4State();
                break;

            case BossState.PlayerLost:
                HandlePlayerLostState();
                break;

            case BossState.Retreat:
                HandleRetreatState();
                break;
        }
    }

    void HandleIdleState()
    {
        // Idle logic, could include animation, waiting, etc.
        // If the player is detected, change to "PlayerDetected" state
        if (isPlayerInRange)
        {
            stateTimer = detectionDelay;  // Start the detection delay timer
            ChangeState(BossState.PlayerDetected);
        }
    }

    void HandlePatrolState()
    {
        // Add patrol movement logic here if desired
        if (isPlayerInRange)
        {
            stateTimer = detectionDelay;
            ChangeState(BossState.PlayerDetected);
        }
    }

    void HandlePlayerDetectedState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            // Randomly choose either Pattern 1, Pattern 2, Pattern 3 or Pattern 4 for the attack
            int chosenPattern = Random.Range(1, 5);  // Random number between 1 and 4

            // Start with either AttackPattern1, AttackPattern2, or AttackPattern3
            if (chosenPattern == 1)
            {
                lastPatternWas1 = true;
                ChangeState(BossState.AttackPattern1);
            }
            else if (chosenPattern == 2)
            {
                lastPatternWas1 = false;
                ChangeState(BossState.AttackPattern2);
            }
            else
            {
                ChangeState(BossState.AttackPattern3); // Switch to Attack Pattern 3 (summon)
            }

            stateTimer = attackPatternDuration;
        }
    }

    void HandleAttackPattern1State()
    {
        stateTimer -= Time.deltaTime;

        if (bulletSpawner != null && !bulletSpawner.isFiringPattern1)
        {
            bulletSpawner.StartFiringPattern1();  // Start firing pattern 1
        }

        // After the pattern duration, switch to attack pattern 2
        if (stateTimer <= 0f)
        {
            bulletSpawner.StopFiringPattern1();  // Stop Pattern 1
            stateTimer = attackPatternDuration;  // Reset the timer for the second pattern
            ChangeState(BossState.AttackPattern4);  // Switch to the second attack pattern
        }

        // If the player leaves the range, change state to "PlayerLost"
        if (!isPlayerInRange)
        {
            stateTimer = playerLostDelay;
            ChangeState(BossState.PlayerLost);
        }
    }

    void HandleAttackPattern2State()
    {
        stateTimer -= Time.deltaTime;

        if (bulletSpawner != null && !bulletSpawner.isFiringPattern2)
        {
            bulletSpawner.StartFiringPattern2();  // Start firing pattern 2
        }

        // After the pattern duration, switch back to attack pattern 1
        if (stateTimer <= 0f)
        {
            bulletSpawner.StopFiringPattern2();  // Stop Pattern 2
            stateTimer = attackPatternDuration;
            ChangeState(BossState.AttackPattern4);  // Loop back to pattern 1
        }

        // If the player leaves the range, change state to "PlayerLost"
        if (!isPlayerInRange)
        {
            stateTimer = playerLostDelay;
            ChangeState(BossState.PlayerLost);
        }
    }

    // Handle the state for Attack Pattern 3 (summoning one type of entity)
    void HandleAttackPattern3State()
    {
        stateTimer -= Time.deltaTime;

        if (bulletSpawner != null && !bulletSpawner.isFiringPattern3)
        {
            bulletSpawner.StartFiringPattern3();  // Start firing pattern 3

            // Summon the entity only once
            if (!hasSummonedEntityPattern3)
            {
                SummonEntity();
                hasSummonedEntityPattern3 = true;  // Set the flag so it doesn't summon again
            }
        }

        // After the pattern duration, switch back to attack pattern 1
        if (stateTimer <= 0f)
        {
            bulletSpawner.StopFiringPattern3();  // Stop Pattern 3
            stateTimer = attackPatternDuration;
            ChangeState(BossState.AttackPattern1);  // Loop back to pattern 1
            hasSummonedEntityPattern3 = false;  // Reset the flag when switching patterns
        }

        // If the player leaves the range, change state to "PlayerLost"
        if (!isPlayerInRange)
        {
            stateTimer = playerLostDelay;
            ChangeState(BossState.PlayerLost);
            hasSummonedEntityPattern3 = false;  // Reset the flag when switching states
        }
    }

    // Summon entity at a specific point
    void SummonEntity()
    {
        if (summonablePrefab3 != null && summonPoint != null)
        {
            Instantiate(summonablePrefab3, summonPoint.position, Quaternion.identity);
        }
    }

    public void HandleAttackPattern4State()
    {
        stateTimer -= Time.deltaTime;

        if (bulletSpawner != null && !bulletSpawner.isFiringPattern3)
        {
            bulletSpawner.StartFiringPattern3();  // Start firing pattern 3

            // Summon entities only once
            if (!hasSummonedEntityPattern4)
            {
                List<float> initialAngles = new List<float> { 0f, 60f, 120f, 180f, 240f, 300f };  // Example angles for a circle
                SummonEntitiesInCircle(6, initialAngles);  // Spawn 6 summonable entities in a circle
                hasSummonedEntityPattern4 = true;  // Set the flag so it doesn't summon again
            }
        }

        // After the pattern duration, switch back to attack pattern 1
        if (stateTimer <= 0f)
        {
            bulletSpawner.StopFiringPattern3();  // Stop Pattern 3
            stateTimer = attackPatternDuration;
            ChangeState(BossState.AttackPattern1);  // Loop back to pattern 1
            hasSummonedEntityPattern4 = false;  // Reset the flag when switching patterns
        }

        // If the player leaves the range, change state to "PlayerLost"
        if (!isPlayerInRange)
        {
            stateTimer = playerLostDelay;
            ChangeState(BossState.PlayerLost);
            hasSummonedEntityPattern4 = false;  // Reset the flag when switching states
        }
    }

    // Summon entities in a circular formation around the boss or a specific point, with custom initial angles
    void SummonEntitiesInCircle(int numberOfEntities, List<float> initialAngles)
    {
        float radius = 1f;  // Distance from the center (summon point or boss)

        // Ensure we have the correct number of angles provided, or default to evenly spaced angles
        if (initialAngles == null || initialAngles.Count != numberOfEntities)
        {
            // Generate evenly spaced angles if custom angles are not provided or the count doesn't match
            initialAngles = new List<float>();
            float angleStep = 360f / numberOfEntities;  // Angle between each entity
            for (int i = 0; i < numberOfEntities; i++)
            {
                initialAngles.Add(i * angleStep);
            }
        }

        for (int i = 0; i < numberOfEntities; i++)
        {
            // Convert the custom initial angle to radians
            float angle = initialAngles[i] * Mathf.Deg2Rad;

            // Calculate the spawn position based on the angle and radius
            Vector3 spawnPosition = summonPoint.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

            // Instantiate the summonable entity
            GameObject summonedEntity = Instantiate(summonablePrefab4, spawnPosition, Quaternion.identity);

            // Get the SummonableMovement script on the summoned entity
            SummonableMovement summonableMovement = summonedEntity.GetComponent<SummonableMovement>();
            if (summonableMovement != null)
            {
                // Set movement type to Orbit and configure its orbit settings
                summonableMovement.movementType = MovementType.Orbit;
                summonableMovement.orbitSpeed = -18f;  // Set the orbit speed
                summonableMovement.offsetDistance = radius;  // Distance from the target (same as radius)
                summonableMovement.initialAngle = initialAngles[i];  // Set the initial angle
            }
        }
    }




    void HandlePlayerLostState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            bulletSpawner.StopAllFiring();  // Stop all bullet patterns
            ChangeState(canPatrol ? BossState.Patrol : BossState.Idle);  // Return to patrol or idle state
        }
    }

    void HandleRetreatState()
    {
        // Logic for retreat state, if needed
    }

    void ChangeState(BossState newState)
    {
        currentState = newState;
    }

    // Detect when the player enters the detection zone
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
        }
    }
}
