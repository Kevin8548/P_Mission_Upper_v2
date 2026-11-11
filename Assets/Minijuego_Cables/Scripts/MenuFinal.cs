using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Menú final de ganar / perder (igual al del laberinto y el lab Oracle):
// medalla grande (si ganas), título, subtítulo y las opciones REINTENTAR / SALIR.
// Lo usan el rompecabezas y el minijuego de cables. Se maneja con W/S o flechas + Enter/Espacio/E, o con el mouse.
public class MenuFinal : MonoBehaviour
{
    Action alReintentar, alSalir;
    TMP_FontAsset fuente;

    GameObject panel;
    Image icono;
    RectTransform iconoRT, tituloRT, subtituloRT;
    TMP_Text titulo, subtitulo;
    readonly TMP_Text[] opciones = new TMP_Text[2];
    readonly RectTransform[] opcionesRT = new RectTransform[2];
    readonly string[] etiquetas = { "REINTENTAR", "SALIR" };
    int seleccion;
    float desde;
    bool visible;

    public static MenuFinal Crear(TMP_FontAsset fuente, Action alReintentar, Action alSalir)
    {
        GameObject go = new GameObject("MenuFinal");
        MenuFinal m = go.AddComponent<MenuFinal>();
        m.fuente = fuente;
        m.alReintentar = alReintentar;
        m.alSalir = alSalir;
        m.Construir();
        return m;
    }

    public void Mostrar(bool gano, string textoSubtitulo, Sprite medalla)
    {
        titulo.text = gano ? "¡OBJETIVO CUMPLIDO!" : "¡FALLASTE!";
        subtitulo.text = textoSubtitulo;

        bool conMedalla = gano && medalla != null;
        icono.sprite = medalla;
        icono.enabled = conMedalla;

        // Con medalla (190x380) el texto baja para que quepa; sin ella queda más centrado
        float dy = conMedalla ? -120f : 40f;
        iconoRT.anchoredPosition = new Vector2(0f, 250f);
        tituloRT.anchoredPosition = new Vector2(0f, 100f + dy);
        subtituloRT.anchoredPosition = new Vector2(0f, -30f + dy);
        for (int i = 0; i < opcionesRT.Length; i++)
            opcionesRT[i].anchoredPosition = new Vector2(0f, -130f - i * 85f + dy);

        panel.SetActive(true);
        visible = true;
        desde = Time.unscaledTime;
        Seleccionar(0);
    }

    public void Ocultar()
    {
        visible = false;
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (!visible) return;

        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Seleccionar(0);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Seleccionar(1);

            bool confirmar = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame
                          || k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame;
            if (confirmar && Time.unscaledTime > desde + 0.4f) { Confirmar(); return; }
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
                    if (m.leftButton.wasPressedThisFrame && Time.unscaledTime > desde + 0.4f) { Confirmar(); return; }
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
        Action a = seleccion == 0 ? alReintentar : alSalir;
        if (a != null) a();
    }

    // ---------------- Interfaz construida por código ----------------

    TMP_Text CrearTexto(string nombre, Transform padre, float tamano, Vector2 pos, Vector2 caja, out RectTransform rt)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = tamano;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (fuente != null) t.font = fuente;
        rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = caja;
        return t;
    }

    void Construir()
    {
        GameObject cgo = new GameObject("CanvasMenuFinal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // encima de todo
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        // Velo oscuro (también bloquea los clics hacia el minijuego que quedó debajo)
        GameObject fgo = new GameObject("Velo", typeof(RectTransform), typeof(Image));
        fgo.transform.SetParent(cgo.transform, false);
        Image velo = fgo.GetComponent<Image>();
        velo.color = new Color(0f, 0f, 0f, 0.88f);
        velo.raycastTarget = true;
        RectTransform vrt = velo.rectTransform;
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        panel = fgo;

        GameObject igo = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        igo.transform.SetParent(fgo.transform, false);
        icono = igo.GetComponent<Image>();
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        iconoRT = icono.rectTransform;
        iconoRT.anchorMin = iconoRT.anchorMax = iconoRT.pivot = new Vector2(0.5f, 0.5f);
        iconoRT.sizeDelta = new Vector2(190f, 380f);

        titulo = CrearTexto("Titulo", fgo.transform, 96f, Vector2.zero, new Vector2(1500f, 130f), out tituloRT);
        subtitulo = CrearTexto("Subtitulo", fgo.transform, 56f, Vector2.zero, new Vector2(1200f, 90f), out subtituloRT);
        for (int i = 0; i < opciones.Length; i++)
            opciones[i] = CrearTexto("Opcion" + i, fgo.transform, 52f, Vector2.zero, new Vector2(600f, 75f), out opcionesRT[i]);

        panel.SetActive(false);
    }
}
