using UnityEngine;
using UnityEngine.InputSystem;

public class NPCDialogue : MonoBehaviour
{
    [TextArea(2, 5)]
    public string[] lines;

    bool playerNear;

    void Update()
    {
        if (!playerNear) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!DialogueManager.Instance.IsActive)
                DialogueManager.Instance.StartDialogue(lines);
            else
                DialogueManager.Instance.Next();
        }
    }

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