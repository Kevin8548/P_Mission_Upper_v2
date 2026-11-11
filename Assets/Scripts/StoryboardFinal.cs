using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

// Storyboard final: imágenes a pantalla completa con botón SIGUIENTE (o tecla E).
// En la última dice FINALIZAR; al darle, se cierra y el juego sigue donde estabas.
// Imágenes: Assets/Resources/Storyboard/Storyboard_01.png, _02, _03...
public class StoryboardFinal : MonoBehaviour
{
    // true mientras el storyboard está abierto (el DialogueManager lo usa para no abrir diálogos)
    public static bool Activo { get; private set; }

    const float DuracionFade = 0.5f;
    static readonly Color Dorado = new Color(1f, 0.85f, 0.2f);

    public static void Mostrar(TMP_FontAsset fuente)
    {
        if (Activo) return;
        Activo = true;
        var go = new GameObject("StoryboardFinal");
        go.AddComponent<StoryboardFinal>().fuente = fuente;
    }

    TMP_FontAsset fuente;
    Sprite[] imagenes;
    int total, indice = -1;
    bool ocupado = true;
    float timeScaleAnterior = 1f;

    GameObject canvasGO;
    CanvasGroup grupoRaiz, grupoImagen;
    Image imagen;
    RectTransform botonRT;
    TextMeshProUGUI textoBoton;

    void Start()
    {
        imagenes = Resources.LoadAll<Sprite>("Storyboard").OrderBy(s => s.name).ToArray();
        total = imagenes.Length;

        if (total == 0)
        {
            Debug.LogWarning("[Storyboard] No encontré imágenes en Assets/Resources/Storyboard/ " +
                             "(Texture Type = Sprite (2D and UI)). Cancelo el storyboard.");
            Activo = false;
            Destroy(gameObject);
            return;
        }

        foreach (var s in imagenes) s.texture.filterMode = FilterMode.Point; // pixel art nítido

        timeScaleAnterior = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f; // congela el juego de fondo

        Construir();
        StartCoroutine(Inicio());
    }

    void Update()
    {
        if (botonRT == null) return;

        // Efecto hover del botón con el mouse
        bool hover = Mouse.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(botonRT, Mouse.current.position.ReadValue(), null);
        botonRT.localScale = Vector3.Lerp(botonRT.localScale,
            Vector3.one * (hover ? 1.08f : 1f), Time.unscaledDeltaTime * 14f);

        if (ocupado) return;

        Keyboard k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame) Siguiente();
    }

    void Siguiente()
    {
        if (ocupado) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Click();
        StartCoroutine(Transicion(indice + 1));
    }

    IEnumerator Inicio()
    {
        yield return Fade(grupoRaiz, 1f);
        yield return Transicion(0);
    }

    IEnumerator Transicion(int nuevo)
    {
        ocupado = true;

        // Pasó la última imagen: se desvanece todo y se cierra
        if (nuevo >= total)
        {
            yield return Fade(grupoRaiz, 0f);
            StartCoroutine(Cerrar());
            yield break;
        }

        if (indice >= 0) yield return Fade(grupoImagen, 0f);

        indice = nuevo;
        imagen.sprite = imagenes[indice];
        textoBoton.text = (indice == total - 1) ? "FINALIZAR  [E]" : "SIGUIENTE  [E]";

        yield return Fade(grupoImagen, 1f);
        ocupado = false;
    }

    IEnumerator Fade(CanvasGroup g, float objetivo)
    {
        float desde = g.alpha, t = 0f;
        while (t < DuracionFade)
        {
            t += Time.unscaledDeltaTime; // el juego está en timeScale 0
            g.alpha = Mathf.Lerp(desde, objetivo, t / DuracionFade);
            yield return null;
        }
        g.alpha = objetivo;
    }

    IEnumerator Cerrar()
    {
        canvasGO.SetActive(false);
        Time.timeScale = timeScaleAnterior; // el juego sigue exactamente donde estaba

        // Activo sigue true unos frames para que la E con la que cerraste no abra un diálogo de un NPC
        yield return null;
        yield return null;
        Activo = false;
        Destroy(gameObject);
    }

    void Click()
    {
        if (DialogueManager.Instance == null || DialogueManager.Instance.clickClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(DialogueManager.Instance.clickClip, pos, DialogueManager.Instance.clickVolume);
    }

    // ---------------- Interfaz construida por código ----------------

    void Construir()
    {
        canvasGO = new GameObject("CanvasStoryboard", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // por encima del HUD de medallas
        var cs = canvasGO.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;
        grupoRaiz = canvasGO.GetComponent<CanvasGroup>();
        grupoRaiz.alpha = 0f;

        // Fondo negro que tapa el juego
        var fondo = NuevoHijo("Fondo", canvasGO.transform, typeof(Image));
        fondo.GetComponent<Image>().color = Color.black;
        Estirar(fondo.GetComponent<RectTransform>());

        // Imagen del storyboard (con su propio fade)
        var igo = NuevoHijo("Imagen", canvasGO.transform, typeof(Image), typeof(CanvasGroup));
        imagen = igo.GetComponent<Image>();
        imagen.preserveAspect = true;
        imagen.raycastTarget = false;
        grupoImagen = igo.GetComponent<CanvasGroup>();
        grupoImagen.alpha = 0f;
        Estirar(igo.GetComponent<RectTransform>());

        // Botón SIGUIENTE / FINALIZAR (abajo a la derecha)
        var bgo = NuevoHijo("BotonSiguiente", canvasGO.transform, typeof(Image), typeof(Button));
        botonRT = bgo.GetComponent<RectTransform>();
        botonRT.anchorMin = botonRT.anchorMax = new Vector2(1f, 0f);
        botonRT.pivot = new Vector2(1f, 0f);
        botonRT.anchoredPosition = new Vector2(-50f, 40f);
        botonRT.sizeDelta = new Vector2(440f, 100f);

        var bImg = bgo.GetComponent<Image>();
        bImg.color = new Color(0f, 0f, 0f, 0.7f);

        var boton = bgo.GetComponent<Button>();
        boton.targetGraphic = bImg;
        boton.transition = Selectable.Transition.None;
        var nav = new Navigation(); nav.mode = Navigation.Mode.None;
        boton.navigation = nav;
        boton.onClick.AddListener(Siguiente);

        textoBoton = NuevoTexto("Texto", bgo.transform, 44f);
        textoBoton.color = Dorado;
        Estirar(textoBoton.rectTransform);
    }

    TextMeshProUGUI NuevoTexto(string nombre, Transform padre, float size)
    {
        var go = NuevoHijo(nombre, padre, typeof(TextMeshProUGUI));
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        if (fuente != null) t.font = fuente;
        return t;
    }

    static GameObject NuevoHijo(string nombre, Transform padre, params System.Type[] comps)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        foreach (var c in comps) go.AddComponent(c);
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