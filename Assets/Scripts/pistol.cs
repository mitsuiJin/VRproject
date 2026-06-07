using UnityEngine;

public class Pistol : MonoBehaviour
{
    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 20f;
    public float bulletLifetime = 5f; // seconds before bullet is destroyed

    [Header("Audio")]
    public AudioClip clip;
    private AudioSource source;

    private void Start()
    {
        source = GetComponent<AudioSource>();

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
    }

    public void FireBullet()
    {
        // Spawn bullet (90° Y offset compensates for bullet mesh being along X axis)
        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            firePoint.rotation * Quaternion.Euler(0f, 90f, 0f)
        );

        // Apply velocity
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = firePoint.forward * bulletSpeed;
        }

        // Play gun sound
        if (clip != null && source != null)
        {
            source.PlayOneShot(clip);
        }

        // Destroy bullet after time
        Destroy(bullet, bulletLifetime);
    }
}