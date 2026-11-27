using UnityEngine;

// Reproduce los sonidos del minijuego de base de datos.
// Desde cualquier script se usa así:
//   SonidosJuego.Acierto();   SonidosJuego.Error();   SonidosJuego.Fase2();
public class SonidosJuego : MonoBehaviour
{
    public AudioClip acierto;
    public AudioClip error;
    public AudioClip fase2;
    [Range(0f, 1f)] public float volumen = 1f;

    static SonidosJuego instancia;
    AudioSource fuente;

    void Awake()
    {
        instancia = this;
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f; // 2D: se oye igual en toda la pantalla
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    public static void Acierto() { Sonar(instancia != null ? instancia.acierto : null); }
    public static void Error() { Sonar(instancia != null ? instancia.error : null); }
    public static void Fase2() { Sonar(instancia != null ? instancia.fase2 : null); }

    static void Sonar(AudioClip clip)
    {
        if (instancia == null || clip == null) return;
        instancia.fuente.PlayOneShot(clip, instancia.volumen);
    }
}