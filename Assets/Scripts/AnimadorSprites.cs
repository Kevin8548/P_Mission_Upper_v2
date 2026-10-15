using UnityEngine;

// Cambia entre los cuadros de un sprite para animarlo (estrella y obstáculos)
public class AnimadorSprites : MonoBehaviour
{
    Sprite[] frames;
    float velocidad = 3f;
    SpriteRenderer sr;
    float t;

    public void Configurar(Sprite[] f, float cuadrosPorSegundo)
    {
        frames = f;
        velocidad = cuadrosPorSegundo;
        sr = GetComponent<SpriteRenderer>();
        t = Random.value * 10f; // para que no todos se muevan sincronizados
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;
        t += Time.deltaTime;
        sr.sprite = frames[(int)(t * velocidad) % frames.Length];
    }
}
