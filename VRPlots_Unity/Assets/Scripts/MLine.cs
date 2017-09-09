using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MLine : MGraphic
    {
        [JsonProperty]
        public float[] XData;
        [JsonProperty]
        public float[] YData;
        [JsonProperty]
        public float[] ZData;
        public const string classStr = "matlab.graphics.primitive.Line";
        public const string altClassStr = "matlab.graphics.chart.primitive.Line";

        private GameObject line;

        public override void init()
        {
            base.init();

            line = new GameObject();
            line.transform.SetParent(transform);
            line.transform.localPosition = Vector3.zero;
            line.transform.localScale = Vector3.one;
            line.name = "Line";

            var lineRenderer = line.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.widthMultiplier = 0.01f;
        }
        
        public override void refresh()
        {
            base.refresh();
            if (needsRefresh)
                return;

            if (line == null)
            {
                // not yet fully initialized
                needsRefresh = true;
                return;
            }

            var lineRenderer = line.GetComponent<LineRenderer>();
            var col = Misc.ArrayToColor(Color);
            lineRenderer.material = defaultMaterial;
            lineRenderer.material.color = col;

            List<Vector3> positions = new List<Vector3>();
            for (int i = 0; i < XData.Length; i++)
            {
                var pos = new Vector3(XData[i], YData[i], 0);
                if (ZData != null && ZData.Length == XData.Length)
                    pos.z = ZData[i];
                pos = convertSizeToPixels(pos, MUnits.data,true);
                positions.Add(pos);
            }
            lineRenderer.positionCount = XData.Length;
            lineRenderer.SetPositions(positions.ToArray());
        }
    }
}