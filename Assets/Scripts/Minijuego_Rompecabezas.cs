using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Minijuego del rompecabezas (profe Mago).
// Uso: GameObject vacio en una escena nueva + este script + arrastrar la imagen al campo "Imagen".
// Necesita un EventSystem en la escena (GameObject > UI > Event System).
// Todo (fondo, recuadro, piezas, arrastre, corazones, cronometro, pantalla de derrota) se construye por codigo.
public class RompecabezasManager : MonoBehaviour
{
    [Header("Imagen del rompecabezas")]
    public Texture2D imagen;

    [Header("Piezas")]
    public int columnas = 5;
    public int filas = 4;
    [Range(0.08f, 0.18f)] public float tamPestana = 0.13f;      // tamano de la pestana respecto a la pieza
    [Range(0f, 0.12f)] public float variacionPestana = 0.08f;   // cuanto se desfasa la pestana del centro
    public int grosorBorde = 2;                                  // contorno oscuro de cada pieza (en px de la imagen)
    public float distanciaIman = 55f;                            // que tan cerca hay que soltarla para que encaje

    [Header("Tiempo y corazones")]
    public float tiempoTotal = 60f;                              // 1 minuto
    public float tiempoCorazon1 = 30f;                           // al llegar a este tiempo restante pierde el 1er corazon
    public float tiempoCorazon2 = 15f;                           // al llegar a este tiempo restante pierde el 2do corazon
                                                                 // al llegar a 0 pierde el 3er corazon y el juego

    [Header("Pantalla")]
    [Range(0.5f, 0.98f)] public float tamanoTablero = 0.94f;     // alto del tablero respecto a la zona de juego
    [Range(0.4f, 0.92f)] public float anchoTablero = 0.62f;      // ancho maximo del tablero (deja espacio a los lados para las piezas)
    [Range(0f, 1f)] public float opacidadPanel = 0.40f;          // 0 = totalmente transparente, 1 = solido
    public Color colorFondoArriba = new Color32(16, 32, 61, 255);
    public Color colorFondoAbajo = new Color32(38, 142, 108, 255);
    public Color colorAtardecerArriba = new Color32(70, 30, 110, 255);   // 2do cielo que se mezcla poco a poco
    public Color colorAtardecerAbajo = new Color32(240, 120, 90, 255);
    public Color colorMarco = new Color32(236, 240, 228, 255);
    public Color colorPanel = new Color32(26, 38, 66, 255);
    public TMP_FontAsset fuente;                                 // opcional: la misma fuente de tu DialogueManager

    [Header("Al terminar")]
    public string escenaDeRegreso = "SampleScene";               // nombre de tu escena principal
    public UnityEvent alCompletar;
    public UnityEvent alPerder;

    struct Borde
    {
        public int arr, der, aba, izq;                           // 0 plano, 1 pestana hacia afuera, -1 hueco
        public float offArr, offDer, offAba, offIzq;             // desfase de la pestana a lo largo del borde
    }

    class CapaFondo
    {
        public RawImage raw;
        public float vel;
        public float pos;
    }

    // Fondo
    const int FW = 240, FH = 135;                                // cada pixel = 8 px en 1920x1080
    const int NumLuces = 32;
    readonly List<CapaFondo> capas = new List<CapaFondo>();
    RawImage atardecerRaw, estrellasA, estrellasB;
    RectTransform lunaRT;
    RectTransform[] lucesRT;
    Image[] lucesIm;
    Vector2[] lucesPos;
    float[] lucesVel, lucesFase, lucesAmp;
    RectTransform fugazRT;
    Image fugazIm;
    float fugazT = -1f, fugazEspera = 3f;
    Vector2 fugazIni;

    // UI
    RectTransform canvasRT, mesa, piezasRoot, completa, tituloRT, cronoRT, barraRect;
    RawImage completaRaw;
    TextMeshProUGUI contador, cronometro;
    Image barraRelleno, flash;
    Sprite corazonLleno, corazonVacio;
    readonly List<Image> corazones = new List<Image>();

    Texture2D legible;
    readonly List<PiezaRompecabezas> piezas = new List<PiezaRompecabezas>();
    readonly List<Object> temporales = new List<Object>();
    int colocadas;

    // Estado
    bool jugando;
    float tiempo;
    int vidas = 3;
    bool perdioC1, perdioC2;
    MenuFinal menu;

    public bool Jugando { get { return jugando; } }

    IEnumerator Start()
    {
        if (imagen == null)
        {
            Debug.LogError("RompecabezasManager: falta asignar la imagen.");
            yield break;
        }
        if (FindFirstObjectByType<EventSystem>() == null)
            Debug.LogWarning("RompecabezasManager: falta un EventSystem (GameObject > UI > Event System).");

        tiempo = tiempoTotal;
        vidas = 3;
        ConstruirPantalla();
        yield return null;                 // esperamos un frame para que el Canvas calcule tamanos
        Canvas.ForceUpdateCanvases();
        ConstruirPiezas();
        jugando = true;                    // el reloj arranca cuando ya estan las piezas
    }

    void Update()
    {
        AnimarFondo();
        AnimarTitulo();
        if (!jugando) return;

        tiempo -= Time.unscaledDeltaTime;

        if (!perdioC1 && tiempo <= tiempoCorazon1) { perdioC1 = true; QuitarCorazon(); }
        if (!perdioC2 && tiempo <= tiempoCorazon2) { perdioC2 = true; QuitarCorazon(); }

        if (tiempo <= 0f)
        {
            tiempo = 0f;
            ActualizarTiempoUI();
            QuitarCorazon();               // el 3er corazon
            Perder();
            return;
        }

        ActualizarTiempoUI();

        // Ultimo corazon latiendo
        if (vidas == 1 && corazones.Count > 0)
            corazones[0].rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f));
    }

    void OnDestroy()
    {
        foreach (var o in temporales) if (o != null) Destroy(o);
    }

    // ───────────────────────── PANTALLA ─────────────────────────

    void ConstruirPantalla()
    {
        var cgo = new GameObject("CanvasRompecabezas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvasRT = cgo.GetComponent<RectTransform>();
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Fondo animado (cielo, estrellas, luna, nubes, colinas, luciernagas, estrella fugaz)
        ConstruirFondo(cgo.transform);

        // Marco: solo el contorno (claro + oscuro), el interior es translucido para ver el fondo
        var marco = NuevoUI("Marco", cgo.transform);
        marco.anchorMin = new Vector2(0.02f, 0.03f);
        marco.anchorMax = new Vector2(0.98f, 0.97f);
        marco.offsetMin = marco.offsetMax = Vector2.zero;
        Marco4(marco, 8f, 0f, colorMarco);
        Marco4(marco, 6f, 8f, new Color32(18, 24, 44, 255));

        var panel = NuevoUI("Panel", marco);
        Estirar(panel, 14);
        Color pc = colorPanel;
        pc.a = opacidadPanel;
        Img(panel, pc);

        // ── Titulo ──
        tituloRT = NuevoUI("Titulo", panel);
        tituloRT.anchorMin = new Vector2(0f, 1f);
        tituloRT.anchorMax = new Vector2(1f, 1f);
        tituloRT.pivot = new Vector2(0.5f, 1f);
        tituloRT.anchoredPosition = new Vector2(0f, -8f);
        tituloRT.sizeDelta = new Vector2(0f, 80f);

        var tSombra = CrearTexto("Sombra", tituloRT, 64, new Color(0f, 0f, 0f, 0.6f), TextAlignmentOptions.Center);
        Estirar(tSombra.rectTransform, 0);
        tSombra.rectTransform.offsetMin = new Vector2(4f, -4f);
        tSombra.rectTransform.offsetMax = new Vector2(4f, -4f);
        tSombra.characterSpacing = 8f;
        tSombra.text = "ARMA EL ROMPECABEZAS";

        var tTexto = CrearTexto("Texto", tituloRT, 64, Color.white, TextAlignmentOptions.Center);
        Estirar(tTexto.rectTransform, 0);
        tTexto.characterSpacing = 8f;
        tTexto.enableVertexGradient = true;
        Color32 g1 = new Color32(255, 244, 160, 255), g2 = new Color32(255, 160, 60, 255);
        tTexto.colorGradient = new VertexGradient(g1, g1, g2, g2);
        tTexto.text = "ARMA EL ROMPECABEZAS";

        // ── Corazones (pastilla a la izquierda) ──
        corazonLleno = CrearCorazon(true);
        corazonVacio = CrearCorazon(false);
        var hRT = Pastilla(panel, "Corazones", new Vector2(0f, 1f), new Vector2(30f, -12f), new Vector2(240f, 66f));
        for (int i = 0; i < 3; i++)
        {
            var c = NuevoUI("Corazon" + i, hRT);
            c.anchorMin = c.anchorMax = c.pivot = new Vector2(0f, 0.5f);
            c.anchoredPosition = new Vector2(30f + i * 66f, 0f);
            c.sizeDelta = new Vector2(52f, 48f);
            var im = c.gameObject.AddComponent<Image>();
            im.sprite = corazonLleno;
            im.raycastTarget = false;
            corazones.Add(im);
        }

        // ── Contador de piezas (pastilla a la derecha) ──
        var cPill = Pastilla(panel, "ContadorPill", new Vector2(1f, 1f), new Vector2(-30f, -12f), new Vector2(340f, 66f));
        contador = CrearTexto("Contador", cPill, 34, Color.white, TextAlignmentOptions.Center);
        Estirar(contador.rectTransform, 0);

        // ── Barra de tiempo con el cronometro adentro ──
        var bRT = NuevoUI("Barra", panel);
        bRT.anchorMin = new Vector2(0f, 1f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -94f);
        bRT.sizeDelta = new Vector2(-60f, 38f);
        Img(bRT, new Color32(10, 14, 30, 230));
        barraRect = NuevoUI("Relleno", bRT);
        Estirar(barraRect, 4);
        barraRelleno = Img(barraRect, new Color32(80, 220, 140, 255));

        cronometro = CrearTexto("Cronometro", bRT, 30, Color.white, TextAlignmentOptions.Center);
        cronoRT = cronometro.rectTransform;
        Estirar(cronoRT, 0);

        // Zona donde viven tablero y piezas (debajo del encabezado)
        mesa = NuevoUI("Mesa", panel);
        Estirar(mesa, 0);
        mesa.offsetMin = new Vector2(10f, 10f);
        mesa.offsetMax = new Vector2(-10f, -142f);

        // Destello rojo cuando se pierde un corazon (encima de todo, no bloquea clics)
        var fRT = NuevoUI("Flash", canvasRT);
        Estirar(fRT, 0);
        flash = Img(fRT, new Color(1f, 0.1f, 0.1f, 0f));

        ActualizarTiempoUI();
        ActualizarContador();
    }

    // Cuatro tiras que forman un contorno hueco (deja ver lo que hay detras)
    static void Marco4(RectTransform padre, float grosor, float inset, Color color)
    {
        var t = NuevoUI("Arriba", padre);
        t.anchorMin = new Vector2(0f, 1f); t.anchorMax = new Vector2(1f, 1f);
        t.offsetMin = new Vector2(inset, -inset - grosor); t.offsetMax = new Vector2(-inset, -inset);
        Img(t, color);

        var b = NuevoUI("Abajo", padre);
        b.anchorMin = new Vector2(0f, 0f); b.anchorMax = new Vector2(1f, 0f);
        b.offsetMin = new Vector2(inset, inset); b.offsetMax = new Vector2(-inset, inset + grosor);
        Img(b, color);

        var l = NuevoUI("Izq", padre);
        l.anchorMin = new Vector2(0f, 0f); l.anchorMax = new Vector2(0f, 1f);
        l.offsetMin = new Vector2(inset, inset); l.offsetMax = new Vector2(inset + grosor, -inset);
        Img(l, color);

        var r = NuevoUI("Der", padre);
        r.anchorMin = new Vector2(1f, 0f); r.anchorMax = new Vector2(1f, 1f);
        r.offsetMin = new Vector2(-inset - grosor, inset); r.offsetMax = new Vector2(-inset, -inset);
        Img(r, color);
    }

    // Cajita oscura translucida con borde claro (para corazones y contador)
    RectTransform Pastilla(Transform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        var rt = NuevoUI(nombre, padre);
        rt.anchorMin = rt.anchorMax = rt.pivot = ancla;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        Img(rt, new Color(colorMarco.r, colorMarco.g, colorMarco.b, 0.9f));
        var inner = NuevoUI("Fondo", rt);
        Estirar(inner, 4);
        Img(inner, new Color32(10, 16, 34, 215));
        return rt;
    }

    // ───────────────────────── FONDO DINAMICO ─────────────────────────

    void ConstruirFondo(Transform padre)
    {
        var raiz = NuevoUI("Fondo", padre);
        Estirar(raiz, 0);

        // Cielo base + 2do cielo (atardecer) que se mezcla lento
        NuevaCapa(raiz, "Cielo", NuevaTex(CrearGradiente(colorFondoArriba, colorFondoAbajo)), 0f);
        atardecerRaw = NuevaCapa(raiz, "Atardecer", NuevaTex(CrearGradiente(colorAtardecerArriba, colorAtardecerAbajo)), 0f);
        atardecerRaw.color = new Color(1f, 1f, 1f, 0f);

        // Estrellas que titilan (dos capas con fase distinta)
        estrellasA = NuevaCapa(raiz, "EstrellasA", NuevaTex(CrearEstrellas(777, 44)), 0f);
        estrellasB = NuevaCapa(raiz, "EstrellasB", NuevaTex(CrearEstrellas(4242, 44)), 0f);

        // Luna pixelada flotando suave
        var lunaTex = CrearLuna();
        lunaRT = NuevoUI("Luna", raiz);
        lunaRT.anchorMin = lunaRT.anchorMax = new Vector2(0.92f, 0.72f);
        lunaRT.sizeDelta = new Vector2(170f, 170f);
        var lunaIm = lunaRT.gameObject.AddComponent<Image>();
        var lunaSp = Sprite.Create(lunaTex, new Rect(0, 0, lunaTex.width, lunaTex.height), new Vector2(0.5f, 0.5f), 100f);
        temporales.Add(lunaSp);
        lunaIm.sprite = lunaSp;
        lunaIm.raycastTarget = false;

        // Nubes lejanas y medias (parallax)
        NuevaCapa(raiz, "NubesLejanas", NuevaTex(CrearNubes(11, 7, 14, 24, 0.50f, 0.90f, 70)), 0.004f);
        NuevaCapa(raiz, "NubesMedias", NuevaTex(CrearNubes(22, 5, 22, 36, 0.35f, 0.80f, 105)), 0.009f);

        // Colinas pixeladas en movimiento
        Color colLejana = Color.Lerp(colorFondoAbajo, colorFondoArriba, 0.45f);
        Color colCercana = Color.Lerp(colorFondoAbajo, Color.black, 0.5f);
        NuevaCapa(raiz, "ColinasLejanas", NuevaTex(CrearColinas(colLejana, 30, 7, 3, 4, 1, 0.5f)), 0.006f);
        NuevaCapa(raiz, "ColinasCercanas", NuevaTex(CrearColinas(colCercana, 16, 5, 2, 7, 2, 1.7f)), 0.014f);

        // Nubes cercanas (pasan por delante de las colinas)
        NuevaCapa(raiz, "NubesCercanas", NuevaTex(CrearNubes(33, 4, 30, 46, 0.25f, 0.60f, 85)), 0.016f);

        // Luciernagas / particulas flotando hacia arriba
        lucesRT = new RectTransform[NumLuces];
        lucesIm = new Image[NumLuces];
        lucesPos = new Vector2[NumLuces];
        lucesVel = new float[NumLuces];
        lucesFase = new float[NumLuces];
        lucesAmp = new float[NumLuces];
        for (int i = 0; i < NumLuces; i++)
        {
            var rt = NuevoUI("Luz" + i, raiz);
            float s = Random.Range(5f, 12f);
            rt.sizeDelta = new Vector2(s, s);
            var im = rt.gameObject.AddComponent<Image>();
            im.raycastTarget = false;
            im.color = Random.value < 0.6f ? new Color32(255, 240, 160, 255) : new Color32(160, 255, 225, 255);
            lucesRT[i] = rt; lucesIm[i] = im;
            lucesPos[i] = new Vector2(Random.value, Random.value);
            lucesVel[i] = Random.Range(0.012f, 0.04f);
            lucesFase[i] = Random.Range(0f, 6.28f);
            lucesAmp[i] = Random.Range(0.005f, 0.02f);
        }

        // Estrella fugaz
        fugazRT = NuevoUI("Fugaz", raiz);
        fugazRT.sizeDelta = new Vector2(110f, 4f);
        fugazRT.localRotation = Quaternion.Euler(0f, 0f, 24f);
        fugazIm = fugazRT.gameObject.AddComponent<Image>();
        fugazIm.raycastTarget = false;
        fugazIm.color = new Color(1f, 1f, 1f, 0f);
    }

    RawImage NuevaCapa(RectTransform padre, string nombre, Texture2D tex, float vel)
    {
        var rt = NuevoUI(nombre, padre);
        Estirar(rt, 0);
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;
        if (vel != 0f) capas.Add(new CapaFondo { raw = raw, vel = vel, pos = Random.value });
        return raw;
    }

    Texture2D NuevaTex(Color32[] px)
    {
        var tex = new Texture2D(FW, FH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels32(px);
        tex.Apply();
        temporales.Add(tex);
        return tex;
    }

    // Degradado por bandas planas con costuras tramadas (dither)
    Color32[] CrearGradiente(Color arriba, Color abajo)
    {
        const int niveles = 8;
        var paleta = new Color32[niveles];
        for (int i = 0; i < niveles; i++)
            paleta[i] = Color.Lerp(arriba, abajo, i / (float)(niveles - 1));

        int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        var px = new Color32[FW * FH];
        for (int y = 0; y < FH; y++)
        {
            float k = 1f - y / (float)(FH - 1);          // y = 0 es abajo
            float nivel = k * (niveles - 1);
            int baseIdx = Mathf.FloorToInt(nivel);
            float frac = nivel - baseIdx;
            float f = Mathf.Clamp01((frac - 0.35f) / 0.3f);
            for (int x = 0; x < FW; x++)
            {
                float umbral = (bayer[y % 4, x % 4] + 0.5f) / 16f;
                int idx = Mathf.Clamp(baseIdx + (f > umbral ? 1 : 0), 0, niveles - 1);
                px[y * FW + x] = paleta[idx];
            }
        }
        return px;
    }

    Color32[] CrearEstrellas(int semilla, int cantidad)
    {
        var px = new Color32[FW * FH];                   // todo transparente
        var rnd = new System.Random(semilla);
        for (int i = 0; i < cantidad; i++)
        {
            int x = rnd.Next(0, FW);
            int y = rnd.Next((int)(FH * 0.5f), FH - 2);
            Poner(px, x, y, new Color32(255, 255, 255, 255));
            if (rnd.NextDouble() < 0.25)                 // algunas son cruz
            {
                var c = new Color32(255, 255, 255, 120);
                Poner(px, x + 1, y, c); Poner(px, x - 1, y, c);
                Poner(px, x, y + 1, c); Poner(px, x, y - 1, c);
            }
        }
        return px;
    }

    // Luna en cuarto creciente con un halo suave
    Texture2D CrearLuna()
    {
        const int n = 20;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - 9.5f, dy = y - 9.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ex = x - 12.5f, ey = y - 11f;
                float de = Mathf.Sqrt(ex * ex + ey * ey);
                if (d <= 7f && de > 6f) px[y * n + x] = new Color32(255, 246, 200, 255);
                else if (d <= 9.5f) px[y * n + x] = new Color32(255, 246, 200, 38);
                else px[y * n + x] = new Color32(0, 0, 0, 0);
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        temporales.Add(tex);
        return tex;
    }

    Color32[] CrearNubes(int semilla, int cantidad, int anchoMin, int anchoMax, float yMin, float yMax, byte alfa)
    {
        var px = new Color32[FW * FH];
        var rnd = new System.Random(semilla);
        for (int i = 0; i < cantidad; i++)
        {
            int cx = (FW / cantidad) * i + rnd.Next(-8, 9);
            int cy = rnd.Next((int)(FH * yMin), (int)(FH * yMax));
            int ancho = rnd.Next(anchoMin, anchoMax + 1);
            Banda(px, cx - ancho / 2, cy, ancho, 3, alfa);
            Banda(px, cx - ancho / 3, cy + 3, ancho * 2 / 3, 3, alfa);
            Banda(px, cx - ancho / 6, cy + 6, ancho / 3, 2, alfa);
        }
        return px;
    }

    // f1 y f2 deben ser enteros para que el scroll sea continuo
    Color32[] CrearColinas(Color color, int baseAlto, int a1, int a2, int f1, int f2, float fase)
    {
        var px = new Color32[FW * FH];
        Color32 c = color;
        Color32 borde = Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.18f);
        for (int x = 0; x < FW; x++)
        {
            float ang = x / (float)FW * Mathf.PI * 2f;
            int alto = Mathf.RoundToInt(baseAlto + Mathf.Sin(ang * f1 + fase) * a1 + Mathf.Sin(ang * f2 * 2f + fase * 2f) * a2);
            alto = Mathf.Clamp(alto, 1, FH - 1);
            for (int y = 0; y < alto; y++) px[y * FW + x] = (y == alto - 1) ? borde : c;
        }
        return px;
    }

    static void Poner(Color32[] px, int x, int y, Color32 c)
    {
        if (y < 0 || y >= FH) return;
        x = ((x % FW) + FW) % FW;                        // da la vuelta para que el scroll sea continuo
        px[y * FW + x] = c;
    }

    static void Banda(Color32[] px, int x0, int y0, int bw, int bh, byte alfa)
    {
        for (int y = y0; y < y0 + bh; y++)
            for (int x = x0; x < x0 + bw; x++)
            {
                if (y < 0 || y >= FH) continue;
                int xx = ((x % FW) + FW) % FW;
                if (px[y * FW + xx].a < alfa) px[y * FW + xx] = new Color32(255, 255, 255, alfa);
            }
    }

    void AnimarFondo()
    {
        if (capas.Count == 0) return;
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        // Scroll con parallax
        foreach (var c in capas)
        {
            c.pos = (c.pos + c.vel * dt) % 1f;
            c.raw.uvRect = new Rect(c.pos, 0f, 1f, 1f);
        }

        // Cielo que cambia lento entre noche y atardecer
        atardecerRaw.color = new Color(1f, 1f, 1f, (0.5f + 0.5f * Mathf.Sin(t * 0.12f)) * 0.7f);

        // Estrellas titilando
        estrellasA.color = new Color(1f, 1f, 1f, 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 1.7f)));
        estrellasB.color = new Color(1f, 1f, 1f, 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 1.3f + 2.2f)));

        // Luna flotando
        if (lunaRT != null)
        {
            lunaRT.anchoredPosition = new Vector2(Mathf.Sin(t * 0.3f) * 10f, Mathf.Sin(t * 0.6f) * 8f);
            lunaRT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.4f) * 4f);
        }

        // Luciernagas
        for (int i = 0; i < NumLuces; i++)
        {
            lucesPos[i].y += lucesVel[i] * dt;
            if (lucesPos[i].y > 1.05f)
            {
                lucesPos[i].y = -0.05f;
                lucesPos[i].x = Random.value;
            }
            float x = lucesPos[i].x + Mathf.Sin(t * 0.8f + lucesFase[i]) * lucesAmp[i];
            lucesRT[i].anchorMin = lucesRT[i].anchorMax = new Vector2(x, lucesPos[i].y);
            lucesRT[i].anchoredPosition = Vector2.zero;
            var col = lucesIm[i].color;
            col.a = 0.2f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 2f + lucesFase[i]));
            lucesIm[i].color = col;
        }

        // Estrella fugaz de vez en cuando
        if (fugazT < 0f)
        {
            fugazEspera -= dt;
            if (fugazEspera <= 0f)
            {
                fugazT = 0f;
                fugazIni = new Vector2(Random.Range(0.35f, 0.95f), Random.Range(0.7f, 0.95f));
            }
        }
        else
        {
            fugazT += dt / 0.7f;
            if (fugazT >= 1f)
            {
                fugazT = -1f;
                fugazEspera = Random.Range(4f, 9f);
                fugazIm.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                Vector2 p = fugazIni + new Vector2(-0.28f, -0.2f) * fugazT;
                fugazRT.anchorMin = fugazRT.anchorMax = p;
                fugazRT.anchoredPosition = Vector2.zero;
                fugazIm.color = new Color(1f, 1f, 1f, Mathf.Sin(fugazT * Mathf.PI));
            }
        }
    }

    void AnimarTitulo()
    {
        if (tituloRT == null) return;
        float t = Time.unscaledTime;
        tituloRT.anchoredPosition = new Vector2(0f, -8f + Mathf.Sin(t * 2f) * 3f);
        tituloRT.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(t * 3f));
    }

    // ───────────────────────── CORAZONES Y TIEMPO ─────────────────────────

    // Corazon pixel-art generado por codigo (lleno = rojo, vacio = gris oscuro)
    Sprite CrearCorazon(bool lleno)
    {
        string[] forma =
        {
            ".XX...XX.",
            "XXXX.XXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            ".XXXXXXX.",
            "..XXXXX..",
            "...XXX...",
            "....X...."
        };
        const int w = 11, h = 10;
        var f = new bool[w, h];
        for (int fila = 0; fila < 8; fila++)
            for (int col = 0; col < 9; col++)
                if (forma[fila][col] == 'X') f[col + 1, h - 2 - fila] = true;

        Color32 relleno = lleno ? new Color32(232, 55, 72, 255) : new Color32(58, 62, 84, 255);
        Color32 brillo = new Color32(255, 165, 175, 255);
        Color32 contorno = new Color32(16, 18, 34, 255);

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (f[x, y])
                {
                    px[y * w + x] = relleno;
                    // brillito arriba a la izquierda
                    if (lleno && x == 2 && (y == h - 3 || y == h - 4)) px[y * w + x] = brillo;
                }
                else
                {
                    bool vecino = (x > 0 && f[x - 1, y]) || (x < w - 1 && f[x + 1, y]) ||
                                  (y > 0 && f[x, y - 1]) || (y < h - 1 && f[x, y + 1]);
                    px[y * w + x] = vecino ? contorno : new Color32(0, 0, 0, 0);
                }
            }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        temporales.Add(tex);

        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        temporales.Add(sp);
        return sp;
    }

    void ActualizarTiempoUI()
    {
        if (cronometro == null) return;
        int s = Mathf.CeilToInt(Mathf.Max(0f, tiempo));
        cronometro.text = "TIEMPO  " + (s / 60).ToString("00") + ":" + (s % 60).ToString("00");

        barraRelleno.color = tiempo <= tiempoCorazon2 ? new Color32(235, 70, 70, 255)
                           : tiempo <= tiempoCorazon1 ? new Color32(250, 200, 60, 255)
                           : new Color32(80, 220, 140, 255);
        barraRect.anchorMax = new Vector2(Mathf.Clamp01(tiempo / tiempoTotal), 1f);

        // latido del reloj en los ultimos segundos
        if (jugando && tiempo <= tiempoCorazon2)
        {
            float fr = tiempo - Mathf.Floor(tiempo);
            cronoRT.localScale = Vector3.one * (1f + 0.12f * fr);
        }
        else cronoRT.localScale = Vector3.one;
    }

    void QuitarCorazon()
    {
        if (vidas <= 0) return;
        vidas--;
        if (vidas < corazones.Count)
        {
            corazones[vidas].rectTransform.localScale = Vector3.one;
            StartCoroutine(AnimCorazon(corazones[vidas]));
        }
        StartCoroutine(FlashRojo());
    }

    IEnumerator AnimCorazon(Image im)
    {
        var rt = im.rectTransform;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
        {
            rt.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(t * Mathf.PI));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 14f * (1f - t));
            if (t > 0.4f) im.sprite = corazonVacio;
            yield return null;
        }
        im.sprite = corazonVacio;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    IEnumerator FlashRojo()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
        {
            flash.color = new Color(1f, 0.1f, 0.1f, 0.35f * (1f - t));
            yield return null;
        }
        flash.color = new Color(1f, 0.1f, 0.1f, 0f);
    }

    // ───────────────────────── DERROTA ─────────────────────────

    void Perder()
    {
        if (!jugando) return;
        jugando = false;
        cronoRT.localScale = Vector3.one;
        if (alPerder != null) alPerder.Invoke();
        StartCoroutine(MostrarDerrota());
    }

    IEnumerator MostrarDerrota()
    {
        yield return new WaitForSecondsRealtime(0.7f);

        var ov = NuevoUI("Derrota", canvasRT);
        Estirar(ov, 0);
        var velo = Img(ov, new Color(0.02f, 0.03f, 0.08f, 0.78f));
        velo.raycastTarget = true;                       // bloquea las piezas de abajo
        var cg = ov.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        var cuadro = NuevoUI("Cuadro", ov);
        Centrar(cuadro, Vector2.zero, new Vector2(900f, 540f));
        Img(cuadro, colorMarco);
        var b = NuevoUI("Borde", cuadro);
        Estirar(b, 8);
        Img(b, new Color32(18, 24, 44, 255));
        var p = NuevoUI("Panel", b);
        Estirar(p, 6);
        Img(p, new Color32(26, 38, 66, 245));

        // Corazon roto (vacio) grande
        var hRT = NuevoUI("CorazonGrande", p);
        Centrar(hRT, new Vector2(0f, 165f), new Vector2(110f, 100f));
        var hIm = hRT.gameObject.AddComponent<Image>();
        hIm.sprite = corazonVacio;
        hIm.raycastTarget = false;

        var tit = CrearTexto("Titulo", p, 64, new Color32(255, 120, 120, 255), TextAlignmentOptions.Center);
        Centrar(tit.rectTransform, new Vector2(0f, 60f), new Vector2(820f, 90f));
        tit.characterSpacing = 4f;
        tit.text = "SE ACABÓ EL TIEMPO";

        var sub = CrearTexto("Sub", p, 36, new Color32(235, 240, 228, 255), TextAlignmentOptions.Center);
        Centrar(sub.rectTransform, new Vector2(0f, -22f), new Vector2(820f, 60f));
        sub.fontStyle = FontStyles.Normal;
        sub.text = "Colocaste " + colocadas + " de " + piezas.Count + " piezas";

        CrearBoton("REINTENTAR", p, new Vector2(-200f, -150f), new Vector2(350f, 96f), new Color32(38, 142, 108, 255), Reintentar);
        CrearBoton("SALIR", p, new Vector2(200f, -150f), new Vector2(350f, 96f), new Color32(190, 70, 80, 255), Salir);

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
        {
            cg.alpha = t;
            cuadro.localScale = Vector3.one * (0.85f + 0.15f * Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cg.alpha = 1f;
        cuadro.localScale = Vector3.one;
    }

    void CrearBoton(string texto, Transform padre, Vector2 pos, Vector2 tam, Color32 color, UnityAction accion)
    {
        var rt = NuevoUI("Boton" + texto, padre);
        Centrar(rt, pos, tam);
        var borde = rt.gameObject.AddComponent<Image>();
        borde.color = new Color32(18, 24, 44, 255);

        var inner = NuevoUI("Fondo", rt);
        Estirar(inner, 5);
        var im = inner.gameObject.AddComponent<Image>();
        im.color = color;

        // brillo en la mitad de arriba del boton
        var luz = NuevoUI("Brillo", inner);
        luz.anchorMin = new Vector2(0f, 0.55f);
        luz.anchorMax = new Vector2(1f, 1f);
        luz.offsetMin = luz.offsetMax = Vector2.zero;
        Img(luz, new Color(1f, 1f, 1f, 0.14f));

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = borde;
        btn.onClick.AddListener(accion);

        var t = CrearTexto("Texto", inner, 42, Color.white, TextAlignmentOptions.Center);
        Estirar(t.rectTransform, 0);
        t.characterSpacing = 3f;
        t.text = texto;
    }

    void Reintentar()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Salir()
    {
        if (!string.IsNullOrEmpty(escenaDeRegreso)) SceneManager.LoadScene(escenaDeRegreso);
        else Debug.LogWarning("RompecabezasManager: falta 'Escena De Regreso' (el nombre de tu escena principal).");
    }

    // ───────────────────────── PIEZAS ─────────────────────────

    void ConstruirPiezas()
    {
        Color32[] src = LeerPixels(imagen, out legible);
        temporales.Add(legible);
        int imgW = legible.width, imgH = legible.height;
        int cw = imgW / columnas, ch = imgH / filas;     // tamano de cada celda en px de la imagen
        float rad = Mathf.Min(cw, ch) * tamPestana;
        int pad = Mathf.CeilToInt(rad * 2.15f) + grosorBorde + 2;

        // Tamano y posicion del tablero dentro de la zona de juego
        Vector2 mesaTam = mesa.rect.size;
        float escala = (mesaTam.y * tamanoTablero) / (filas * ch);
        escala = Mathf.Min(escala, (mesaTam.x * anchoTablero) / (columnas * cw));
        Vector2 tabTam = new Vector2(columnas * cw, filas * ch) * escala;
        Vector2 centro = Vector2.zero;
        Vector2 origen = centro - tabTam * 0.5f;
        var tabRect = new Rect(origen, tabTam);
        var uv = new Rect(0f, 0f, (float)(columnas * cw) / imgW, (float)(filas * ch) / imgH);

        // Borde claro + fondo oscuro + guia tenue de la imagen
        var tb = NuevoUI("TableroBorde", mesa);
        Centrar(tb, centro, tabTam + new Vector2(16f, 16f));
        Img(tb, new Color(1f, 1f, 1f, 0.3f));

        var tf = NuevoUI("TableroFondo", mesa);
        Centrar(tf, centro, tabTam);
        Img(tf, new Color(0f, 0f, 0f, 0.4f));

        var guia = NuevoUI("Guia", mesa);
        Centrar(guia, centro, tabTam);
        var guiaRaw = guia.gameObject.AddComponent<RawImage>();
        guiaRaw.texture = legible;
        guiaRaw.uvRect = uv;
        guiaRaw.color = new Color(1f, 1f, 1f, 0.22f);
        guiaRaw.raycastTarget = false;

        piezasRoot = NuevoUI("Piezas", mesa);
        Estirar(piezasRoot, 0);

        // Pestanas: una por cada borde interior, compartida entre las dos piezas vecinas
        int[,] ver = new int[columnas - 1, filas];
        float[,] verO = new float[columnas - 1, filas];
        int[,] hor = new int[columnas, filas - 1];
        float[,] horO = new float[columnas, filas - 1];
        for (int c = 0; c < columnas - 1; c++)
            for (int r = 0; r < filas; r++)
            {
                ver[c, r] = Random.value < 0.5f ? 1 : -1;
                verO[c, r] = Random.Range(-variacionPestana, variacionPestana);
            }
        for (int c = 0; c < columnas; c++)
            for (int r = 0; r < filas - 1; r++)
            {
                hor[c, r] = Random.value < 0.5f ? 1 : -1;
                horO[c, r] = Random.Range(-variacionPestana, variacionPestana);
            }

        for (int r = 0; r < filas; r++)
        {
            for (int c = 0; c < columnas; c++)
            {
                var b = new Borde();
                if (c < columnas - 1) { b.der = ver[c, r]; b.offDer = verO[c, r] * ch; }
                if (c > 0) { b.izq = -ver[c - 1, r]; b.offIzq = verO[c - 1, r] * ch; }
                if (r < filas - 1) { b.aba = hor[c, r]; b.offAba = horO[c, r] * cw; }
                if (r > 0) { b.arr = -hor[c, r - 1]; b.offArr = horO[c, r - 1] * cw; }

                Texture2D tex = CrearTexturaPieza(src, imgW, imgH, c, r, b, cw, ch, pad, rad);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                temporales.Add(sprite);

                // Raiz de la pieza (sombra + imagen), centrada en el centro de su celda
                var rt = NuevoUI("Pieza_" + c + "_" + r, piezasRoot);
                Vector2 tamPieza = new Vector2(tex.width, tex.height) * escala;
                Vector2 destino = origen + new Vector2((c + 0.5f) * cw, (filas - r - 0.5f) * ch) * escala;
                Centrar(rt, Vector2.zero, tamPieza);

                var sombra = NuevoUI("Sombra", rt);
                Estirar(sombra, 0);
                var imS = sombra.gameObject.AddComponent<Image>();
                imS.sprite = sprite;
                imS.color = new Color(0f, 0f, 0f, 0.38f);
                imS.raycastTarget = false;

                var imgRT = NuevoUI("Imagen", rt);
                Estirar(imgRT, 0);
                var im = imgRT.gameObject.AddComponent<Image>();
                im.sprite = sprite;
                im.alphaHitTestMinimumThreshold = 0.3f;   // solo se agarra por la parte visible

                var p = rt.gameObject.AddComponent<PiezaRompecabezas>();
                p.Iniciar(this, rt, piezasRoot, sombra, im, destino);
                rt.anchoredPosition = PosicionInicial(tamPieza, mesaTam, tabRect, destino);
                piezas.Add(p);
            }
        }

        // Imagen completa que aparece al terminar (tapa las costuras)
        completa = NuevoUI("Completa", piezasRoot);
        Centrar(completa, centro, tabTam);
        completaRaw = completa.gameObject.AddComponent<RawImage>();
        completaRaw.texture = legible;
        completaRaw.uvRect = uv;
        completaRaw.color = new Color(1f, 1f, 1f, 0f);
        completaRaw.raycastTarget = false;

        ActualizarContador();
    }

    // Reparte las piezas por la zona de juego, de preferencia fuera del tablero (a los lados)
    Vector2 PosicionInicial(Vector2 tamPieza, Vector2 mesaTam, Rect tablero, Vector2 destino)
    {
        Vector2 lim = mesaTam * 0.5f - tamPieza * 0.45f;
        var zonaTablero = new Rect(tablero.position - tamPieza * 0.3f, tablero.size + tamPieza * 0.6f);
        Vector2 p = Vector2.zero;
        for (int i = 0; i < 60; i++)
        {
            p = new Vector2(Random.Range(-lim.x, lim.x), Random.Range(-lim.y, lim.y));
            bool fuera = !zonaTablero.Contains(p);
            bool lejos = Vector2.Distance(p, destino) > distanciaIman * 2f;
            if (lejos && (fuera || i > 40)) break;
        }
        return p;
    }

    // Genera la textura de una pieza: recorta su celda de la imagen y le agrega/quita pestanas
    Texture2D CrearTexturaPieza(Color32[] src, int imgW, int imgH, int c, int r, Borde b, int cw, int ch, int pad, float rad)
    {
        int tw = cw + pad * 2, th = ch + pad * 2;
        var m = new bool[tw * th];

        for (int j = 0; j < th; j++)
        {
            float fy = j - pad + 0.5f;
            for (int i = 0; i < tw; i++)
            {
                float fx = i - pad + 0.5f;
                bool d = fx >= 0f && fx < cw && fy >= 0f && fy < ch;
                d = Aplicar(d, b.der, fx - cw, fy - ch * 0.5f - b.offDer, rad);
                d = Aplicar(d, b.izq, -fx, fy - ch * 0.5f - b.offIzq, rad);
                d = Aplicar(d, b.arr, fy - ch, fx - cw * 0.5f - b.offArr, rad);
                d = Aplicar(d, b.aba, -fy, fx - cw * 0.5f - b.offAba, rad);
                m[j * tw + i] = d;
            }
        }

        var px = new Color32[tw * th];
        int x0 = c * cw;
        int y0 = (filas - 1 - r) * ch;                   // la imagen va de abajo hacia arriba
        var oscuro = new Color32(22, 24, 44, 255);
        int g = Mathf.Max(0, grosorBorde);               // 0 = piezas sin contorno

        for (int j = 0; j < th; j++)
        {
            for (int i = 0; i < tw; i++)
            {
                int k = j * tw + i;
                if (!m[k]) { px[k] = new Color32(0, 0, 0, 0); continue; }

                int sx = Mathf.Clamp(x0 + i - pad, 0, imgW - 1);
                int sy = Mathf.Clamp(y0 + j - pad, 0, imgH - 1);
                Color32 col = src[sy * imgW + sx];
                col.a = 255;
                if (g > 0 && (!Dentro(m, tw, th, i - g, j) || !Dentro(m, tw, th, i + g, j) ||
                               !Dentro(m, tw, th, i, j - g) || !Dentro(m, tw, th, i, j + g)))
                    col = Color32.Lerp(col, oscuro, 0.78f);   // contorno de la pieza
                px[k] = col;
            }
        }

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply();
        temporales.Add(tex);
        return tex;
    }

    static bool Dentro(bool[] m, int tw, int th, int i, int j)
    {
        return i >= 0 && j >= 0 && i < tw && j < th && m[j * tw + i];
    }

    // u = distancia al borde (positiva hacia afuera), v = posicion a lo largo del borde
    static bool Aplicar(bool dentro, int tab, float u, float v, float r)
    {
        if (tab > 0 && EnForma(u, v, r)) return true;     // pestana: agrega
        if (tab < 0 && EnForma(-u, v, r)) return false;   // hueco: quita (misma forma, hacia adentro)
        return dentro;
    }

    // Cuello + cabeza redonda, como una pestana de rompecabezas
    static bool EnForma(float u, float v, float r)
    {
        if (u < 0f) return false;
        float d = r * 1.15f, cuello = r * 0.55f;
        if (u <= d && Mathf.Abs(v) <= cuello) return true;
        float du = u - d;
        return du * du + v * v <= r * r;
    }

    // Copia la imagen a una textura legible sin importar como este importada
    static Color32[] LeerPixels(Texture2D t, out Texture2D legible)
    {
        var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(t, rt);
        var previo = RenderTexture.active;
        RenderTexture.active = rt;
        legible = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false, false);
        legible.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
        legible.Apply();
        RenderTexture.active = previo;
        RenderTexture.ReleaseTemporary(rt);
        return legible.GetPixels32();
    }

    // ───────────────────────── JUEGO ─────────────────────────

    public void Soltada(PiezaRompecabezas p)
    {
        if (!jugando) return;
        if (Vector2.Distance(p.Rect.anchoredPosition, p.Destino) > distanciaIman) return;

        p.Fijar();
        colocadas++;
        ActualizarContador();
        if (colocadas >= piezas.Count)
        {
            jugando = false;               // se detiene el reloj
            cronoRT.localScale = Vector3.one;
            StartCoroutine(Final());
        }
    }

    void ActualizarContador()
    {
        if (contador == null) return;
        contador.text = "PIEZAS  " + colocadas + " / " + piezas.Count;
    }

    IEnumerator Final()
    {
        jugando = false;
        contador.text = "COMPLETADO";
        yield return new WaitForSecondsRealtime(0.35f);
        completa.SetAsLastSibling();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
        {
            completaRaw.color = new Color(1f, 1f, 1f, t);
            yield return null;
        }
        completaRaw.color = Color.white;
        yield return new WaitForSecondsRealtime(1.3f);

        // Para que la profe diga sus Victory Lines al regresar (lo lee NPCDialogue)
        EstadoJuego.minijuegoGanado = true;
        EstadoJuego.minijuegoGanadoEscena = SceneManager.GetActiveScene().name;

        // Medalla negra del rompecabezas (se ve en el marcador del juego principal)
        Medallas.Ganar(Medallas.Rompecabezas);

        if (alCompletar != null) alCompletar.Invoke();
        MostrarMenu(true, "PIEZAS: " + colocadas + " / " + piezas.Count);
    }

    // Menú final (igual al de los otros minijuegos): REINTENTAR / SALIR
    void MostrarMenu(bool gano, string subtitulo)
    {
        if (menu == null) menu = MenuFinal.Crear(fuente, Reintentar, Salir);
        menu.Mostrar(gano, subtitulo, gano ? Medallas.SpriteDe(Medallas.Rompecabezas) : null);
    }

    // ───────────────────────── AYUDAS DE UI ─────────────────────────

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, float tam, Color color, TextAlignmentOptions alineacion)
    {
        var rt = NuevoUI(nombre, padre);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.fontSize = tam;
        t.fontStyle = FontStyles.Bold;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        t.outlineWidth = 0.2f;                           // contorno oscuro para que se lea sobre cualquier fondo
        t.outlineColor = new Color32(10, 14, 30, 255);
        return t;
    }

    static RectTransform NuevoUI(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return go.GetComponent<RectTransform>();
    }

    static void Estirar(RectTransform rt, float margen)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen);
        rt.offsetMax = new Vector2(-margen, -margen);
    }

    static void Centrar(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    static Image Img(RectTransform rt, Color color)
    {
        var im = rt.gameObject.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        return im;
    }
}

// Arrastre de una pieza (se agrega por codigo desde RompecabezasManager)
public class PiezaRompecabezas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    RompecabezasManager manager;
    RectTransform rt, zona, sombra;
    Image imagen;
    Vector2 destino, desfase;
    bool arrastrando, colocada;

    public RectTransform Rect { get { return rt; } }
    public Vector2 Destino { get { return destino; } }

    public void Iniciar(RompecabezasManager m, RectTransform propio, RectTransform area, RectTransform sombraRT, Image img, Vector2 dest)
    {
        manager = m; rt = propio; zona = area; sombra = sombraRT; imagen = img; destino = dest;
        Sombra(7f);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (colocada || !manager.Jugando) return;
        arrastrando = true;
        rt.SetAsLastSibling();                           // la pieza agarrada queda encima de todas
        desfase = rt.anchoredPosition - PuntoLocal(e);
        Sombra(16f);                                     // se "levanta"
    }

    public void OnDrag(PointerEventData e)
    {
        if (colocada || !arrastrando || !manager.Jugando) return;
        Vector2 p = PuntoLocal(e) + desfase;
        Vector2 lim = zona.rect.size * 0.5f - rt.sizeDelta * 0.3f;
        p.x = Mathf.Clamp(p.x, -lim.x, lim.x);
        p.y = Mathf.Clamp(p.y, -lim.y, lim.y);
        rt.anchoredPosition = p;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (colocada || !arrastrando) return;
        arrastrando = false;
        Sombra(7f);
        manager.Soltada(this);
    }

    public void Fijar()
    {
        colocada = true;
        rt.anchoredPosition = destino;
        sombra.gameObject.SetActive(false);
        imagen.raycastTarget = false;
        rt.SetAsFirstSibling();                          // las piezas fijas quedan debajo de las sueltas
        StartCoroutine(Pop());
    }

    Vector2 PuntoLocal(PointerEventData e)
    {
        Vector2 p;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(zona, e.position, e.pressEventCamera, out p);
        return p;
    }

    void Sombra(float d)
    {
        sombra.offsetMin = new Vector2(d, -d);
        sombra.offsetMax = new Vector2(d, -d);
    }

    IEnumerator Pop()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
        {
            rt.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}