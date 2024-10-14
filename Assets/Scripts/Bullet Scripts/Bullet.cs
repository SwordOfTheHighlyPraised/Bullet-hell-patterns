using UnityEngine;

public class Bullet : MonoBehaviour
{
    private BulletSpawner spawner;  // Reference to the BulletSpawner
    private Camera mainCamera;      // Reference to the main camera

    // Initialize the bullet with a reference to its spawner
    public void Initialize(BulletSpawner bulletSpawner)
    {
        spawner = bulletSpawner;  // Set the reference to the spawner
        mainCamera = Camera.main; // Set the main camera

        // Debug to check if the spawner is correctly initialized
        if (spawner != null)
        {
            Debug.Log("Spawner successfully assigned to Bullet.");
        }
        else
        {
            Debug.LogError("Spawner is not assigned to the Bullet during initialization.");
        }
    }

    void Start()
    {
        // Ensure the mainCamera is set, in case Initialize was not called
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        // Check if the spawner and mainCamera are set correctly
        if (spawner == null)
        {
            Debug.LogError("Spawner is not assigned to the Bullet. Please check the initialization.");
            return;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main camera is not assigned.");
            return;
        }

        // Check if the bullet is outside the camera's view
        Vector3 screenPos = mainCamera.WorldToViewportPoint(transform.position);

        // If the bullet goes off the screen, destroy it
        if (screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1)
        {
            Destroy(gameObject);        // Destroy the bullet
        }
    }
}
