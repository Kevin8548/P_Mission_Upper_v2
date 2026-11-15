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

    [Header("Diagnóstico (solo para pruebas)")]
    [Tooltip("Actívalo solo en el NPC que quieres revisar (por ejemplo Gaby). Muestra un texto amarillo al cargar el mapa.")]
    public bool mostrarDiagnostico = false;

    bool playerNear;

    // DIAGNOSTICO: texto en pantalla unos segundos al cargar el mapa (se puede borrar después)
    string diag;
    float diagHasta;

    void Start()
    {
        // DIAGNOSTICO (puedes borrar esta línea cuando todo funcione)
        if (mostrarDiagnostico && victoryLines != null && victoryLines.Length > 0)
        {
            diag = "[NPC " + name + "] ganado=" + EstadoJuego.minijuegoGanado
                 + " | escenaGanada='" + EstadoJuego.minijuegoGanadoEscena
                 + "' | mi sceneToLoad='" + sceneToLoad
                 + "' | storyboard=" + StoryboardFinal.Activo
                 + " | pausa=" + PausaMenu.Pausado;
            diagHasta = Time.time + 10f;
            Debug.Log(diag);
        }

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
            || Normalizar(EstadoJuego.minijuegoGanadoEscena) == Normalizar(sceneToLoad);

        if (!esMiMinijuego)
        {
            Debug.Log("[NPC " + name + "] la victoria fue de '" + EstadoJuego.minijuegoGanadoEscena
                      + "' y mi Scene To Load es '" + sceneToLoad + "': no es mi minijuego");
            return;
        }

        // La victoria se "gasta" hasta que el diálogo de verdad se abre (ver MostrarVictoria)
        playerNear = true;
        StartCoroutine(MostrarVictoria());
    }

    // Compara nombres de escena sin importar mayúsculas/minúsculas ni espacios sobrantes
    // (Unity abre la escena aunque el nombre esté escrito con otra mayúscula, pero un == exacto fallaría)
    static string Normalizar(string n)
    {
        return string.IsNullOrEmpty(n) ? "" : n.Trim().ToLowerInvariant();
    }

    // DialogueManager.StartDialogue NO abre nada (y no avisa) mientras el storyboard o la pausa
    // estén activos. Aquí esperamos a que se liberen (hasta 5 s) y solo "gastamos" la victoria
    // cuando el diálogo ya está abierto, para no perderla.
    System.Collections.IEnumerator MostrarVictoria()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.5f);

        float limite = Time.unscaledTime + 5f;
        while (Time.unscaledTime < limite)
        {
            DialogueManager dm = DialogueManager.Instance;
            if (dm != null && !StoryboardFinal.Activo && !PausaMenu.Pausado)
            {
                dm.StartDialogue(victoryLines);
                if (dm.IsActive)
                {
                    EstadoJuego.minijuegoGanado = false;
                    EstadoJuego.minijuegoGanadoEscena = null;
                    if (diag != null) diag += " | VICTORIA MOSTRADA";
                    yield break;
                }
            }
            yield return null;
        }

        if (diag != null)
            diag += " | NO SE PUDO ABRIR (storyboard=" + StoryboardFinal.Activo + ", pausa=" + PausaMenu.Pausado + ")";
    }

    void OnGUI()
    {
        if (diag == null || Time.time > diagHasta) return;
        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 20;
        st.fontStyle = FontStyle.Bold;
        st.wordWrap = true;
        st.normal.textColor = Color.yellow;
        GUIStyle sombra = new GUIStyle(st);
        sombra.normal.textColor = Color.black;
        GUI.Label(new Rect(11f, 101f, 900f, 200f), diag, sombra);
        GUI.Label(new Rect(10f, 100f, 900f, 200f), diag, st);
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