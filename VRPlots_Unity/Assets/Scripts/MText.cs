using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

using TMPro;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MText : MGraphic
    {
        [JsonProperty]
        public string String;
        [JsonProperty]
        public string HorizontalAlignment;
        [JsonProperty]
        public string VerticalAlignment;
        [JsonProperty]
        public float[] Rotation;
        [JsonProperty]
        public float[] FontSize;

        public const string classStr = "matlab.graphics.primitive.Text";

        private GameObject textObj;
        private GameObject mainCamera;

        public Vector3 UpVec = Vector3.zero;

        public override void init()
        {
            base.init();

            // Debug.Log("MText init");

            textObj = new GameObject();
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one;
            textObj.name = "Text";

            var textMesh = textObj.AddComponent<TextMeshPro>();
            textMesh.GetComponent<Renderer>().enabled = false;
            textMesh.enableWordWrapping = false;
        }

        protected override void Start()
        {
            base.Start();
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        protected override void Update()
        {
            base.Update();

            if (String != null && String.Length > 0)
            {
                if (UpVec == Vector3.zero)
                {
                    transform.LookAt(mainCamera.transform);
                    transform.Rotate(Vector3.up, 180);
                }
                else
                {
                    var upVec = transform.parent.transform.TransformDirection(UpVec);
                    transform.LookAt(mainCamera.transform, upVec);
                    transform.Rotate(180, 0, 0);
                }
            }
        }

        public override void refresh()
        {
            base.refresh();
            if (needsRefresh)
                return;

            if (String != null && String.Length > 0)
            {
                if (textObj == null)
                {
                    // not fully initialized
                    Debug.LogWarning("Text obj not initialized before refresh");
                    init();
                }

                var textMesh = textObj.GetComponent<TextMeshPro>();
                var scaleMultiplier = 1f / transform.lossyScale.magnitude; //TODO: maybe do this resizing in update() to account for scale changes
                textMesh.fontSize = Mathf.RoundToInt(FontSize[0] * scaleMultiplier / 20f);
                textMesh.autoSizeTextContainer = true;
                textMesh.text = String;
                switch(HorizontalAlignment)
                {
                    case "left":
                        switch(VerticalAlignment)
                        {
                            case "bottom":
                                textMesh.alignment = TextAlignmentOptions.BottomLeft;
                                break;
                            case "top":
                                textMesh.alignment = TextAlignmentOptions.TopLeft;
                                break;
                            default:
                                textMesh.alignment = TextAlignmentOptions.MidlineLeft;
                                Debug.LogWarningFormat("Unsupported Alignment: {0} {1}", HorizontalAlignment, VerticalAlignment);
                                break;
                        }
                        break;
                    case "center":
                        switch (VerticalAlignment)
                        {
                            case "bottom":
                                textMesh.alignment = TextAlignmentOptions.Bottom;
                                break;
                            case "top":
                                textMesh.alignment = TextAlignmentOptions.Top;
                                break;
                            default:
                                textMesh.alignment = TextAlignmentOptions.Center;
                                Debug.LogWarningFormat("Unsupported Alignment: {0} {1}", HorizontalAlignment, VerticalAlignment);
                                break;
                        }
                        break;
                    case "right":
                        switch (VerticalAlignment)
                        {
                            case "bottom":
                                textMesh.alignment = TextAlignmentOptions.BottomRight;
                                break;
                            case "top":
                                textMesh.alignment = TextAlignmentOptions.TopRight;
                                break;
                            default:
                                textMesh.alignment = TextAlignmentOptions.Right;
                                Debug.LogWarningFormat("Unsupported Alignment: {0} {1}", HorizontalAlignment, VerticalAlignment);
                                break;
                        }
                        break;
                }
                textMesh.color = Misc.ArrayToColor(Color);
                var textRenderer = textMesh.GetComponent<Renderer>();
                //textRenderer.material.color = Misc.ArrayToColor(Color);
                textRenderer.enabled = true;
            }
        }

    }

    
}
