using UnityEngine;

// Reproduce los sonidos del minijuego del laberinto.
// Desde cualquier script se usa así:
//   SonidosLaberinto.Paso();   SonidosLaberinto.Estrella();   SonidosLaberinto.Choque();
public class SonidosLaberinto : MonoBehaviour
{
    public AudioClip paso;
    public AudioClip estrella;
    public AudioClip choque;
    [Range(0f, 1f)] public float volumen = 1f;
    [Range(0f, 1f)] public float volumenPasos = 0.6f; // los pasos suenan más bajito

    static SonidosLaberinto instancia;
    AudioSource fuente;

    void Awake()
    {
        instancia = this;
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    public static void Paso() { Sonar(instancia != null ? instancia.paso : null, true); }
    public static void Estrella() { Sonar(instancia != null ? instancia.estrella : null, false); }
    public static void Choque() { Sonar(instancia != null ? instancia.choque : null, false); }

    static void Sonar(AudioClip clip, bool esPaso)
    {
        if (instancia == null || clip == null) return;
        float v = instancia.volumen * (esPaso ? instancia.volumenPasos : 1f);
        instancia.fuente.PlayOneShot(clip, v);
    }
}