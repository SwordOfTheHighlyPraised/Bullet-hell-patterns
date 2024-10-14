using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeamCollisionHandler : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))  // Assuming the player has the "Player" tag
        {
            Debug.Log("Player has been hit by the beam!");
        }
    }
}