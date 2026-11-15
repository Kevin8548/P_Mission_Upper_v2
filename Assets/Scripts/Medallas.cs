using System.Collections.Generic;
using UnityEngine;

// Sistema de medallas: una por minijuego.
// Los PNG van en Assets/Resources/Medallas/ con estos nombres:
//   Medalla_Cables.png (verde), Medalla_Laberinto.png (morada),
//   Medalla_Rompecabezas.png (negra), Medalla_BaseDatos.png (roja)
public static class Medallas
{
    public const string Cables = "cables";
    public const string Laberinto = "laberinto";
    public const string Rompecabezas = "rompecabezas";
    public const string BaseDatos = "basedatos";

    public static readonly string[] Todas = { Cables, Laberinto, Rompecabezas, BaseDatos };

    static readonly Dictionary<string, string> archivo = new Dictionary<string, string>
    {
        { Cables, "Medalla_Cables" },
        { Laberinto, "Medalla_Laberinto" },
        { Rompecabezas, "Medalla_Rompecabezas" },
        { BaseDatos, "Medalla_BaseDatos" },
    };

    static readonly HashSet<string> ganadas = new HashSet<string>();

    // Medalla ganada hace poco; el marcador la anima y luego la borra
    public static string nueva;

    // true cuando ya salió el storyboard final (para que salga una sola vez)
    public static bool finalMostrado;

    public static int Total { get { return Todas.Length; } }
    public static int Cantidad { get { return ganadas.Count; } }

    public static bool Tiene(string id) { return ganadas.Contains(id); }

    // Llamar cuando el jugador gana el minijuego. Ganar dos veces el mismo no suma doble.
    public static void Ganar(string id)
    {
        if (ganadas.Add(id)) nueva = id;
    }

    // Empezar de cero (por ejemplo un botón "Nueva partida")
    public static void Reiniciar()
    {
        ganadas.Clear();
        nueva = null;
        finalMostrado = false;
    }

    public static Sprite SpriteDe(string id)
    {
        Texture2D t = TexturaDe(id);
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