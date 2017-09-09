using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MTypes;

public class FigureManager : MonoBehaviour {

    public List<MFigure> figures = new List<MFigure>();

    private bool rearrangeInProgress = false;

    public bool needsRearrange = false;

    // Use this for initialization
    void Start () {
		
	}

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            // press 'r' to rearrange 
            needsRearrange = true;

        if (Input.GetKeyDown(KeyCode.C))
            // press 'c' to clear all figures
            DestroyAllFigures();

    }

    void LateUpdate () {
        if (needsRearrange && !rearrangeInProgress)
        {
            needsRearrange = false;
            StartCoroutine(RearrangeAllFigures());
        }
	}

    private void DestroyAllFigures()
    {
        foreach (var fig in figures)
        {
            Destroy(fig.gameObject);
        }
        figures = new List<MFigure>();
    }


    private IEnumerator RearrangeAllFigures(bool doClearPreviousArrangements = true)
    {
        while (rearrangeInProgress)
            yield return null;

        rearrangeInProgress = true;

        // don't rearrange yet if any figure still needs refresh
        while (true)
        {
            var anyNeedRefresh = false;
            foreach (var fig in figures)
            {
                if (fig.needsRefresh)
                {
                    Debug.Log("A figure needs refresh");
                    anyNeedRefresh = true;
                    break;
                }
            }
            if (anyNeedRefresh)
            {
                Debug.Log("Waiting to rearrange until figures are refreshed");
                yield return null;
            }
            else
                break;
        }

        //Debug.LogFormat("Rearranging");

        // temporarily move figures out of the way prior to rearranging
        foreach (var fig in figures)
        {
            if (doClearPreviousArrangements || !fig.hasBeenRearranged)
                fig.transform.position = new Vector3(1000f, 0f, 0f);
        }

        //var arrangementParams = new DomeArrangementParams();
        var arrangementParams = new AisleArrangementParams();
        arrangementParams.origin = GameObject.FindGameObjectWithTag("MainCamera").transform.position - Vector3.up * 0.4f;

        foreach (var fig in figures)
        {
            if (doClearPreviousArrangements || !fig.hasBeenRearranged)
            {
                arrangementParams.incrementUntilNonIntersecting(fig.gameObject);
                fig.hasBeenRearranged = true;
            }
        }

        rearrangeInProgress = false;

        //Debug.LogFormat("Done rearranging");
    }
}

namespace MTypes
{
    class FigureArrangementParam
    {
        public string name;
        public float min;
        public float max;
        public float value;
        public float increment;
    }

    class ParametersAsTransform
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    abstract class NestedFigureArrangementParams
    {
        public List<FigureArrangementParam> parameters;

        public Vector3 origin = Vector3.up * 1f;

        public void increment(int paramIndex = 0)
        {
            var param = parameters[paramIndex];
            param.value += param.increment;
            if (param.value > param.max)
            {
                param.value = param.min;
                if (paramIndex < parameters.Count)
                {
                    Debug.LogFormat("Incrementing next parameters ({0}->{1})", param.name, parameters[paramIndex + 1].name);
                    increment(paramIndex + 1);
                }
                else
                {
                    Debug.LogError("All figure arrangement parameters have reached limit");
                }
            }
            //Debug.LogFormat("Current parameters: {0}", this);
        }

        public abstract ParametersAsTransform getCurrentTransformParameters(); // move object according to current arrangement parameters

        public void incrementUntilNonIntersecting(GameObject movingObject)
        {
            var localBounds = Misc.CalculateLocalBoundsInGlobalScale(movingObject);
            ParametersAsTransform transf;

            var globalBounds = Misc.CalculateGlobalBounds(movingObject);

            localBounds = globalBounds;
            localBounds.center = localBounds.center - movingObject.transform.position;

            //Debug.LogFormat("Local bounds: {0}   Global bounds: {1}", localBounds, globalBounds);

            var figs = GameObject.FindObjectsOfType<MFigure>();

            while (true)
            {
                transf = getCurrentTransformParameters();

                var movingBounds = localBounds;
                movingBounds.center = movingBounds.center + transf.position;

                movingBounds.extents = movingBounds.extents * 1.1f; // multiply by extra factor to leave some spacing between figures

                //Debug.LogFormat("Extents: ({0})({1})", localBounds.center, localBounds.extents);
                bool doesCollide = false;

                if (true) {
                    foreach (var fig in figs)
                    {
                        if (fig.gameObject == movingObject)
                            continue;
                        var otherBounds = fig.GetComponent<BoxCollider>().bounds;
                        if (movingBounds.Intersects(otherBounds))
                        {
                            doesCollide = true;
                            //Debug.LogFormat("{0} collides with {1}", movingBounds, otherBounds);
                            break;
                        }
                        //Debug.LogFormat("{0} does not collide with {1}", movingBounds, otherBounds);
                    }
                }
                else
                {
                    var colliders = Physics.OverlapBox(movingBounds.center, movingBounds.extents, transf.rotation);
                    
                    var thisMFig = movingObject.GetComponent<MFigure>();
                    Debug.Assert(thisMFig != null);

                    foreach (var col in colliders)
                    {
                        var mfig = col.GetComponentInParent<MFigure>();
                        if (mfig != null && mfig != thisMFig)
                        {
                            doesCollide = true;
                            break;
                        }
                    }
                }
                
                if (!doesCollide)
                    break;

                increment();
            }
            //Debug.LogFormat("Found non-intersecting transf at ({0})", this);
            movingObject.transform.position = transf.position;
            movingObject.transform.rotation = transf.rotation;
        }

        public override string ToString()
        {
            string str = "";
            foreach (var param in parameters)
            {
                str += string.Format("{0}:{1}, ", param.name, param.value);
            }
            return str.Substring(0, str.Length - 2);
        }
    }

    class DomeArrangementParams : NestedFigureArrangementParams
    {
        public DomeArrangementParams()
        {
            parameters = new List<FigureArrangementParam>();

            parameters.Add(new FigureArrangementParam()
            {
                name = "theta",
                min = 0,
                max = 180,
                value = 0,
                increment = 2f
            });

            parameters.Add(new FigureArrangementParam()
            {
                name = "phi",
                min = 30,
                max = 80,
                value = 0,
                increment = 2f
            });

            parameters.Add(new FigureArrangementParam()
            {
                name = "radius",
                min = 1,
                max = float.PositiveInfinity,
                value = 1,
                increment = 0.5f
            });
        }

        public override ParametersAsTransform getCurrentTransformParameters()
        {
            float theta = Mathf.Deg2Rad * parameters[0].value;
            float phi = Mathf.Deg2Rad * parameters[1].value;
            float radius = parameters[2].value;

            var toReturn = new ParametersAsTransform();

            toReturn.position = origin + new Vector3(Mathf.Sin(theta), Mathf.Sin(phi), Mathf.Cos(theta)) * radius;

            var forwardVec = toReturn.position - origin;
            forwardVec.Normalize();
            toReturn.rotation = Quaternion.LookRotation(forwardVec, Vector3.RotateTowards(forwardVec, Vector3.up, Mathf.Deg2Rad * 90, 0.0f));

            return toReturn;
        }
    }

    class AisleArrangementParams : NestedFigureArrangementParams
    {
        public AisleArrangementParams()
        {
            parameters = new List<FigureArrangementParam>();

            parameters.Add(new FigureArrangementParam()
            {
                name = "x",
                min = -1,
                max = 5,
                value = -1,
                increment = 0.1f
            });

            parameters.Add(new FigureArrangementParam()
            {
                name = "y",
                min = 0,
                max = 1,
                value = 0,
                increment = 0.1f
            });

            parameters.Add(new FigureArrangementParam()
            {
                name = "z",
                min = 0.5f,
                max = float.PositiveInfinity,
                value = 0.5f,
                increment = 1f
            });
        }

        public override ParametersAsTransform getCurrentTransformParameters()
        {
            var toReturn = new ParametersAsTransform();
            toReturn.position = origin + new Vector3(parameters[0].value, parameters[1].value, parameters[2].value);

            return toReturn;
        }
    }
}
