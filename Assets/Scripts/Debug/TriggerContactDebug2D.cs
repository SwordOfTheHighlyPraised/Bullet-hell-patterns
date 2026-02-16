using UnityEngine;

[DisallowMultipleComponent]
public class TriggerContactDebug2D : MonoBehaviour
{
    [Header("Filter")]
    public bool filterByTag = true;
    public string requiredTag = "Player";
    public bool acceptRootTag = true;

    [Header("Logging")]
    public bool logEnter = true;
    public bool logExit = true;
    public bool logStay = false;
    public float stayLogInterval = 0.25f;

    [Header("Visual")]
    public bool drawColliderGizmo = true;

    private float nextStayLogTime;

    private bool PassesFilter(Collider2D other)
    {
        if (!filterByTag) return true;
        if (other.CompareTag(requiredTag)) return true;
        if (acceptRootTag && other.transform.root != null && other.transform.root.CompareTag(requiredTag)) return true;
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PassesFilter(other)) return;

        if (logEnter)
            Debug.Log($"[CONTACT ENTER] {name} <- {other.name} (root={other.transform.root.name})", this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!logStay) return;
        if (!PassesFilter(other)) return;

        if (Time.time >= nextStayLogTime)
        {
            nextStayLogTime = Time.time + stayLogInterval;
            Debug.Log($"[CONTACT STAY] {name} <- {other.name}", this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PassesFilter(other)) return;

        if (logExit)
            Debug.Log($"[CONTACT EXIT] {name} <- {other.name}", this);
    }

    private void OnDrawGizmos()
    {
        if (!drawColliderGizmo) return;

        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(col.offset, col.size);
    }
}
