using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

// Pausa del juego: botón "II" arriba a la derecha o tecla ESC.
// Por ahora solo pausa y reanuda. Se crea solo en cualquier escena que tenga a Leo.
public class PausaMenu : MonoBehaviour
{
    // true mientras el juego está en pausa (y 2 frames después de reanudar,
    // para que la tecla con la que reanudaste no abra un diálogo de un NPC)
    public static bool Pausado { get; private set; }

    public TMP_FontAsset fuente; // opcional; si el MedallasHUD tiene una, usa esa

    static readonly Color Amarillo = new Color(1f, 0.92f, 0.2f);       // igual que el Sí/No
    static readonly Color FondoSel = new Color(1f, 1f, 1f, 0.18f);
    const float EscalaSel = 1.12f;

    GameObject panel, botonPausaGO;
    RectTransform botonPausaRT, botonContinuarRT;
    bool abierto;
    float timeScaleAnterior = 1f;
    bool cursorVisibleAnterior;
    CursorLockMode lockAnterior;

    // ---------------- Creación automática ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        SceneManager.sceneLoaded += (escena, modo) => CrearSiHaceFalta();
        CrearSiHaceFalta();
    }

    static void CrearSiHaceFalta()
    {
        if (FindObjectOfType<PausaMenu>() != null) return;
        if (FindObjectOfType<LeoController>() == null) return;
        new GameObject("PausaMenu").AddComponent<PausaMenu>();
    }

    void Start()
    {
        Construir();
        panel.SetActive(false);
    }

    void OnDestroy()
    {
        // Por si cambia la escena mientras estaba pausado: deja todo como estaba
        if (abierto)
        {
            Time.timeScale = timeScaleAnterior;
            Cursor.visible = cursorVisibleAnterior;
            Cursor.lockState = lockAnterior;
        }
        Pausado = false;
    }

    // ---------------- Lógica ----------------

    bool PuedePausar()
    {
        if (Pausado) return false;
        if (StoryboardFinal.Activo) return false;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return false;
        if (Time.timeScale == 0f) return false; // otra cosa ya congeló el juego
        return true;
    }

    void Update()
    {
        // El botón "II" solo se ve cuando se puede pausar
        botonPausaGO.SetActive(!abierto && PuedePausar());
        if (botonPausaGO.activeSelf) Hover(botonPausaRT, 1f);

        Keyboard k = Keyboard.current;

        if (k != null && k.escapeKey.wasPressedThisFrame)
        {
            if (abierto) Reanudar();
            else if (PuedePausar()) Pausar();
            return;
        }

        if (abierto)
        {
            Hover(botonContinuarRT, EscalaSel);

            // Enter también reanuda (la E se deja libre para no chocar con los diálogos)
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                Reanudar();
        }
    }

    public void Pausar()
    {
        if (abierto || !PuedePausar()) return;

        abierto = true;
        Pausado = true;

        timeScaleAnterior = Time.timeScale;
        Time.timeScale = 0f;

        // Muestra el mouse para poder darle clic a CONTINUAR
        cursorVisibleAnterior = Cursor.visible;
        lockAnterior = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        panel.SetActive(true);
        Click();
    }

    public void Reanudar()
    {
        if (!abierto) return;

        abierto = false;
        panel.SetActive(false);
        Time.timeScale = timeScaleAnterior;
        Cursor.visible = cursorVisibleAnterior;
        Cursor.lockState = lockAnterior;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Click();
        StartCoroutine(SoltarBloqueo());
    }

    IEnumerator SoltarBloqueo()
    {
        yield return null;
        yield return null;
        Pausado = false;
    }

    // Reusa el sonidito de botón del DialogueManager (no se afecta por el timeScale)
    void Click()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.clickClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(dm.clickClip, pos, dm.clickVolume);
    }

    static void Hover(RectTransform rt, float escalaBase)
    {
        bool encima = Mouse.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(rt, Mouse.current.position.ReadValue(), null);
        rt.localScale = Vector3.Lerp(rt.localScale,
            Vector3.one * escalaBase * (encima ? 1.08f : 1f), Time.unscaledDeltaTime * 14f);
    }

    // ---------------- Interfaz construida por código ----------------

    void Construir()
    {
        if (fuente == null)
        {
            MedallasHUD hud = FindObjectOfType<MedallasHUD>();
            if (hud != null) fuente = hud.fuente;
        }

        GameObject cgo = new GameObject("CanvasPausa", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // arriba del HUD de medallas (20), abajo del storyboard (100)
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;

        // --- Botón "II" arriba a la derecha ---
        botonPausaGO = Hijo("BotonPausa", cgo.transform, typeof(Image), typeof(Button));
        botonPausaRT = botonPausaGO.GetComponent<RectTransform>();
        botonPausaRT.anchorMin = botonPausaRT.anchorMax = new Vector2(1f, 1f);
        botonPausaRT.pivot = new Vector2(1f, 0.5f);
        botonPausaRT.anchoredPosition = new Vector2(-40f, -64f);
        botonPausaRT.sizeDelta = new Vector2(90f, 90f);
        botonPausaGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        ConfigurarBoton(botonPausaGO.GetComponent<Button>(), Pausar);
        TextMeshProUGUI tII = NuevoTexto("Texto", botonPausaGO.transform, 52f, Color.white);
        Estirar(tII.rectTransform);
        tII.text = "II";

        // --- Panel de pausa (oscuro translúcido) ---
        panel = Hijo("Panel", cgo.transform, typeof(Image));
        Estirar(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // también bloquea clics al juego

        TextMeshProUGUI titulo = NuevoTexto("Titulo", panel.transform, 120f, Color.white);
        titulo.rectTransform.anchorMin = titulo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        titulo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        titulo.rectTransform.anchoredPosition = new Vector2(0f, 150f);
        titulo.rectTransform.sizeDelta = new Vector2(1200f, 160f);
        titulo.outlineWidth = 0.25f;
        titulo.outlineColor = new Color32(0, 0, 0, 255);
        titulo.text = "PAUSA";

        // --- Botón CONTINUAR (estilo "opción seleccionada": amarillo, fondo clarito, más grande) ---
        GameObject bgo = Hijo("BotonContinuar", panel.transform, typeof(Image), typeof(Button));
        botonContinuarRT = bgo.GetComponent<RectTransform>();
        botonContinuarRT.anchorMin = botonContinuarRT.anchorMax = new Vector2(0.5f, 0.5f);
        botonContinuarRT.pivot = new Vector2(0.5f, 0.5f);
        botonContinuarRT.anchoredPosition = new Vector2(0f, -30f);
        botonContinuarRT.sizeDelta = new Vector2(560f, 110f);
        botonContinuarRT.localScale = Vector3.one * EscalaSel;
        bgo.GetComponent<Image>().color = FondoSel;
        ConfigurarBoton(bgo.GetComponent<Button>(), Reanudar);
        TextMeshProUGUI tc = NuevoTexto("Texto", bgo.transform, 60f, Amarillo);
        Estirar(tc.rectTransform);
        tc.text = "CONTINUAR";

        // --- Ayuda abajo del botón ---
        TextMeshProUGUI hint = NuevoTexto("Hint", panel.transform, 36f, new Color(1f, 1f, 1f, 0.75f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -170f);
        hint.rectTransform.sizeDelta = new Vector2(1200f, 60f);
        hint.text = "[ESC] o [ENTER] para continuar";
    }

    static void ConfigurarBoton(Button b, UnityEngine.Events.UnityAction accion)
    {
        b.transition = Selectable.Transition.None;
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
        b.onClick.AddListener(accion);
    }

    TextMeshProUGUI NuevoTexto(string nombre, Transform padre, float size, Color color)
    {
        GameObject go = Hijo(nombre, padre, typeof(TextMeshProUGUI));
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        if (fuente != null) t.font = fuente;
        return t;
    }

    static GameObject Hijo(string nombre, Transform padre, params System.Type[] comps)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        foreach (System.Type c in comps) go.AddComponent(c);
        go.transform.SetParent(padre, false);
        return go;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}