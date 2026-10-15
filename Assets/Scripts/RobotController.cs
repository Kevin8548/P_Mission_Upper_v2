using UnityEngine;
using UnityEngine.InputSystem;

public class RobotController : MonoBehaviour
{
    public float velocidad = 5f;
    public MinijuegoManager manager;
    public Vector2 origenCuadricula;   // lo asigna MazeBuilder

    Rigidbody2D rb;
    Vector2 direccion;
    bool prefiereHorizontal = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null || manager == null || !manager.Jugando)
        {
            direccion = Vector2.zero;
            return;
        }

        bool izq = k.leftArrowKey.isPressed || k.aKey.isPressed;
        bool der = k.rightArrowKey.isPressed || k.dKey.isPressed;
        bool arr = k.upArrowKey.isPressed || k.wKey.isPressed;
        bool aba = k.downArrowKey.isPressed || k.sKey.isPressed;

        float x = (der ? 1f : 0f) - (izq ? 1f : 0f);
        float y = (arr ? 1f : 0f) - (aba ? 1f : 0f);

        // La última tecla que presionas tiene prioridad (sin diagonales)
        bool nuevaH = k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame
                   || k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame;
        bool nuevaV = k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
                   || k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame;
        if (nuevaH) prefiereHorizontal = true;
        if (nuevaV) prefiereHorizontal = false;

        if (x != 0f && y != 0f)
        {
            if (prefiereHorizontal) y = 0f; else x = 0f;
        }

        direccion = new Vector2(x, y);
    }

    void FixedUpdate()
    {
        Vector2 vel = direccion * velocidad;
        Vector2 pos = rb.position;

        // Se alinea al centro del pasillo para que sea fácil girar en las esquinas
        if (direccion.x != 0f)
        {
            float centroY = Mathf.Round(pos.y - origenCuadricula.y) + origenCuadricula.y;
            vel.y = Mathf.Clamp((centroY - pos.y) * 12f, -velocidad, velocidad);
        }
        else if (direccion.y != 0f)
        {
            float centroX = Mathf.Round(pos.x - origenCuadricula.x) + origenCuadricula.x;
            vel.x = Mathf.Clamp((centroX - pos.x) * 12f, -velocidad, velocidad);
        }

        rb.linearVelocity = vel;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (manager == null || !manager.Jugando) return;

        ItemMinijuego item = other.GetComponent<ItemMinijuego>();
        if (item == null || item.usado) return;

        if (item.tipo == TipoItem.Estrella)
        {
            item.usado = true;
            Destroy(other.gameObject);
            manager.SumarEstrella();
        }
        else if (item.tipo == TipoItem.Obstaculo)
        {
            manager.Perder();
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (manager == null || !manager.Jugando) return;

        ItemMinijuego item = other.GetComponent<ItemMinijuego>();
        if (item != null && item.tipo == TipoItem.Salida)
            manager.IntentarGanar();
    }
}
