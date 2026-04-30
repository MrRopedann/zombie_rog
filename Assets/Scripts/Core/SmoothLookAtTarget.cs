using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothLookAtTarget : MonoBehaviour
{
    [Header("Target Setting")]
    [SerializeField]
    private Transform target;
    [SerializeField]
    private bool useMainCameraAsTarget = false;

    [Header("Ratation Setting")]
    [SerializeField]
    private float rotateSpeed = 5f;
    [SerializeField]
    private bool rotateOnliYAxys = false;
    [SerializeField]
    private bool lookAwayFromTarget = false;

    [Header("Look offset")]
    [SerializeField]
    private Vector3 lookOffset = Vector3.zero;

    private Quaternion targetRotate;
    private Vector3 targetDirection;

    private void Start()
    {
        if (useMainCameraAsTarget && Camera.main != null)
        {
            target = Camera.main.transform;
        }
        else
        {
            Debug.LogError("Main camera не найдена");
        }
              
    }

    private void Update()
    {
        if (target == null) return;

        SmoothLookAt();
    }

    private void SmoothLookAt()
    {
        Vector3 targetPos = target.position + lookOffset;
        targetDirection = targetPos - transform.position;

        if (lookAwayFromTarget)
        {
            targetDirection = -targetDirection;
        }

        if (rotateOnliYAxys)
        {
            targetDirection.y = 0;
        }

        if (targetDirection != Vector3.zero)
        {
            targetRotate = Quaternion.LookRotation(targetDirection * -1f);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotate,
                rotateSpeed * Time.deltaTime
                );
        }

    }
}
