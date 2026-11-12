using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class IntroVideo : MonoBehaviour
{
    public VideoPlayer videoLazo;
    public string escenaDelJuego;

    VideoPlayer videoInicio;
    bool listo = false;            // el primer video ya terminó
    bool lazoPreparado = false;
    bool lazoIniciado = false;
    bool lazoVisible = false;

    void Start()
    {
        videoInicio = GetComponent<VideoPlayer>();
        videoInicio.playOnAwake = false;
        videoInicio.isLooping = false;
        videoInicio.skipOnDrop = false;

        videoLazo.playOnAwake = false;
        videoLazo.isLooping = true;
        videoLazo.skipOnDrop = false;
        videoLazo.prepareCompleted += (v) =>
        {
            lazoPreparado = true;
            if (listo) IniciarLazo();
        };
        videoLazo.Prepare();

        videoInicio.prepareCompleted += (v) => v.Play();
        videoInicio.loopPointReached += (v) =>
        {
            listo = true;
            if (lazoPreparado) IniciarLazo();
        };
        videoInicio.Prepare();
    }

    void IniciarLazo()
    {
        if (lazoIniciado) return;
        lazoIniciado = true;
        videoLazo.sendFrameReadyEvents = true;
        videoLazo.frameReady += AlPrimerCuadroDelLazo;
        videoLazo.Play();
    }

    // Cuando el lazo ya dibujó su primer cuadro, escondemos el primer video
    void AlPrimerCuadroDelLazo(VideoPlayer vp, long frame)
    {
        if (lazoVisible) return;
        lazoVisible = true;
        videoLazo.frameReady -= AlPrimerCuadroDelLazo;
        videoLazo.sendFrameReadyEvents = false;
        videoInicio.targetCameraAlpha = 0f;
    }

    void Update()
    {
        if (listo && EnterPresionado())
        {
            Medallas.Reiniciar();
            SceneManager.LoadScene(escenaDelJuego);
        }
    }

    bool EnterPresionado()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}