using UnityEngine;
using UnityEngine.Tilemaps;

public class MapBoundsLogger : MonoBehaviour
{
    public Tilemap[] tilemaps; // arrastra Paredes, Piso, etc.

    void Start()
    {
        Bounds total = new Bounds();
        bool first = true;

        foreach (var tm in tilemaps)
        {
            tm.CompressBounds();
            Bounds b = tm.localBounds;
            // pasar a coordenadas de mundo
            Vector3 min = tm.transform.TransformPoint(b.min);
            Vector3 max = tm.transform.TransformPoint(b.max);
            Bounds wb = new Bounds();
            wb.SetMinMax(min, max);

            if (first) { total = wb; first = false; }
            else total.Encapsulate(wb);
        }

        Debug.Log($"MIN: {total.min.x}, {total.min.y}  |  MAX: {total.max.x}, {total.max.y}");
    }
}