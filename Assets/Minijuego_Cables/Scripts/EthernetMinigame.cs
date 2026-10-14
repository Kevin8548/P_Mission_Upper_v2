using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minijuego estilo "Fix Wiring" de Among Us, pero con cables ethernet (RJ45)
/// entre un ROUTER (izquierda) y un SWITCH (derecha). Arrastra cada cable al
/// puerto del mismo color. 3 fases: 4, 6 y 8 cables.
///
/// La interfaz se construye por código (pantalla completa). Los sprites pixel art
/// se cargan solos desde una carpeta Resources/EthernetMinigame; si no los
/// encuentra, dibuja una versión simple con rectángulos.
/// </summary>
public class EthernetMinigame : MonoBehaviour
{
    [Header("Fases (cables por fase)")]
    public int[] cablesPerPhase = { 4, 6, 8 };

    [Header("Bloqueo del juego mientras se juega")]
    [Tooltip("Arrastra aquí el script de movimiento de Leo, el de interacción con E, etc. Se desactivan al abrir el minijuego y se reactivan al cerrarlo.")]
    public Behaviour[] disableWhilePlaying;
    [Tooltip("Pausa el juego (Time.timeScale = 0) mientras dura el minijuego.")]
    public bool freezeTime = true;

    [Header("Apariencia (opcional)")]
    [Tooltip("Tu fuente pixel art de TextMeshPro. Si se deja vacío usa la de por defecto.")]
    public TMP_FontAsset font;

    [Header("Eventos")]
    [Tooltip("Se dispara al terminar las 3 fases.")]
    public UnityEvent onCompleted;
    [Tooltip("Se dispara al cerrar el minijuego, sin importar si se completó o no.")]
    public UnityEvent onClosed;

    // ------------------------------------------------------------------
    // Paleta (8 colores distintos para llegar hasta 8 cables)
    // ------------------------------------------------------------------
    static readonly Color[] Palette =
    {
        new Color32(230,  57,  53, 255), // rojo
        new Color32( 41,  98, 255, 255), // azul
        new Color32(255, 214,   0, 255), // amarillo
        new Color32( 67, 200,  90, 255), // verde
        new Color32(224,  64, 251, 255), // magenta
        new Color32( 38, 222, 230, 255), // cian
        new Color32(255, 138,   0, 255), // naranja
        new Color32(240, 240, 240, 255), // blanco
    };

    static readonly Color Copper = new Color32(205, 120, 50, 255);
    static readonly Color LedOff = new Color32(70, 70, 80, 255);
    static readonly Color LedOn = new Color32(70, 255, 110, 255);
    static readonly Color Good = new Color32(90, 255, 130, 255);
    static readonly Color Bad = new Color32(255, 90, 90, 255);

    const string DefaultHint = "Conecta cada cable ethernet al puerto del mismo color";

    // Layout (coordenadas del "escenario" de 1920x1080)
    const float PanelX = 700f;
    const float PortHeadX = 720f;
    const float PortsTop = 330f;
    const float PortsRange = 770f;
    const float CableThickness = 20f;
    const float CableOutline = 28f;

    // ------------------------------------------------------------------
    // Estado
    // ------------------------------------------------------------------
    Canvas canvas;
    RectTransform canvasRT, stage, cableLayer, dragLayer, portLayer;
    GameObject root;
    TextMeshProUGUI titleText, counterText, statusText;
    Image dragOutline, dragFill, dragShine;

    Sprite spBg, spRouter, spSwitch, spPlugBody, spPlugBoot, spJack;
    bool hasSprites;

    readonly List<EthernetPort> ports = new List<EthernetPort>();
    readonly List<Behaviour> disabledByUs = new List<Behaviour>();
    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    bool playing, busy, worldLocked, timeFrozen;
    float prevTimeScale = 1f;
    int phase, connectedCount, cablesThisPhase;
    EthernetPort dragging;
    Coroutine statusRoutine;

    public bool IsPlaying { get { return playing; } }

    // ------------------------------------------------------------------
    // API pública
    // ------------------------------------------------------------------

    /// <summary>Abre el minijuego en pantalla completa, desde la fase 1.</summary>
    public void Begin()
    {
        if (playing) return;
        EnsureUI();
        LockWorld();

        if (freezeTime)
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            timeFrozen = true;
        }

        playing = true;
        busy = false;
        phase = 0;
        root.SetActive(true);
        FitStage();
        BuildPhase();
    }

    /// <summary>Cierra el minijuego. completed = true si se terminaron las 3 fases.</summary>
    public void Close(bool completed)
    {
        StopAllCoroutines();
        statusRoutine = null;
        playing = false;
        dragging = null;
        if (root != null) root.SetActive(false);

        if (timeFrozen)
        {
            Time.timeScale = prevTimeScale;
            timeFrozen = false;
        }

        UnlockWorld();
        if (completed) onCompleted.Invoke();
        onClosed.Invoke();
    }

    /// <summary>Desactiva los scripts de movimiento/interacción (los usa también el cuadro "¿Estás listo?").</summary>
    public void LockWorld()
    {
        if (worldLocked) return;
        worldLocked = true;
        disabledByUs.Clear();
        foreach (var b in disableWhilePlaying)
        {
            if (b != null && b.enabled)
            {
                b.enabled = false;
                disabledByUs.Add(b);
            }
        }
    }

    public void UnlockWorld()
    {
        if (!worldLocked) return;
        worldLocked = false;
        foreach (var b in disabledByUs)
            if (b != null) b.enabled = true;
        disabledByUs.Clear();
    }

    void OnDisable()
    {
        // Seguridad: si el objeto se desactiva con el juego pausado, no dejar el tiempo congelado.
        if (timeFrozen)
        {
            Time.timeScale = prevTimeScale;
            timeFrozen = false;
        }
    }

    void Update()
    {
        if (playing) FitStage();
    }

    // ------------------------------------------------------------------
    // Construcción de la UI
    // ------------------------------------------------------------------

    void LoadSprites()
    {
        spBg = PixelSprites.Load("background");
        spRouter = PixelSprites.Load("router_body");
        spSwitch = PixelSprites.Load("switch_body");
        spPlugBody = PixelSprites.Load("plug_body");
        spPlugBoot = PixelSprites.Load("plug_boot");
        spJack = PixelSprites.Load("port_jack");

        hasSprites = spRouter != null && spSwitch != null && spPlugBody != null
                     && spPlugBoot != null && spJack != null;

        if (!hasSprites)
            Debug.LogWarning("EthernetMinigame: no encontré los sprites en Resources/EthernetMinigame. Uso el dibujo simple.");
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        LoadSprites();

        canvas = PixelUI.MakeCanvas("EthernetMinigameCanvas", 500);
        canvasRT = (RectTransform)canvas.transform;
        root = canvas.gameObject;

        // Fondo sólido a pantalla completa (también bloquea los clics hacia el juego)
        var bg = PixelUI.MakeImage("Fondo", canvasRT, Vector2.zero, Vector2.zero,
                                   new Color(0.04f, 0.05f, 0.09f, 1f), null, true);
        bg.type = Image.Type.Simple;
        PixelUI.Stretch(bg.rectTransform);

        // Escenario de 1920x1080 que se escala para caber en cualquier ventana
        stage = PixelUI.MakeRect("Escenario", canvasRT);
        stage.sizeDelta = new Vector2(1920f, 1080f);

        // Fondo pixel art (cuarto de servidores)
        if (spBg != null)
            PixelUI.MakeSpriteImage("FondoPixel", stage, new Vector2(1920f, 1080f), Vector2.zero, spBg);

        // Equipos: router a la izquierda, switch a la derecha
        BuildDevicePanel("ROUTER", "RT-01  GATEWAY", -PanelX, true);
        BuildDevicePanel("SWITCH", "SW-01  24 PUERTOS", PanelX, false);

        titleText = PixelUI.MakeText("Titulo", stage, "", 54f, new Vector2(0f, 485f),
                                       new Vector2(1200f, 80f), TextAlignmentOptions.Center, Color.white, font, false);
        counterText = PixelUI.MakeText("Contador", stage, "", 36f, new Vector2(0f, 415f),
                                       new Vector2(900f, 60f), TextAlignmentOptions.Center,
                                       new Color(0.7f, 0.85f, 1f), font, false);
        statusText = PixelUI.MakeText("Estado", stage, DefaultHint, 36f, new Vector2(0f, -510f),
                                       new Vector2(1400f, 60f), TextAlignmentOptions.Center, Color.white, font, false);

        // Capas: cables permanentes < cable que se arrastra < conectores
        cableLayer = PixelUI.MakeLayer("Cables", stage);
        dragLayer = PixelUI.MakeLayer("CableArrastrando", stage);
        portLayer = PixelUI.MakeLayer("Puertos", stage);

        dragOutline = PixelUI.MakeImage("DragBorde", dragLayer, new Vector2(10f, CableOutline), Vector2.zero, Color.black);
        dragFill = PixelUI.MakeImage("DragCable", dragLayer, new Vector2(10f, CableThickness), Vector2.zero, Color.white);
        dragShine = AddShine(dragFill, Color.white);
        dragOutline.gameObject.SetActive(false);
        dragFill.gameObject.SetActive(false);

        // Botón para salir
        PixelUI.MakeButton("BotonSalir", stage, "X", new Vector2(70f, 70f), new Vector2(895f, 490f),
                           new Color32(200, 60, 60, 255), font, () => Close(false), 40f);

        root.SetActive(false);
    }

    void BuildDevicePanel(string title, string subtitle, float x, bool isRouter)
    {
        if (hasSprites)
        {
            // El router trae antenas, por eso es más alto (980 vs 920)
            if (isRouter)
                PixelUI.MakeSpriteImage("Router", stage, new Vector2(300f, 980f), new Vector2(x, 10f), spRouter);
            else
                PixelUI.MakeSpriteImage("Switch", stage, new Vector2(300f, 920f), new Vector2(x, -20f), spSwitch);
        }
        else
        {
            PixelUI.MakeImage("PanelBorde_" + title, stage, new Vector2(316f, 936f), new Vector2(x, -20f),
                              new Color32(15, 20, 35, 255));
            PixelUI.MakeImage("Panel_" + title, stage, new Vector2(300f, 920f), new Vector2(x, -20f),
                              new Color32(38, 46, 66, 255));
        }

        PixelUI.MakeText("Etiqueta_" + title, stage, title, 44f, new Vector2(x, 395f),
                         new Vector2(290f, 60f), TextAlignmentOptions.Center, Color.white, font, false);
        PixelUI.MakeText("Sub_" + title, stage, subtitle, 22f, new Vector2(x, 352f),
                         new Vector2(290f, 40f), TextAlignmentOptions.Center,
                         new Color(0.6f, 0.7f, 0.85f), font, false);
    }

    void FitStage()
    {
        float s = Mathf.Min(canvasRT.rect.width / 1920f, canvasRT.rect.height / 1080f);
        if (s <= 0f) s = 1f;
        stage.localScale = new Vector3(s, s, 1f);
    }

    // ------------------------------------------------------------------
    // Fases
    // ------------------------------------------------------------------

    void BuildPhase()
    {
        // Limpiar fase anterior
        foreach (Transform c in cableLayer) Destroy(c.gameObject);
        foreach (Transform c in portLayer) Destroy(c.gameObject);
        ports.Clear();
        dragging = null;
        dragOutline.gameObject.SetActive(false);
        dragFill.gameObject.SetActive(false);

        cablesThisPhase = Mathf.Clamp(cablesPerPhase[phase], 1, Palette.Length);
        connectedCount = 0;

        // Elegir colores y ordenar ambos lados al azar (sin cables "derechos")
        var ids = new List<int>();
        for (int i = 0; i < Palette.Length; i++) ids.Add(i);
        Shuffle(ids);
        ids.RemoveRange(cablesThisPhase, ids.Count - cablesThisPhase);

        var left = new List<int>(ids);
        var right = new List<int>(ids);
        Shuffle(left);
        int guard = 0;
        do { Shuffle(right); guard++; }
        while (cablesThisPhase > 1 && HasFixedPoint(left, right) && guard < 50);

        float step = PortsRange / cablesThisPhase;
        for (int i = 0; i < cablesThisPhase; i++)
        {
            float y = PortsTop - (i + 0.5f) * step;
            CreatePort(EthernetPort.Side.Router, left[i], new Vector2(-PortHeadX, y));
            CreatePort(EthernetPort.Side.Switch, right[i], new Vector2(PortHeadX, y));
        }

        titleText.text = "CONEXIÓN DE RED - FASE " + (phase + 1) + "/" + cablesPerPhase.Length;
        UpdateCounter();
        SetStatus(DefaultHint, Color.white);
    }

    void CreatePort(EthernetPort.Side side, int colorId, Vector2 pos)
    {
        Color col = Palette[colorId];

        var hit = PixelUI.MakeImage("Puerto_" + side + "_" + colorId, portLayer, new Vector2(220f, 80f),
                                    pos, new Color(0f, 0f, 0f, 0f), null, true);
        var rt = hit.rectTransform;
        var port = hit.gameObject.AddComponent<EthernetPort>();
        port.side = side;
        port.colorId = colorId;

        // Todo se dibuja "mirando a la derecha"; el lado del switch se voltea con escala -1
        var visual = PixelUI.MakeRect("Visual", rt);
        visual.sizeDelta = Vector2.zero;
        visual.localScale = new Vector3(side == EthernetPort.Side.Switch ? -1f : 1f, 1f, 1f);

        float anchorX;
        if (hasSprites)
        {
            // puerto del equipo, conector transparente con pines, y funda del color del cable
            PixelUI.MakeSpriteImage("Puerto", visual, new Vector2(56f, 80f), new Vector2(-68f, 0f), spJack);
            PixelUI.MakeSpriteImage("Funda", visual, new Vector2(48f, 40f), new Vector2(48f, 0f), spPlugBoot).color = col;
            PixelUI.MakeSpriteImage("Conector", visual, new Vector2(96f, 56f), new Vector2(-24f, 0f), spPlugBody);

            PixelUI.MakeImage("LedBorde", visual, new Vector2(14f, 14f), new Vector2(-68f, 34f), Color.black);
            port.led = PixelUI.MakeImage("Led", visual, new Vector2(10f, 10f), new Vector2(-68f, 34f), LedOff);
            anchorX = 76f;
        }
        else
        {
            PixelUI.MakeImage("Borde", visual, new Vector2(128f, 64f), Vector2.zero, Color.black);
            PixelUI.MakeImage("Cabeza", visual, new Vector2(120f, 56f), Vector2.zero, col);
            PixelUI.MakeImage("Brillo", visual, new Vector2(100f, 8f), new Vector2(0f, 16f), Color.Lerp(col, Color.white, 0.4f));
            PixelUI.MakeImage("Sombra", visual, new Vector2(100f, 8f), new Vector2(0f, -16f), Color.Lerp(col, Color.black, 0.3f));
            PixelUI.MakeImage("PuntaBorde", visual, new Vector2(36f, 44f), new Vector2(76f, 0f), Color.black);
            PixelUI.MakeImage("Punta", visual, new Vector2(28f, 36f), new Vector2(74f, 0f), Copper);
            PixelUI.MakeImage("PuntaLinea", visual, new Vector2(28f, 6f), new Vector2(74f, 0f), Color.Lerp(Copper, Color.black, 0.45f));
            PixelUI.MakeImage("LedBorde", visual, new Vector2(20f, 20f), new Vector2(-40f, 0f), Color.black);
            port.led = PixelUI.MakeImage("Led", visual, new Vector2(12f, 12f), new Vector2(-40f, 0f), LedOff);
            anchorX = 90f;
        }

        var anchor = PixelUI.MakeRect("Ancla", visual);
        anchor.sizeDelta = Vector2.zero;
        anchor.anchoredPosition = new Vector2(anchorX, 0f);
        port.anchorPoint = anchor;

        port.Down = OnPortDown;
        port.Drag = OnPortDrag;
        port.Up = OnPortUp;
        ports.Add(port);
    }

    // ------------------------------------------------------------------
    // Interacción (arrastrar cables)
    // ------------------------------------------------------------------

    void OnPortDown(EthernetPort p, PointerEventData e)
    {
        if (!playing || busy || p.connected || dragging != null) return;
        if (e.button != PointerEventData.InputButton.Left) return;

        dragging = p;
        Color col = Palette[p.colorId];
        dragFill.color = col;
        dragShine.color = Color.Lerp(col, Color.white, 0.45f);
        dragOutline.gameObject.SetActive(true);
        dragFill.gameObject.SetActive(true);
        Vector2 a = AnchorPos(p);
        SetLine(dragOutline.rectTransform, a, a + Vector2.right * 2f, CableOutline);
        SetLine(dragFill.rectTransform, a, a + Vector2.right * 2f, CableThickness);
    }

    void OnPortDrag(EthernetPort p, PointerEventData e)
    {
        if (dragging != p) return;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(cableLayer, e.position, null, out local);
        Vector2 a = AnchorPos(p);
        SetLine(dragOutline.rectTransform, a, local, CableOutline);
        SetLine(dragFill.rectTransform, a, local, CableThickness);
    }

    void OnPortUp(EthernetPort p, PointerEventData e)
    {
        if (dragging != p) return;
        dragging = null;
        dragOutline.gameObject.SetActive(false);
        dragFill.gameObject.SetActive(false);

        EthernetPort target = FindPortUnder(e);
        if (target == null || target == p || target.connected || target.side == p.side) return;

        if (target.colorId == p.colorId)
            Connect(p, target);
        else
            ShowTempStatus("¡Ese cable no va en ese puerto!", Bad);
    }

    static EthernetPort FindPortUnder(PointerEventData e)
    {
        hits.Clear();
        EventSystem.current.RaycastAll(e, hits);
        foreach (var h in hits)
        {
            var port = h.gameObject.GetComponent<EthernetPort>();
            if (port != null) return port;
        }
        return null;
    }

    void Connect(EthernetPort a, EthernetPort b)
    {
        a.connected = b.connected = true;
        a.led.color = LedOn;
        b.led.color = LedOn;

        Color col = Palette[a.colorId];
        Vector2 pa = AnchorPos(a);
        Vector2 pb = AnchorPos(b);
        var outline = PixelUI.MakeImage("CableBorde", cableLayer, new Vector2(10f, CableOutline), Vector2.zero, Color.black);
        var fill = PixelUI.MakeImage("Cable", cableLayer, new Vector2(10f, CableThickness), Vector2.zero, col);
        AddShine(fill, col);
        SetLine(outline.rectTransform, pa, pb, CableOutline);
        SetLine(fill.rectTransform, pa, pb, CableThickness);

        connectedCount++;
        UpdateCounter();

        if (connectedCount >= cablesThisPhase)
            StartCoroutine(PhaseDone());
    }

    IEnumerator PhaseDone()
    {
        busy = true;
        if (statusRoutine != null) { StopCoroutine(statusRoutine); statusRoutine = null; }

        if (phase < cablesPerPhase.Length - 1)
        {
            SetStatus("¡Fase " + (phase + 1) + " completada!", Good);
            yield return new WaitForSecondsRealtime(1.2f);
            phase++;
            BuildPhase();
            busy = false;
        }
        else
        {
            SetStatus("¡Red restaurada! Todos los enlaces están activos", Good);
            yield return new WaitForSecondsRealtime(2f);
            Close(true);
        }
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    Vector2 AnchorPos(EthernetPort p)
    {
        return (Vector2)cableLayer.InverseTransformPoint(p.anchorPoint.position);
    }

    static void SetLine(RectTransform rt, Vector2 a, Vector2 b, float thickness)
    {
        Vector2 d = b - a;
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Max(d.magnitude, 0.01f), thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // Franja clara arriba del cable para que parezca un cable redondo
    static Image AddShine(Image fill, Color baseColor)
    {
        var rt = PixelUI.MakeRect("Brillo", fill.rectTransform);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 6f);
        rt.anchoredPosition = new Vector2(0f, 5f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.Lerp(baseColor, Color.white, 0.45f);
        img.raycastTarget = false;
        return img;
    }

    void UpdateCounter()
    {
        counterText.text = "Cables conectados: " + connectedCount + "/" + cablesThisPhase;
    }

    void SetStatus(string msg, Color c)
    {
        statusText.text = msg;
        statusText.color = c;
    }

    void ShowTempStatus(string msg, Color c)
    {
        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(TempStatus(msg, c));
    }

    IEnumerator TempStatus(string msg, Color c)
    {
        SetStatus(msg, c);
        yield return new WaitForSecondsRealtime(1.2f);
        SetStatus(DefaultHint, Color.white);
        statusRoutine = null;
    }

    static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    static bool HasFixedPoint(List<int> a, List<int> b)
    {
        for (int i = 0; i < a.Count; i++)
            if (a[i] == b[i]) return true;
        return false;
    }
}

/// <summary>Carga los sprites desde Resources/EthernetMinigame (con filtro Point para que se vean nítidos).</summary>
public static class PixelSprites
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Load(string name)
    {
        Sprite sp;
        if (cache.TryGetValue(name, out sp) && sp != null) return sp;

        string path = "EthernetMinigame/" + name;
        sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        sp.texture.filterMode = FilterMode.Point;
        cache[name] = sp;
        return sp;
    }
}

/// <summary>Helpers para construir UI por código (la usan el minijuego y el cuadro de pregunta).</summary>
public static class PixelUI
{
    public static Canvas MakeCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    public static RectTransform MakeLayer(string name, Transform parent)
    {
        var rt = MakeRect(name, parent);
        Stretch(rt);
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Image MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color,
                                  Sprite sprite = null, bool raycast = false)
    {
        var rt = MakeRect(name, parent);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = raycast;
        if (sprite != null) img.type = Image.Type.Sliced;
        return img;
    }

    /// <summary>Imagen con un sprite pixel art, estirado sin suavizar.</summary>
    public static Image MakeSpriteImage(string name, Transform parent, Vector2 size, Vector2 pos, Sprite sprite)
    {
        var img = MakeImage(name, parent, size, pos, Color.white, sprite, false);
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        return img;
    }

    public static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Vector2 pos,
                                           Vector2 box, TextAlignmentOptions align, Color color,
                                           TMP_FontAsset font, bool wrap)
    {
        var rt = MakeRect(name, parent);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static Button MakeButton(string name, Transform parent, string label, Vector2 size, Vector2 pos,
                                    Color color, TMP_FontAsset font, UnityAction onClick, float fontSize)
    {
        MakeImage(name + "_Borde", parent, size + new Vector2(8f, 8f), pos, Color.black);
        var img = MakeImage(name, parent, size, pos, color, null, true);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        MakeText(name + "_Texto", img.rectTransform, label, fontSize, Vector2.zero, size,
                 TextAlignmentOptions.Center, Color.white, font, false);
        return btn;
    }
}