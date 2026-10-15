using UnityEngine;

// Guarda datos que deben sobrevivir al cambiar de escena
public static class EstadoJuego
{
    public static bool tieneGuardado;
    public static Vector3 posicionLeo;
    public static Vector2 direccionLeo = new Vector2(0f, -1f);
    public static bool minijuegoGanado;

    // Nombre de la escena del minijuego que se ganó (ej. "MinijuegoCables").
    // Sirve para que solo el NPC de ese minijuego diga las Victory Lines.
    public static string minijuegoGanadoEscena;
}