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

    string[] currentLines;
    int index;
    string sceneToLoad;
    int choiceIndex;

    public bool IsActive { get; private set; }
    public bool IsChoosing { get; private set; }

    void Awake()
    {
        Instance = this;
        dialoguePanel.SetActive(false);
        promptPanel.SetActive(false);
        choicePanel.SetActive(false);

        yesButton.onClick.AddListener(OnYes);
        noButton.onClick.AddListener(OnNo);
    }

    void Update()
    {
        if (!IsChoosing) return;

        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)
            SelectChoice(0);
        if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame)
            SelectChoice(1);
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
            SelectChoice(0);
        }
    }

    void SelectChoice(int i)
    {
        choiceIndex = i;
        Button b = (i == 0) ? yesButton : noButton;
        EventSystem.current.SetSelectedGameObject(b.gameObject);
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