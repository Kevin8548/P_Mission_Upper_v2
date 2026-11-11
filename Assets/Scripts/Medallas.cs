using System.Collections.Generic;
using UnityEngine;

// Sistema de medallas: una por minijuego.
// Los PNG van en Assets/Resources/Medallas/ con estos nombres:
//   Medalla_Cables.png (verde), Medalla_Laberinto.png (morada),
//   Medalla_Rompecabezas.png (negra), Medalla_BaseDatos.png (roja)
public static class Medallas
{
    // Ids de cada minijuego
    public const string Cables = "cables";             // verde  - minijuego de redes
    public const string Laberinto = "laberinto";       // morada
    public const string Rompecabezas = "rompecabezas"; // negra
    public const string BaseDatos = "basedatos";       // roja   - lab Oracle

    // Orden en que aparecen en el marcador del juego principal
    public static readonly string[] Todas = { Cables, Laberinto, Rompecabezas, BaseDatos };

    static readonly Dictionary<string, string> archivo = new Dictionary<string, string>
    {
        { Cables, "Medalla_Cables" },
        { Laberinto, "Medalla_Laberinto" },
        { Rompecabezas, "Medalla_Rompecabezas" },
        { BaseDatos, "Medalla_BaseDatos" },
    };

    static readonly HashSet<string> ganadas = new HashSet<string>();

    // Medalla que se ganó hace poco; el marcador la usa para animarla y luego la borra
    public static string nueva;

    public static int Total { get { return Todas.Length; } }
    public static int Cantidad { get { return ganadas.Count; } }

    public static bool Tiene(string id) { return ganadas.Contains(id); }

    // Llamar cuando el jugador gana el minijuego. Ganar dos veces el mismo no suma doble.
    public static void Ganar(string id)
    {
        if (ganadas.Add(id)) nueva = id;
    }

    // Por si quieres empezar de cero (por ejemplo un botón "Nueva partida")
    public static void Reiniciar()
    {
        ganadas.Clear();
        nueva = null;
    }

    public static Sprite SpriteDe(string id)
    {
        Texture2D t = TexturaDe(id); // carga y deja la textura en filtro Point
        if (t == null) return null;
        return Resources.Load<Sprite>("Medallas/" + archivo[id]);
    }

    public static Texture2D TexturaDe(string id)
    {
        string nombre;
        if (!archivo.TryGetValue(id, out nombre)) return null;
        Texture2D t = Resources.Load<Texture2D>("Medallas/" + nombre);
        if (t == null)
        {
            Debug.LogWarning("Medallas: no encuentro Assets/Resources/Medallas/" + nombre + ".png " +
                             "(Texture Type = Sprite (2D and UI)).");
            return null;
        }
        t.filterMode = FilterMode.Point; // pixel art nítido
        return t;
    }
}
