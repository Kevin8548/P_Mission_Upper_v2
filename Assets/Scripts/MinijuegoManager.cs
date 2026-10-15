using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class MinijuegoManager : MonoBehaviour
{
    public bool Jugando { get; private set; } = true;

    string escenaDeRegreso;
    TMP_FontAsset fuente;
    Sprite iconoEstrella, iconoFallo;
    int estrellas, total;

    Transform raiz;
    TMP_Text textoContador;
    GameObject avisoObjeto;
    TMP_Text textoAviso;
    float avisoHasta;
    float escalaContador = 1f;

    GameObject panelFin;
    TMP_Text finTitulo, finSubtitulo;
    Image finIcono;
    readonly TMP_Text[] opciones = new TMP_Text[2];
    readonly RectTransform[] opcionesRT = new RectTransform[2];
    readonly string[] etiquetas = { "REINTENTAR", "SALIR" };
    int seleccion;
    float menuDesde;

    static readonly Color Verde = new Color(0.18f, 0.62f, 0.35f);
    static readonly Color FondoOscuro = new Color(0.01f, 0.07f, 0.03f, 0.750f);
    static readonly Color Dorado = new Color(1f, 0.85f, 0.2f);

    public void Iniciar(int estrellasTotales, string escena, TMP_FontAsset fuenteUI, Sprite estrella, Sprite fallo)
    {
        total = estrellasTotales;
        escenaDeRegreso = escena;
        fuente = fuenteUI;
        iconoEstrella = estrella;
        iconoFallo = fallo;
        ConstruirUI();
    }

    // ---------------- Juego ----------------

    public void SumarEstrella()
    {
        if (!Jugando) return;
        estrellas++;
        ActualizarContador();
        escalaContador = 1.4f;
    }

    public void Perder()
    {
        if (!Jugando) return;
        Jugando = false;
        MostrarFin("¡FALLASTE!", "ESTRELLAS: " + estrellas + "/" + total, iconoFallo, Color.white);
    }

    public void IntentarGanar()
    {
        if (!Jugando) return;

        if (estrellas >= total)
        {
            Jugando = false;
            EstadoJuego.minijuegoGanado = true;
            MostrarFin("¡OBJETIVO CUMPLIDO!", "ESTRELLAS: " + estrellas + "/" + total, iconoEstrella, Color.white);
        }
        else
        {
            int faltan = total - estrellas;
            textoAviso.text = faltan == 1 ? "TE FALTA 1 ESTRELLA" : "TE FALTAN " + faltan + " ESTRELLAS";
            avisoObjeto.SetActive(true);
            avisoHasta = Time.time + 2f;
        }
    }

    void ActualizarContador()
    {
        textoContador.text = estrellas + "/" + total;
    }

    // ---------------- Actualización ----------------

    void Update()
    {
        if (textoContador != null)
        {
            escalaContador = Mathf.Lerp(escalaContador, 1f, Time.deltaTime * 10f);
            textoContador.transform.localScale = Vector3.one * escalaContador;
        }

        if (Jugando)
        {
            if (avisoObjeto != null && avisoObjeto.activeSelf && Time.time > avisoHasta)
                avisoObjeto.SetActive(false);
            return;
        }

        ManejarMenu();
    }

    void ManejarMenu()
    {
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Seleccionar(0);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Seleccionar(1);

            bool confirmar = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame
                          || k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame;
            if (confirmar && Time.time > menuDesde + 0.4f)
            {
                Confirmar();
                return;
            }
        }

        Mouse m = Mouse.current;
        if (m != null)
        {
            Vector2 p = m.position.ReadValue();
            for (int i = 0; i < opcionesRT.Length; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(opcionesRT[i], p, null))
                {
                    if (seleccion != i) Seleccionar(i);
                    if (m.leftButton.wasPressedThisFrame && Time.time > menuDesde + 0.4f)
                    {
                        Confirmar();
                        return;
                    }
                }
            }
        }
    }

    void Seleccionar(int i)
    {
        seleccion = i;
        for (int n = 0; n < opciones.Length; n++)
        {
            opciones[n].text = etiquetas[n];
            opciones[n].color = Color.white;
            opciones[n].transform.localScale = Vector3.one * (n == seleccion ? 1.12f : 1f);
        }
    }

    void Confirmar()
    {
        if (seleccion == 0)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        else
            SceneManager.LoadScene(escenaDeRegreso);
    }

    void MostrarFin(string titulo, string subtitulo, Sprite icono, Color colorTitulo)
    {
        finTitulo.text = titulo;
        finTitulo.color = colorTitulo;
        finSubtitulo.text = subtitulo;
        finIcono.sprite = icono;
        finIcono.enabled = icono != null;
        avisoObjeto.SetActive(false);
        panelFin.SetActive(true);
        menuDesde = Time.time;
        Seleccionar(0);
    }

    // ---------------- Construcción de la interfaz ----------------

    RectTransform CrearPanel(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    TMP_Text CrearTexto(string nombre, Transform padre, string contenido, float tamano, Color color, TextAlignmentOptions alineacion)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenido;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = alineacion;
        t.raycastTarget = false;
        if (fuente != null) t.font = fuente;
        return t;
    }

    static void Ajustar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = pivote;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    static void Estirar(RectTransform rt)
    {
        Ajustar(rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    void ConstruirUI()
    {
        GameObject cgo = new GameObject("CanvasMinijuego", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        raiz = cgo.transform;

        // Menú final: velo negro transparente sobre el juego (la barra de arriba queda encima)
        RectTransform fondo = CrearPanel("MenuFin", raiz, new Color(0f, 0f, 0f, 0.8f));
        Estirar(fondo);
        panelFin = fondo.gameObject;

        RectTransform ic = CrearPanel("Icono", fondo, Color.white);
        Ajustar(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(150, 150));
        finIcono = ic.GetComponent<Image>();
        finIcono.preserveAspect = true;

        finTitulo = CrearTexto("Titulo", fondo, "", 96f, Color.white, TextAlignmentOptions.Center);
        Ajustar(finTitulo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(1500, 130));

        finSubtitulo = CrearTexto("Subtitulo", fondo, "", 56f, Color.white, TextAlignmentOptions.Center);
        Ajustar(finSubtitulo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1200, 90));

        for (int i = 0; i < opciones.Length; i++)
        {
            opciones[i] = CrearTexto("Opcion" + i, fondo, etiquetas[i], 52f, Color.white, TextAlignmentOptions.Center);
            Ajustar(opciones[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -130 - i * 85), new Vector2(600, 75));
            opcionesRT[i] = opciones[i].rectTransform;
        }


        // Marco verde alrededor de la pantalla (abajo, izquierda, derecha)
        RectTransform marcoAbajo = CrearPanel("MarcoAbajo", raiz, Verde);
        Ajustar(marcoAbajo, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 8));
        RectTransform marcoIzq = CrearPanel("MarcoIzquierda", raiz, Verde);
        Ajustar(marcoIzq, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, 0));
        RectTransform marcoDer = CrearPanel("MarcoDerecha", raiz, Verde);
        Ajustar(marcoDer, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(8, 0));

        // Barra superior
        RectTransform barra = CrearPanel("Barra", raiz, new Color(0.01f, 0.08f, 0.04f, 0.94f));
        Ajustar(barra, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 100));
        RectTransform linea = CrearPanel("LineaBarra", barra, Verde);
        Ajustar(linea, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 8));

        TMP_Text titulo = CrearTexto("Titulo", barra, "DALE INSTRUCCIONES AL ROBOT", 52f, Color.white, TextAlignmentOptions.Center);
        Estirar(titulo.rectTransform);

        // Contador de estrellas
        RectTransform icono = CrearPanel("IconoEstrella", barra, Color.white);
        Ajustar(icono, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(72, 72));
        Image iconoImg = icono.GetComponent<Image>();
        iconoImg.sprite = iconoEstrella;
        iconoImg.preserveAspect = true;

        textoContador = CrearTexto("Contador", barra, "0/" + total, 60f, Dorado, TextAlignmentOptions.MidlineLeft);
        Ajustar(textoContador.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(300, 90));
        textoContador.rectTransform.pivot = new Vector2(0f, 0.5f);

        // Aviso temporal (por ejemplo "te faltan estrellas")
        RectTransform aviso = CrearPanel("Aviso", raiz, new Color(0.01f, 0.08f, 0.04f, 0.95f));
        Ajustar(aviso, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 90));
        textoAviso = CrearTexto("TextoAviso", aviso, "", 44f, Dorado, TextAlignmentOptions.Center);
        Estirar(textoAviso.rectTransform);
        avisoObjeto = aviso.gameObject;
        avisoObjeto.SetActive(false);

        panelFin.SetActive(false);
        ActualizarContador();
    }
}
