using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

// Minijuego de la Profe Eli (Laboratorio Oracle): arrastra cada tarjeta de datos a su tabla.
// Se construye solo al dar Play: no hay que crear nada de interfaz a mano.
public class OracleGame : MonoBehaviour
{
    [Header("Opcional: fuente pixel art (TMP Font Asset)")]
    public TMP_FontAsset fuente;
    [Header("Opcional: sprite de la Profe Eli (carpeta Sprites_Eli)")]
    public Sprite eliSprite;
    [Header("Nombre exacto de la escena a la que regresa SALIR")]
    public string escenaDeRegreso = "SampleScene";

    // ---------------- Datos del juego ----------------
    static readonly string[] Nombres = { "Evelyn", "Esmeralda", "Kevin", "Larisa", "Miguel Ángel", "Brandon", "Abdiel", "Sahid", "Brian", "Alexa", "Gael", "Rodrigo", "Ared", "Emilio", "Alfredo", "Verónica", "Miguel", "Yamileth" };
    static readonly string[] Paises = { "México", "Brasil", "Argentina", "Japón", "Canadá", "Chile", "España", "Perú", "Francia", "Italia", "Colombia", "Egipto" };
    static readonly string[] ColoresNombre = { "Rojo", "Azul", "Verde", "Amarillo", "Morado", "Naranja", "Rosa", "Café", "Turquesa", "Gris" };
    static readonly string[] Carreras = { "Medicina", "Derecho", "Arquitectura", "Enfermería", "Psicología", "Ingeniería en Software", "Ingeniería en Redes Digitales", "Ingeniería en Entornos Virtuales", "Nutrición", "Odontología", "Pedagogía", "Veterinaria" };
    static readonly string[] NombreCat = { "NOMBRE", "EDAD", "PAÍS", "COLOR", "CARRERA" };
    static readonly Color[] ColorCat = {
        new Color32(111, 163, 255, 255), new Color32(179, 145, 255, 255), new Color32(111, 211, 224, 255), new Color32(255, 183, 131, 255), new Color32(134, 224, 176, 255) };

    static readonly Color Tinta = new Color32(46, 42, 94, 255);
    static readonly Color Crema = new Color32(255, 248, 231, 255);
    static readonly Color Sombra = new Color32(232, 214, 176, 255);
    static readonly Color FondoZona = new Color32(247, 245, 255, 255);
    static readonly Color Acierto = new Color32(125, 245, 182, 255);
    static readonly Color Fallo = new Color32(255, 157, 184, 255);
    static readonly Color Dorado = new Color32(255, 216, 107, 255);
    static readonly Color Lila = new Color32(165, 139, 255, 255);

    class Dato { public int cat; public string texto; }
    class Carta { public Dato dato; public RectTransform rt; public Vector2 destino; public int slot; public float temblor; }
    class Zona
    {
        public int cat; public RectTransform rt; public Image borde; public TMP_Text filas; public TMP_Text conteo;
        public int esperados; public int colocados; public float flash;
    }
    enum Estado { Intro, Jugando, Transicion, Fin }

    // ---------------- Estado ----------------
    System.Random rnd;
    Estado estado;
    int fase, vidas = 3, totalFase, colocadasFase;
    float tiempo, tiempoIntro, tiempoTransicion;
    List<string> poolNombres, poolPaises, poolCarreras, poolColores;
    List<int> poolEdades;
    List<Dato> pendientes = new List<Dato>();
    readonly List<Carta> cartas = new List<Carta>();
    readonly List<Zona> zonas = new List<Zona>();
    readonly Carta[] enSlot = new Carta[6];
    Carta arrastrando;
    Vector2 offsetArrastre;

    static readonly Vector2[] SlotPos = {
        new Vector2(-380, 270), new Vector2(0, 270), new Vector2(380, 270),
        new Vector2(-380, 150), new Vector2(0, 150), new Vector2(380, 150) };

    // ---------------- Interfaz ----------------
    RectTransform capaCartas, zonasRaiz;
    TMP_Text textoFase, textoTiempo, textoMensaje, textoProgreso;
    Image mensajeFondo;
    RectTransform canvasRT;
    RawImage fondoRaw;
    Vector2 ultimoTamano;
    RawImage[] corazones = new RawImage[3];
    Image barraProgreso, flashImg;
    float mensajeT, flashA; Color flashColor;
    GameObject panelIntro, panelFin;
    TMP_Text introTitulo, introDetalle, finTitulo, finSubtitulo;
    RawImage finIcono;
    readonly TMP_Text[] opciones = new TMP_Text[2];
    readonly RectTransform[] opcionesRT = new RectTransform[2];
    readonly string[] etiquetas = { "REINTENTAR", "SALIR" };
    int seleccion; float menuDesde;
    Texture2D texCorazonLleno, texCorazonVacio, texReloj, texEstrella, texFondo;

    // =====================================================
    void Start()
    {
        rnd = new System.Random();
        CrearTexturas();
        ConstruirUI();
        PrepararPools();
        IniciarFase(1);
    }

    // ---------------- Pools de datos (sin repetir entre fases) ----------------
    void PrepararPools()
    {
        poolNombres = new List<string>(Nombres); Mezclar(poolNombres);
        poolPaises = new List<string>(Paises); Mezclar(poolPaises);
        poolCarreras = new List<string>(Carreras); Mezclar(poolCarreras);
        poolColores = new List<string>(ColoresNombre); Mezclar(poolColores);
        poolEdades = new List<int>();
        for (int e = 15; e <= 35; e++) poolEdades.Add(e);
        Mezclar(poolEdades);
    }

    void Mezclar<T>(List<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            T tmp = l[i]; l[i] = l[j]; l[j] = tmp;
        }
    }

    T Sacar<T>(List<T> pool)
    {
        T v = pool[0];
        pool.RemoveAt(0);
        return v;
    }

    Dato NuevoDato(int cat)
    {
        Dato d = new Dato(); d.cat = cat;
        if (cat == 0) d.texto = Sacar(poolNombres);
        else if (cat == 1) d.texto = Sacar(poolEdades) + " años";
        else if (cat == 2) d.texto = Sacar(poolPaises);
        else if (cat == 3) d.texto = Sacar(poolColores);
        else d.texto = Sacar(poolCarreras);
        return d;
    }

    // ---------------- Fases ----------------
    void IniciarFase(int n)
    {
        fase = n;
        foreach (Carta c in cartas) Destroy(c.rt.gameObject);
        cartas.Clear();
        for (int i = 0; i < enSlot.Length; i++) enSlot[i] = null;
        foreach (Zona z in zonas) Destroy(z.rt.gameObject);
        zonas.Clear();
        arrastrando = null;

        // Categorías de cada fase (0 nombre, 1 edad, 2 país, 3 color, 4 carrera) y cuántas tarjetas de cada una.
        // Nombres: 5 + 6 + 7 = 18 (se usan todos los nombres de la lista).
        int[] cats = n == 1 ? new[] { 0, 1 } : n == 2 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 3, 4 };
        int[] cuantos = n == 1 ? new[] { 5, 4 } : n == 2 ? new[] { 6, 4, 4 } : new[] { 7, 3, 3, 3, 3 };
        tiempo = n == 1 ? 20f : n == 2 ? 30f : 40f;

        CrearZonas(cats, cuantos);

        pendientes = new List<Dato>();
        for (int k = 0; k < cats.Length; k++)
            for (int i = 0; i < cuantos[k]; i++) pendientes.Add(NuevoDato(cats[k]));
        Mezclar(pendientes);
        totalFase = pendientes.Count;
        colocadasFase = 0;
        for (int s = 0; s < enSlot.Length; s++) CrearCartaEnSlot(s);

        textoFase.text = "FASE " + n + " / 3";
        ActualizarProgreso();

        string lista = "";
        for (int i = 0; i < cats.Length; i++) lista += (i > 0 ? " · " : "") + NombreCat[cats[i]];
        introTitulo.text = "FASE " + n;
        introDetalle.text = "Clasifica: " + lista + "\nTiempo: " + (int)tiempo + " s   ·   Vidas: " + vidas;
        panelIntro.SetActive(true);
        estado = Estado.Intro;
        tiempoIntro = 0f;
    }

    void CompletarFase()
    {
        estado = Estado.Transicion;
        Mensaje("¡FASE COMPLETA!", Dorado);
        tiempoTransicion = 2.2f;
    }

    void Ganar()
    {
        estado = Estado.Fin;
        EstadoJuego.minijuegoGanado = true;
        EstadoJuego.minijuegoGanadoEscena = SceneManager.GetActiveScene().name;

        // Se gana la medalla roja (base de datos) y se muestra en lugar de la estrella
        Medallas.Ganar(Medallas.BaseDatos);
        Texture2D medalla = Medallas.TexturaDe(Medallas.BaseDatos);
        MostrarFin("¡OBJETIVO CUMPLIDO!", "FASES COMPLETADAS: 3 / 3", medalla != null ? medalla : texEstrella, medalla != null);
    }

    void Perder(string titulo, Texture2D icono)
    {
        if (estado == Estado.Fin) return;
        estado = Estado.Fin;
        MostrarFin(titulo, "LLEGASTE A LA FASE " + fase + " DE 3", icono);
    }

    // ---------------- Cartas ----------------
    void CrearCartaEnSlot(int slot)
    {
        if (pendientes.Count == 0) return;
        Dato d = pendientes[0];
        pendientes.RemoveAt(0);

        GameObject go = new GameObject("Carta", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(capaCartas, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Ajustar(rt, C, C, C, new Vector2(SlotPos[slot].x, 700f), new Vector2(340, 92));
        go.GetComponent<Image>().color = Tinta;

        RectTransform cara = CrearPanel("Cara", rt, Crema);
        Ajustar(cara, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        cara.offsetMin = new Vector2(4, 4); cara.offsetMax = new Vector2(-4, -4);
        RectTransform luz = CrearPanel("Luz", rt, Color.white);
        Ajustar(luz, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(-8, 6));
        RectTransform som = CrearPanel("Sombra", rt, Sombra);
        Ajustar(som, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(-8, 8));

        TMP_Text t = CrearTexto("Texto", rt, d.texto, 40f, Tinta, TextAlignmentOptions.Center);
        Ajustar(t.rectTransform, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        t.rectTransform.offsetMin = new Vector2(8, 0); t.rectTransform.offsetMax = new Vector2(-8, 0);
        t.fontStyle = FontStyles.Bold;

        Carta c = new Carta(); c.dato = d; c.rt = rt; c.slot = slot; c.destino = SlotPos[slot];
        cartas.Add(c);
        enSlot[slot] = c;
    }

    Carta CartaBajo(Vector2 p)
    {
        for (int i = cartas.Count - 1; i >= 0; i--)
            if (RectTransformUtility.RectangleContainsScreenPoint(cartas[i].rt, p, null)) return cartas[i];
        return null;
    }

    Zona ZonaBajo(Vector2 p)
    {
        foreach (Zona z in zonas)
            if (RectTransformUtility.RectangleContainsScreenPoint(z.rt, p, null)) return z;
        return null;
    }

    // ---------------- Zonas (tablas) ----------------
    void CrearZonas(int[] cats, int[] cuantos)
    {
        int n = cats.Length;
        float gap = 24f;
        float w = Mathf.Min(520f, (1760f - gap * (n - 1)) / n);
        float total = w * n + gap * (n - 1);
        float x0 = -total / 2f + w / 2f;
        float h = 460f, y = -160f;

        for (int i = 0; i < n; i++)
        {
            int cat = cats[i];
            Color col = ColorCat[cat];
            Zona z = new Zona(); z.cat = cat; z.esperados = cuantos[i];

            GameObject go = new GameObject("Zona_" + NombreCat[cat], typeof(RectTransform), typeof(Image));
            go.transform.SetParent(zonasRaiz, false);
            z.rt = go.GetComponent<RectTransform>();
            Ajustar(z.rt, C, C, C, new Vector2(x0 + i * (w + gap), y), new Vector2(w, h));
            z.borde = go.GetComponent<Image>(); z.borde.color = col;

            RectTransform cuerpo = CrearPanel("Cuerpo", z.rt, FondoZona);
            Ajustar(cuerpo, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            cuerpo.offsetMin = new Vector2(6, 6); cuerpo.offsetMax = new Vector2(-6, -6);

            RectTransform cab = CrearPanel("Cabecera", z.rt, col);
            Ajustar(cab, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 96));
            TMP_Text tn = CrearTexto("Nombre", cab, NombreCat[cat], 48f, Tinta, TextAlignmentOptions.Center);
            Ajustar(tn.rectTransform, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            tn.fontStyle = FontStyles.Bold;

            z.filas = CrearTexto("Filas", z.rt, "", cuantos[i] >= 6 ? 28f : 34f, Tinta, TextAlignmentOptions.TopLeft);
            Ajustar(z.filas.rectTransform, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            z.filas.rectTransform.offsetMin = new Vector2(28, 70); z.filas.rectTransform.offsetMax = new Vector2(-16, -112);
            z.filas.lineSpacing = cuantos[i] >= 6 ? 4f : 14f;

            z.conteo = CrearTexto("Conteo", z.rt, "0/" + cuantos[i], 40f, Tinta, TextAlignmentOptions.Center);
            Ajustar(z.conteo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(0, 56));
            z.conteo.fontStyle = FontStyles.Bold;

            zonas.Add(z);
        }
    }

    // ---------------- Bucle principal ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        AjustarFondo();
        AnimarEfectos(dt);

        if (estado == Estado.Intro)
        {
            tiempoIntro += dt;
            Keyboard k = Keyboard.current; Mouse m = Mouse.current;
            bool saltar = (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                       || (m != null && m.leftButton.wasPressedThisFrame);
            if (tiempoIntro > 0.4f && saltar)
            {
                panelIntro.SetActive(false);
                estado = Estado.Jugando;
            }
        }
        else if (estado == Estado.Jugando)
        {
            tiempo -= dt;
            if (tiempo <= 0f)
            {
                tiempo = 0f;
                ActualizarTiempo();
                Perder("¡SE ACABÓ EL TIEMPO!", texReloj);
                return;
            }
            ActualizarTiempo();
            ManejarArrastre();
        }
        else if (estado == Estado.Transicion)
        {
            tiempoTransicion -= dt;
            if (tiempoTransicion <= 0f)
            {
                if (fase < 3) IniciarFase(fase + 1);
                else Ganar();
            }
        }
        else if (estado == Estado.Fin)
        {
            ManejarMenu();
        }
    }

    // El fondo en mosaico se adapta al tamaño de la ventana en cualquier momento
    void AjustarFondo()
    {
        if (canvasRT == null) return;
        Vector2 tam = canvasRT.rect.size;
        if (tam == ultimoTamano || tam.x <= 0f) return;
        ultimoTamano = tam;
        fondoRaw.uvRect = new Rect(0, 0, tam.x / 64f, tam.y / 64f);
    }

    void ManejarArrastre()
    {
        Mouse m = Mouse.current;
        if (m == null) return;
        Vector2 p = m.position.ReadValue();

        if (arrastrando == null)
        {
            if (m.leftButton.wasPressedThisFrame)
            {
                Carta c = CartaBajo(p);
                if (c != null)
                {
                    arrastrando = c;
                    c.rt.SetAsLastSibling();
                    Vector2 l;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(capaCartas, p, null, out l);
                    offsetArrastre = c.rt.anchoredPosition - l;
                }
            }
        }
        else
        {
            if (m.leftButton.isPressed)
            {
                Vector2 l;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(capaCartas, p, null, out l);
                arrastrando.rt.anchoredPosition = l + offsetArrastre;
                arrastrando.destino = arrastrando.rt.anchoredPosition;
            }
            else
            {
                Soltar(p);
            }
        }
    }

    void Soltar(Vector2 p)
    {
        Carta c = arrastrando;
        arrastrando = null;
        Zona z = ZonaBajo(p);
        c.destino = SlotPos[c.slot];
        if (z == null) return; // soltó fuera de las tablas: regresa sin castigo

        if (z.cat == c.dato.cat) AcertarCarta(c, z);
        else FallarCarta(c, z);
    }

    void AcertarCarta(Carta c, Zona z)
    {
        cartas.Remove(c);
        enSlot[c.slot] = null;
        int slot = c.slot;
        Destroy(c.rt.gameObject);

        z.filas.text += (z.colocados > 0 ? "\n" : "") + "• " + c.dato.texto;
        z.colocados++;
        z.conteo.text = z.colocados + "/" + z.esperados;
        z.flash = 1f;

        colocadasFase++;
        ActualizarProgreso();
        Mensaje("¡CORRECTO!", Acierto);
        Flash(Acierto);

        if (colocadasFase >= totalFase) CompletarFase();
        else CrearCartaEnSlot(slot);
    }

    void FallarCarta(Carta c, Zona z)
    {
        vidas--;
        ActualizarVidas();
        c.temblor = 0.45f;
        z.flash = -1f;
        Mensaje("¡ERROR! ESO NO ES " + NombreCat[z.cat], Fallo);
        Flash(Fallo);
        if (vidas <= 0) Perder("¡FALLASTE!", texCorazonVacio);
    }

    // ---------------- Efectos y animación ----------------
    void AnimarEfectos(float dt)
    {
        foreach (Carta c in cartas)
        {
            if (c == arrastrando) continue;
            Vector2 pos = Vector2.Lerp(c.rt.anchoredPosition, c.destino, 1f - Mathf.Exp(-14f * dt));
            if (c.temblor > 0f)
            {
                c.temblor -= dt;
                pos.x += Mathf.Sin(Time.time * 70f) * 14f * Mathf.Clamp01(c.temblor / 0.45f);
            }
            c.rt.anchoredPosition = pos;
        }

        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        Zona hover = (arrastrando != null) ? ZonaBajo(mouse) : null;
        foreach (Zona z in zonas)
        {
            Color baseCol = ColorCat[z.cat];
            Color col = baseCol;
            if (z.flash > 0f) { z.flash = Mathf.Max(0f, z.flash - dt * 2.5f); col = Color.Lerp(baseCol, Acierto, 0.9f * z.flash + 0.1f); if (z.flash == 0f) col = baseCol; }
            else if (z.flash < 0f) { z.flash = Mathf.Min(0f, z.flash + dt * 2.5f); col = Color.Lerp(baseCol, Fallo, -z.flash); }
            if (z == hover) col = Tinta;
            z.borde.color = col;
        }

        if (mensajeT > 0f)
        {
            mensajeT -= dt;
            float pop = Mathf.Clamp01((mensajeT - 1.1f) / 0.2f);
            textoMensaje.transform.localScale = Vector3.one * (1f + pop * 0.4f);
            if (mensajeT <= 0f) { textoMensaje.text = ""; mensajeFondo.gameObject.SetActive(false); }
        }

        if (flashA > 0f)
        {
            flashA = Mathf.Max(0f, flashA - dt * 2f);
            Color fc = flashColor; fc.a = flashA * 0.3f;
            flashImg.color = fc;
        }

        // el reloj parpadea cuando quedan pocos segundos
        if (estado == Estado.Jugando && tiempo <= 10f)
            textoTiempo.color = Mathf.Sin(Time.time * 10f) > 0f ? Fallo : Color.white;
        else if (textoTiempo != null)
            textoTiempo.color = Color.white;
    }

    void Mensaje(string texto, Color color)
    {
        mensajeFondo.gameObject.SetActive(true);
        textoMensaje.text = texto;
        textoMensaje.color = color;
        mensajeT = 1.4f;
    }

    void Flash(Color c) { flashColor = c; flashA = 1f; }

    void ActualizarVidas()
    {
        for (int i = 0; i < corazones.Length; i++)
            corazones[i].texture = i < vidas ? texCorazonLleno : texCorazonVacio;
    }

    void ActualizarTiempo() { textoTiempo.text = Mathf.CeilToInt(tiempo).ToString(); }

    void ActualizarProgreso()
    {
        float f = totalFase > 0 ? (float)colocadasFase / totalFase : 0f;
        RectTransform rt = barraProgreso.rectTransform;
        rt.anchorMax = new Vector2(f, 1f);
        textoProgreso.text = "TARJETAS: " + colocadasFase + " / " + totalFase;
    }

    // ---------------- Menú final ----------------
    void MostrarFin(string titulo, string subtitulo, Texture2D icono, bool esMedalla = false)
    {
        // La medalla mide 16x32 (1:2), así que se muestra más alta que el icono normal
        finIcono.rectTransform.sizeDelta = esMedalla ? new Vector2(190, 380) : new Vector2(150, 150);
        finIcono.rectTransform.anchoredPosition = new Vector2(0, esMedalla ? 250 : 250);
        float dy = esMedalla ? -120f : 0f; // el texto baja un poco para que quepa la medalla grande
        finTitulo.rectTransform.anchoredPosition = new Vector2(0, 100 + dy);
        finSubtitulo.rectTransform.anchoredPosition = new Vector2(0, -30 + dy);
        for (int n = 0; n < opcionesRT.Length; n++)
            opcionesRT[n].anchoredPosition = new Vector2(0, -130 - n * 85 + dy);
        finTitulo.text = titulo;
        finSubtitulo.text = subtitulo;
        finIcono.texture = icono;
        panelFin.SetActive(true);
        menuDesde = Time.time;
        Seleccionar(0);
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
            if (confirmar && Time.time > menuDesde + 0.4f) { Confirmar(); return; }
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
                    if (m.leftButton.wasPressedThisFrame && Time.time > menuDesde + 0.4f) { Confirmar(); return; }
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
        if (seleccion == 0) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        else SceneManager.LoadScene(escenaDeRegreso);
    }

    // ---------------- Construcción de la interfaz ----------------
    static readonly Vector2 C = new Vector2(0.5f, 0.5f);

    static void Ajustar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = anclaMin; rt.anchorMax = anclaMax; rt.pivot = pivote;
        rt.anchoredPosition = pos; rt.sizeDelta = tam;
    }

    RectTransform CrearPanel(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    TMP_Text CrearTexto(string nombre, Transform padre, string contenido, float tam, Color color, TextAlignmentOptions alin)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenido; t.fontSize = tam; t.color = color; t.alignment = alin;
        t.raycastTarget = false;
        if (fuente != null) t.font = fuente;
        return t;
    }

    RawImage CrearRaw(string nombre, Transform padre, Texture tex, Vector2 pos, Vector2 tam)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(padre, false);
        RawImage r = go.GetComponent<RawImage>();
        r.texture = tex; r.raycastTarget = false;
        Ajustar(r.rectTransform, C, C, C, pos, tam);
        return r;
    }

    void ConstruirUI()
    {
        GameObject cgo = new GameObject("CanvasOracle", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // siempre se ve todo y escala con la ventana
        Transform raiz = cgo.transform;

        // Fondo con patrón de circuitos (se repite en mosaico)
        GameObject fgo = new GameObject("Fondo", typeof(RectTransform), typeof(RawImage));
        fgo.transform.SetParent(raiz, false);
        RawImage fondo = fgo.GetComponent<RawImage>();
        fondo.texture = texFondo; fondo.raycastTarget = false;
        Ajustar(fondo.rectTransform, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        fondoRaw = fondo;
        canvasRT = cgo.GetComponent<RectTransform>();

        zonasRaiz = new GameObject("Zonas", typeof(RectTransform)).GetComponent<RectTransform>();
        zonasRaiz.SetParent(raiz, false);
        Ajustar(zonasRaiz, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);

        if (eliSprite != null)
        {
            GameObject eg = new GameObject("Eli", typeof(RectTransform), typeof(Image));
            eg.transform.SetParent(raiz, false);
            Image ei = eg.GetComponent<Image>();
            ei.sprite = eliSprite; ei.preserveAspect = true; ei.raycastTarget = false;
            Ajustar(ei.rectTransform, C, C, C, new Vector2(-790, 205), new Vector2(150, 224));
        }

        // Cabecera fija arriba: no se mueve al cambiar el tamaño de la ventana
        RectTransform barra = CrearPanel("Barra", raiz, new Color32(52, 71, 143, 255));
        Ajustar(barra, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 100));
        RectTransform linea = CrearPanel("Linea", barra, Lila);
        Ajustar(linea, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 8));
        RectTransform cab = new GameObject("Cabecera", typeof(RectTransform)).GetComponent<RectTransform>();
        cab.SetParent(raiz, false);
        Ajustar(cab, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1920, 100));

        for (int i = 0; i < corazones.Length; i++)
            corazones[i] = CrearRaw("Corazon" + i, cab, texCorazonLleno, new Vector2(-840 + i * 84, 4), new Vector2(72, 64));

        textoFase = CrearTexto("Fase", cab, "FASE 1 / 3", 58f, Color.white, TextAlignmentOptions.Center);
        Ajustar(textoFase.rectTransform, C, C, C, new Vector2(0, 4), new Vector2(800, 80));
        textoFase.fontStyle = FontStyles.Bold;

        CrearRaw("IconoReloj", cab, texReloj, new Vector2(620, 4), new Vector2(60, 60));
        textoTiempo = CrearTexto("Tiempo", cab, "20", 62f, Color.white, TextAlignmentOptions.MidlineLeft);
        Ajustar(textoTiempo.rectTransform, C, C, new Vector2(0, 0.5f), new Vector2(664, 4), new Vector2(200, 80));
        textoTiempo.fontStyle = FontStyles.Bold;

        // Barra de progreso de la fase
        RectTransform pbg = CrearPanel("ProgresoFondo", raiz, Tinta);
        Ajustar(pbg, C, C, C, new Vector2(0, -470), new Vector2(1000, 36));
        RectTransform pin = CrearPanel("ProgresoInterior", pbg, new Color32(30, 33, 80, 255));
        Ajustar(pin, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        pin.offsetMin = new Vector2(4, 4); pin.offsetMax = new Vector2(-4, -4);
        RectTransform pf = CrearPanel("ProgresoRelleno", pin, Lila);
        Ajustar(pf, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        pf.offsetMin = Vector2.zero; pf.offsetMax = Vector2.zero;
        barraProgreso = pf.GetComponent<Image>();
        textoProgreso = CrearTexto("ProgresoTexto", raiz, "", 32f, Tinta, TextAlignmentOptions.Center);
        Ajustar(textoProgreso.rectTransform, C, C, C, new Vector2(0, -515), new Vector2(800, 44));

        // Capa de tarjetas
        capaCartas = new GameObject("Cartas", typeof(RectTransform)).GetComponent<RectTransform>();
        capaCartas.SetParent(raiz, false);
        Ajustar(capaCartas, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);

        // Mensajes de acierto / error
        RectTransform pill = CrearPanel("MensajeFondo", raiz, new Color(0.12f, 0.14f, 0.32f, 0.92f));
        Ajustar(pill, C, C, C, new Vector2(0, 392), new Vector2(1250, 78));
        mensajeFondo = pill.GetComponent<Image>(); mensajeFondo.raycastTarget = false;
        pill.gameObject.SetActive(false);
        textoMensaje = CrearTexto("Mensaje", raiz, "", 54f, Color.white, TextAlignmentOptions.Center);
        Ajustar(textoMensaje.rectTransform, C, C, C, new Vector2(0, 392), new Vector2(1300, 70));
        textoMensaje.fontStyle = FontStyles.Bold;

        // Destello de pantalla
        RectTransform fl = CrearPanel("Destello", raiz, new Color(1, 1, 1, 0));
        Ajustar(fl, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        flashImg = fl.GetComponent<Image>(); flashImg.raycastTarget = false;

        // Introducción de cada fase
        RectTransform intro = CrearPanel("Intro", raiz, new Color(0.12f, 0.14f, 0.32f, 0.92f));
        Ajustar(intro, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        panelIntro = intro.gameObject;
        introTitulo = CrearTexto("IntroTitulo", intro, "", 120f, Color.white, TextAlignmentOptions.Center);
        Ajustar(introTitulo.rectTransform, C, C, C, new Vector2(0, 150), new Vector2(1500, 160));
        introTitulo.fontStyle = FontStyles.Bold;
        introDetalle = CrearTexto("IntroDetalle", intro, "", 50f, Dorado, TextAlignmentOptions.Center);
        Ajustar(introDetalle.rectTransform, C, C, C, new Vector2(0, -20), new Vector2(1600, 200));
        TMP_Text hint = CrearTexto("IntroHint", intro, "ARRASTRA CADA TARJETA A SU TABLA\nHAZ CLIC PARA EMPEZAR", 40f, Color.white, TextAlignmentOptions.Center);
        Ajustar(hint.rectTransform, C, C, C, new Vector2(0, -210), new Vector2(1700, 130));

        // Menú final
        RectTransform fin = CrearPanel("MenuFin", raiz, new Color(0f, 0f, 0f, 0.88f));
        Ajustar(fin, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        panelFin = fin.gameObject;
        finIcono = CrearRaw("Icono", fin, texEstrella, new Vector2(0, 250), new Vector2(150, 150));
        finTitulo = CrearTexto("Titulo", fin, "", 96f, Color.white, TextAlignmentOptions.Center);
        Ajustar(finTitulo.rectTransform, C, C, C, new Vector2(0, 100), new Vector2(1500, 130));
        finSubtitulo = CrearTexto("Subtitulo", fin, "", 56f, Color.white, TextAlignmentOptions.Center);
        Ajustar(finSubtitulo.rectTransform, C, C, C, new Vector2(0, -30), new Vector2(1200, 90));
        for (int i = 0; i < opciones.Length; i++)
        {
            opciones[i] = CrearTexto("Opcion" + i, fin, etiquetas[i], 52f, Color.white, TextAlignmentOptions.Center);
            Ajustar(opciones[i].rectTransform, C, C, C, new Vector2(0, -130 - i * 85), new Vector2(600, 75));
            opcionesRT[i] = opciones[i].rectTransform;
        }
        panelFin.SetActive(false);
        panelIntro.SetActive(false);

        ActualizarVidas();
    }

    // ---------------- Texturas pixel art generadas por código ----------------
    void CrearTexturas()
    {
        texCorazonLleno = Mapa(new[] {
            ".##...##.", "####.####", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...." },
            new Color32(255, 79, 123, 255), new Color32(255, 190, 205, 255));
        texCorazonVacio = Mapa(new[] {
            ".##...##.", "####.####", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...." },
            new Color32(203, 181, 201, 255), new Color32(225, 210, 223, 255));
        texReloj = Mapa(new[] {
            "..#####..", ".#######.", "#########", "####h####", "####hhh##", "#########", "#########", ".#######.", "..#####.." },
            new Color32(255, 255, 255, 255), new Color32(60, 60, 90, 255));
        texEstrella = Mapa(new[] {
            "....#....", "....#....", "...###...", "#########", ".#######.", "..#####..", "..##.##..", ".##...##.", ".#.....#." },
            new Color32(255, 214, 10, 255), new Color32(255, 245, 160, 255));

        texFondo = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        texFondo.filterMode = FilterMode.Point;
        texFondo.wrapMode = TextureWrapMode.Repeat;
        Color a = new Color32(220, 214, 247, 255), b = new Color32(207, 199, 242, 255), n = new Color32(185, 174, 239, 255);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                Color col = a;
                if (x == 0 || y == 0) col = b;
                if (x == 8 && y == 8) col = n;
                if (x > 8 && y == 8) col = b;
                if (x == 4 && y == 12) col = new Color32(240, 236, 255, 255);
                texFondo.SetPixel(x, y, col);
            }
        texFondo.Apply();
    }

    // '#' = relleno, 'h' = brillo. Agrega contorno oscuro automáticamente.
    Texture2D Mapa(string[] f, Color relleno, Color brillo)
    {
        int w = f[0].Length + 2, h = f.Length + 2;
        Color[] px = new Color[w * h];
        bool[] lleno = new bool[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
        for (int y = 0; y < f.Length; y++)
            for (int x = 0; x < f[y].Length; x++)
            {
                char c = f[y][x];
                if (c != '#' && c != 'h') continue;
                int idx = (h - 2 - y) * w + (x + 1);
                px[idx] = (c == 'h') ? brillo : relleno;
                lleno[idx] = true;
            }
        Color contorno = new Color32(20, 12, 30, 255);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (lleno[idx]) continue;
                bool cerca = (x > 0 && lleno[idx - 1]) || (x < w - 1 && lleno[idx + 1]) || (y > 0 && lleno[idx - w]) || (y < h - 1 && lleno[idx + w]);
                if (cerca) px[idx] = contorno;
            }
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}