using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;         // Aquí se arrastra a Leo
    public float smoothTime = 0.15f; // Más alto = cámara más lenta y suave

    Vector3 velocity;

    void Start()
    {
        // Al empezar (o al volver del minijuego), la cámara ya está sobre Leo
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 destino = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, destino, ref velocity, smoothTime);
    }
}
