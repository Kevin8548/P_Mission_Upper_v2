using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public GameObject promptPanel;

    string[] currentLines;
    int index;

    public bool IsActive { get; private set; }

    void Awake()
    {
        Instance = this;
        dialoguePanel.SetActive(false);
        promptPanel.SetActive(false);
    }

    public void ShowPrompt(bool show)
    {
        if (IsActive) show = false;
        promptPanel.SetActive(show);
    }

    public void StartDialogue(string[] lines)
    {
        if (lines == null || lines.Length == 0) return;

        currentLines = lines;
        index = 0;
        IsActive = true;

        promptPanel.SetActive(false);
        dialoguePanel.SetActive(true);
        dialogueText.text = currentLines[index];
    }

    public void Next()
    {
        index++;

        if (index >= currentLines.Length)
        {
            EndDialogue();
        }
        else
        {
            dialogueText.text = currentLines[index];
        }
    }

    void EndDialogue()
    {
        IsActive = false;
        dialoguePanel.SetActive(false);
        promptPanel.SetActive(true); // Leo sigue cerca del profesor
    }
}