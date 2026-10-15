using UnityEngine;

// Pega esto completo dentro de MinimapClamp.cs
// (la clase se queda como MinimapFollow, igual que en tu proyecto)
[RequireComponent(typeof(Camera))]
public class MinimapFollow : MonoBehaviour
{
    [Header("Seguimiento")]
    public Transform target;                         // arrastra a Leo

    [Header("Límites del mapa (bordes crudos, sin restar nada)")]
    public Vector2 minBounds = new Vector2(4.1f, -100.4f);
    public Vector2 maxBounds = new Vector2(43.9f, 1.4f);

    [Header("Zoom")]
    public float size = 25f;                         // zoom que quieres (más grande = más lejos)
    public bool ajustarAlAncho = true;               // baja el size solo lo necesario para que no sobre nada a los lados

    [Header("Fondo")]
    public Color backgroundColor = Color.black;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;

        // Si el Target está vacío, lo busca por tag Player y luego por nombre Leo
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Leo");
            if (p != null) target = p.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("MinimapFollow: falta el Target. Arrastra a Leo al campo Target.");
            return;
        }

        float mapW = maxBounds.x - minBounds.x;
        float mapH = maxBounds.y - minBounds.y;

        // Calcula el zoom final
        float s = size;
        if (ajustarAlAncho)
        {
            s = Mathf.Min(s, mapW / (2f * cam.aspect));   // que no sea más ancha que el mapa
            s = Mathf.Min(s, mapH / 2f);                  // ni más alta
        }
        cam.orthographicSize = s;

        float halfH = s;
        float halfW = s * cam.aspect;

        Vector3 pos = target.position;
        pos.z = transform.position.z;

        // Sigue a Leo y se detiene en los bordes del mapa
        pos.x = Mathf.Clamp(pos.x, minBounds.x + halfW, maxBounds.x - halfW);
        pos.y = Mathf.Clamp(pos.y, minBounds.y + halfH, maxBounds.y - halfH);

        transform.position = pos;
    }

    // Cuadro verde en la Scene con los límites (selecciona la cámara para verlo)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 c = new Vector3((minBounds.x + maxBounds.x) / 2f, (minBounds.y + maxBounds.y) / 2f, 0f);
        Gizmos.DrawWireCube(c, new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0f));
    }
}