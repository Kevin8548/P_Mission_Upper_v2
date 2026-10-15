using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Un puerto (conector) del minijuego. Lo crea EthernetMinigame por código,
/// no hace falta agregarlo a mano en ninguna escena.
/// </summary>
public class EthernetPort : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum Side { Switch, Router }

    [HideInInspector] public Side side;
    [HideInInspector] public int colorId;
    [HideInInspector] public bool connected;
    [HideInInspector] public RectTransform anchorPoint;   // de aquí sale el cable
    [HideInInspector] public Image led;                   // lucecita de enlace

    public Action<EthernetPort, PointerEventData> Down, Drag, Up;

    public void OnPointerDown(PointerEventData e) { if (Down != null) Down(this, e); }
    public void OnDrag(PointerEventData e) { if (Drag != null) Drag(this, e); }
    public void OnPointerUp(PointerEventData e) { if (Up != null) Up(this, e); }
}