using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;          // The player to follow
    public Vector2 offset = new Vector2(3f, 2f); // Offset for the camera
    public float followSpeed = 5f;    // Camera smooth speed
    public Vector2 deadZone = new Vector2(2f, 1f); // Allow some freedom before camera follows

    // Boundary limits for camera movement
    public float minX;  // Minimum X position for the camera
    public float maxX;  // Maximum X position for the camera
    public float minY;  // Minimum Y position for the camera
    public float maxY;  // Maximum Y position for the camera

    private Vector2 currentVelocity;

    void Update()
    {
        Vector3 targetPosition = transform.position;

        // Horizontal camera follow with dead zone
        if (Mathf.Abs(player.position.x - transform.position.x) > deadZone.x)
        {
            targetPosition.x = Mathf.SmoothDamp(transform.position.x, player.position.x + offset.x, ref currentVelocity.x, followSpeed * Time.deltaTime);
        }

        // Vertical camera follow with dead zone
        if (Mathf.Abs(player.position.y - transform.position.y) > deadZone.y)
        {
            targetPosition.y = Mathf.SmoothDamp(transform.position.y, player.position.y + offset.y, ref currentVelocity.y, followSpeed * Time.deltaTime);
        }

        // Clamp the target position within the level boundaries
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        // Update the camera's position
        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
    }

    // To visualize the boundary in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(minX, minY, 0), new Vector3(maxX, minY, 0));
        Gizmos.DrawLine(new Vector3(minX, maxY, 0), new Vector3(maxX, maxY, 0));
        Gizmos.DrawLine(new Vector3(minX, minY, 0), new Vector3(minX, maxY, 0));
        Gizmos.DrawLine(new Vector3(maxX, minY, 0), new Vector3(maxX, maxY, 0));
    }
}
