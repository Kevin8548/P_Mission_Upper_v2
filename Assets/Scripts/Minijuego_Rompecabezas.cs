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
// Todo (fondo, recuadro, piezas, arrastre) se construye por codigo.
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

    [Header("Pantalla")]
    [Range(0.4f, 0.85f)] public float alturaTablero = 0.66f;     // alto del tablero respecto al recuadro
    public Color colorFondoArriba = new Color32(16, 32, 61, 255);
    public Color colorFondoAbajo = new Color32(38, 142, 108, 255);
    public Color colorMarco = new Color32(236, 240, 228, 255);
    public Color colorPanel = new Color32(26, 38, 66, 255);
    public TMP_FontAsset fuente;                                 // opcional: la misma fuente de tu DialogueManager

    [Header("Al terminar")]
    public string escenaDeRegreso = "SampleScene";               // nombre de tu escena principal
    public UnityEvent alCompletar;

    [Header("Tiempo (para poder perder)")]
    [Tooltip("Segundos para armar el rompecabezas. 0 = sin límite (entonces nunca se pierde).")]
    public float tiempoLimite = 180f;

    struct Borde
    {
        public int arr, der, aba, izq;                           // 0 plano, 1 pestana hacia afuera, -1 hueco
        public float offArr, offDer, offAba, offIzq;             // desfase de la pestana a lo largo del borde
    }

    RectTransform mesa, piezasRoot, completa;
    RawImage fondoRaw, completaRaw;
    TextMeshProUGUI contador;
    Texture2D legible;
    readonly List<PiezaRompecabezas> piezas = new List<PiezaRompecabezas>();
    readonly List<Object> temporales = new List<Object>();
    int colocadas;
    float scroll;
    float tiempoRestante;
    int ultimoSegundo = -1;
    bool jugando;
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

        ConstruirPantalla();
        yield return null;                 // esperamos un frame para que el Canvas calcule tamanos
        Canvas.ForceUpdateCanvases();
        ConstruirPiezas();
        tiempoRestante = tiempoLimite;
        jugando = true;
        ActualizarContador();
    }

    void Update()
    {
        if (fondoRaw == null) return;
        scroll = (scroll + 0.01f * Time.unscaledDeltaTime) % 1f;   // las nubes se mueven muy despacio
        fondoRaw.uvRect = new Rect(scroll, 0f, 1f, 1f);

        // Cuenta regresiva: si llega a 0 antes de terminar, se pierde
        if (jugando && tiempoLimite > 0f)
        {
            tiempoRestante -= Time.unscaledDeltaTime;
            if (tiempoRestante <= 0f) { tiempoRestante = 0f; Perder(); }
            else
            {
                int seg = Mathf.CeilToInt(tiempoRestante);
                if (seg != ultimoSegundo) { ultimoSegundo = seg; ActualizarContador(); }
            }
        }
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
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Fondo pixel
        var fondo = NuevoUI("Fondo", cgo.transform);
        Estirar(fondo, 0);
        fondoRaw = fondo.gameObject.AddComponent<RawImage>();
        fondoRaw.texture = CrearFondo();
        fondoRaw.raycastTarget = false;

        // Recuadro grande (marco claro > borde oscuro > panel)
        var marco = NuevoUI("Marco", cgo.transform);
        marco.anchorMin = new Vector2(0.05f, 0.06f);
        marco.anchorMax = new Vector2(0.95f, 0.94f);
        marco.offsetMin = marco.offsetMax = Vector2.zero;
        Img(marco, colorMarco);

        var borde = NuevoUI("Borde", marco);
        Estirar(borde, 8);
        Img(borde, new Color32(18, 24, 44, 255));

        var panel = NuevoUI("Panel", borde);
        Estirar(panel, 6);
        Img(panel, colorPanel);

        // Contador de piezas
        var cRT = NuevoUI("Contador", panel);
        cRT.anchorMin = new Vector2(0f, 1f);
        cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot = new Vector2(0.5f, 1f);
        cRT.anchoredPosition = new Vector2(0f, -12f);
        cRT.sizeDelta = new Vector2(0f, 64f);
        contador = cRT.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) contador.font = fuente;
        contador.fontSize = 40;
        contador.fontStyle = FontStyles.Bold;
        contador.alignment = TextAlignmentOptions.Center;
        contador.color = Color.white;
        contador.raycastTarget = false;

        // Zona donde viven tablero y piezas
        mesa = NuevoUI("Mesa", panel);
        Estirar(mesa, 0);
    }

    Texture2D CrearFondo()
    {
        const int w = 240, h = 135;                      // cada pixel = 8 px en 1920x1080
        const int niveles = 7;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        temporales.Add(tex);

        var paleta = new Color32[niveles];
        for (int i = 0; i < niveles; i++)
            paleta[i] = Color.Lerp(colorFondoArriba, colorFondoAbajo, i / (float)(niveles - 1));

        int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        var px = new Color32[w * h];

        // Degradado por bandas planas con costuras tramadas (dither)
        for (int y = 0; y < h; y++)
        {
            float k = 1f - y / (float)(h - 1);           // y = 0 es abajo
            float nivel = k * (niveles - 1);
            int baseIdx = Mathf.FloorToInt(nivel);
            float frac = nivel - baseIdx;
            float f = Mathf.Clamp01((frac - 0.35f) / 0.3f);
            for (int x = 0; x < w; x++)
            {
                float umbral = (bayer[y % 4, x % 4] + 0.5f) / 16f;
                int idx = Mathf.Clamp(baseIdx + (f > umbral ? 1 : 0), 0, niveles - 1);
                px[y * w + x] = paleta[idx];
            }
        }

        // Estrellitas arriba y nubes pixeladas sutiles
        var rnd = new System.Random(12345);
        for (int i = 0; i < 45; i++)
            Aclarar(px, w, h, rnd.Next(0, w), rnd.Next((int)(h * 0.55f), h - 2), 0.55f);
        int[] nubesX = { 20, 70, 120, 170, 215 };
        foreach (int nx in nubesX)
            Nube(px, w, h, nx + rnd.Next(-8, 9), rnd.Next((int)(h * 0.4f), (int)(h * 0.85f)), rnd.Next(22, 37));

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void Nube(Color32[] px, int w, int h, int cx, int cy, int ancho)
    {
        Banda(px, w, h, cx - ancho / 2, cy, ancho, 3, 0.15f);
        Banda(px, w, h, cx - ancho / 3, cy + 3, ancho * 2 / 3, 3, 0.15f);
        Banda(px, w, h, cx - ancho / 6, cy + 6, ancho / 3, 2, 0.15f);
    }

    static void Banda(Color32[] px, int w, int h, int x0, int y0, int bw, int bh, float cant)
    {
        for (int y = y0; y < y0 + bh; y++)
            for (int x = x0; x < x0 + bw; x++)
                Aclarar(px, w, h, x, y, cant);
    }

    static void Aclarar(Color32[] px, int w, int h, int x, int y, float cant)
    {
        if (y < 0 || y >= h) return;
        x = ((x % w) + w) % w;                           // da la vuelta para que el scroll sea continuo
        px[y * w + x] = Color32.Lerp(px[y * w + x], new Color32(255, 255, 255, 255), cant);
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

        // Tamano y posicion del tablero dentro del recuadro
        Vector2 mesaTam = mesa.rect.size;
        float escala = (mesaTam.y * alturaTablero) / (filas * ch);
        escala = Mathf.Min(escala, (mesaTam.x * 0.78f) / (columnas * cw));
        Vector2 tabTam = new Vector2(columnas * cw, filas * ch) * escala;
        Vector2 centro = new Vector2(0f, -mesaTam.y * 0.03f);
        Vector2 origen = centro - tabTam * 0.5f;
        var tabRect = new Rect(origen, tabTam);
        var uv = new Rect(0f, 0f, (float)(columnas * cw) / imgW, (float)(filas * ch) / imgH);

        // Borde claro + fondo oscuro + guia tenue de la imagen
        var tb = NuevoUI("TableroBorde", mesa);
        Centrar(tb, centro, tabTam + new Vector2(14f, 14f));
        Img(tb, new Color(1f, 1f, 1f, 0.25f));

        var tf = NuevoUI("TableroFondo", mesa);
        Centrar(tf, centro, tabTam);
        Img(tf, new Color(0f, 0f, 0f, 0.35f));

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

    // Reparte las piezas por el recuadro, de preferencia fuera del tablero
    Vector2 PosicionInicial(Vector2 tamPieza, Vector2 mesaTam, Rect tablero, Vector2 destino)
    {
        Vector2 lim = mesaTam * 0.5f - tamPieza * 0.45f;
        Vector2 p = Vector2.zero;
        for (int i = 0; i < 40; i++)
        {
            p = new Vector2(Random.Range(-lim.x, lim.x), Random.Range(-lim.y, lim.y));
            bool fuera = !tablero.Contains(p);
            bool lejos = Vector2.Distance(p, destino) > distanciaIman * 2f;
            if (lejos && (fuera || i > 25)) break;
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
        if (colocadas >= piezas.Count) StartCoroutine(Final());
    }

    void ActualizarContador()
    {
        string texto = "PIEZAS  " + colocadas + " / " + piezas.Count;
        if (tiempoLimite > 0f)
        {
            int seg = Mathf.CeilToInt(tiempoRestante);
            texto += "     TIEMPO  " + (seg / 60) + ":" + (seg % 60).ToString("00");
        }
        contador.text = texto;
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

    void Perder()
    {
        if (!jugando) return;
        jugando = false;
        contador.text = "SE ACABÓ EL TIEMPO";
        MostrarMenu(false, "SE ACABÓ EL TIEMPO");
    }

    // Menú final (igual al de los otros minijuegos): REINTENTAR / SALIR
    void MostrarMenu(bool gano, string subtitulo)
    {
        if (menu == null) menu = MenuFinal.Crear(fuente, Reintentar, Salir);
        menu.Mostrar(gano, subtitulo, gano ? Medallas.SpriteDe(Medallas.Rompecabezas) : null);
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

    // ───────────────────────── AYUDAS DE UI ─────────────────────────

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
        if (colocada || !arrastrando) return;
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