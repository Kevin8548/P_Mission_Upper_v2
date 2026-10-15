using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public GameObject promptPanel;

    public GameObject choicePanel;
    public Button yesButton;
    public Button noButton;

    [Header("Colores de la elección Sí / No")]
    public Color selectedColor = new Color(1f, 0.92f, 0.2f); // texto amarillo
    public Color normalColor = Color.white;                  // texto blanco

    [Header("Resaltado visual de la opción seleccionada")]
    public Color selectedBgColor = new Color(1f, 1f, 1f, 0.18f); // fondo clarito detrás del botón elegido
    public float selectedScale = 1.12f;                          // el botón elegido crece un poco
    public float animSpeed = 14f;                                // qué tan rápido se anima el cambio

    string[] currentLines;
    int index;
    string sceneToLoad;
    int choiceIndex;

    public bool IsActive { get; private set; }
    public bool IsChoosing { get; private set; }

    void Awake()
    {
        Instance = this;
        Debug.Log("[Dialogo] Script v5 cargado");
        dialoguePanel.SetActive(false);
        promptPanel.SetActive(false);
        choicePanel.SetActive(false);

        // Sin el tinte por defecto de Unity (el blanco/gris al seleccionar)
        yesButton.transition = Selectable.Transition.None;
        noButton.transition = Selectable.Transition.None;

        // La navegación automática de Unity también usa A/D y flechas y se pelea con la nuestra
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        yesButton.navigation = nav;
        noButton.navigation = nav;

        yesButton.onClick.AddListener(OnYes);
        noButton.onClick.AddListener(OnNo);

        // Con el mouse: al pasar encima se selecciona esa opción
        AddHover(yesButton, 0);
        AddHover(noButton, 1);
    }

    void Update()
    {
        Keyboard kk = Keyboard.current;
        if (kk != null && (kk.aKey.wasPressedThisFrame || kk.dKey.wasPressedThisFrame
            || kk.leftArrowKey.wasPressedThisFrame || kk.rightArrowKey.wasPressedThisFrame
            || kk.enterKey.wasPressedThisFrame))
            Debug.Log("[Dialogo] tecla detectada | IsChoosing=" + IsChoosing + " | panelEleccionActivo=" + choicePanel.activeSelf);

        // Animación suave del botón elegido (crece) y del otro (regresa a su tamaño)
        if (choicePanel.activeSelf)
        {
            AnimateScale(yesButton, choiceIndex == 0);
            AnimateScale(noButton, choiceIndex == 1);
        }

        if (!IsChoosing) return;

        Keyboard k = Keyboard.current;
        if (k == null)
        {
            Debug.LogWarning("[Dialogo] Keyboard.current es null: Unity no está recibiendo el teclado");
            return;
        }

        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)
        {
            Debug.Log("[Dialogo] Izquierda -> Sí");
            SelectChoice(0);
        }
        if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame)
        {
            Debug.Log("[Dialogo] Derecha -> No");
            SelectChoice(1);
        }

        // Enter confirma la opción seleccionada (la E también sigue funcionando desde NPCDialogue)
        if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
        {
            Debug.Log("[Dialogo] Enter -> confirmar " + (choiceIndex == 0 ? "Sí" : "No"));
            ConfirmChoice();
        }
    }

    public void ShowPrompt(bool show)
    {
        if (promptPanel == null) return;
        if (IsActive) show = false;
        promptPanel.SetActive(show);
    }

    public void StartDialogue(string[] lines, string scene = "")
    {
        if (lines == null || lines.Length == 0) return;

        currentLines = lines;
        sceneToLoad = scene;
        index = 0;
        IsActive = true;

        promptPanel.SetActive(false);
        dialoguePanel.SetActive(true);
        ShowLine();
    }

    public void Next()
    {
        if (IsChoosing) return;

        index++;

        if (index >= currentLines.Length)
            EndDialogue();
        else
            ShowLine();
    }

    void ShowLine()
    {
        dialogueText.text = currentLines[index];

        bool lastLine = index == currentLines.Length - 1;
        if (lastLine && !string.IsNullOrEmpty(sceneToLoad))
        {
            IsChoosing = true;
            choicePanel.SetActive(true);
            Debug.Log("[Dialogo] Mostrando Sí/No");
            SelectChoice(0);
        }
    }

    void SelectChoice(int i)
    {
        choiceIndex = i;
        Debug.Log("[Dialogo] SelectChoice(" + i + ")");
        Tint(yesButton, i == 0);
        Tint(noButton, i == 1);

        Button b = (i == 0) ? yesButton : noButton;
        EventSystem.current.SetSelectedGameObject(b.gameObject);
    }

    // Resalta el botón seleccionado: texto amarillo, fondo clarito y un poco más grande
    void Tint(Button b, bool selected)
    {
        Color c = selected ? selectedColor : normalColor;

        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null)
        {
            t.color = c;
            t.fontStyle = FontStyles.Bold;
        }
        else
        {
            // Por si el botón todavía trae el Text normal de Unity
            Text lt = b.GetComponentInChildren<Text>();
            if (lt != null) lt.color = c;
        }

        Image bg = b.targetGraphic as Image;
        if (bg != null)
            bg.color = selected ? selectedBgColor : new Color(1f, 1f, 1f, 0f);
    }

    void AnimateScale(Button b, bool selected)
    {
        Vector3 target = selected ? Vector3.one * selectedScale : Vector3.one;
        b.transform.localScale = Vector3.Lerp(b.transform.localScale, target, Time.unscaledDeltaTime * animSpeed);
    }

    // Hace que pasar el mouse por encima seleccione esa opción
    void AddHover(Button b, int i)
    {
        EventTrigger et = b.GetComponent<EventTrigger>();
        if (et == null) et = b.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) =>
        {
            if (!IsChoosing) return;

            // Solo cuenta si el mouse se movió de verdad: un cursor quieto encima de un botón no debe pisar el teclado
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude < 0.01f) return;

            SelectChoice(i);
        });
        et.triggers.Add(entry);
    }

    public void ConfirmChoice()
    {
        if (choiceIndex == 0) OnYes();
        else OnNo();
    }

    void OnYes()
    {
        // Sin escena a cargar (por ejemplo en el diálogo de victoria) no hay nada que iniciar
        if (string.IsNullOrEmpty(sceneToLoad) || !IsChoosing) return;

        IsChoosing = false;
        choicePanel.SetActive(false);

        // Guarda dónde está Leo (y hacia dónde mira) para regresarlo ahí al salir del minijuego
        GameObject leo = GameObject.FindGameObjectWithTag("Player");
        if (leo != null)
        {
            EstadoJuego.posicionLeo = leo.transform.position;
            LeoController lc = leo.GetComponent<LeoController>();
            if (lc != null) EstadoJuego.direccionLeo = lc.UltimaDireccion;
            EstadoJuego.tieneGuardado = true;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    void OnNo()
    {
        EndDialogue();
    }

    void EndDialogue()
    {
        // Suelta la selección de los botones para que Enter/Espacio no los active después
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        IsActive = false;
        IsChoosing = false;
        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);
        promptPanel.SetActive(true);
    }
}