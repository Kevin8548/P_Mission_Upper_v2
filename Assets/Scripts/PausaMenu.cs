using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

// Pausa del juego: tecla ESC, tecla ESPACIO o botón de pausa (II) arriba a la derecha.
// Opciones: CONTINUAR y REINICIAR (con confirmación).
// Se crea solo en cualquier escena que tenga a Leo.
public class PausaMenu : MonoBehaviour
{
    // true mientras el juego está en pausa (y 2 frames después de reanudar,
    // para que la tecla con la que reanudaste no abra un diálogo de un NPC)
    public static bool Pausado { get; private set; }

    public TMP_FontAsset fuente; // opcional; si el MedallasHUD tiene una, usa esa

    // A dónde va REINICIAR. Vacío = vuelve a cargar la escena actual.
    const string EscenaAlReiniciar = "";

    // ---- BOTÓN DE PAUSA (las dos barras "II", arriba a la derecha) ----
    const float BotonDistDerecha = 40f;   // distancia al borde derecho
    const float BotonDistArriba = 64f;    // distancia al borde de arriba (centro de las barras)
    const float BarraAncho = 11f;
    const float BarraAlto = 42f;
    const float BarraSeparacion = 26f;


    static readonly Color Oro = new Color(0.925f, 0.784f, 0.431f);
    static readonly Color Blanco = new Color(0.94f, 0.94f, 0.97f);
    static readonly Color Apagado = new Color(0.55f, 0.55f, 0.59f);
    static readonly Color MedallaApagada = new Color(0.22f, 0.22f, 0.27f, 1f);
    const string HintNormal = "ESC o ESPACIO para continuar";
    const string HintConfirmar = "Se borran tus medallas    ENTER para confirmar";
    const float DuracionFade = 0.2f;

    GameObject panel, botonPausaGO;
    CanvasGroup panelCG;
    RectTransform botonPausaRT;
    Image botonBarra1, botonBarra2;
    RectTransform[] botonesRT = new RectTransform[2];
    TextMeshProUGUI[] botonesTxt = new TextMeshProUGUI[2];
    Image[] medallasImg = new Image[4];
    TextMeshProUGUI hint;
    string[] etiquetas = { "CONTINUAR", "REINICIAR" };

    bool abierto;
    int seleccion;
    bool confirmando;
    float fade;
    bool dialogoPrevio;
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
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;

        // Si hace un instante había un diálogo abierto, esa misma tecla pudo haberlo
        // cerrado: no la usamos para pausar.
        bool dialogoAhora = DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
        bool recienDialogo = dialogoPrevio;
        dialogoPrevio = dialogoAhora;

        // El botón de pausa solo se ve cuando se puede pausar
        botonPausaGO.SetActive(!abierto && PuedePausar());
        if (botonPausaGO.activeSelf)
        {
            Hover(botonPausaRT, 1f);
            Color cb = Sobre(botonPausaRT) ? Oro : Blanco;
            botonBarra1.color = cb;
            botonBarra2.color = cb;
        }

        bool teclaPausa = k != null &&
            (k.escapeKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);

        // ----- Juego corriendo -----
        if (!abierto)
        {
            if (teclaPausa && !recienDialogo && PuedePausar()) { Pausar(); return; }
            if (botonPausaGO.activeSelf && Clic(botonPausaRT)) Pausar();
            return;
        }

        // ----- Menú de pausa abierto -----
        fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / DuracionFade);
        panelCG.alpha = fade;

        if (teclaPausa) { Reanudar(); return; }

        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Seleccionar(seleccion - 1);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Seleccionar(seleccion + 1);
        }

        // El mouse solo cambia la selección cuando se mueve (para no pelear con el teclado)
        if (m != null && m.delta.ReadValue() != Vector2.zero)
            for (int i = 0; i < 2; i++)
                if (Sobre(botonesRT[i])) Seleccionar(i);

        for (int i = 0; i < 2; i++)
            if (Clic(botonesRT[i])) { Seleccionar(i); Activar(i); return; }

        if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
        {
            Activar(seleccion);
            return;
        }

        // Estilo: la opción elegida va en dorado y con flechitas; la otra, apagada
        for (int i = 0; i < 2; i++)
        {
            bool sel = (i == seleccion);
            string txt = sel ? "> " + etiquetas[i] + " <" : etiquetas[i];
            if (botonesTxt[i].text != txt) botonesTxt[i].text = txt;
            botonesTxt[i].color = sel ? Oro : Apagado;
            botonesRT[i].localScale = Vector3.Lerp(botonesRT[i].localScale,
                Vector3.one * (sel ? 1.05f : 1f), Time.unscaledDeltaTime * 14f);
        }
    }

    void Seleccionar(int i)
    {
        int nuevo = (i + 2) % 2;
        if (nuevo == seleccion) return;
        seleccion = nuevo;
        CancelarConfirmacion();
        Click();
    }

    void Activar(int i)
    {
        if (i == 0) { Reanudar(); return; }

        // REINICIAR: la primera vez pide confirmación
        if (!confirmando)
        {
            confirmando = true;
            etiquetas[1] = "¿SEGURO?";
            hint.text = HintConfirmar;
            Click();
            return;
        }
        Reiniciar();
    }

    void CancelarConfirmacion()
    {
        confirmando = false;
        etiquetas[1] = "REINICIAR";
        if (hint != null) hint.text = HintNormal;
    }

    void ActualizarMedallas()
    {
        for (int i = 0; i < medallasImg.Length; i++)
            medallasImg[i].color = Medallas.Tiene(Medallas.Todas[i]) ? Color.white : MedallaApagada;
    }

    public void Pausar()
    {
        if (abierto || !PuedePausar()) return;

        abierto = true;
        Pausado = true;
        seleccion = 0;
        CancelarConfirmacion();
        ActualizarMedallas();

        timeScaleAnterior = Time.timeScale;
        Time.timeScale = 0f;

        // Muestra el mouse para poder darle clic a las opciones
        cursorVisibleAnterior = Cursor.visible;
        lockAnterior = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        fade = 0f;
        panelCG.alpha = 0f;
        panel.SetActive(true);
        Click();
    }

    public void Reanudar()
    {
        if (!abierto) return;

        abierto = false;
        CancelarConfirmacion();
        panel.SetActive(false);
        Time.timeScale = timeScaleAnterior;
        Cursor.visible = cursorVisibleAnterior;
        Cursor.lockState = lockAnterior;

        Click();
        StartCoroutine(SoltarBloqueo());
    }

    void Reiniciar()
    {
        Medallas.Reiniciar();

        // Dejamos todo normal antes de cambiar de escena
        Time.timeScale = 1f;
        Cursor.visible = cursorVisibleAnterior;
        Cursor.lockState = lockAnterior;
        abierto = false;
        Pausado = false;

        string destino = string.IsNullOrEmpty(EscenaAlReiniciar)
            ? SceneManager.GetActiveScene().name
            : EscenaAlReiniciar;
        SceneManager.LoadScene(destino);
    }

    IEnumerator SoltarBloqueo()
    {
        yield return null;
        yield return null;
        Pausado = false;
    }

    // Reusa el sonidito de botón del DialogueManager
    void Click()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.clickClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(dm.clickClip, pos, dm.clickVolume);
    }

    // ---------------- Mouse (sin depender del EventSystem) ----------------

    static bool Sobre(RectTransform rt)
    {
        return Mouse.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(rt, Mouse.current.position.ReadValue(), null);
    }

    static bool Clic(RectTransform rt)
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Sobre(rt);
    }

    static void Hover(RectTransform rt, float escalaBase)
    {
        rt.localScale = Vector3.Lerp(rt.localScale,
            Vector3.one * escalaBase * (Sobre(rt) ? 1.08f : 1f), Time.unscaledDeltaTime * 14f);
    }

    // ---------------- Interfaz construida por código ----------------

    void Construir()
    {
        if (fuente == null)
        {
            MedallasHUD hud = FindObjectOfType<MedallasHUD>();
            if (hud != null) fuente = hud.fuente;
        }

        GameObject cgo = new GameObject("CanvasPausa", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // arriba del HUD de medallas (20), abajo del storyboard (100)
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;

        // --- Botón de pausa: dos barras gruesas "II" arriba a la derecha ---
        botonPausaGO = Hijo("BotonPausa", cgo.transform);
        botonPausaRT = botonPausaGO.GetComponent<RectTransform>();
        botonPausaRT.anchorMin = botonPausaRT.anchorMax = new Vector2(1f, 1f);
        botonPausaRT.pivot = new Vector2(1f, 0.5f);
        botonPausaRT.anchoredPosition = new Vector2(-BotonDistDerecha, -BotonDistArriba);
        botonPausaRT.sizeDelta = new Vector2(90f, 90f);

        botonBarra1 = NuevaBarra(botonPausaGO.transform, -BarraSeparacion / 2f);
        botonBarra2 = NuevaBarra(botonPausaGO.transform, BarraSeparacion / 2f);

        // --- Fondo oscuro (con fundido) ---
        panel = Hijo("Panel", cgo.transform, typeof(Image), typeof(CanvasGroup));
        Estirar(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.94f);
        panelCG = panel.GetComponent<CanvasGroup>();

        // Bordes más oscuros (viñeteado)
        GameObject vin = Hijo("Vineta", panel.transform, typeof(Image));
        Estirar(vin.GetComponent<RectTransform>());
        Image vi = vin.GetComponent<Image>();
        vi.sprite = CrearVineta();
        vi.raycastTarget = false;

        // --- Título con sombra ---
        TextMeshProUGUI sombra = NuevoTexto("TituloSombra", panel.transform, 110f, new Color(0.04f, 0.05f, 0.12f, 1f));
        Centrar(sombra.rectTransform, new Vector2(6f, 184f), new Vector2(1000f, 160f));
        sombra.text = "PAUSA";
        TextMeshProUGUI titulo = NuevoTexto("Titulo", panel.transform, 110f, Blanco);
        Centrar(titulo.rectTransform, new Vector2(0f, 190f), new Vector2(1000f, 160f));
        titulo.text = "PAUSA";

        GameObject linea = Hijo("Linea", panel.transform, typeof(Image));
        Centrar(linea.GetComponent<RectTransform>(), new Vector2(0f, 125f), new Vector2(240f, 6f));
        linea.GetComponent<Image>().color = Oro;

        // --- Las 4 medallas (a color las ganadas, apagadas las que faltan) ---
        for (int i = 0; i < 4; i++)
        {
            GameObject mg = Hijo("Medalla" + i, panel.transform, typeof(Image));
            Image mi = mg.GetComponent<Image>();
            mi.sprite = Medallas.SpriteDe(Medallas.Todas[i]);
            mi.preserveAspect = true;
            mi.raycastTarget = false;
            Centrar(mg.GetComponent<RectTransform>(), new Vector2((i - 1.5f) * 150f, 20f), new Vector2(99f, 160f));
            medallasImg[i] = mi;
        }

        // --- Opciones ---
        CrearOpcion(0, 58f, -135f);
        CrearOpcion(1, 54f, -230f);

        // --- Ayuda abajo ---
        hint = NuevoTexto("Hint", panel.transform, 28f, new Color(1f, 1f, 1f, 0.45f));
        hint.fontStyle = FontStyles.Normal;
        Centrar(hint.rectTransform, new Vector2(0f, -480f), new Vector2(1400f, 50f));
        hint.text = HintNormal;
    }

    void CrearOpcion(int indice, float size, float y)
    {
        TextMeshProUGUI t = NuevoTexto("Opcion" + indice, panel.transform, size, Apagado);
        Centrar(t.rectTransform, new Vector2(0f, y), new Vector2(800f, 100f));
        t.text = etiquetas[indice];
        botonesRT[indice] = t.rectTransform;
        botonesTxt[indice] = t;
    }

    // Una barra gruesa con una sombra oscura ligera detrás (para que se distinga sobre el minimapa)
    Image NuevaBarra(Transform padre, float x)
    {
        GameObject sh = Hijo("Sombra", padre, typeof(Image));
        Centrar(sh.GetComponent<RectTransform>(), new Vector2(x + 4f, -4f), new Vector2(BarraAncho, BarraAlto));
        Image si = sh.GetComponent<Image>();
        si.color = new Color(0f, 0f, 0f, 0.55f);
        si.raycastTarget = false;

        GameObject go = Hijo("Barra", padre, typeof(Image));
        Centrar(go.GetComponent<RectTransform>(), new Vector2(x, 0f), new Vector2(BarraAncho, BarraAlto));
        Image img = go.GetComponent<Image>();
        img.color = Blanco;
        img.raycastTarget = false;
        return img;
    }

    static Sprite CrearVineta()
    {
        int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x / (n - 1f) - 0.5f) * 2f;
                float dy = (y / (n - 1f) - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01((r - 0.55f) / 0.9f), 1.5f) * 0.67f;
                px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
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

    static void Centrar(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }
}