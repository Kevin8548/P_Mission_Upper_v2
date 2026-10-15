using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Diálogo de un NPC con la tecla E.
/// Permite iniciar diálogos normales o gestionar el minijuego de cables.
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

    [Header("Minijuego")]
    [Tooltip("Si se deja vacío, lo toma del mismo objeto.")]
    public NPCMinigameLauncher launcher;

    [Tooltip("Si se deja vacío, lo busca en la escena.")]
    public EthernetMinigame minigame;

    [Header("Felicitación al completar el minijuego")]
    [TextArea(2, 5)]
    public string[] congratsLines =
    {
        "¡LO LOGRASTE, LEO! La red de la planta alta vuelve a estar en línea.",
        "Te lo digo en serio: ni los switches se conectan tan rápido como tú.",
        "Así se trabaja en Redes y Ciberseguridad: paciencia, lógica y mucho cable.",
        "Gracias por la ayuda. ¡Sigue así, ya casi recuperas la base de datos!"
    };

    [Header("Diálogo después de completar el minijuego")]
    [Tooltip("Si se deja vacío, repite la felicitación.")]
    [TextArea(2, 5)]
    public string[] afterLines =
    {
        "¡Ya todo funciona gracias a ti! Ve con las demás docentes, que te esperan."
    };

    enum Kind { None, Intro, Congrats, After }

    bool playerNear;
    bool wasActive;
    bool myDialogue;
    bool completed;
    Kind current = Kind.None;

    bool HasMinigame
    {
        get { return launcher != null && minigame != null; }
    }

    void Start()
    {
        // Configuración del minijuego
        if (launcher == null)
            launcher = GetComponent<NPCMinigameLauncher>();

        if (launcher != null)
        {
            if (minigame == null)
                minigame = launcher.minigame;

            if (minigame == null)
                minigame = FindFirstObjectByType<EthernetMinigame>();

            if (launcher.minigame == null)
                launcher.minigame = minigame;
        }

        // Registrar eventos del minijuego
        if (HasMinigame)
        {
            minigame.onCompleted.AddListener(OnMinigameCompleted);
            minigame.onClosed.AddListener(OnMinigameClosed);
            launcher.onDeclined.AddListener(OnDeclined);
        }

        // Diálogo al regresar de ganar el minijuego anterior
        if (EstadoJuego.minijuegoGanado
            && !string.IsNullOrEmpty(sceneToLoad)
            && victoryLines != null
            && victoryLines.Length > 0
            && DialogueManager.Instance != null)
        {
            EstadoJuego.minijuegoGanado = false;
            playerNear = true;
            myDialogue = true;
            current = Kind.After;

            DialogueManager.Instance.StartDialogue(victoryLines);
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

        // Detectar cuándo termina el diálogo
        bool active = DialogueManager.Instance.IsActive;

        if (wasActive && !active)
            OnDialogueEnded();

        wasActive = active;

        if (!playerNear) return;
        if (Keyboard.current == null) return;

        // No aceptar E mientras la pregunta o el minijuego estén activos
        if (HasMinigame &&
            (launcher.IsPromptOpen || minigame.IsPlaying))
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            DialogueManager dm = DialogueManager.Instance;

            if (!dm.IsActive)
            {
                // Si existe un minijuego, gestionar el diálogo correspondiente
                if (HasMinigame)
                {
                    StartTalking();
                }
                else
                {
                    // Mantener el comportamiento anterior para NPC normales
                    dm.StartDialogue(lines, sceneToLoad);
                }
            }
            else if (dm.IsChoosing)
            {
                dm.ConfirmChoice();
            }
            else if (myDialogue)
            {
                dm.Next();
            }
            else
            {
                dm.Next();
            }
        }
    }

    // --------------------------------------------------
    // Diálogos
    // --------------------------------------------------

    void StartTalking()
    {
        string[] toSay = lines;
        current = Kind.Intro;

        if (HasMinigame && completed)
        {
            current = Kind.After;

            toSay = (afterLines != null && afterLines.Length > 0)
                ? afterLines
                : congratsLines;
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
                // Al terminar la introducción, ofrecer iniciar el minijuego
                if (HasMinigame && !completed)
                {
                    DialogueManager.Instance.ShowPrompt(false);
                    launcher.OnDialogueFinished();
                }
                break;

            case Kind.Congrats:
            case Kind.After:
                if (playerNear)
                    DialogueManager.Instance.ShowPrompt(true);
                break;
        }
    }

    // --------------------------------------------------
    // Eventos del minijuego
    // --------------------------------------------------

    void OnMinigameCompleted()
    {
        completed = true;

        if (DialogueManager.Instance == null
            || congratsLines == null
            || congratsLines.Length == 0)
            return;

        current = Kind.Congrats;
        myDialogue = true;

        DialogueManager.Instance.StartDialogue(congratsLines);
    }

    void OnMinigameClosed()
    {
        if (!completed)
            ShowPromptIfNear();
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

    // --------------------------------------------------
    // Detección de proximidad
    // --------------------------------------------------

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