using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Diálogo de un NPC con la tecla E.
/// - Si "Scene To Load" tiene un nombre, la última línea muestra la pregunta Sí/No
///   y al decir Sí se carga esa escena (minijuego).
/// - Al regresar de ganar el minijuego, SOLO el NPC cuyo "Scene To Load" coincide
///   con el minijuego ganado dice las "Victory Lines".
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    [Header("Diálogo inicial")]
    [TextArea(2, 5)]
    public string[] lines;

    [Tooltip("Nombre exacto de la escena del minijuego. Vacío = sin pregunta Sí/No")]
    public string sceneToLoad;

    [Header("Al regresar de ganar el minijuego")]
    [TextArea(2, 5)]
    public string[] victoryLines;

    bool playerNear;

    void Start()
    {
        // Diálogo al regresar de ganar el minijuego
        if (!EstadoJuego.minijuegoGanado) return;
        if (string.IsNullOrEmpty(sceneToLoad)) return;
        if (victoryLines == null || victoryLines.Length == 0) return;
        if (DialogueManager.Instance == null) return;

        // Solo reacciona el NPC del minijuego que se ganó.
        // Si EstadoJuego.minijuegoGanadoEscena está vacío, se comporta como antes
        // (compatibilidad por si algún minijuego todavía no la asigna).
        bool esMiMinijuego =
            string.IsNullOrEmpty(EstadoJuego.minijuegoGanadoEscena)
            || EstadoJuego.minijuegoGanadoEscena == sceneToLoad;

        if (!esMiMinijuego) return;

        EstadoJuego.minijuegoGanado = false;
        EstadoJuego.minijuegoGanadoEscena = null;
        playerNear = true;
        DialogueManager.Instance.StartDialogue(victoryLines);
    }

    void Update()
    {
        if (!playerNear) return;
        if (DialogueManager.Instance == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            DialogueManager dm = DialogueManager.Instance;

            if (!dm.IsActive)
                dm.StartDialogue(lines, sceneToLoad);
            else if (dm.IsChoosing)
                dm.ConfirmChoice();
            else
                dm.Next();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.ShowPrompt(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.ShowPrompt(false);
        }
    }
}