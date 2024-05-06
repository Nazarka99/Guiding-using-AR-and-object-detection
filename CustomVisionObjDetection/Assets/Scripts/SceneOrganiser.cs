using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SceneOrganiser : MonoBehaviour
{

    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static SceneOrganiser Instance;

    /// <summary>
    /// The cursor object attached to the Main Camera
    /// </summary>
    internal GameObject cursor;

    /// <summary>
    /// The label used to display the analysis on the objects in the real world
    /// </summary>
    public GameObject label;

    /// <summary>
    /// Reference to the last Label positioned
    /// </summary>
    internal Transform lastLabelPlaced;

    /// <summary>
    /// Reference to the last Label positioned
    /// </summary>
    internal TextMesh lastLabelPlacedText;

    /// <summary>
    /// Current threshold accepted for displaying the label
    /// Reduce this value to display the recognition more often
    /// </summary>
    internal float probabilityThreshold = 0.8f;

    /// <summary>
    /// The quad object hosting the imposed image captured
    /// </summary>
    private GameObject quad;

    /// <summary>
    /// Renderer of the quad object
    /// </summary>
    internal Renderer quadRenderer;

    /// <summary>
    /// Called on initialization
    /// </summary>
    private void Awake()
    {
        // Use this class instance as singleton
        Instance = this;

        // Add the ImageCapture class to this Gameobject
        gameObject.AddComponent<ImageCapture>();

        // Add the CustomVisionAnalyser class to this Gameobject
        gameObject.AddComponent<CustomVisionAnalyser>();

        // Add the CustomVisionObjects class to this Gameobject
        gameObject.AddComponent<CustomVisionObjects>();
    }

    /// <summary>
    /// Instantiate a Label in the appropriate location relative to the Main Camera.
    /// </summary>
    public void PlaceAnalysisLabel()
    {
        lastLabelPlaced = Instantiate(label.transform, cursor.transform.position, transform.rotation);
        lastLabelPlacedText = lastLabelPlaced.GetComponent<TextMesh>();
        lastLabelPlacedText.text = "";
        lastLabelPlaced.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

        // Create a GameObject to which the texture can be applied
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadRenderer = quad.GetComponent<Renderer>() as Renderer;
        Material m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        quadRenderer.material = m;

        // Here you can set the transparency of the quad. Useful for debugging
        float transparency = 0f;
        quadRenderer.material.color = new Color(1, 1, 1, transparency);

        // Set the position and scale of the quad depending on user position
        quad.transform.parent = transform;
        quad.transform.rotation = transform.rotation;

        // The quad is positioned slightly forward in font of the user
        quad.transform.localPosition = new Vector3(0.0f, 0.0f, 3.0f);

        // The quad scale as been set with the following value following experimentation,  
        // to allow the image on the quad to be as precisely imposed to the real world as possible
        quad.transform.localScale = new Vector3(3f, 1.65f, 1f);
        quad.transform.parent = null;
    }

    /// <summary>
    /// Set the Tags as Text of the last label created. 
    /// </summary>
    public void FinaliseLabel(AnalysisRootObject analysisObject)
    {
        if (analysisObject.predictions != null)
        {
            // Define the hierarchy
            List<List<string>> hierarchy = new List<List<string>>
        {
            new List<string> { "Full body" },
            new List<string> { "Middle + ground" },
            new List<string> { "Middle part", "Upper part", "Ground part" }
        };

            // Filter out predictions below the threshold
            List<Prediction> predictions = analysisObject.predictions
                .Where(p => p.probability > 0.5f)
                .ToList();

            // Sort the predictions first by the hierarchy, then by confidence
            predictions.Sort((p1, p2) =>
            {
                int hierarchyIndex1 = hierarchy.FindIndex(h => h.Contains(p1.tagName));
                int hierarchyIndex2 = hierarchy.FindIndex(h => h.Contains(p2.tagName));
                if (hierarchyIndex1 == hierarchyIndex2)
                {
                    // If same hierarchy level, sort by confidence
                    return p2.probability.CompareTo(p1.probability);
                }
                else
                {
                    // Otherwise, sort by hierarchy level
                    return hierarchyIndex1.CompareTo(hierarchyIndex2);
                }
            });

            if (predictions.Count > 0)
            {
                // The best prediction is now the first one in the list
                Prediction bestPrediction = predictions[0];

                if (bestPrediction.probability > probabilityThreshold)
                {
                    quadRenderer = quad.GetComponent<Renderer>() as Renderer;
                    Bounds quadBounds = quadRenderer.bounds;

                    // Position the label as close as possible to the Bounding Box of the prediction 
                    // At this point it will not consider depth
                    lastLabelPlaced.transform.parent = quad.transform;
                    lastLabelPlaced.transform.localPosition = CalculateBoundingBoxPosition(quadBounds, bestPrediction.boundingBox);

                    // Set the tag text
                    lastLabelPlacedText.text = bestPrediction.tagName;

                    // Cast a ray from the user's head to the currently placed label, it should hit the object detected by the Service.
                    // At that point it will reposition the label where the ray HL sensor collides with the object,
                    // (using the HL spatial tracking)
                    Debug.Log("Repositioning Label");
                    Vector3 headPosition = Camera.main.transform.position;
                    RaycastHit objHitInfo;
                    Vector3 objDirection = lastLabelPlaced.position;
                    if (Physics.Raycast(headPosition, objDirection, out objHitInfo, 30.0f, SpatialMapping.PhysicsRaycastMask))
                    {
                        lastLabelPlaced.position = objHitInfo.point;
                    }
                }

                // Remove the best prediction from the list
                predictions.RemoveAt(0);

                // Check if there's another prediction that's not in the same hierarchy level as the best prediction
                Prediction nextBestPrediction = predictions.FirstOrDefault(p => !hierarchy[hierarchy.FindIndex(h => h.Contains(bestPrediction.tagName))].Contains(p.tagName));

                if (nextBestPrediction != null)
                {
                    // Position the label as close as possible to the Bounding Box of the prediction 
                    // At this point it will not consider depth
                    Transform nextLabelPlaced = Instantiate(label.transform, cursor.transform.position, transform.rotation);
                    TextMesh nextLabelPlacedText = nextLabelPlaced.GetComponent<TextMesh>();
                    nextLabelPlacedText.text = "";
                    nextLabelPlaced.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

                    quadRenderer = quad.GetComponent<Renderer>() as Renderer;
                    Bounds quadBounds = quadRenderer.bounds;

                    nextLabelPlaced.transform.parent = quad.transform;
                    nextLabelPlaced.transform.localPosition = CalculateBoundingBoxPosition(quadBounds, nextBestPrediction.boundingBox);

                    // Set the tag text
                    lastLabelPlacedText.text = bestPrediction.tagName;

                    // Cast a ray from the user's head to the currently placed label, it should hit the object detected by the Service.
                    // At that point it will reposition the label where the ray HL sensor collides with the object,
                    // (using the HL spatial tracking)
                    Debug.Log("Repositioning Label");
                    Vector3 headPosition = Camera.main.transform.position;
                    RaycastHit objHitInfo;
                    Vector3 objDirection = lastLabelPlaced.position;
                    if (Physics.Raycast(headPosition, objDirection, out objHitInfo, 30.0f, SpatialMapping.PhysicsRaycastMask))
                    {
                        lastLabelPlaced.position = objHitInfo.point;
                    }
                }

                // Remove the best prediction from the list
                predictions.RemoveAt(0);

                // Check if there's another prediction that's not in the same hierarchy level as the best prediction
                // Prediction nextBestPrediction = predictions.FirstOrDefault(p => !hierarchy[hierarchy.FindIndex(h => h.Contains(bestPrediction.tagName))].Contains(p.tagName));

                if (nextBestPrediction != null)
                {
                    // Position the label as close as possible to the Bounding Box of the prediction 
                    // At this point it will not consider depth
                    Transform nextLabelPlaced = Instantiate(label.transform, cursor.transform.position, transform.rotation);
                    TextMesh nextLabelPlacedText = nextLabelPlaced.GetComponent<TextMesh>();
                    nextLabelPlacedText.text = "";
                    nextLabelPlaced.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

                    quadRenderer = quad.GetComponent<Renderer>() as Renderer;
                    Bounds quadBounds = quadRenderer.bounds;

                    nextLabelPlaced.transform.parent = quad.transform;
                    nextLabelPlaced.transform.localPosition = CalculateBoundingBoxPosition(quadBounds, nextBestPrediction.boundingBox);

                    // Set the tag text
                    nextLabelPlacedText.text = nextBestPrediction.tagName;

                    // Cast a ray from the user's head to the currently placed label, it should hit the object detected by the Service.
                    // At that point it will reposition the label where the ray HL sensor collides with the object,
                    // (using the HL spatial tracking)
                    Debug.Log("Repositioning Label");
                    Vector3 headPosition = Camera.main.transform.position;
                    RaycastHit objHitInfo;
                    Vector3 objDirection = nextLabelPlaced.position;
                    if (Physics.Raycast(headPosition, objDirection, out objHitInfo, 30.0f, SpatialMapping.PhysicsRaycastMask))
                    {
                        nextLabelPlaced.position = objHitInfo.point;
                    }
                }

            }
        }
        // Reset the color of the cursor
        cursor.GetComponent<Renderer>().material.color = Color.green;

        // Stop the analysis process
        ImageCapture.Instance.ResetImageCapture();
    }

    /// <summary>
    /// This method hosts a series of calculations to determine the position 
    /// of the Bounding Box on the quad created in the real world
    /// by using the Bounding Box received back alongside the Best Prediction
    /// </summary>
    public Vector3 CalculateBoundingBoxPosition(Bounds b, BoundingBox boundingBox)
    {
        Debug.Log($"BB: left {boundingBox.left}, top {boundingBox.top}, width {boundingBox.width}, height {boundingBox.height}");

        double centerFromLeft = boundingBox.left + (boundingBox.width / 2);
        double centerFromTop = boundingBox.top + (boundingBox.height / 2);
        Debug.Log($"BB CenterFromLeft {centerFromLeft}, CenterFromTop {centerFromTop}");

        double quadWidth = b.size.normalized.x;
        double quadHeight = b.size.normalized.y;
        Debug.Log($"Quad Width {b.size.normalized.x}, Quad Height {b.size.normalized.y}");

        double normalisedPos_X = (quadWidth * centerFromLeft) - (quadWidth / 2);
        double normalisedPos_Y = (quadHeight * centerFromTop) - (quadHeight / 2);

        return new Vector3((float)normalisedPos_X, (float)normalisedPos_Y, 0);
    }


}