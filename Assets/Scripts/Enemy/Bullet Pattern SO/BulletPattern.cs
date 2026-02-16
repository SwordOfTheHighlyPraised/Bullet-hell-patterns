using UnityEngine;

[CreateAssetMenu(fileName = "NewBulletPattern", menuName = "Bullet Pattern")]
public class BulletPattern : ScriptableObject
{
    public float bulletSpeed;
    public int numberOfBullets;
    public float spreadAngle;
    public GameObject bulletPrefab;

    public virtual void Fire(Transform origin)
    {
        // Default firing logic
        for (int i = 0; i < numberOfBullets; i++)
        {
            float angle = spreadAngle / numberOfBullets * i;
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            GameObject bullet = Instantiate(bulletPrefab, origin.position, rotation);
            bullet.GetComponent<Rigidbody2D>().linearVelocity = bullet.transform.up * bulletSpeed;
        }
    }
}
