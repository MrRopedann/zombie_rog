using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
    public LayerMask pushLayers;
    public bool canPush;
    [Range(0.5f, 5f)] public float strength = 1.1f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (canPush) PushRigidBodies(hit);
    }

    private void PushRigidBodies(ControllerColliderHit hit)
    {
        // Убеждаемся, что мы попали в не кинематический Rigidbody
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        // Убеждаемся, что мы воздействуем только на нужные слои
        var bodyLayerMask = 1 << body.gameObject.layer;
        if ((bodyLayerMask & pushLayers.value) == 0) return;

        // Мы не хотим толкать объекты, находящиеся под нами
        if (hit.moveDirection.y < -0.3f) return;

        // Рассчитываем направление толчка из направления движения, только горизонтальное перемещение
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

        // Применяем толчок с учётом силы
        body.AddForce(pushDir * strength, ForceMode.Impulse);
    }
}