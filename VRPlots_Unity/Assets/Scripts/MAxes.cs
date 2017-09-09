using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MAxes : MGraphic
    {
        [JsonProperty]
        public float[] XLim;
        [JsonProperty]
        public float[] YLim;
        [JsonProperty]
        public float[] ZLim;
        [JsonProperty]
        public List<GameObject> XAxis;
        [JsonProperty]
        public List<GameObject> YAxis;
        [JsonProperty]
        public List<GameObject> ZAxis;
        [JsonProperty]
        public List<GameObject> XLabel;
        [JsonProperty]
        public List<GameObject> YLabel;
        [JsonProperty]
        public List<GameObject> ZLabel;
        [JsonProperty]
        public float[] XTick;
        [JsonProperty]
        public float[] YTick;
        [JsonProperty]
        public float[] ZTick;
        [JsonProperty]
        public List<string[]> XTickLabel;
        [JsonProperty]
        public List<string[]> YTickLabel;
        [JsonProperty]
        public List<string[]> ZTickLabel;


        [JsonProperty]
        public List<GameObject> Title;
        [JsonProperty]
        public float[] DataAspectRatio;

        public bool transformScaleSetForData = false; //TODO: make read-only

        public const string classStr = "matlab.graphics.axis.Axes";

        private Dictionary<Axis,GameObject> rAxisArrows = new Dictionary<Axis, GameObject>();

        public override void LinkChildren()
        {
            // add other children-like objects to list of children
            foreach (var obj in XLabel)
                Children.Add(obj);
            foreach (var obj in YLabel)
                Children.Add(obj);
            foreach (var obj in ZLabel)
                Children.Add(obj);
            foreach (var obj in Title)
                Children.Add(obj);

            base.LinkChildren();
        }

        override public void init()
        {
            base.init();

            var axes = new List<Axis> { Axis.x, Axis.y, Axis.z };
            foreach (Axis axis in axes)
            {
                var go = new GameObject();
                go.transform.SetParent(transform);
                var rAxis = go.AddComponent<rAxis>();
                rAxis.axis = axis;
                rAxisArrows.Add(axis, go);
            }
        }

        override protected void Start()
        {
        }

        override protected void Update()
        {
            base.Update();
        }


        public Vector3 convertFromDataToPixelSpace(Vector3 dataCoord, bool doOffset = false)
        {
            Vector3 dataSpan = new Vector3(XLim[1] - XLim[0], YLim[1] - YLim[0], ZLim[1] - ZLim[0]);
            // right now, this setting of local scale doesn't account for any camera rotation
            //TODO: compensate localScale and change transform.rotation to match matlab's camera angle
            // for now, assume X axis and Y axis, combined with DataAspectRatio define relative scaling
            // (this assumption is almost certainly incorrect, but proceed anyways)

            var dataWidth = (XLim[1] - XLim[0]);

            //TODO: based on camera angle, form critical dimension from weighted combination of directions
            // for now, just use x axis

            // max size available is only constrained in X/Y (actually, based on camera angle)
            var USz = USize;
            if (USz[2] == 0)
                USz[2] = Mathf.Infinity;

            var axisSz = dataSpan;
            axisSz = Misc.InverseScale(axisSz, Misc.ArrayToVec3(DataAspectRatio));
            
            var scale = Misc.VecMin(Misc.InverseScale(USz, axisSz));

            axisSz = axisSz * scale;
            
            if (doOffset)
            {
                Vector3 origin = new Vector3(XLim[0], YLim[0], ZLim[0]);
                dataCoord = dataCoord - origin;
            }
            
            Vector3 pixelCoord = Vector3.Scale(Misc.InverseScale(dataCoord,dataSpan), axisSz);
            
            if (doOffset)
            {
                // add offset for axis to be centered inside figure
                USz = USize;
                pixelCoord = pixelCoord + USz / 2 - axisSz / 2;

                pixelCoord.Scale(new Vector3(1, 1, -1)); // correct for Unity's left-handed coordinate system
            }
            
            return pixelCoord;
        }

        public override void refresh()
        {
            // called once all properties are read in from JSON

            Vector3 dataSpan = new Vector3(XLim[1] - XLim[0], YLim[1] - YLim[0], ZLim[1] - ZLim[0]);
            Vector3 dataOrigin = new Vector3(XLim[0], YLim[0], ZLim[0]);
            Vector3 pixelOrigin = convertFromDataToPixelSpace(dataOrigin, true);
            
            var axes = new List<Axis> { Axis.x, Axis.y, Axis.z };
            foreach (Axis axis in axes)
            {
                var go = rAxisArrows[axis];
                go.transform.SetParent(transform);
                var rAxis = go.GetComponent<rAxis>();
                rAxis.transform.localPosition = Vector3.zero;
                rAxis.transform.localScale = Vector3.one;
                rAxis.Color = UnityEngine.Color.black; // TODO: dynamically set from axis decorator instead of hardcoding
                rAxis.Material = defaultMaterial;
                var end = new Vector3(XLim[0], YLim[0], ZLim[0]);
                switch(axis)
                {
                    case Axis.x:
                        end = new Vector3(XLim[1], YLim[0], ZLim[0]);
                        break;
                    case Axis.y:
                        end = new Vector3(XLim[0], YLim[1], ZLim[0]);
                        break;
                    case Axis.z:
                        end = new Vector3(XLim[0], YLim[0], ZLim[1]);
                        break;
                }

                rAxis.start = pixelOrigin;
                rAxis.end = convertFromDataToPixelSpace(end, true);

                switch(axis)
                {
                    case Axis.x:
                        rAxis.SetTicks(XTick, XLim, XTickLabel);
                        break;
                    case Axis.y:
                        rAxis.SetTicks(YTick, YLim, YTickLabel);
                        break;
                    case Axis.z:
                        rAxis.SetTicks(ZTick, ZLim, ZTickLabel);
                        break;
                }
            }

            // override Label positions since our axes may be in slightly different locations with respect to eachother
            foreach (Axis axis in axes)
            {
                var offAxisFactor = 5f;
                switch (axis)
                {
                    case Axis.x:
                        XLabel[0].GetComponent<MText>().Position = new float[]
                            {XLim[0] + dataSpan[0]/2f, YLim[0] - dataSpan[1]/offAxisFactor, ZLim[0] - dataSpan[2]/offAxisFactor};
                        break;
                    case Axis.y:
                        YLabel[0].GetComponent<MText>().Position = new float[]
                            {XLim[0] - dataSpan[0]/offAxisFactor, YLim[0] + dataSpan[1]/2f, ZLim[0] - dataSpan[2]/offAxisFactor};
                        break;
                    case Axis.z:
                        ZLabel[0].GetComponent<MText>().Position = new float[]
                            {XLim[0] - dataSpan[0]/offAxisFactor, YLim[0] - dataSpan[1]/offAxisFactor, ZLim[0] + dataSpan[2]/2f};
                        break;
                }
                
            }

            base.refresh();
            if (needsRefresh)
                return;

            // override title position to centered above axes
            var center = dataOrigin + dataSpan/2;
            center = convertFromDataToPixelSpace(center, true);
            center = center + transform.localPosition;
            center = transform.TransformPoint(center); // from local to world space;
            var bounds = new Bounds(center, Vector3.zero);
            foreach (GameObject child in Children)
            {
                if (Title.Contains(child))
                    continue; // don't include title in bounds calculation

                if (child.GetComponent<MText>() != null)
                    continue; // don't include text elements in bounds calculation

                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.gameObject.name == "Bounds")
                        continue; // don't include MGraphic bounding boxes in bounds calculation

                    bounds.Encapsulate(renderer.bounds);
                }
            }
            var titlePos = new Vector3(0, bounds.extents.y * 1.5f, 0) + bounds.center;
            titlePos = transform.InverseTransformPoint(titlePos); // convert from world to local
            var titleText = Title[0].GetComponent<MText>();
            titleText.Position = Misc.VecToArray(titlePos);
            titleText.Units = MUnits.Unity;
            titleText.refresh();
        }
    }

    public class rAxis : MonoBehaviour
    {
        private Vector3 _start;
        private Vector3 _end;
        private Material _material;
        private UnityEngine.Color _color;

        public Vector3 start
        {
            get { return _start; }
            set
            {
                _start = value;
                if (rAxisLine != null)
                    rAxisLine.GetComponent<LineRenderer>().SetPosition(0, value);
            }
        }
        public Vector3 end
        {
            get { return _end; }
            set
            {
                _end = value;
                if (rAxisLine != null)
                    rAxisLine.GetComponent<LineRenderer>().SetPosition(1, value);
            }
        }
        public UnityEngine.Color Color
        {
            set
            {
                _color = value;
                if (rAxisLine != null)
                    rAxisLine.GetComponent<LineRenderer>().material.color = value;
            }
        }
        public Material Material
        {
            get
            {
                return _material;
            }
            set
            {
                _material = value;
                if (rAxisLine != null)
                {
                    rAxisLine.GetComponent<LineRenderer>().material = value;
                    rAxisLine.GetComponent<LineRenderer>().material.color = _color;
                }
            }
        }
        public Axis axis = Axis.x;

        private GameObject rAxisLine;
        private List<Vector3> tickLocs;
        private List<GameObject> tickTexts;
        private List<GameObject> tickLines;
        private const float tickLength = 10f;

        void Start()
        {
            //transform.localScale = Vector3.one;
            name = "Axis";
            rAxisLine = new GameObject();
            rAxisLine.transform.SetParent(transform);
            rAxisLine.transform.localScale = Vector3.one;
            rAxisLine.transform.localPosition = Vector3.zero;
            rAxisLine.name = "AxisLine";

            var lineRenderer = rAxisLine.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.widthMultiplier = 0.005f;

            if (_material != null)
               lineRenderer.material = _material;
            if (_color != null)
                lineRenderer.material.color = _color;
            if (_start != null)
                lineRenderer.SetPosition(0, _start);
            if (_end != null)
                lineRenderer.SetPosition(1, _end);
        }

        private void Update()
        {
        }

        public void SetTicks(float[] ticks, float[] lim, List<string[]> tickLabels)
        {
            tickLocs = new List<Vector3>();
            int numTicks = ticks.Length;
            foreach (var tick in ticks)
            {
                tickLocs.Add(Vector3.Lerp(start, end, (tick - lim[0]) / (lim[1] - lim[0])));
                //Debug.LogFormat("Tick loc: {0}", tickLocs[tickLocs.Count-1]);
            }

            var tickDir = -1*Vector3.one;
            switch(axis)
            {
                case Axis.x:
                    tickDir.x = 0;
                    break;
                case Axis.y:
                    tickDir.y = 0;
                    break;
                case Axis.z:
                    tickDir.z = 0;
                    break;
            }
            tickDir.Normalize();
            tickDir.z = tickDir.z * -1;
            //Debug.LogFormat("Tick dir: {0}", tickDir);

            tickLines = new List<GameObject>();
            tickTexts = new List<GameObject>();
            for (int i=0; i<numTicks; i++)
            {
                var go = new GameObject();
                go.transform.SetParent(transform);
                go.name = "rAxisTick";
                go.transform.localPosition = tickLocs[i];
                go.transform.localScale = Vector3.one;
                var lineRenderer = go.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.widthMultiplier = 0.005f;
                lineRenderer.positionCount = 2;
                var localTickVec = tickDir * tickLength;
                lineRenderer.SetPosition(0, Vector3.zero);
                lineRenderer.SetPosition(0, localTickVec);
                lineRenderer.material = Material;
                lineRenderer.material.color = _color;
                tickLines.Add(go);

                go = new GameObject();
                go.transform.SetParent(transform);
                go.name = "rAxisTickLabel";
                go.transform.localScale = Vector3.one;
                var text = go.AddComponent<MText>();
                if (tickLabels != null && tickLabels[i] != null && tickLabels[i][0] != null)
                    text.String = tickLabels[i][0];
                text.HorizontalAlignment = "center";
                text.VerticalAlignment = "top";
                //TODO: set font size from axes' font size
                text.FontSize = new float[] { 10 };
                text.Position = Misc.VecToArray(tickLocs[i] + localTickVec*2);
                text.Units = MUnits.Unity;
                text.Color = Misc.ColorToArray(_color);
                text.UpVec = localTickVec;
                text.init();
                text.refresh();
                tickTexts.Add(go);
            }
        }
    }
}
