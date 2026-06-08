using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DefaultExecutionOrder(-100)]
[ExecuteAlways]
public class PlayerWeaponLoadout : MonoBehaviour
{
    public enum WeaponType
    {
        Pistol,
        Katana
    }

    [Serializable]
    public class WeaponMount
    {
        public GameObject weaponObject;
        public bool useAttachTransform = true;
        public string attachTransformName = "Attach Transform";
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [Header("Selection")]
    public WeaponType startingWeapon = WeaponType.Pistol;

    [Header("Hold Target")]
    public Transform weaponHoldParent;
    public string weaponHoldPointName = "Weapon Hold Point";
    public Transform rightHandVisualRoot;
    public string rightHandVisualName = "Right Hand Model";
    public bool hideRightHandVisual = true;

    [Header("Weapons")]
    public WeaponMount pistol = new WeaponMount
    {
        localPosition = new Vector3(0.04f, -0.05f, 0.12f),
        localEulerAngles = new Vector3(0f, 90f, 0f),
        localScale = Vector3.one
    };

    public WeaponMount katana = new WeaponMount
    {
        localPosition = new Vector3(0f, -0.08f, 0.2f),
        localEulerAngles = new Vector3(90f, 0f, 0f),
        localScale = Vector3.one
    };

    [Tooltip("Scene weapons or props that should never appear on the desk during the demo.")]
    public GameObject[] extraWeaponsToHide;
    public bool hideOtherSceneWeapons = true;

    [Header("Integration")]
    public GloveUDPCommunicator glove;
    public bool sendGloveModeOnStart = true;

    private WeaponMount _activeMount;
    private Pistol _activePistol;
    private bool _runtimeModeCommandSent;

    private void Awake()
    {
        ApplyLoadout(Application.isPlaying);
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            ApplyLoadout(!_runtimeModeCommandSent);
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            ApplyLoadout(false);
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyLoadout(false);
        }
    }

    private void LateUpdate()
    {
        if (_activeMount?.weaponObject == null || weaponHoldParent == null)
        {
            return;
        }

        ApplyHeldPose(_activeMount);
    }

    [ContextMenu("Apply Loadout")]
    public void ApplyLoadout()
    {
        ApplyLoadout(Application.isPlaying);
    }

    private void ApplyLoadout(bool allowGloveModeCommand)
    {
        EnsureSceneReferences();
        ApplyRightHandVisibility();

        if (weaponHoldParent == null)
        {
            Debug.LogWarning("[WeaponLoadout] Weapon hold parent is not assigned.");
            return;
        }

        WeaponMount selected = GetSelectedMount();

        HideWeapon(pistol);
        HideWeapon(katana);
        HideExtraWeapons(selected?.weaponObject);
        HideOtherSceneWeapons(selected?.weaponObject);

        _activeMount = selected;
        _activePistol = null;

        if (selected == null || selected.weaponObject == null)
        {
            Debug.LogWarning("[WeaponLoadout] Selected weapon is not assigned.");
            return;
        }

        selected.weaponObject.SetActive(true);
        selected.weaponObject.transform.SetParent(weaponHoldParent, false);
        ApplyHeldPose(selected);
        LockWeapon(selected.weaponObject);

        _activePistol = selected.weaponObject.GetComponentInChildren<Pistol>(true);
        if (_activePistol != null)
        {
            _activePistol.allowControllerFire = false;
        }

        if (allowGloveModeCommand)
        {
            ApplyGloveMode();
        }
    }

    private WeaponMount GetSelectedMount()
    {
        return startingWeapon == WeaponType.Pistol ? pistol : katana;
    }

    private void EnsureSceneReferences()
    {
        if (weaponHoldParent == null)
        {
            Transform rightHand = FindChildByName(transform, "Right Hand") ?? FindSceneObject("Right Hand")?.transform;
            if (rightHand != null)
            {
                Transform holdPoint = rightHand.Find(weaponHoldPointName);
                weaponHoldParent = holdPoint != null ? holdPoint : rightHand;
            }
        }

        if (rightHandVisualRoot == null)
        {
            rightHandVisualRoot = FindChildByName(transform, rightHandVisualName) ?? FindSceneObject(rightHandVisualName)?.transform;
        }

        if (pistol.weaponObject == null)
        {
            Pistol pistolComponent = FindWeaponComponent<Pistol>("Pistol");
            if (pistolComponent != null)
            {
                pistol.weaponObject = pistolComponent.gameObject;
            }
        }

        if (katana.weaponObject == null)
        {
            Katana katanaComponent = FindWeaponComponent<Katana>("Katana");
            if (katanaComponent != null)
            {
                katana.weaponObject = katanaComponent.gameObject;
            }
        }

        if (glove == null)
        {
            glove = FindSceneComponent<GloveUDPCommunicator>();
        }
    }

    private void HideWeapon(WeaponMount mount)
    {
        if (mount?.weaponObject == null)
        {
            return;
        }

        mount.weaponObject.SetActive(false);
        LockWeapon(mount.weaponObject);
    }

    private void HideExtraWeapons(GameObject selectedWeapon)
    {
        if (extraWeaponsToHide == null)
        {
            return;
        }

        foreach (GameObject weapon in extraWeaponsToHide)
        {
            if (weapon == null)
            {
                continue;
            }

            HideIfNotSelected(weapon, selectedWeapon);
        }
    }

    private void HideOtherSceneWeapons(GameObject selectedWeapon)
    {
        if (!hideOtherSceneWeapons)
        {
            return;
        }

        foreach (Pistol otherPistol in FindSceneComponents<Pistol>())
        {
            HideIfNotSelected(otherPistol.gameObject, selectedWeapon);
        }

        foreach (Katana otherKatana in FindSceneComponents<Katana>())
        {
            HideIfNotSelected(otherKatana.gameObject, selectedWeapon);
        }

        foreach (TwoHandKatana otherKatana in FindSceneComponents<TwoHandKatana>())
        {
            HideIfNotSelected(otherKatana.gameObject, selectedWeapon);
        }

        foreach (GameObject sceneObject in FindSceneObjects())
        {
            string objectName = sceneObject.name;
            if (objectName.IndexOf("Rifle", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Pistol", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Katana", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                HideIfNotSelected(sceneObject, selectedWeapon);
            }
        }
    }

    private void HideIfNotSelected(GameObject weapon, GameObject selectedWeapon)
    {
        if (weapon == null || IsSelectedWeapon(weapon, selectedWeapon))
        {
            return;
        }

        weapon.SetActive(false);
        LockWeapon(weapon);
    }

    private bool IsSelectedWeapon(GameObject candidate, GameObject selectedWeapon)
    {
        if (candidate == null || selectedWeapon == null)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;
        Transform selectedTransform = selectedWeapon.transform;
        return candidate == selectedWeapon
            || candidateTransform.IsChildOf(selectedTransform)
            || selectedTransform.IsChildOf(candidateTransform);
    }

    private void ApplyHeldPose(WeaponMount mount)
    {
        Transform weaponTransform = mount.weaponObject.transform;
        weaponTransform.localScale = SanitizeScale(mount.localScale);

        if (mount.useAttachTransform && TryGetAttachTransform(mount, out Transform attachTransform))
        {
            AlignAttachTransformToHoldPoint(weaponTransform, attachTransform, mount);
            return;
        }

        weaponTransform.localPosition = mount.localPosition;
        weaponTransform.localRotation = Quaternion.Euler(mount.localEulerAngles);
    }

    private Vector3 SanitizeScale(Vector3 scale)
    {
        if (Mathf.Approximately(scale.x, 0f))
        {
            scale.x = 1f;
        }

        if (Mathf.Approximately(scale.y, 0f))
        {
            scale.y = 1f;
        }

        if (Mathf.Approximately(scale.z, 0f))
        {
            scale.z = 1f;
        }

        return scale;
    }

    private bool TryGetAttachTransform(WeaponMount mount, out Transform attachTransform)
    {
        attachTransform = null;

        XRGrabInteractable grab = mount.weaponObject.GetComponentInChildren<XRGrabInteractable>(true);
        if (grab != null && grab.attachTransform != null)
        {
            attachTransform = grab.attachTransform;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(mount.attachTransformName))
        {
            attachTransform = FindChildByName(mount.weaponObject.transform, mount.attachTransformName);
        }

        return attachTransform != null;
    }

    private void AlignAttachTransformToHoldPoint(Transform weaponTransform, Transform attachTransform, WeaponMount mount)
    {
        weaponTransform.localPosition = Vector3.zero;
        weaponTransform.localRotation = Quaternion.identity;

        Quaternion targetAttachRotation = weaponHoldParent.rotation * Quaternion.Euler(mount.localEulerAngles);
        Quaternion rotationDelta = targetAttachRotation * Quaternion.Inverse(attachTransform.rotation);
        weaponTransform.rotation = rotationDelta * weaponTransform.rotation;

        Vector3 targetAttachPosition = weaponHoldParent.TransformPoint(mount.localPosition);
        weaponTransform.position += targetAttachPosition - attachTransform.position;
    }

    private void ApplyRightHandVisibility()
    {
        if (rightHandVisualRoot == null)
        {
            return;
        }

        bool visible = !hideRightHandVisual;
        foreach (Renderer renderer in rightHandVisualRoot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }
    }

    private void LockWeapon(GameObject weapon)
    {
        foreach (XRGrabInteractable grab in weapon.GetComponentsInChildren<XRGrabInteractable>(true))
        {
            grab.enabled = false;
        }

        foreach (Rigidbody rb in weapon.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void ApplyGloveMode()
    {
        if (glove == null)
        {
            return;
        }

        glove.pistol = startingWeapon == WeaponType.Pistol ? _activePistol : null;
        string mode = startingWeapon == WeaponType.Pistol ? "gun" : "sword";
        glove.initialMode = mode;

        if (!sendGloveModeOnStart)
        {
            return;
        }

        _runtimeModeCommandSent = true;
        glove.SendModeCommand(mode);
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject sceneObject in FindSceneObjects())
        {
            if (sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private T FindWeaponComponent<T>(string preferredName) where T : Component
    {
        T fallback = null;

        foreach (T component in FindSceneComponents<T>())
        {
            if (component.name == preferredName)
            {
                return component;
            }

            fallback ??= component;
        }

        return fallback;
    }

    private T FindSceneComponent<T>() where T : Component
    {
        foreach (T component in FindSceneComponents<T>())
        {
            return component;
        }

        return null;
    }

    private T[] FindSceneComponents<T>() where T : Component
    {
        T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        int count = 0;

        foreach (T component in components)
        {
            if (component != null && component.gameObject.scene == gameObject.scene)
            {
                components[count++] = component;
            }
        }

        Array.Resize(ref components, count);
        return components;
    }

    private GameObject[] FindSceneObjects()
    {
        GameObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        int count = 0;

        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.scene == gameObject.scene)
            {
                sceneObjects[count++] = sceneObject;
            }
        }

        Array.Resize(ref sceneObjects, count);
        return sceneObjects;
    }
}
