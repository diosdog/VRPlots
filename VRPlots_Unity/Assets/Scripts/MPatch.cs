using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MPatch : MGraphic
    {
        [JsonProperty]
        public MMatrixOrVec<float> FaceVertexCData;
        [JsonProperty]
        public float[][] Vertices;
        [JsonProperty]
        public int[][] Faces;
        [JsonProperty]
        public MPatchColor FaceColor;
        [JsonProperty]
        public MPatchColor EdgeColor;

        public const string classStr = "matlab.graphics.primitive.Patch";

        private Mesh mesh;

        public override void init()
        {
            base.init();
        }
        
        public override void refresh()
        {
            base.refresh();
            if (needsRefresh)
                return;

            if (mesh != null)
                Destroy(mesh);

            mesh = new Mesh();
            var meshFilter = transform.gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            var meshRenderer = transform.gameObject.AddComponent<MeshRenderer>();
            if (FaceVertexCData != null)
                meshRenderer.material = defaultVertexColorMaterial;
            else
                meshRenderer.material = defaultMaterial;
            
            if (FaceColor.array != null) 
                meshRenderer.material.color = Misc.ArrayToColor(FaceColor.array);

            if (EdgeColor.str != null)
            {
                if (EdgeColor.str != "none")
                    Debug.LogErrorFormat("Unsupported EdgeColor {0}", EdgeColor.str);
                // else do nothing
            } else
            {
                Debug.Assert(EdgeColor.array != null & EdgeColor.array.Length == 3);
                // add second material as wireframe to show both underlying color and mesh edges
                var wireframeMaterial = defaultWireframeMaterial;
                wireframeMaterial.color = Misc.ArrayToColor(EdgeColor.array);
                meshRenderer.materials = new Material[] { meshRenderer.material, defaultWireframeMaterial };
            }
            
            //TODO: add second wireframe material if edge color is not none

            // convert coordinate systems and from float[3] to Vector3
            var convertedVertices = new Vector3[Vertices.Length];
            for (int i = 0; i < Vertices.Length; i++)
            {
                convertedVertices[i] = convertSizeToPixels(Misc.ArrayToVec3(Vertices[i]), MUnits.data, true);
            }
            mesh.vertices = convertedVertices;

            // convert from (numFaces by numTriangles) to (numFaces*numTriangles vector) as expected by Unity
            var numVerticesPerFace = Faces[0].Length;

            Debug.Assert(numVerticesPerFace == 3); // quadilaterials and other size face elements not currently supported

            var convertedFaces = new int[Faces.Length * numVerticesPerFace];
            //Debug.LogFormat("Size of faces: {0},{1}", Faces.Length, Faces[0].Length);
            for (int i = 0; i < Faces.Length; i++)
            {
                Debug.Assert(Faces[i].Length == numVerticesPerFace);
                for (int j = 0; j < numVerticesPerFace; j++)
                {
                    convertedFaces[numVerticesPerFace * i + j] = Faces[i][j] - 1; // subtract 1 to convert to zero-based indexing
                }
            }
            
            //Debug.LogFormat("Max index: {0}", Mathf.Max(convertedFaces));
            //Debug.LogFormat("Size of Vertices: {0},{1}", Vertices.Length, Vertices[0].Length);

            MeshTopology meshTopol = MeshTopology.Triangles;
            if (numVerticesPerFace == 3)
                meshTopol = MeshTopology.Triangles;
            //else if (numVerticesPerFace == 4)
            //    meshTopol = MeshTopology.Quads;
            else
                Debug.LogErrorFormat("Unsupported number of vertices per face: {0}", meshTopol);

            mesh.SetIndices(convertedFaces, meshTopol, 0);
            //mesh.triangles = convertedFaces;
            
            if (FaceVertexCData != null) {
                // convert scalar maps into colormap into RGB colors
                if (FaceVertexCData.Length > 0 && (FaceVertexCData[0].Length == 1))
                    // assume if one element needs to be converted, all do
                    FaceVertexCData = mapIntoColormap((float [][])FaceVertexCData);

                var colors = new List<Color>();
                if (FaceVertexCData.Length == 1) {
                    // replicate single color
                    Debug.Assert(FaceVertexCData[0].Length == 3);
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        colors.Add(Misc.ArrayToColor(FaceVertexCData[0]));
                    }
                } else
                {
                    Debug.Assert(FaceVertexCData.Length == Vertices.Length); // for now, only support coloring by vertex (not face)
                    //TODO: add support for coloring by face if FaceVertexCData.Length == numFaces
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        colors.Add(Misc.ArrayToColor(FaceVertexCData[i]));
                    }
                }
                mesh.SetColors(colors);
            }

            // duplicate faces and flip normals
            Misc.MakeMeshDoubleSided(ref mesh);
        }

        public class MPatchColor
        {
            public float[] array;
            public string str;
        }

        public class PatchColorConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(MPatchColor).IsAssignableFrom(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);

                var ob = new MPatchColor();

                if (token.Type == JTokenType.String)
                    ob.str = (string)token;
                else if (token.Type == JTokenType.Array)
                {
                    ob.array = serializer.Deserialize<float []>(token.CreateReader());
                }
                else
                    Debug.LogErrorFormat("Cannot parse PatchColor of type {0}", token.Type);

                return ob;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}