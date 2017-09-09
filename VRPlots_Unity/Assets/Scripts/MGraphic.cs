using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class MGraphic : MonoBehaviour
    {

        protected MUnits _Units = MUnits.pixels;
        protected float[] _Position; // matlab x,y
        protected float[] _Size; // matlab width, height
        protected Vector3 _Color;
        public bool needsRefresh = true;
        protected bool showBoundingBox = false;

        protected Material defaultMaterial;
        protected Material defaultTextMaterial;
        protected Material defaultVertexColorMaterial;
        protected Material defaultWireframeMaterial;

        public GameObject boundingBox;

        public bool _hasStarted = false; //TODO: debug, change back to private

        [JsonProperty]
        protected List<GameObject> Children;
        [JsonProperty]
        string ObjectClass;

        [JsonProperty]
        virtual public float[] Position
        {
            get
            {
                if (_Position == null || _Position.Length == 0)
                    return _Position;
                else if (_Position.Length == 3) // position probably in data (x,y,z) coords
                    return _Position;
                else // position probably in (start,stop, width, height) format
                    return new float[] { _Position[0], _Position[1], _Size[0], _Size[1] };
            }
            set
            {
                if (value == null || value.Length == 0)
                    _Position = value;
                else if (value.Length == 3) // position probably in data (x,y,z) coords
                    _Position = value;
                else // position probably in (start,stop, width, height) format
                {
                    _Position = new float[] { value[0], value[1] };
                    _Size = new float[] { value[2], value[3] };
                }
            }
        }

        public virtual void init()
        {
            defaultMaterial = Resources.Load("LineMaterial") as Material;

            //defaultVertexColorMaterial = Resources.Load("VertexColorMaterial") as Material;
            defaultVertexColorMaterial = Resources.Load("StandardSpecular") as Material;

            defaultWireframeMaterial = Resources.Load("WireframeMaterial") as Material;

            defaultTextMaterial = Resources.Load("TextMaterial") as Material;
            
            if (showBoundingBox)
            {
                boundingBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boundingBox.name = "Bounds";
                boundingBox.GetComponent<MeshRenderer>().material = defaultWireframeMaterial;
                boundingBox.transform.SetParent(transform);
                boundingBox.transform.localPosition = Vector3.zero;
                boundingBox.GetComponent<BoxCollider>().enabled = false;
                //boundingBox.transform.localPosition = UPosition;
            }

            if (Children != null)
            {
                foreach (var child in Children)
                {
                    if (child == null)
                        continue;
                    var mChild = child.GetComponent<MGraphic>();
                    if (mChild == null)
                        continue;
                    mChild.init();
                }
            }

            _hasStarted = true;
        }

        public bool HasStarted
        {
            get
            {
                return _hasStarted;
            }
        }

        virtual public Vector3 USize // size in Unity units
        {
            get
            {
                if (_Size == null)
                    return Vector3.zero;

                float[] sz = convertSizeToPixels(_Size, _Units);
                return new Vector3(sz[0], sz[1], 0);
            }
        }
        virtual public Vector3 UPosition // position in Unity units
        {
            get
            {
                if (_Position == null)
                    return Vector3.zero;
                float[] pos = convertSizeToPixels(_Position, _Units,true);
                if (pos.Length == 2)
                    pos = new float[] { pos[0], pos[1], 0 };
                return new Vector3(pos[0], pos[1], pos[2]);
            }
        }

        [JsonProperty]
        virtual public MUnits Units
        {
            get
            {
                return _Units;
            }
            set
            {
                _Units = value;
            }
        }

        [JsonProperty]
        virtual public float[] Color
        {
            get
            {
                return new float[] { _Color.x, _Color.y, _Color.z };
            }
            set
            {
                if (value == null || value.Length == 0)
                    _Color = Vector3.zero;
                else
                    _Color = new Vector3(value[0], value[1], value[2]);
            }
        }

        protected MGraphic mParent
        {
            get
            {
                if (transform.parent == null)
                    return null;
                return transform.parent.GetComponent<MGraphic>();
            }
        }

        protected int mTreeDepth
        {
            get
            {
                if (mParent == null)
                    return 0;
                else
                    return mParent.mTreeDepth + 1;
            }
        }

        virtual public void LinkChildren()
        {
            //Debug.Log("Linking children");
            foreach (GameObject child in Children)
            {
                if (child == null)
                {
                    //Debug.LogWarning("Child is null");
                    continue;
                }
                child.transform.SetParent(transform);
            }
        }

        virtual public float convertSizeToPixels(float inputScalar, MUnits units, bool isOffset = false)
        {
            var sz = Vector3.one * inputScalar;
            sz = convertSizeToPixels(sz, units, isOffset);
            return sz[0];
        }

        virtual public float[] convertSizeToPixels(float[] inputSz, MUnits units, bool isOffset = false)
        {
            float[] sz = new float[inputSz.Length];
            inputSz.CopyTo(sz,0);
            // relative units are assumed to be relative to parent, not this
            switch (units)
            {
                case MUnits.pixels:
                    return sz;
                case MUnits.normalized:
                    if (mParent==null)
                    {
                        Debug.LogWarning("Unable to convert normalized size without parent");
                        return sz;
                    }

                    var parentSize = convertSizeToPixels(mParent._Size, mParent._Units);
                    for (int i=0; i<sz.Length; i++)
                        sz[i] = parentSize[i]*sz[i];
                    //Debug.LogFormat("Size: {0}", sz);
                    return sz;
                case MUnits.data:
                    if (mParent == null)
                    {
                        Debug.LogWarning("Unable to convert data units without parent");
                        return sz;
                    }

                    // look for [XYZ]Lim fields in parent to determine data span
                    MAxes parentAxes = transform.parent.GetComponent<MAxes>();
                    if (parentAxes == null)
                    {
                        sz = mParent.convertSizeToPixels(inputSz, units, isOffset);
                        //Debug.LogWarning("Unable to convert data units without an MAxes parent");
                        return sz;
                    }

                    var tmp = new Vector3(sz[0], sz[1], sz[2]);
                    tmp = parentAxes.convertFromDataToPixelSpace(tmp,isOffset);
                    sz = new float[] { tmp.x, tmp.y, tmp.z };

                    return sz;
                case MUnits.Unity:
                    return _Position;
                default:
                    Debug.LogErrorFormat("Unable to handle units: {0}", units);
                    return sz;
            }
        }

        virtual public Vector3 convertSizeToPixels(Vector3 inputSz, MUnits units, bool isOffset = false)
        {
            var toReturn = convertSizeToPixels(new float[] { inputSz.x, inputSz.y, inputSz.z }, units, isOffset);
            return new Vector3(toReturn[0], toReturn[1], toReturn[2]);
        }

        virtual public float[][] mapIntoColormap(float[] vals)
        {
            // assume input represents full range of colormap (such as scalar values specified as colors for scatter())
            var colormap = getColormap();

            var numEntriesInMap = colormap.Length;
            var minVal = Mathf.Min(vals);
            var maxVal = Mathf.Max(vals);

            float[][] colors = new float[vals.Length][];
            for (int i = 0; i < vals.Length; i++)
            {
                float normVal = (vals[i] - minVal) / (maxVal - minVal); // normalize to [0,1]
                int index = Mathf.RoundToInt(normVal * (numEntriesInMap - 1));
                colors[i] = new float[3];
                //Debug.LogFormat("colormap size: {0},{1}", colormap.Length, colormap[0].Length);
                //Debug.LogFormat("Color at {0}", index);
                colormap[index].CopyTo(colors[i], 0);
            }
            return colors;
        }

        virtual public float[][] mapIntoColormap(float[][] vals)
        {
            float[] tmp = new float[vals.Length];
            for (int i = 0; i < vals.Length; i++)
            {
                Debug.Assert(vals[i].Length == 1);
                tmp[i] = vals[i][0];
            }
            return mapIntoColormap(tmp);
        }

        virtual public float[][] getColormap()
        {
            // traverse back up hierarchy of objects until reaching figure (which has the colormap)
            //TODO: add support for subplots with different colormaps in same figure

            var go = transform.gameObject.GetComponent<MFigure>();
            if (go == null)
            {
                return mParent.getColormap();
            }
            // if reached here, we are an object with a colormap
            MFigure fig = transform.gameObject.GetComponent<MFigure>();
            return fig.Colormap;
        }

        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
        }

        protected virtual void Update()
        {
            if (needsRefresh)
                refresh();
        }

        virtual public void refresh()
        {
            //Debug.Log("MGraphic refresh");

            needsRefresh = false;

            transform.localPosition = UPosition;

            if (Children != null)
            {
                foreach (var child in Children)
                {
                    if (child == null)
                        continue;
                    var mChild = child.GetComponent<MGraphic>();
                    if (mChild == null)
                        continue;
                    mChild.refresh();

                    if (mChild.needsRefresh)
                        needsRefresh = true;
                }
            }

            if (showBoundingBox && boundingBox != null)
            {
                var bounds = Misc.CalculateLocalBounds(transform.gameObject);
                boundingBox.transform.localPosition = bounds.center;
                boundingBox.transform.localScale = bounds.extents*2;
            }
        }
    }
}
