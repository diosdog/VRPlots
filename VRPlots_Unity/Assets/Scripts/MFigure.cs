using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MFigure : MGraphic
    {
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public int[] Number; // scalar
        [JsonProperty]
        public float[][] Colormap;

        public bool hasBeenRearranged = false;
        public bool isCurrentlyRearranging = false;

        public int uniqueFigNum = 0;

        [JsonProperty]
        override public MUnits Units
        {
            get
            {
                return _Units;
            }
            set
            {
                if (value != MUnits.pixels)
                    Debug.LogErrorFormat("Unsupported Figure units: {0}", value);
                else
                    _Units = MUnits.pixels;
            }
        }

        [JsonProperty]
        override public float[] Color
        {
            get
            {
                return new float[] { _Color.x, _Color.y, _Color.z };
            }
            set
            {
                _Color = new Vector3(value[0], value[1], value[2]);
            }
        }

        override public Vector3 UPosition // position in Unity units
        {
            get
            {
                // ignore _Position
                return Vector3.zero + defaultOrigin;
            }
        }

        private const float defaultScale = 1000; // number of matlab pixels per Unity 'meter'
        private Vector3 defaultOrigin = new Vector3(0, 1, 0); // in m

        override public void init()
        {
            showBoundingBox = false;

            base.init();

            var figManager = GameObject.FindObjectOfType<FigureManager>();
            Debug.Assert(figManager != null);
            figManager.figures.Add(this);
            
            transform.localScale = Vector3.one / defaultScale;
            transform.position = defaultOrigin;
        }

        protected override void Awake()
        {
        }

        // Use this for initialization
        override protected void Start()
        {
        }

        // Update is called once per frame
        override protected void Update()
        {
            base.Update();
            //Debug.LogFormat("MFig pos: {0}", transform.position);
            var collider = transform.gameObject.GetComponent<BoxCollider>();
            //if (collider != null)
                //Debug.LogFormat("MFig collider: {0} {1}", collider.center, collider.size);
        }

        override public void refresh()
        {
            //Debug.Log("MFigure refresh");
            base.refresh();
            if (needsRefresh)
                return;
            //boundingBox.GetComponent<Renderer>().material.color = new Color(_Color.x, _Color.y, _Color.z);
            var localSize = USize;
            localSize[2] = 1;
            //boundingBox.transform.localScale = localSize;
            //boundingBox.transform.localPosition = localSize/2;
            //boundingBox.GetComponent<Renderer>().enabled = false;

            var bounds = Misc.CalculateLocalBounds(transform.gameObject);
            var collider = transform.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            var rb = transform.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.drag = 2f;
            rb.angularDrag = 2f;

            var io = transform.gameObject.AddComponent<VRTK.VRTK_InteractableObject>();
            io.isGrabbable = true;
            io.holdButtonToGrab = true;
            io.allowedGrabControllers = VRTK.VRTK_InteractableObject.AllowedController.Both;

            var gam = transform.gameObject.AddComponent<VRTK.GrabAttachMechanics.VRTK_ChildOfControllerGrabAttach>();
            gam.precisionGrab = true;

            var ga = transform.gameObject.AddComponent<VRTK.SecondaryControllerGrabActions.VRTK_AxisScaleGrabAction>();
            ga.uniformScaling = true;

            var ih = transform.gameObject.AddComponent<VRTK.VRTK_InteractHaptics>();
            ih.strengthOnGrab = 0.5f;
            ih.intervalOnGrab = 0.05f;
            ih.durationOnGrab = 0.1f;

            // allow figures to collide/intersect with each other
            var figureLayer = LayerMask.NameToLayer("Figures");
            transform.gameObject.layer = figureLayer;
            Physics.IgnoreLayerCollision(figureLayer, figureLayer);

            // move this figure so that it is not intersecting with any others
            var figManager = GameObject.FindObjectOfType<FigureManager>();
            Debug.Assert(figManager != null);
            figManager.needsRearrange = true;
        }   
    }


    public class MFigureConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(MFigure).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            GameObject gameObject = new GameObject();
            gameObject.AddComponent<MFigure>();
            MFigure mf = gameObject.GetComponent<MFigure>();
            mf.name = "MFigure";

            try
            {
                JObject jo = JObject.Load(reader);
                serializer.Populate(jo.CreateReader(), mf);
                mf.LinkChildren();
            } catch (Exception ex)
            {
                Debug.LogErrorFormat("Exception during serialization: {0}: {1}\n{2}", ex.GetType().Name, ex.Message, ex.StackTrace);
            }

            return mf;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}