using UnityEngine;

public enum TipoItem { Estrella, Obstaculo, Salida }

// Marca qué es cada objeto del laberinto (lo agrega MazeBuilder solo)
public class ItemMinijuego : MonoBehaviour
{
    public TipoItem tipo;
    [HideInInspector] public bool usado;
}
