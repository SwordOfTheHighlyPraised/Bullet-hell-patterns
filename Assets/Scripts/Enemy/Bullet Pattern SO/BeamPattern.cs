using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "BeamPattern", menuName = "Bullet Patterns/Beam")]
public class BeamPattern : BulletPatternBase
{
    [Header("Beam Settings")]
    public GameObject beamTelegraphPrefab;    // Prefab for the 2D telegraph sprite (rectangular)
    public GameObject beamPrefab;             // Prefab for the actual beam
    public float telegraphDuration = 1f;      // How long the telegraph beam stays active
    public float beamDuration = 2f;           // How long the actual beam stays active
    public float beamWidth = 1f;              // The width of the beam
    public float beamRange = 10f;             // How far the beam extends
    public float yOffsetBeam = 0.5f;          // Y-offset for the beam position
    public float growthDuration = 0.5f;       // Time it takes for the beam to grow to full length

    [Header("Pivot Settings")]
    public Vector3 pivotOffset = new Vector3(-0.5f, 0f, 0f);  // Pivot offset for spinning and beam origin

    public override void Fire(Transform firePoint, Transform player = null)
    {
        // Start the beam firing sequence
        MonoBehaviour monoBehaviour = firePoint.GetComponent<MonoBehaviour>();
        if (monoBehaviour != null)
        {
            monoBehaviour.StartCoroutine(FireBeamSequence(firePoint));
        }
    }

    private IEnumerator FireBeamSequence(Transform firePoint)
    {
        // Store the original fireAngle so we can reset it at the end
        float originalFireAngle = fireAngle;

        // Calculate beam start (adjust with yOffset for vertical alignment and pivot offset)
        Vector3 beamStart = firePoint.position + new Vector3(xOffset, yOffsetBeam, 0f) + pivotOffset;

        // Calculate beam direction based on fireAngle
        Vector3 beamDirection = Quaternion.Euler(0, 0, fireAngle) * Vector3.right;

        // Step 1: Instantiate telegraph as a 2D square sprite (thin rectangle)
        GameObject telegraph = Instantiate(beamTelegraphPrefab, beamStart, Quaternion.identity);
        SpriteRenderer telegraphSprite = telegraph.GetComponent<SpriteRenderer>();
        if (telegraphSprite != null)
        {
            // Set the telegraph sprite's scale to be thin (0.1) and match the length of the beam
            telegraphSprite.transform.localScale = new Vector3(beamRange, 0.1f, 1f); // Adjust thickness here
            telegraphSprite.transform.position = beamStart;
            telegraphSprite.transform.rotation = Quaternion.Euler(0, 0, fireAngle); // Align with fireAngle
        }

        // Wait for the telegraph to be displayed for the configured duration
        yield return new WaitForSeconds(telegraphDuration);

        // Step 2: Destroy the telegraph and instantiate the actual beam
        Destroy(telegraph);

        GameObject beam = Instantiate(beamPrefab, beamStart, Quaternion.identity);
        SpriteRenderer beamSprite = beam.GetComponent<SpriteRenderer>();
        BoxCollider2D beamCollider = beam.GetComponent<BoxCollider2D>();

        // Set the rotation based on fireAngle and apply the pivot offset
        beam.transform.rotation = Quaternion.Euler(0, 0, fireAngle);
        beam.transform.position = beamStart;  // Start position is the same as the telegraph

        // Ensure the beam grows rightward only
        beam.transform.localScale = new Vector3(0, beamWidth, 1);  // Start with zero length

        if (beamCollider != null)
        {
            // Set the initial size of the BoxCollider2D to zero width
            beamCollider.size = new Vector2(0, beamWidth);
            beamCollider.offset = new Vector2(0, 0);  // Adjust the offset to match the growth
        }

        // Step 3: Grow the actual beam over the growth duration
        float growthTimer = 0f;
        while (growthTimer < growthDuration)
        {
            growthTimer += Time.deltaTime;
            float growthProgress = Mathf.Clamp01(growthTimer / growthDuration);  // Progress of growth (0 to 1)
            float currentLength = beamRange * growthProgress;

            // Update the actual beam size and position
            beam.transform.localScale = new Vector3(currentLength, beamWidth, 1);

            // Update the BoxCollider2D to match the beam's growth
            if (beamCollider != null)
            {
                beamCollider.size = new Vector2(currentLength, beamWidth);
                beamCollider.offset = new Vector2(currentLength / 2, 0);  // Keep the offset centered as it grows
            }

            yield return null;
        }

        // Step 4: Allow the beam to move/spin during its active duration
        float elapsedTime = 0f;
        while (elapsedTime < beamDuration)
        {
            elapsedTime += Time.deltaTime;

            // If spin is enabled, update fireAngle dynamically
            if (enableSpin)
            {
                // Update fireAngle based on current spin speed
                fireAngle += currentSpinSpeed * Time.deltaTime;

                // Clamp the spin speed to the maximum spin speed
                currentSpinSpeed += spinSpeedChangeRate * Time.deltaTime;
                currentSpinSpeed = Mathf.Clamp(currentSpinSpeed, -maxSpinSpeed, maxSpinSpeed);

                // If spin reversal is enabled, reverse spin direction when reaching max speed
                if (spinReversal && Mathf.Abs(currentSpinSpeed) >= maxSpinSpeed)
                {
                    spinSpeedChangeRate = -spinSpeedChangeRate;
                }

                // Normalize the fireAngle to stay within 0-359 degrees
                fireAngle = fireAngle % 360f;
                if (fireAngle < 0f) fireAngle += 360f;
            }

            // Update the beam's rotation to match the new fireAngle and apply the pivot offset
            beam.transform.rotation = Quaternion.Euler(0, 0, fireAngle);
            beam.transform.position = firePoint.position + new Vector3(xOffset, yOffsetBeam, 0f) + pivotOffset;

            yield return null;  // Wait for the next frame to continue the update
        }

        // Destroy the actual beam after it has been displayed for the configured duration
        Destroy(beam);

        // Reset the fireAngle back to its original value
        fireAngle = originalFireAngle;
    }
}
