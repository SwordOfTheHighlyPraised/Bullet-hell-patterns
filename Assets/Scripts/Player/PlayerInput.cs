using UnityEngine;
using System.Collections;

[RequireComponent (typeof (Player))]
public class PlayerInput : MonoBehaviour {

	Player player;
	private PlatformEffector2D currentEffector;

	void Start () {
		player = GetComponent<Player> ();
	}

	void Update () {
		Vector2 directionalInput = new Vector2 (Input.GetAxisRaw ("Horizontal"), Input.GetAxisRaw ("Vertical"));
		player.SetDirectionalInput (directionalInput);

		if (Input.GetKeyDown (KeyCode.Space)) {
			player.OnJumpInputDown ();
		}
		if (Input.GetKeyUp (KeyCode.Space)) {
			player.OnJumpInputUp ();
		}

		if (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.Space))
		{
			DropThroughPlatform();
		}
	}

	void DropThroughPlatform()
	{
		// Assuming the player's Collider2D is used for ground detection
		Collider2D playerCollider = player.GetComponent<Collider2D>();

		// Temporarily disable collision between the player and the platform by ignoring the collision
		if (currentEffector != null)
		{
			StartCoroutine(DisableCollisionWithPlatformTemporarily(playerCollider));
		}
	}

	IEnumerator DisableCollisionWithPlatformTemporarily(Collider2D playerCollider)
	{
		// Disable collision
		Physics2D.IgnoreCollision(playerCollider, currentEffector.GetComponent<Collider2D>(), true);

		// Wait for 0.5 seconds or however long you want the player to fall through
		yield return new WaitForSeconds(0.5f);

		// Re-enable the collision
		Physics2D.IgnoreCollision(playerCollider, currentEffector.GetComponent<Collider2D>(), false);
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if (collision.gameObject.CompareTag("OneWayPlatform"))
		{
			currentEffector = collision.gameObject.GetComponent<PlatformEffector2D>();
		}
	}

	private void OnCollisionExit2D(Collision2D collision)
	{
		if (collision.gameObject.CompareTag("OneWayPlatform"))
		{
			currentEffector = null;
		}
	}
}
