using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCameraCenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private InputsController _inputsController;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private bool rotateOnlyWhileAiming = true;

    [Header("Aim Settings")]
    [SerializeField] private float maxAimDistance = 1000f;
    [SerializeField] private LayerMask aimLayerMask;

    private Quaternion _defaultLocalRotation;

    private void Awake()
    {
        _defaultLocalRotation = transform.localRotation;

        if(_camera == null)
            _camera = Camera.main;

        if (_inputsController == null)
        {
            _inputsController = GetComponentInParent<InputsController>();
        }
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            return;
        }

        if (rotateOnlyWhileAiming && (_inputsController == null || !_inputsController.aim))
        {
            ResetToDefaultRotation();
            return;
        }

        Vector3 aimPoint = GetAimPoint();
        TurnedTowards(aimPoint);
    }

    private Vector3 GetAimPoint()
    {
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask))
        { 
            return hit.point;
        }
        return ray.origin + ray.direction * maxAimDistance;
    }

    private void TurnedTowards(Vector3 targetPoint)
    { 
        Vector3 direction = targetPoint - transform.position;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        if (smoothRotation)
        { 
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    private void ResetToDefaultRotation()
    {
        if (smoothRotation)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                _defaultLocalRotation,
                rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.localRotation = _defaultLocalRotation;
        }
    }
}
