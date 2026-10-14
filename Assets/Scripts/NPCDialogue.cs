using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Diálogo de un NPC con la tecla E.
///
/// - Si el NPC NO tiene NPCMinigameLauncher (Eli, Gaby, etc.), funciona igual que antes.
/// - Si SÍ lo tiene (Gress): al terminar el diálogo inicial pregunta "¿Estás listo?";
///   al completar el minijuego te felicita; y si vuelves a hablarle, te dice las
///   líneas de "después de completar".
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    [Header("Diálogo inicial")]
    [TextArea(2, 5)]
    public string[] lines;

    [Header("Minijuego (solo para el NPC que tenga uno)")]
    [Tooltip("Si se deja vacío, lo toma del mismo objeto.")]
    public NPCMinigameLauncher launcher;
    [Tooltip("Si se deja vacío, lo busca en la escena.")]
    public EthernetMinigame minigame;

    [Header("Felicitación (sale justo al completar el minijuego)")]
    [TextArea(2, 5)]
    public string[] congratsLines =
    {
        "¡LO LOGRASTE, LEO! La red de la planta alta vuelve a estar en línea.",
        "Te lo digo en serio: ni los switches se conectan tan rápido como tú.",
        "Así se trabaja en Redes y Ciberseguridad: paciencia, lógica y mucho cable.",
        "Gracias por la ayuda. ¡Sigue así, ya casi recuperas la base de datos!"
    };

    [Header("Si vuelves a hablarle después de completarlo")]
    [Tooltip("Si se deja vacío, repite la felicitación.")]
    [TextArea(2, 5)]
    public string[] afterLines =
    {
        "¡Ya todo funciona gracias a ti! Ve con las demás docentes, que te esperan."
    };

    enum Kind { None, Intro, Congrats, After }

    bool playerNear;
    bool wasActive;
    bool myDialogue;     // true solo si el diálogo activo lo inició ESTE npc
    bool completed;
    Kind current = Kind.None;

    bool HasMinigame { get { return launcher != null && minigame != null; } }

    void Start()
    {
        if (launcher == null) launcher = GetComponent<NPCMinigameLauncher>();

        if (launcher != null)
        {
            if (minigame == null) minigame = launcher.minigame;
            if (minigame == null) minigame = FindFirstObjectByType<EthernetMinigame>();
            if (launcher.minigame == null) launcher.minigame = minigame;
        }

        if (HasMinigame)
        {
            minigame.onCompleted.AddListener(OnMinigameCompleted);
            minigame.onClosed.AddListener(OnMinigameClosed);
            launcher.onDeclined.AddListener(OnDeclined);
        }
    }

    void OnDestroy()
    {
        if (!HasMinigame) return;
        minigame.onCompleted.RemoveListener(OnMinigameCompleted);
        minigame.onClosed.RemoveListener(OnMinigameClosed);
        launcher.onDeclined.RemoveListener(OnDeclined);
    }

    void Update()
    {
        if (DialogueManager.Instance == null) return;

        // Detecta el momento exacto en que termina un diálogo de este NPC
        bool active = DialogueManager.Instance.IsActive;
        if (wasActive && !active) OnDialogueEnded();
        wasActive = active;

        if (!playerNear) return;
        if (Keyboard.current == null) return;

        // Mientras está la pregunta o el minijuego, la E no hace nada
        if (HasMinigame && (launcher.IsPromptOpen || minigame.IsPlaying)) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!DialogueManager.Instance.IsActive)
                StartTalking();
            else if (myDialogue)
                DialogueManager.Instance.Next();
        }
    }

    // ------------------------------------------------------------------

    void StartTalking()
    {
        string[] toSay = lines;
        current = Kind.Intro;

        if (HasMinigame && completed)
        {
            current = Kind.After;
            toSay = (afterLines != null && afterLines.Length > 0) ? afterLines : congratsLines;
        }

        myDialogue = true;
        DialogueManager.Instance.StartDialogue(toSay);
    }

    void OnDialogueEnded()
    {
        if (!myDialogue) return;
        myDialogue = false;

        Kind ended = current;
        current = Kind.None;

        switch (ended)
        {
            case Kind.Intro:
                // Terminó el diálogo inicial -> "¿Estás listo?"
                if (HasMinigame && !completed)
                {
                    DialogueManager.Instance.ShowPrompt(false);
                    launcher.OnDialogueFinished();
                }
                break;

            case Kind.Congrats:
            case Kind.After:
                if (playerNear) DialogueManager.Instance.ShowPrompt(true);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Eventos del minijuego
    // ------------------------------------------------------------------

    void OnMinigameCompleted()
    {
        completed = true;
        if (DialogueManager.Instance == null || congratsLines == null || congratsLines.Length == 0) return;

        current = Kind.Congrats;
        myDialogue = true;
        DialogueManager.Instance.StartDialogue(congratsLines);
    }

    void OnMinigameClosed()
    {
        // Si salió con la X (sin completar), vuelve a aparecer "Presiona E"
        if (!completed) ShowPromptIfNear();
    }

    void OnDeclined()
    {
        ShowPromptIfNear();
    }

    void ShowPromptIfNear()
    {
        if (playerNear && DialogueManager.Instance != null)
            DialogueManager.Instance.ShowPrompt(true);
    }

    // ------------------------------------------------------------------

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
            DialogueManager.Instance.ShowPrompt(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            DialogueManager.Instance.ShowPrompt(false);
        }
    }
}