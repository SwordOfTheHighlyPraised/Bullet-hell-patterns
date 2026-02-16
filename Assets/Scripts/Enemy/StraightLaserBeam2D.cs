using System.Collections.Generic;
using UnityEngine;

public partial class StraightLaserBeam2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject segmentPrefab;
    [Tooltip("If <= 0, auto-measure from sprite bounds (prefab scale 1).")]
    [SerializeField] private float segmentLength = 0f;
    [SerializeField] private bool trimLastSegment = true;

    [Header("Hitbox")]
    [SerializeField] private BoxCollider2D hitCollider;
    [SerializeField] private float thickness = 0.08f;

    private readonly List<Transform> segments = new();
    private float measuredLocalSegmentLength = -1f;
    private Vector3 baseSegmentScale = Vector3.one;
    private float measuredBaseLocalWidth = -1f;
    private float measuredBaseLocalHeight = -1f;

    [Header("Damage")]
    public int damageAmount = 10;              // Damage dealt by beam
    public float damageTickInterval = 0.15f;   // Damage every X seconds while inside beam
    public bool debugDamageLogs = true;
    public bool filterByTag = true;
    public string targetTag = "Player";

    private readonly Dictionary<Health, float> nextDamageTime = new();

    private void Awake()
    {
        if (visualRoot == null) visualRoot = transform;

        if (hitCollider == null) hitCollider = GetComponent<BoxCollider2D>();
        if (hitCollider == null) hitCollider = gameObject.AddComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
    }

    public void Configure(GameObject segPrefab, float thickness)
    {
        segmentPrefab = segPrefab;
        this.thickness = Mathf.Max(0.001f, thickness);

        if (segmentPrefab != null)
            baseSegmentScale = segmentPrefab.transform.localScale;

        measuredBaseLocalWidth = -1f;
        measuredBaseLocalHeight = -1f;
        measuredLocalSegmentLength = -1f; // keep for compatibility if referenced elsewhere
    }

    public void SetLength(float totalLength)
    {
        totalLength = Mathf.Max(0f, totalLength);
        if (segmentPrefab == null) return;

        EnsureSegments(1);

        Transform probe = segments[0];
        probe.gameObject.SetActive(true);
        probe.localScale = baseSegmentScale;
        probe.localRotation = Quaternion.identity;
        probe.localPosition = Vector3.zero;

        var sr = probe.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Transform root = (visualRoot != null) ? visualRoot : transform;
        Bounds sb = sr.sprite.bounds;

        // Measure segment length along segment LOCAL +X, converted into root-local units
        Vector3 wx0 = sr.transform.TransformPoint(new Vector3(sb.min.x, 0f, 0f));
        Vector3 wx1 = sr.transform.TransformPoint(new Vector3(sb.max.x, 0f, 0f));
        Vector3 lx0 = root.InverseTransformPoint(wx0);
        Vector3 lx1 = root.InverseTransformPoint(wx1);
        float baseW = Vector3.Distance(lx0, lx1);

        // Measure segment thickness along segment LOCAL +Y, converted into root-local units
        Vector3 wy0 = sr.transform.TransformPoint(new Vector3(0f, sb.min.y, 0f));
        Vector3 wy1 = sr.transform.TransformPoint(new Vector3(0f, sb.max.y, 0f));
        Vector3 ly0 = root.InverseTransformPoint(wy0);
        Vector3 ly1 = root.InverseTransformPoint(wy1);
        float baseH = Vector3.Distance(ly0, ly1);

        if (baseW <= 0.0001f || baseH <= 0.0001f) return;

        float segLen = (segmentLength > 0f) ? segmentLength : baseW;
        segLen = Mathf.Max(0.0001f, segLen);

        int count = Mathf.CeilToInt(totalLength / segLen);
        EnsureSegments(count);

        // Supports any sprite pivot (left/center/etc.)
        float pivot01x = (sr.sprite.rect.width > 0f)
            ? sr.sprite.pivot.x / sr.sprite.rect.width
            : 0.5f;

        float cursor = 0f;
        for (int i = 0; i < segments.Count; i++)
        {
            var t = segments[i];
            bool active = i < count;
            t.gameObject.SetActive(active);
            if (!active) continue;

            float thisLen = segLen;
            if (trimLastSegment && i == count - 1)
            {
                float remain = totalLength - cursor;
                thisLen = Mathf.Clamp(remain, 0f, segLen);
            }

            float sx = baseSegmentScale.x * (thisLen / baseW);
            float sy = baseSegmentScale.y * (thickness / baseH);
            t.localScale = new Vector3(sx, sy, 1f);
            t.localRotation = Quaternion.identity;

            // Place so LEFT EDGE starts at cursor, regardless of sprite pivot
            t.localPosition = new Vector3(cursor + pivot01x * thisLen, 0f, 0f);

            float seam = Mathf.Min(0.001f, thisLen * 0.1f);
            cursor += thisLen - seam;
        }

        if (hitCollider != null)
        {
            hitCollider.size = new Vector2(totalLength, thickness);
            hitCollider.offset = new Vector2(totalLength * 0.5f, 0f);
        }
    }


    private bool TryMeasureBaseLocalSize(out float widthLocal, out float heightLocal)
    {
        // Cache if already measured
        if (measuredBaseLocalWidth > 0f && measuredBaseLocalHeight > 0f)
        {
            widthLocal = measuredBaseLocalWidth;
            heightLocal = measuredBaseLocalHeight;
            return true;
        }

        widthLocal = 0f;
        heightLocal = 0f;

        if (segments.Count == 0 || segments[0] == null) return false;

        var t = segments[0];
        t.localScale = baseSegmentScale;
        t.localRotation = Quaternion.identity;
        t.localPosition = Vector3.zero;

        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return false;

        float parentScaleX = Mathf.Abs((visualRoot != null ? visualRoot.lossyScale.x : transform.lossyScale.x));
        float parentScaleY = Mathf.Abs((visualRoot != null ? visualRoot.lossyScale.y : transform.lossyScale.y));
        if (parentScaleX < 0.0001f) parentScaleX = 1f;
        if (parentScaleY < 0.0001f) parentScaleY = 1f;

        // Convert rendered world bounds back to local units
        widthLocal = sr.bounds.size.x / parentScaleX;
        heightLocal = sr.bounds.size.y / parentScaleY;

        if (widthLocal <= 0.0001f || heightLocal <= 0.0001f) return false;

        measuredBaseLocalWidth = widthLocal;
        measuredBaseLocalHeight = heightLocal;
        measuredLocalSegmentLength = widthLocal; // optional compatibility
        return true;
    }

    public void SetSegmentLengthOverride(float value)
    {
        segmentLength = Mathf.Max(0f, value);
        measuredLocalSegmentLength = -1f;
    }

    private float GetSegmentLocalLength()
    {
        if (segmentLength > 0f) return segmentLength; // manual override wins
        if (measuredLocalSegmentLength > 0f) return measuredLocalSegmentLength;

        if (segments.Count == 0) return 0f;

        var sr = segments[0].GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 0f;

        // Measure along sprite LOCAL +X (length axis), not world X.
        Transform root = visualRoot != null ? visualRoot : transform;

        Bounds sb = sr.sprite.bounds; // sprite local-space bounds
        Vector3 p0 = sr.transform.TransformPoint(new Vector3(sb.min.x, 0f, 0f));
        Vector3 p1 = sr.transform.TransformPoint(new Vector3(sb.max.x, 0f, 0f));
        float worldLenAlongLocalX = Vector3.Distance(p0, p1);

        // Convert world length back into this root's local X units.
        float worldPerRootLocalX = root.TransformVector(Vector3.right).magnitude;
        if (worldPerRootLocalX < 0.0001f) worldPerRootLocalX = 1f;

        measuredLocalSegmentLength = worldLenAlongLocalX / worldPerRootLocalX;
        return measuredLocalSegmentLength;
    }

    private void EnsureSegments(int wanted)
    {
        while (segments.Count < wanted)
        {
            var go = Instantiate(segmentPrefab, visualRoot);
            go.name = $"Seg_{segments.Count}";
            segments.Add(go.transform);
        }
    }

    private bool IsValidTarget(Collider2D other)
    {
        if (!filterByTag) return true;
        return other.CompareTag(targetTag) || other.transform.root.CompareTag(targetTag);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidTarget(other)) return;

        // Immediate hit on entry
        TryDamage(other, immediate: true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsValidTarget(other)) return;

        // Tick damage while staying in beam
        TryDamage(other, immediate: false);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health != null)
            nextDamageTime.Remove(health);
    }

    private void OnDisable()
    {
        nextDamageTime.Clear();
    }

    private void TryDamage(Collider2D other, bool immediate)
    {
        // Use InParent because player's collider is often on child objects
        Health healthComponent = other.GetComponentInParent<Health>();
        if (healthComponent == null) return;

        float now = Time.time;

        if (immediate)
        {
            healthComponent.TakeDamage(damageAmount);
            nextDamageTime[healthComponent] = now + damageTickInterval;

            if (debugDamageLogs)
                Debug.Log($"[BeamDamage ENTER] {name} hit {healthComponent.name} for {damageAmount}", this);

            return;
        }

        if (!nextDamageTime.TryGetValue(healthComponent, out float next) || now >= next)
        {
            healthComponent.TakeDamage(damageAmount);
            nextDamageTime[healthComponent] = now + damageTickInterval;

            if (debugDamageLogs)
                Debug.Log($"[BeamDamage TICK] {name} hit {healthComponent.name} for {damageAmount}", this);
        }
    }

}
