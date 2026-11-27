using System.Collections.Generic;
using UnityEngine;

// Pónselo al objeto "MusicaFondo" del minijuego.
// Silencia cualquier OTRA música larga que esté sonando (la del recorrido) mientras dura el
// minijuego, y la vuelve a activar al salir. También escribe en la Console qué silenció.
public class SilenciarOtrasMusicas : MonoBehaviour
{
    // Los audios más cortos que esto (clics, aciertos, errores) no se tocan
    public float duracionMinimaDeMusica = 8f;

    AudioSource propia;
    readonly List<AudioSource> silenciadas = new List<AudioSource>();

    void Start()
    {
        propia = GetComponent<AudioSource>();
        Revisar();
        InvokeRepeating(nameof(Revisar), 0.25f, 0.5f);
        Invoke(nameof(ListarTodas), 0.6f);
    }

    void Revisar()
    {
        AudioSource[] todas = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioSource a in todas)
        {
            if (a == null || a == propia || a.clip == null) continue;
            if (a.clip.length < duracionMinimaDeMusica) continue;

            if (!silenciadas.Contains(a))
            {
                silenciadas.Add(a);
                Debug.Log("[Silenciar] Silencié el objeto '" + a.name + "' (clip: " + a.clip.name +
                          ", escena: " + a.gameObject.scene.name + ")");
            }
            a.mute = true;
        }
    }

    // Para diagnóstico: lista TODOS los Audio Source que existen
    void ListarTodas()
    {
        AudioSource[] todas = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioSource a in todas)
        {
            string clip = a.clip != null ? a.clip.name + " (" + a.clip.length.ToString("0.0") + " s)" : "sin clip";
            Debug.Log("[Audio] '" + a.name + "' | " + clip + " | suena: " + a.isPlaying +
                      " | loop: " + a.loop + " | mute: " + a.mute + " | escena: " + a.gameObject.scene.name);
        }
    }

    void OnDestroy()
    {
        CancelInvoke();
        foreach (AudioSource a in silenciadas)
            if (a != null) a.mute = false;
    }
}