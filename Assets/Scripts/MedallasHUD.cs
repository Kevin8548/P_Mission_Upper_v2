using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Marcador de medallas para el juego principal (donde Leo camina el recorrido virtual).
// Muestra "MEDALLAS: 1/4" arriba a la izquierda. Al ganar un minijuego, la medalla aparece
// a mitad de pantalla, suena un sonidito al salir del centro y vuela hacia el contador.
// Al llegar a 4/4, espera 5 s sin diálogo y lanza el storyboard final.
public class MedallasHUD : MonoBehaviour
{
    [Header("Opcional: la fuente pixel art de tus otros minijuegos")]
    public TMP_FontAsset fuente;

    [Header("Animación de la medalla ganada")]
    public Vector2 tamanoMedalla = new Vector2(170f, 340f);
    public float duracionAparece = 0.45f;
    public float duracionEspera = 0.9f;
    public float duracionVuelo = 0.8f;

    [Header("Sonido")]
    public AudioClip sonidoMedalla; // opcional; si está vacío usa Resources/Medallas/Medalla_Sonido
    [Range(0f, 1f)] public float volumenSonido = 0.8f;

    [Header("Final de la historia")]
    public float esperaFinal = 5f; // segundos SIN diálogo antes de lanzar el storyboard

    static readonly Color Dorado = new Color(1f, 0.85f, 0.2f);

    TMP_Text textoContador;
    RectTransform canvasRT, medallaRT;
    Image medallaImg;
    AudioSource audioSource;
    int mostrado;
    float escalaContador = 1f;

    enum Fase { Nada, Aparece, Espera, Vuela }
    Fase fase = Fase.Nada;
    float t;
    Vector2 inicioVuelo, destino;

    // Se crea solo en cualquier escena que tenga a Leo (no hace falta agregarlo a mano).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        SceneManager.sceneLoaded += (escena, modo) => CrearSiHaceFalta();
        CrearSiHaceFalta();
    }

    static void CrearSiHaceFalta()
    {
        if (FindObjectOfType<MedallasHUD>() != null) return;
        if (FindObjectOfType<LeoController>() == null) return;
        new GameObject("MedallasHUD").AddComponent<MedallasHUD>();
    }

    void Start()
    {
        Debug.Log("[MedallasHUD v5 - con sonido] escena " + SceneManager.GetActiveScene().name +
                  " | medallas: " + Medallas.Cantidad + "/" + Medallas.Total);
        Construir();

        // Sonido de la medalla (2D)
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (sonidoMedalla == null)
            sonidoMedalla = Resources.Load<AudioClip>("Medallas/Medalla_Sonido");
        if (sonidoMedalla == null)
            Debug.LogWarning("[MedallasHUD] No encuentro Assets/Resources/Medallas/Medalla_Sonido (wav/mp3/ogg).");

        // PRUEBA: descomenta la siguiente línea para simular que ganaste las 4 medallas.
        // Vuelve a comentarla cuando termines de probar.
        // foreach (string id in Medallas.Todas) Medallas.Ganar(id);

        string nueva = Medallas.nueva;
        Medallas.nueva = null;
        Sprite sp = string.IsNullOrEmpty(nueva) ? null : Medallas.SpriteDe(nueva);
        if (sp != null)
        {
            mostrado = Mathf.Max(0, Medallas.Cantidad - 1);
            medallaImg.sprite = sp;
            medallaRT.anchoredPosition = Vector2.zero;
            medallaRT.localScale = Vector3.zero;
            medallaRT.gameObject.SetActive(true);
            fase = Fase.Aparece;
            t = 0f;
        }
        else
        {
            mostrado = Medallas.Cantidad;
        }
        ActualizarTexto();
        RevisarFinal();
    }

    void Update()
    {
        escalaContador = Mathf.Lerp(escalaContador, 1f, Time.deltaTime * 6f);
        textoContador.transform.localScale = Vector3.one * escalaContador;

        switch (fase)
        {
            case Fase.Aparece:
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / duracionAparece);
                    medallaRT.localScale = Vector3.one * EaseOutBack(k);
                    if (k >= 1f) { fase = Fase.Espera; t = 0f; }
                    break;
                }
            case Fase.Espera:
                {
                    t += Time.deltaTime;
                    // se mece suavemente mientras espera
                    medallaRT.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 6f) * 8f);
                    if (t >= duracionEspera)
                    {
                        inicioVuelo = medallaRT.anchoredPosition;
                        destino = new Vector2(-canvasRT.rect.width / 2f + 150f, canvasRT.rect.height / 2f - 64f);
                        Sonar(); // suena al salir del centro rumbo al contador
                        fase = Fase.Vuela;
                        t = 0f;
                    }
                    break;
                }
            case Fase.Vuela:
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / duracionVuelo);
                    float e = k * k; // arranca lento y se acelera
                    Vector2 pos = Vector2.Lerp(inicioVuelo, destino, e);
                    pos.x -= Mathf.Sin(k * Mathf.PI) * 120f; // pequeña curva en el camino
                    medallaRT.anchoredPosition = pos;
                    medallaRT.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, e);
                    if (k >= 1f)
                    {
                        medallaRT.gameObject.SetActive(false);
                        mostrado = Medallas.Cantidad;
                        ActualizarTexto();
                        escalaContador = 1.6f; // rebote del contador al recibir la medalla
                        fase = Fase.Nada;
                        RevisarFinal();
                    }
                    break;
                }
        }
    }

    void Sonar()
    {
        if (sonidoMedalla != null && audioSource != null)
            audioSource.PlayOneShot(sonidoMedalla, volumenSonido);
    }

    // ---------------- Final de la historia ----------------

    void RevisarFinal()
    {
        if (fase != Fase.Nada) return; // todavía hay una medalla volando
        if (Medallas.Cantidad < Medallas.Total || Medallas.finalMostrado) return;

        Medallas.finalMostrado = true;
        StartCoroutine(SecuenciaFinal());
    }

    IEnumerator SecuenciaFinal()
    {
        // un respiro para que alcance a abrirse el diálogo de victoria
        yield return new WaitForSeconds(1f);

        // cuenta 5 s seguidos SIN diálogo; si se abre uno, reinicia la cuenta
        float libre = 0f;
        while (libre < esperaFinal)
        {
            bool hablando = DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
            if (hablando) libre = 0f;
            else libre += Time.deltaTime;
            yield return null;
        }

        StoryboardFinal.Mostrar(fuente);
    }

    // ---------------- Animación ----------------

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    void ActualizarTexto()
    {
        textoContador.text = "MEDALLAS: " + mostrado + "/" + Medallas.Total;
    }

    // ---------------- Interfaz construida por código ----------------

    void Construir()
    {
        GameObject cgo = new GameObject("CanvasMedallas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        canvasRT = cgo.GetComponent<RectTransform>();

        // Texto "MEDALLAS: x/4", sin panel de fondo (con contorno negro para que se lea)
        GameObject tgo = new GameObject("Contador", typeof(RectTransform));
        tgo.transform.SetParent(cgo.transform, false);
        TextMeshProUGUI txt = tgo.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 56f;
        txt.color = Dorado;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.raycastTarget = false;
        txt.outlineWidth = 0.25f;
        txt.outlineColor = new Color32(0, 0, 0, 255);
        if (fuente != null) txt.font = fuente;
        RectTransform trt = txt.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(40f, -64f);
        trt.sizeDelta = new Vector2(800f, 90f);
        textoContador = txt;

        // Medalla que vuela (la medalla mide 16x32, o sea 1:2)
        GameObject mgo = new GameObject("MedallaGanada", typeof(RectTransform), typeof(Image));
        mgo.transform.SetParent(cgo.transform, false);
        medallaImg = mgo.GetComponent<Image>();
        medallaImg.preserveAspect = true;
        medallaImg.raycastTarget = false;
        medallaRT = mgo.GetComponent<RectTransform>();
        medallaRT.anchorMin = medallaRT.anchorMax = medallaRT.pivot = new Vector2(0.5f, 0.5f);
        medallaRT.sizeDelta = tamanoMedalla;
        mgo.SetActive(false);
    }
}