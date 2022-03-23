using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Vuforia;

public class MyGroundPlaneUI : MonoBehaviour
{
    #region PRIVATE_MEMBERS
    [Header("UI Elements")]
    [SerializeField] Text title = null;
    [SerializeField] Text instructions = null;
    [SerializeField] CanvasGroup screenReticle = null;

    [Header("UI Buttons")]
    [SerializeField] Toggle groundToggle = null;

    bool resetDefaultToggle = true;
    const string TitleGroundPlane = "Ground Plane";
    
    GraphicRaycaster graphicRayCaster;
    PointerEventData pointerEventData;
    EventSystem eventSystem;
 
    #endregion // PRIVATE_MEMBERS
    #region MONOBEHAVIOUR_METHODS
    void Start()
    {
        this.graphicRayCaster = FindObjectOfType<GraphicRaycaster>();
        this.eventSystem = FindObjectOfType<EventSystem>();
        DeviceTrackerARController.Instance.RegisterDevicePoseStatusChangedCallback(OnDevicePoseStatusChanged);
    }

    void Update()
    {
         this.groundToggle.interactable = MyPlaneManager.TrackingStatusIsTrackedAndNormal;
    }

    void LateUpdate()
    {
        if (MyPlaneManager.GroundPlaneHitReceived && MyPlaneManager.TrackingStatusIsTrackedAndNormal)
        {
            // We got an automatic hit test this frame
            // Hide the onscreen reticle when we get a hit test
            this.screenReticle.alpha = 0;
            this.instructions.transform.parent.gameObject.SetActive(true);
            this.instructions.enabled = true;
            this.instructions.text = "Tap to place a point";  
        }
        else
        {
            this.screenReticle.alpha = 1;
            this.instructions.transform.parent.gameObject.SetActive(true);
            this.instructions.enabled = true;

            this.instructions.text = MyPlaneManager.GroundPlaneHitReceived ?
                    "Move to get better tracking for placing an anchor" :
                    "Point device towards ground";
        }
    }

    void OnDestroy()
    {
        DeviceTrackerARController.Instance.UnregisterDevicePoseStatusChangedCallback(OnDevicePoseStatusChanged);
    }
#endregion // MONOBEHAVIOUR_METHODS


#region PUBLIC_METHODS
    /// <summary>
    /// Resets the UI Buttons and the Initialized property.
    /// It is called by PlaneManager.ResetScene().
    /// </summary>
    public void Reset()
    { 
        this.resetDefaultToggle = true;
    }

    public void UpdateTitle()
    {
           this.title.text = TitleGroundPlane;
    }

    public bool IsCanvasButtonPressed()
    {
        pointerEventData = new PointerEventData(this.eventSystem)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        this.graphicRayCaster.Raycast(pointerEventData, results);

        bool resultIsButton = false;
        foreach (RaycastResult result in results)
        {
            if (result.gameObject.GetComponentInParent<Toggle>() ||
                result.gameObject.GetComponent<Button>())
            {
                resultIsButton = true;
                break;
            }
        }
        return resultIsButton;
    }
#endregion // PUBLIC_METHODS

#region VUFORIA_CALLBACKS

    void OnDevicePoseStatusChanged(TrackableBehaviour.Status status, TrackableBehaviour.StatusInfo statusInfo)
    {
        Debug.Log("GroundPlaneUI.OnDevicePoseStatusChanged(" + status + ", " + statusInfo + ")");

        string statusMessage = "";

        switch (statusInfo)
        {
            case TrackableBehaviour.StatusInfo.NORMAL:
                statusMessage = "";
                break;
            case TrackableBehaviour.StatusInfo.UNKNOWN:
                statusMessage = "Limited Status";
                break;
            case TrackableBehaviour.StatusInfo.INITIALIZING:
                statusMessage = "Point your device to the floor and move to scan";
                break;
            case TrackableBehaviour.StatusInfo.EXCESSIVE_MOTION:
                statusMessage = "Move slower";
                break;
            case TrackableBehaviour.StatusInfo.INSUFFICIENT_FEATURES:
                statusMessage = "Not enough visual features in the scene";
                break;
            case TrackableBehaviour.StatusInfo.INSUFFICIENT_LIGHT:
                statusMessage = "Not enough light in the scene";
                break;
            case TrackableBehaviour.StatusInfo.RELOCALIZING:
                // Display a relocalization message in the UI if:
                // * No AnchorBehaviours are being tracked
                // * None of the active/tracked AnchorBehaviours are in TRACKED status

                // Set the status message now and clear it none of conditions are met.
                statusMessage = "Point back to previously seen area and rescan to relocalize.";

                StateManager stateManager = TrackerManager.Instance.GetStateManager();
                if (stateManager != null)
                {
                    // Cycle through all of the active AnchorBehaviours first.
                    foreach (TrackableBehaviour behaviour in stateManager.GetActiveTrackableBehaviours())
                    {
                        if (behaviour is AnchorBehaviour)
                        {
                            if (behaviour.CurrentStatus == TrackableBehaviour.Status.TRACKED)
                            {
                                // If at least one of the AnchorBehaviours has Tracked status,
                                // then don't display the relocalization message.
                                statusMessage = "";
                            }
                        }
                    }
                }
                break;
            default:
                statusMessage = "";
                break;
        }

        StatusMessage.Instance.Display(statusMessage);
        // Uncomment the following line to show Status and StatusInfo values
        //StatusMessage.Instance.Display(status.ToString() + " -- " + statusInfo.ToString());
    }

#endregion // VUFORIA_CALLBACKS
}
