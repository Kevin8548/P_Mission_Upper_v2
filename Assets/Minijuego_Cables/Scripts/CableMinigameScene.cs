using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Va en la escena "MinijuegoCables", junto al script EthernetMinigame.
/// Abre el minijuego solo al cargar la escena y, al terminar, regresa al mapa:
///  - Si ganas, marca EstadoJuego.minijuegoGanado (y cuál minijuego fue)
///    para que solo el NPC de este minijuego te felicite.
///  - Si sales con la X, regresa sin marcar la victoria.
/// </summary>
public class CableMinigameScene : MonoBehaviour
{
    [Tooltip("Si se deja vacío, lo toma del mismo objeto o lo busca en la escena.")]
    public EthernetMinigame minigame;

    [Tooltip("Nombre exacto de la escena del mapa a la que se regresa.")]
    public string returnScene = "SampleScene";

    bool leaving;

    void Start()
    {
        if (minigame == null) minigame = GetComponent<EthernetMinigame>();
        if (minigame == null) minigame = FindFirstObjectByType<EthernetMinigame>();

        if (minigame == null)
        {
            Debug.LogError("CableMinigameScene: no hay ningún EthernetMinigame en esta escena.");
            return;
        }

        minigame.onCompleted.AddListener(OnWon);
        minigame.onClosed.AddListener(OnClosed);
        minigame.Begin();
    }

    void OnWon()
    {
        EstadoJuego.minijuegoGanado = true;
        // Nombre de esta escena (ej. "MinijuegoCables"); debe coincidir con el
        // "Scene To Load" del NPC que lanza este minijuego.
        EstadoJuego.minijuegoGanadoEscena = SceneManager.GetActiveScene().name;
        Leave();
    }

    void OnClosed()
    {
        Leave();
    }

    void Leave()
    {
        if (leaving) return;
        leaving = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene("SampleScene");
    }
}