using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MScatter : MGraphic
    {
        [JsonProperty]
        public float[] XData;
        [JsonProperty]
        public float[] YData;
        [JsonProperty]
        public float[] ZData;
        [JsonProperty]
        public float[] SizeData;
        [JsonProperty]
        public float[][] CData;
        [JsonProperty]
        public string Marker;
        [JsonProperty]
        public string MarkerEdgeColor;
        [JsonProperty]
        public string MarkerFaceColor;

        public const string classStr = "matlab.graphics.chart.primitive.Scatter";

        private List<GameObject> markers;

        public override void refresh()
        {
            base.refresh();
            if (needsRefresh)
                return;

            if (markers != null)
                foreach (var marker in markers)
                    Destroy(marker);

            markers = new List<GameObject>();

            if (CData[0].Length == 1)
            {
                // colors specified as scalar values mapping into colormap
                // (assume all colors are specified in the same way)
                float[] vals = new float[CData.Length];
                for (int i = 0; i < CData.Length; i++)
                    vals[i] = CData[i][0];

                // convert to array of RGB values
                CData = mapIntoColormap(vals);
            }
            
            for (int i=0; i<XData.Length; i++)
            {
                var go = new GameObject();
                go.name = "ScatterPoint";
                var marker = go.AddComponent<MMarker>();
                marker.transform.SetParent(transform);
                marker.transform.localPosition = convertSizeToPixels(new Vector3(XData[i], YData[i], ZData[i]), MUnits.data, true);
                marker.transform.localScale = Vector3.one;
                marker.Marker = Marker;
                marker.material = defaultMaterial;
                marker.MarkerEdgeColor = CData[i];
                marker.MarkerSize = SizeData[i];
                marker.renderAsSphere = MarkerFaceColor == "flat";
                marker.UpdateMarker();
                
            }
        }
    }

    public class MMarker : MonoBehaviour
    {
        public string Marker;
        public float MarkerSize = 20;
        public float[] MarkerFaceColor;
        public float[] MarkerEdgeColor;
        public float LineWidth = 1;
        public Vector3 Position;
        public Material material;

        public bool renderAsSphere = true;

        private GameObject line;
        private GameObject sphere;

        private const int numLinesPerCircle = 10;
        private GameObject mainCamera;

        private int updateCounter = 0;

        private void Start()
        {
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        }

        private void Update()
        {
            if (!renderAsSphere)
            {
                updateCounter %= 1;
                if (updateCounter == 0)
                    transform.LookAt(mainCamera.transform);
            }
        }
        
        public void UpdateMarker()
        {
            if (line != null)
                Destroy(line);
            if (sphere != null)
                Destroy(sphere);

            switch (Marker)
            {
                case "o":

                    if (!renderAsSphere)
                    {
                        line = new GameObject();
                        line.transform.SetParent(transform);
                        line.transform.localPosition = Vector3.zero;
                        line.transform.localScale = Vector3.one;
                        line.name = "MarkerLine";

                        var lineRenderer = line.AddComponent<LineRenderer>();
                        lineRenderer.useWorldSpace = false;
                        lineRenderer.widthMultiplier = 0.005f * LineWidth;
                        lineRenderer.material = material;
                        lineRenderer.receiveShadows = false;
                        if (MarkerEdgeColor.Length == 3)
                            lineRenderer.material.color = Misc.ArrayToColor(MarkerEdgeColor);

                        float radius = Mathf.Sqrt(MarkerSize / Mathf.PI) * 2;
                        float z = 0;

                        lineRenderer.positionCount = numLinesPerCircle + 1;
                        for (int i = 0; i < (numLinesPerCircle + 1); i++)
                        {
                            float angle = Mathf.Deg2Rad * i * 360f / numLinesPerCircle;
                            float x = Mathf.Sin(angle) * radius;
                            float y = Mathf.Cos(angle) * radius;

                            lineRenderer.SetPosition(i, new Vector3(x, y, z));
                        }
                    } else
                    {
                        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        sphere.transform.SetParent(transform);
                        sphere.transform.localPosition = Vector3.zero;
                        sphere.transform.localScale = Vector3.one * Mathf.Sqrt(MarkerSize/Mathf.PI)*4;
                        sphere.name = "MarkerSphere";
                        sphere.GetComponent<SphereCollider>().enabled = false;
                        var mr = sphere.GetComponent<MeshRenderer>();
                        mr.material = material;
                        mr.receiveShadows = false;
                        if (MarkerEdgeColor.Length == 3)
                            mr.material.color = Misc.ArrayToColor(MarkerEdgeColor);
                        else if (MarkerFaceColor.Length == 3)
                            mr.material.color = Misc.ArrayToColor(MarkerFaceColor);
                         
                        sphere.name = "MarkerSphere";
                    }
                    break;
                default:
                    Debug.LogErrorFormat("Unsupported marker type: {0}", Marker);
                    break;
            }
        }
    }
}