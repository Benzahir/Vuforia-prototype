 using UnityEngine;
using Vuforia;
using System.Collections.Generic;

public class MyProductPlacement : MonoBehaviour
{
    #region PUBLIC_MEMBERS
    public bool IsPlaced { get; private set; }
    public bool AnchorIsTracking { get; private set; }
    #endregion // PUBLIC_MEMBERS


    #region PRIVATE_MEMBERS
    List<GameObject> points = new List<GameObject>();

    [Header("Augmentation Objects")]
    [SerializeField]
    GameObject point1;
    
    [Header("Control Indicators")]

    [Header("Augmentation Size")]
    [Range(0.1f, 2.0f)]
    [SerializeField] float productSize = 0.65f;

    MyGroundPlaneUI groundPlaneUI;
    Camera mainCamera;
    Ray cameraToPlaneRay;
    RaycastHit cameraToPlaneHit;

    float augmentationScale;
    Vector3 productScale;
    string floorName;

    // Property which returns whether object visibility conditions are met
    bool ChairVisibilityConditionsMet
    {
        // The object should only be visible if the following conditions are met:
        // 1. Tracking Status is Tracked or Limited
        // 2. Ground Plane Hit was received for this frame
        get
        {
            return
                MyPlaneManager.TrackingStatusIsTrackedOrLimited &&
                MyPlaneManager.GroundPlaneHitReceived;
        }
    }
    #endregion // PRIVATE_MEMBERS


    #region MONOBEHAVIOUR_METHODS
    void Start()
    {
        this.mainCamera = Camera.main;
        this.groundPlaneUI = FindObjectOfType<MyGroundPlaneUI>();
        SetupFloor();
        this.augmentationScale = VuforiaRuntimeUtilities.IsPlayMode() ? 0.1f : this.productSize;

        this.productScale =
            new Vector3(this.augmentationScale,
                        this.augmentationScale,
                        this.augmentationScale);

        this.point1.transform.localScale = this.productScale;
    }


    void Update()
    {
            if (!this.IsPlaced)
                UtilityHelper.RotateTowardCamera(this.point1);
                
        if (this.IsPlaced)
        {
           
            if (TouchManager.IsSingleFingerDragging || (VuforiaRuntimeUtilities.IsPlayMode() && Input.GetMouseButton(0)))
            {
                if (!this.groundPlaneUI.IsCanvasButtonPressed())
                {
                    this.cameraToPlaneRay = this.mainCamera.ScreenPointToRay(Input.mousePosition);

                    if (Physics.Raycast(this.cameraToPlaneRay, out this.cameraToPlaneHit))
                    {
                        if (this.cameraToPlaneHit.collider.gameObject.name == floorName)
                        {
                            this.point1.PositionAt(this.cameraToPlaneHit.point);
                       
                        }
                    }
                }
            }
        }
        else
        {
            //this.rotationIndicator.SetActive(false);
            //this.translationIndicator.SetActive(false);
        }
    }

    #endregion // MONOBEHAVIOUR_METHODS


    #region PUBLIC_METHODS
    public void Reset()
    {
        this.point1.transform.position = Vector3.zero;
        this.point1.transform.localEulerAngles = Vector3.zero;
        this.point1.transform.localScale = this.productScale;
 
    }

    // Called by Anchor_Placement's DefaultTrackableEventHandler.OnTargetFound()
    public void OnAnchorFound()
    {
        AnchorIsTracking = true;
    }

    // Called by Anchor_Placement's DefaultTrackableEventHandler.OnTargetLost()
    public void OnAnchorLost()
    {
        AnchorIsTracking = false;
    }

    public void PlaceProductAtAnchor(Transform anchor)
    {
        this.point1.transform.SetParent(anchor, true);       
        this.point1.transform.localPosition = Vector3.zero;
        this.IsPlaced = true;
        points.Add(point1);
    }

    public void PlaceProductAtAnchorFacingCamera(Transform anchor)
    {
        PlaceProductAtAnchor(anchor);
        UtilityHelper.RotateTowardCamera(this.point1);
    }

    public void DetachProductFromAnchor()
    {
        this.point1.transform.SetParent(null);
        this.IsPlaced = false;
    }
    #endregion // PUBLIC_METHODS


    #region PRIVATE_METHODS
   
    void SetupFloor()
    {
        if (VuforiaRuntimeUtilities.IsPlayMode())
        {
            this.floorName = "Emulator Ground Plane";
        }
        else
        {
            this.floorName = "Floor";
            GameObject floor = new GameObject(this.floorName, typeof(BoxCollider));
            floor.transform.SetParent(this.point1.transform.parent);
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = Vector3.one;
            floor.GetComponent<BoxCollider>().size = new Vector3(100f, 0, 100f);
        }
    }

   
    #endregion // PRIVATE_METHODS

    public List<GameObject> getPoints()
    {
        return points;
    }

    public Vector3 getPosition(int n)
    {
        n = points.IndexOf(point1);
        Vector3 pos = points[n].transform.position;
        return pos;
    }
 
}

