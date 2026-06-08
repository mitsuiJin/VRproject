using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class Pistol : MonoBehaviour
{
    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 20f;
    public float bulletLifetime = 5f; // seconds before bullet is destroyed

    [Header("Input")]
    [Tooltip("Enable only for testing controller/XR activation based firing.")]
    public bool allowControllerFire = false;

    [Header("Audio")]
    public AudioClip clip;
    private AudioSource source;

    [Header("Right Controller Haptics")]
    public bool enableFireHaptics = true;
    [Range(0f, 1f)]
    public float fireHapticAmplitude = 1f;
    public float fireHapticDuration = 0.08f;

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
        if (!allowControllerFire)
        {
            return;
        }

        SpawnBullet();
    }

    public void FireFromGlove()
    {
        SpawnBullet();
    }

    private void SpawnBullet()
    {
        // Spawn bullet (90° Y offset compensates for bullet mesh being along X axis)
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("[Pistol] Missing bullet prefab or fire point.");
            return;
        }

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

        if (enableFireHaptics)
        {
            RightControllerHaptics.Pulse(fireHapticAmplitude, fireHapticDuration);
        }

        // Destroy bullet after time
        Destroy(bullet, bulletLifetime);
    }
}

public static class RightControllerHaptics
{
    private static readonly List<InputDevice> rightHandDevices = new List<InputDevice>();
    private static InputDevice cachedRightHandDevice;

    public static bool Pulse(float amplitude, float duration)
    {
        amplitude = Mathf.Clamp01(amplitude);
        duration = Mathf.Max(0f, duration);

        if (amplitude <= 0f || duration <= 0f)
        {
            return false;
        }

        if (!TryGetRightHandDevice(out InputDevice device))
        {
            return false;
        }

        return device.SendHapticImpulse(0u, amplitude, duration);
    }

    private static bool TryGetRightHandDevice(out InputDevice device)
    {
        if (HasImpulseHaptics(cachedRightHandDevice))
        {
            device = cachedRightHandDevice;
            return true;
        }

        rightHandDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);

        foreach (InputDevice candidate in rightHandDevices)
        {
            if (!HasImpulseHaptics(candidate))
            {
                continue;
            }

            cachedRightHandDevice = candidate;
            device = cachedRightHandDevice;
            return true;
        }

        device = default;
        return false;
    }

    private static bool HasImpulseHaptics(InputDevice device)
    {
        return device.isValid
            && device.TryGetHapticCapabilities(out HapticCapabilities capabilities)
            && capabilities.supportsImpulse
            && capabilities.numChannels > 0u;
    }
}
