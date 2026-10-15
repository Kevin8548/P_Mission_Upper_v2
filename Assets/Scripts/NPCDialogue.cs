using UnityEngine;
using UnityEngine.InputSystem;

public class NPCDialogue : MonoBehaviour
{
    [TextArea(2, 5)]
    public string[] lines;

    [Tooltip("Nombre exacto de la escena del minijuego. Vacío = sin pregunta Sí/No")]
    public string sceneToLoad;

    [Header("Al regresar de ganar el minijuego (solo para el NPC que tiene minijuego)")]
    [TextArea(2, 5)]
    public string[] victoryLines;

    bool playerNear;

    void Start()
    {
        // Si vienes de ganar el minijuego, este NPC habla de nuevo
        if (EstadoJuego.minijuegoGanado
            && !string.IsNullOrEmpty(sceneToLoad)
            && victoryLines != null && victoryLines.Length > 0
            && DialogueManager.Instance != null)
        {
            EstadoJuego.minijuegoGanado = false;   // se consume para que no se repita
            playerNear = true;                     // Leo reaparece junto a este NPC
            DialogueManager.Instance.StartDialogue(victoryLines); // sin escena => sin Sí/No
        }
    }

    void Update()
    {
        if (!playerNear) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            DialogueManager dm = DialogueManager.Instance;
            if (dm == null) return;

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