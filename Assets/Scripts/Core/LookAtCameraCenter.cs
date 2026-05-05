using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCameraCenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private InputsController _inputsController;
    [SerializeField] private Transform ownerRoot;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private bool rotateOnlyWhileAiming = true;

    [Header("Aim Settings")]
    [SerializeField] private float maxAimDistance = 1000f;
    [SerializeField] private LayerMask aimLayerMask;

    private Quaternion _defaultLocalRotation;
    private Transform _resolvedOwnerRoot;

    private void Awake()
    {
        _defaultLocalRotation = transform.localRotation;

        if(_camera == null)
            _camera = Camera.main;

        if (_inputsController == null)
        {
            _inputsController = GetComponentInParent<InputsController>();
        }

        _resolvedOwnerRoot = ShooterAimUtility.ResolveOwnerRoot(transform, ownerRoot);
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            return;
        }

        if (rotateOnlyWhileAiming && !ShouldRotateToAim())
        {
            ResetToDefaultRotation();
            return;
        }

        Vector3 aimPoint = GetAimPoint();
        TurnedTowards(aimPoint);
    }

    private Vector3 GetAimPoint()
    {
        Ray unusedAimRay;
        return ShooterAimUtility.GetCameraAimPoint(
            _camera,
            maxAimDistance,
            aimLayerMask,
            _resolvedOwnerRoot,
            0f,
            out unusedAimRay);
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

    private bool ShouldRotateToAim()
    {
        return _inputsController != null && _inputsController.IsShooterModeActive;
    }
}
