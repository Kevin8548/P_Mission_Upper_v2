using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MazeBuilder : MonoBehaviour
{
    [Header("Opcional: la fuente pixel art de tus otros minijuegos")]
    public TMP_FontAsset fuente;

    [Header("Nombre exacto de la escena a la que regresa el botón SALIR")]
    public string escenaDeRegreso = "SampleScene";

    // # pared   . piso   P robot   * estrella   X obstáculo   E salida
    [TextArea(18, 24)]
    public string mapa = @"###############################
#P..........*...#X..#.*.......#
#.#.#.###.#.#.###.#.#.###.#.#.#
#...#.........#...#.....#...#.#
#.#.###.X.#.###.#####.X.#.#.#.#
#....X....#.........#...#.#...#
#.#.#.###.###.#####.#.###.#####
#.#.#.#...........#...#...#...#
#.###.#.#####..X..###.#.###.#.#
#.....#...#.X.........#..*..#.#
#.###.#.#.#.###.#.#.#####.###.#
#.#....*..........#.#.....#...#
#.###.#.X.###.###.#.#.X.###.#.#
#.....#.....#.....#.........#.#
###X#.#.#.#.#.#.#.#####.#.###.#
#.....#.......#....X.*....#..E#
###############################";

    string[] filas;
    int ancho, alto;
    readonly Dictionary<int, Sprite> muros = new Dictionary<int, Sprite>();
    Sprite piso, sombra, robot, salida;
    Sprite[] framesEstrella, framesObstaculo;
    MinijuegoManager manager;

    const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

    void Start()
    {
        if (!CargarSprites()) return;
        LeerMapa();
        ConfigurarCamara(out int extraX, out int extraArriba, out int extraAbajo);

        int totalEstrellas = 0;
        foreach (string f in filas) foreach (char c in f) if (c == '*') totalEstrellas++;

        manager = gameObject.AddComponent<MinijuegoManager>();
        manager.Iniciar(totalEstrellas, escenaDeRegreso, fuente, framesEstrella[0], framesObstaculo[0]);

        Construir(extraX, extraArriba, extraAbajo);
    }

    Sprite[] CargarFrames(string ruta)
    {
        Sprite[] s = Resources.LoadAll<Sprite>(ruta);
        System.Array.Sort(s, (a, b) => string.CompareOrdinal(a.name, b.name));
        return s;
    }

    bool CargarSprites()
    {
        piso = Resources.Load<Sprite>("Minijuego/Piso");
        sombra = Resources.Load<Sprite>("Minijuego/Sombra");
        robot = Resources.Load<Sprite>("Minijuego/Robot");
        salida = Resources.Load<Sprite>("Minijuego/Salida");
        framesEstrella = CargarFrames("Minijuego/EstrellaFrames");
        framesObstaculo = CargarFrames("Minijuego/ObstaculoFrames");

        foreach (Sprite s in Resources.LoadAll<Sprite>("Minijuego/Muros"))
        {
            string num = s.name.Replace("Muro_", "");
            if (int.TryParse(num, out int k)) muros[k] = s;
        }

        if (piso == null || sombra == null || robot == null || salida == null ||
            framesEstrella.Length == 0 || framesObstaculo.Length == 0 || muros.Count < 47)
        {
            Debug.LogError("Faltan sprites. Revisa que la carpeta 'Minijuego' este dentro de Assets/Resources " +
                           "y que todos los PNG tengan Texture Type = Sprite (2D and UI) y hayas dado Apply.");
            return false;
        }
        return true;
    }

    void LeerMapa()
    {
        string[] lineas = mapa.Split('\n');
        List<string> lista = new List<string>();
        foreach (string l in lineas)
        {
            string limpia = l.Trim('\r', ' ');
            if (limpia.Length > 0) lista.Add(limpia);
        }
        filas = lista.ToArray();
        alto = filas.Length;
        ancho = 0;
        foreach (string f in filas) if (f.Length > ancho) ancho = f.Length;
    }

    void ConfigurarCamara(out int extraX, out int extraArriba, out int extraAbajo)
    {
        extraX = 4; extraArriba = 4; extraAbajo = 4;

        Camera cam = Camera.main;
        if (cam == null) return;

        float camY = 0.5f; // la cámara sube un poco para dejar espacio al título
        float tamano = Mathf.Max(alto / 2f + 1.25f, (ancho / 2f + 0.5f) / cam.aspect);

        cam.orthographic = true;
        cam.orthographicSize = tamano;
        cam.transform.position = new Vector3(0f, camY, -10f);
        cam.backgroundColor = new Color(0.106f, 0.125f, 0.22f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        // Cuántas casillas de fondo hacen falta para cubrir toda la pantalla (incluso en pantallas muy anchas)
        float mitadAncho = tamano * Mathf.Max(cam.aspect, 2.4f);
        extraX = Mathf.CeilToInt(mitadAncho - ancho / 2f) + 1;
        extraArriba = Mathf.CeilToInt(camY + tamano - alto / 2f) + 1;
        extraAbajo = Mathf.CeilToInt(tamano - camY - alto / 2f) + 1;
        if (extraX < 1) extraX = 1;
        if (extraArriba < 1) extraArriba = 1;
        if (extraAbajo < 1) extraAbajo = 1;
    }

    // Todo lo que está fuera del mapa cuenta como pared (fondo de piedra)
    bool EsPared(int x, int y)
    {
        if (y < 0 || y >= alto || x < 0 || x >= filas[y].Length) return true;
        return filas[y][x] == '#';
    }

    int ClaveMuro(int x, int y)
    {
        int k = 0;
        if (EsPared(x, y - 1)) k |= N;
        if (EsPared(x + 1, y)) k |= E;
        if (EsPared(x, y + 1)) k |= S;
        if (EsPared(x - 1, y)) k |= W;
        if (EsPared(x + 1, y - 1) && (k & N) != 0 && (k & E) != 0) k |= NE;
        if (EsPared(x + 1, y + 1) && (k & S) != 0 && (k & E) != 0) k |= SE;
        if (EsPared(x - 1, y + 1) && (k & S) != 0 && (k & W) != 0) k |= SW;
        if (EsPared(x - 1, y - 1) && (k & N) != 0 && (k & W) != 0) k |= NW;
        return k;
    }

    Vector3 Posicion(int x, int y)
    {
        return new Vector3(x - (ancho - 1) / 2f, (alto - 1) / 2f - y, 0f);
    }

    GameObject Crear(string nombre, Sprite sprite, Vector3 pos, Transform padre, int orden)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = orden;
        return go;
    }

    void Construir(int extraX, int extraArriba, int extraAbajo)
    {
        Transform padrePiso = new GameObject("Piso").transform;
        Transform padreMuros = new GameObject("Muros").transform;
        Transform padreFondo = new GameObject("Fondo").transform;
        Transform padreObjetos = new GameObject("Objetos").transform;
        padrePiso.SetParent(transform);
        padreMuros.SetParent(transform);
        padreFondo.SetParent(transform);
        padreObjetos.SetParent(transform);

        // Un solo colisionador para todas las paredes (evita que el robot se trabe en las uniones)
        Rigidbody2D rbMuros = padreMuros.gameObject.AddComponent<Rigidbody2D>();
        rbMuros.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D comp = padreMuros.gameObject.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;

        // Fondo: paredes decorativas (sin colisión) alrededor del laberinto para llenar toda la pantalla
        for (int y = -extraArriba; y < alto + extraAbajo; y++)
        {
            for (int x = -extraX; x < ancho + extraX; x++)
            {
                bool dentro = x >= 0 && x < ancho && y >= 0 && y < alto;
                if (dentro) continue;
                Crear("Fondo", muros[ClaveMuro(x, y)], Posicion(x, y), padreFondo, 1);
            }
        }

        System.Random azar = new System.Random(3);

        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < filas[y].Length; x++)
            {
                char c = filas[y][x];
                Vector3 pos = Posicion(x, y);

                if (c == '#')
                {
                    int k = ClaveMuro(x, y);
                    GameObject m = Crear("Muro", muros[k], pos, padreMuros, 1);
                    BoxCollider2D bc = m.AddComponent<BoxCollider2D>();
                    bc.size = Vector2.one;
                    bc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    continue;
                }

                // Piso con volteos para que no se vea repetido
                GameObject p = Crear("Piso", piso, pos, padrePiso, 0);
                SpriteRenderer psr = p.GetComponent<SpriteRenderer>();
                psr.flipX = azar.Next(2) == 0;
                psr.flipY = azar.Next(2) == 0;

                // Sombra de la pared sobre el piso que está justo debajo
                if (EsPared(x, y - 1))
                    Crear("Sombra", sombra, pos, padrePiso, 1);

                if (c == '*')
                {
                    GameObject e = Crear("Estrella", framesEstrella[0], pos, padreObjetos, 2);
                    CircleCollider2D cc = e.AddComponent<CircleCollider2D>();
                    cc.radius = 0.3f;
                    cc.isTrigger = true;
                    e.AddComponent<ItemMinijuego>().tipo = TipoItem.Estrella;
                    e.AddComponent<AnimadorSprites>().Configurar(framesEstrella, 2.5f);
                }
                else if (c == 'X')
                {
                    GameObject o = Crear("Obstaculo", framesObstaculo[0], pos, padreObjetos, 2);
                    CircleCollider2D cc = o.AddComponent<CircleCollider2D>();
                    cc.radius = 0.35f;
                    cc.isTrigger = true;
                    o.AddComponent<ItemMinijuego>().tipo = TipoItem.Obstaculo;
                    o.AddComponent<AnimadorSprites>().Configurar(framesObstaculo, 4f);
                }
                else if (c == 'E')
                {
                    GameObject s = Crear("Salida", salida, pos, padreObjetos, 2);
                    BoxCollider2D bc = s.AddComponent<BoxCollider2D>();
                    bc.size = new Vector2(0.7f, 0.8f);
                    bc.isTrigger = true;
                    s.AddComponent<ItemMinijuego>().tipo = TipoItem.Salida;
                }
                else if (c == 'P')
                {
                    CrearRobot(pos, padreObjetos);
                }
            }
        }
    }

    void CrearRobot(Vector3 pos, Transform padre)
    {
        GameObject r = Crear("Robot", robot, pos, padre, 3);

        Rigidbody2D rb = r.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D cc = r.AddComponent<CircleCollider2D>();
        cc.radius = 0.3f;

        RobotController rc = r.AddComponent<RobotController>();
        rc.manager = manager;
        rc.origenCuadricula = new Vector2(-(ancho - 1) / 2f, (alto - 1) / 2f);
    }
}
