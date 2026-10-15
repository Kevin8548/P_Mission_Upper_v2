using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ponlo en el NPC (Gress_Cable). Cuando termina el diálogo, llama a
/// OnDialogueFinished(): muestra "¿Estás listo?" con botones SÍ / NO.
///  - NO: se cierra el cuadro y Leo vuelve a tener control.
///  - SÍ: se abre el minijuego en pantalla completa.
/// </summary>
public class NPCMinigameLauncher : MonoBehaviour
{
    [Tooltip("El objeto con el script EthernetMinigame. Si se deja vacío lo busca solo en la escena.")]
    public EthernetMinigame minigame;

    [TextArea] public string question = "¿Estás listo para el reto de cableado?";
    public string yesText = "SÍ";
    public string noText = "NO";

    [Tooltip("Tu fuente pixel art de TextMeshPro (opcional).")]
    public TMP_FontAsset font;

    [Tooltip("Se dispara cuando el jugador responde NO.")]
    public UnityEvent onDeclined;

    GameObject promptRoot;
    Button yesButton;

    /// <summary>True mientras el cuadro "¿Estás listo?" está en pantalla.</summary>
    public bool IsPromptOpen { get { return promptRoot != null && promptRoot.activeSelf; } }

    /// <summary>Llamar cuando el último diálogo del NPC termine.</summary>
    public void OnDialogueFinished()
    {
        if (minigame == null) minigame = FindFirstObjectByType<EthernetMinigame>();
        if (minigame == null)
        {
            Debug.LogWarning("NPCMinigameLauncher: no hay ningún EthernetMinigame en la escena.");
            return;
        }

        BuildPrompt();
        minigame.LockWorld();
        promptRoot.SetActive(true);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(yesButton.gameObject);
    }

    void BuildPrompt()
    {
        if (promptRoot != null) return;

        var canvas = PixelUI.MakeCanvas("PreguntaMinijuegoCanvas", 400);
        promptRoot = canvas.gameObject;

        // Caja anclada abajo al centro, estilo cuadro de diálogo
        var holder = PixelUI.MakeRect("Caja", canvas.transform);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0f);
        holder.pivot = new Vector2(0.5f, 0f);
        holder.sizeDelta = new Vector2(1100f, 260f);
        holder.anchoredPosition = new Vector2(0f, 60f);

        PixelUI.MakeImage("Borde", holder, new Vector2(1116f, 276f), Vector2.zero, Color.black, null, true);
        PixelUI.MakeImage("Fondo", holder, new Vector2(1100f, 260f), Vector2.zero,
                          new Color32(38, 46, 66, 255));
        PixelUI.MakeText("Pregunta", holder, question, 44f, new Vector2(0f, 55f),
                         new Vector2(1000f, 120f), TextAlignmentOptions.Center, Color.white, font, true);

        yesButton = PixelUI.MakeButton("BotonSi", holder, yesText, new Vector2(260f, 80f), new Vector2(-200f, -70f),
                                       new Color32(67, 200, 90, 255), font, OnYes, 42f);
        PixelUI.MakeButton("BotonNo", holder, noText, new Vector2(260f, 80f), new Vector2(200f, -70f),
                           new Color32(220, 70, 70, 255), font, OnNo, 42f);

        promptRoot.SetActive(false);
    }

    void OnYes()
    {
        promptRoot.SetActive(false);
        minigame.Begin();
    }

    void OnNo()
    {
        promptRoot.SetActive(false);
        minigame.UnlockWorld();
        onDeclined.Invoke();
    }
}