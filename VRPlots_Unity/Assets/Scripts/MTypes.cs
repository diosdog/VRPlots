using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace MTypes
{
    public enum MUnits
    {
        pixels,
        normalized,
        data,
        Unity
    }

    public enum Axis
    {
        x,
        y,
        z
    }

    public class MEmptyChildren
    {
    }

    public static class Misc
    {
        public static Vector3 ArrayToVec3(float[] arr)
        {
            return new Vector3(arr[0], arr[1], arr[2]);
        }
        public static float[] VecToArray(Vector3 vec)
        {
            return new float[] { vec.x, vec.y, vec.z };
        }
        public static Vector3 VecReciprocal(Vector3 vec)
        {
            return new Vector3(1f / vec.x, 1f / vec.y, 1f / vec.z);
        }
        public static Vector3 InverseScale(Vector3 vec1, Vector3 vec2)
        {
            return Vector3.Scale(vec1, new Vector3(1f / vec2.x, 1f / vec2.y, 1f / vec2.z));
        }
        public static float VecMax(Vector3 vec)
        {
            return Mathf.Max(new float[] { vec.x, vec.y, vec.z });
        }
        public static float VecMin(Vector3 vec)
        {
            return Mathf.Min(new float[] { vec.x, vec.y, vec.z });
        }

        public static UnityEngine.Color ArrayToColor(float[] arr)
        {
            Debug.Assert(arr.Length == 3);
            return new UnityEngine.Color(arr[0], arr[1], arr[2]);
        }
        
        public static float[] ColorToArray(UnityEngine.Color color)
        {
            return new float[] { color.r, color.g, color.b };
        }

        public static void MakeMeshDoubleSided(ref Mesh mesh)
        {
            var normals = mesh.normals;
            if (normals == null || normals.Length == 0)
            {
                mesh.RecalculateNormals(); //unity's runtime normal recalculation doesn't actually work all that well in many cases
                //mesh.RecalculateNormals(45);
                normals = mesh.normals;
            }

            var vertices = mesh.vertices;
            var faces = mesh.triangles;
            var colors = mesh.colors;

            var meshTopol = mesh.GetTopology(0);
            int numVerticesPerFace = 0;
            switch (meshTopol)
            {
                case MeshTopology.Triangles:
                    numVerticesPerFace = 3;
                    break;
                case MeshTopology.Quads:
                    numVerticesPerFace = 4;
                    break;
                default:
                    Debug.LogErrorFormat("Unsupported topology: {0}", meshTopol);
                    break;
            }

            //Debug.LogFormat("Num vertices per face: {0}", numVerticesPerFace);

            var nV = vertices.Length;

            Debug.Assert(normals.Length == vertices.Length);

            var newVertices = new Vector3[nV * 2];
            vertices.CopyTo(newVertices, 0);
            vertices.CopyTo(newVertices, nV);

            var newNormals = new Vector3[nV * 2];
            normals.CopyTo(newNormals, 0);
            normals.CopyTo(newNormals, nV);

            var newColors = new Color[nV * 2];
            if (colors.Length > 0)
            {
                colors.CopyTo(newColors,0);
                colors.CopyTo(newColors, nV);
            }


            for (int i = 0; i < nV; i++)
            {
                //newNormals[i + nV] = -1 * newNormals[i + nV]; // invert normals
                newNormals[i] = -1 * newNormals[i]; // invert normals
            }

            var nF = faces.Length;
            var newFaces = new int[nF * 2];
            faces.CopyTo(newFaces, 0);
            faces.CopyTo(newFaces, nF);
            for (int i = 0; i < nF; i++)
            {
                newFaces[i + nF] += nV;
                if (i % numVerticesPerFace == numVerticesPerFace - 1)
                {
                    var tmp = newFaces[i];
                    newFaces[i] = newFaces[i - numVerticesPerFace + 1];
                    newFaces[i - numVerticesPerFace + 1] = tmp;
                    if (numVerticesPerFace > 3)
                    {
                        tmp = newFaces[i - numVerticesPerFace + 2];
                        newFaces[i - numVerticesPerFace + 2] = newFaces[i - numVerticesPerFace + 3];
                        newFaces[i - numVerticesPerFace + 3] = tmp;
                    }
                }
            }

            mesh.Clear();
            mesh.vertices = newVertices;
            mesh.normals = newNormals;
            if (colors.Length > 0)
                mesh.colors = newColors;
            mesh.SetIndices(newFaces, meshTopol, 0);
        }

        public static Bounds CalculateLocalBounds(GameObject ob)
        {
            // from https://forum.unity3d.com/threads/calculating-a-bound-of-a-grouped-model.101121/
            Quaternion currentRotation = ob.transform.rotation;
            ob.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Bounds bounds = new Bounds(ob.transform.position, Vector3.zero);
            foreach (Renderer renderer in ob.GetComponentsInChildren<Renderer>())
            {
                if (renderer.gameObject.GetComponent<MText>() != null || renderer.gameObject.transform.parent.GetComponent<MText>() != null)
                    continue; // don't include text elements in bounds calculation

                //Debug.LogFormat("Encapsulating bounds {0} {1} {2}", renderer.gameObject.name, renderer.bounds.center, renderer.bounds.extents);
                bounds.Encapsulate(renderer.bounds);
            }
            Vector3 localCenter = bounds.center - ob.transform.position;
            //Debug.LogFormat("Bounds before inverse scale: {0}", bounds);
            bounds.center = Misc.InverseScale(localCenter,ob.transform.localScale);
            bounds.extents = Misc.InverseScale(bounds.extents,ob.transform.localScale);
            //Debug.Log("The local bounds of this model is " + bounds);
            ob.transform.rotation = currentRotation;
            return bounds;
        }

        public static Bounds CalculateLocalBoundsInGlobalScale(GameObject ob)
        {
            // from https://forum.unity3d.com/threads/calculating-a-bound-of-a-grouped-model.101121/
            Quaternion currentRotation = ob.transform.rotation;
            ob.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Bounds bounds = new Bounds(ob.transform.position, Vector3.zero);
            foreach (Renderer renderer in ob.GetComponentsInChildren<Renderer>())
            {
                if (renderer.gameObject.GetComponent<MText>() != null || renderer.gameObject.transform.parent.GetComponent<MText>() != null)
                    continue; // don't include text elements in bounds calculation

                //Debug.LogFormat("Encapsulating bounds {0} {1}", renderer.bounds.center, renderer.bounds.extents);
                bounds.Encapsulate(renderer.bounds);
            }
            Vector3 localCenter = bounds.center - ob.transform.position;
            bounds.center = localCenter;
            ob.transform.rotation = currentRotation;
            return bounds;
        }

        public static Bounds CalculateGlobalBounds(GameObject ob)
        {
            Bounds bounds = new Bounds(ob.transform.position, Vector3.zero);
            foreach (Renderer renderer in ob.GetComponentsInChildren<Renderer>())
            {
                if (renderer.gameObject.GetComponent<MText>() != null || renderer.gameObject.transform.parent.GetComponent<MText>() != null)
                    continue; // don't include text elements in bounds calculation

                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }

    public class MMatrixOrVec<T>
    {
        private T[][] vals;

        public MMatrixOrVec(T[][] arr)
        {
            vals = arr;
        }

        public T[] this[int i]
        {
            get { return vals[i]; }
            set { vals[i] = value; }
        }

        static public implicit operator MMatrixOrVec<T>(T[][] value)
        {
            return new MMatrixOrVec<T>(value);
        }

        static public implicit operator MMatrixOrVec<T>(T[] value)
        {
            return new MMatrixOrVec<T>(new T[][] { value });
        }

        static public implicit operator T[][](MMatrixOrVec<T> ob)
        {
            return ob.vals;
        }
        
        static public implicit operator T[](MMatrixOrVec<T> ob)
        {
            if (ob.vals.Length == 1)
                return ob.vals[0];
            else if (ob.vals.Length == 0)
                return new T[0];
            else
                throw new InvalidCastException("This MMatrixOrVec is not a vector and thus cannot be cast to a vector.");
        }

        public bool IsVec
        {
            get { return vals.Length == 1; }
        }

        public int Length
        {
            get { return vals.Length; }
        }
    }

    public class MMatrixOrVecConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(MMatrixOrVec<T>).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            MMatrixOrVec<T> ob;

            Debug.Assert(token.Type == JTokenType.Array);

            try
            {
                ob = serializer.Deserialize<T[][]>(token.CreateReader()); // try to deserialize as a matrix
                
            } catch
            {
                // else try to deserialize as a vector
                ob = serializer.Deserialize<T[]>(token.CreateReader());
            }

            return ob;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
