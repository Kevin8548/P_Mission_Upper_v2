using UnityEngine;
using UnityEngine.InputSystem;

public class LeoController : MonoBehaviour
{
    public float speed = 3f;
    public float runSpeed = 16f;

    Rigidbody2D rb;
    Animator anim;
    Vector2 move;
    Vector2 lastDir = new Vector2(0f, -1f); // empieza mirando hacia abajo
    public Vector2 UltimaDireccion { get { return lastDir; } }
    bool running;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // Si vienes del minijuego, Leo reaparece donde se quedó y mirando hacia el mismo lado
        if (EstadoJuego.tieneGuardado)
        {
            transform.position = EstadoJuego.posicionLeo;
            lastDir = EstadoJuego.direccionLeo;
            EstadoJuego.tieneGuardado = false;
        }
    }

    void Start()
    {
        anim.SetFloat("Horizontal", lastDir.x);
        anim.SetFloat("Vertical", lastDir.y);
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        // Si hay un diálogo abierto, Leo se queda quieto respirando
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive)
        {
            move = Vector2.zero;
            running = false;
            anim.SetBool("IsMoving", false);
            return;
        }

        float x = 0f, y = 0f;

        if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
        if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;

        // Sin diagonales: si hay horizontal, se ignora el vertical
        if (x != 0f) y = 0f;

        move = new Vector2(x, y);
        bool moving = move != Vector2.zero;

        // Shift para correr
        running = moving && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);

        if (moving) lastDir = move;

        anim.SetBool("IsMoving", moving);
        anim.SetFloat("Horizontal", lastDir.x);
        anim.SetFloat("Vertical", lastDir.y);
    }

    void FixedUpdate()
    {
        float currentSpeed = running ? runSpeed : speed;
        rb.linearVelocity = move * currentSpeed;
    }
}