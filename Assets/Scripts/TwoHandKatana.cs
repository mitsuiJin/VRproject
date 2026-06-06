using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 카타나 두 손 잡기 컴포넌트
/// - 한 손만으로는 움직이지 않음 (위치 고정)
/// - 두 손으로 잡아야만 이동/회전 가능
/// - 두 번째 손 방향으로 칼날이 자연스럽게 정렬됨
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class TwoHandKatana : MonoBehaviour
{
    [Header("Attach Points")]
    [Tooltip("첫 번째 손 (손잡이) Attach Transform")]
    public Transform mainHandAttach;
    [Tooltip("두 번째 손 (날 쪽) Attach Transform")]
    public Transform secondHandAttach;

    [Header("Rotation")]
    [Tooltip("두 손 방향 정렬 시 추가 회전 오프셋 (모델 축이 맞지 않을 때 조정)")]
    public Vector3 rotationOffset = Vector3.zero;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    private IXRSelectInteractor _mainInteractor;
    private IXRSelectInteractor _secondInteractor;

    private Vector3 _frozenPosition;
    private Quaternion _frozenRotation;
    private bool _frozen = false;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb   = GetComponent<Rigidbody>();

        _grab.selectEntered.AddListener(OnGrab);
        _grab.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrab);
        _grab.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (_mainInteractor == null)
        {
            _mainInteractor = args.interactorObject;
        }
        else if (_secondInteractor == null)
        {
            _secondInteractor = args.interactorObject;
        }

        UpdateState();
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject == _secondInteractor)
        {
            _secondInteractor = null;
        }
        else if (args.interactorObject == _mainInteractor)
        {
            // 메인 인터렉터가 놓이면 세컨드가 메인으로 승격
            _mainInteractor = _secondInteractor;
            _secondInteractor = null;
        }

        UpdateState();
    }

    private void UpdateState()
    {
        bool twoHanded = _mainInteractor != null && _secondInteractor != null;
        bool oneHanded = _mainInteractor != null && _secondInteractor == null;

        if (oneHanded)
        {
            // 한 손: 위치 고정
            _frozenPosition = transform.position;
            _frozenRotation = transform.rotation;
            _frozen = true;
            _rb.isKinematic = true;
        }
        else if (twoHanded)
        {
            // 두 손: 이동 가능
            _frozen = false;
            _rb.isKinematic = false;
        }
        else
        {
            // 손 없음: 중력 적용
            _frozen = false;
            _rb.isKinematic = false;
        }
    }

    private void LateUpdate()
    {
        // 한 손만 잡고 있을 때 위치 고정
        if (_frozen)
        {
            transform.position = _frozenPosition;
            transform.rotation = _frozenRotation;
            return;
        }

        // 두 손 잡고 있을 때 칼날 방향 정렬
        if (_mainInteractor != null && _secondInteractor != null)
        {
            Vector3 mainPos   = _mainInteractor.GetAttachTransform(_grab).position;
            Vector3 secondPos = _secondInteractor.GetAttachTransform(_grab).position;

            Vector3 direction = (secondPos - mainPos).normalized;
            if (direction == Vector3.zero) return;

            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up)
                                 * Quaternion.Euler(rotationOffset);
            transform.rotation = targetRot;
        }
    }
}
