using System.Collections.Generic;
using System.Timers;
using UnityEngine;
using Vuforia;
using System.Linq;

public class MyPlaneManager : MonoBehaviour
{
    private MyProductPlacement prd;
    public enum PlaneMode
    {
        GROUND   
    }
    #region PUBLIC_MEMBERS
    public static bool GroundPlaneHitReceived { get; private set; }
    #endregion // PUBLIC_MEMBERS

    #region PRIVATE_MEMBERS
    [SerializeField] PlaneFinderBehaviour planeFinder = null;
     
    [Header("Plane, Mid-Air, & Placement Augmentations")]
    [SerializeField] GameObject planeAugmentation;
     
    const string UnsupportedDeviceTitle = "Unsupported Device";
    const string UnsupportedDeviceBody =
        "This device has failed to start the Positional Device Tracker. " +
        "Please check the list of supported Ground Plane devices on our site: " +
        "\n\nhttps://library.vuforia.com/articles/Solution/ground-plane-supported-devices.html";

    StateManager stateManager;
    SmartTerrain smartTerrain;
    PositionalDeviceTracker positionalDeviceTracker;
    ContentPositioningBehaviour contentPositioningBehaviour;
    TouchManager touchManager;
    MyGroundPlaneUI groundPlaneUI;
    AnchorBehaviour planeAnchor;
   
    int automaticHitTestFrameCount;
    static TrackableBehaviour.Status StatusCached = TrackableBehaviour.Status.NO_POSE;
    static TrackableBehaviour.StatusInfo StatusInfoCached = TrackableBehaviour.StatusInfo.UNKNOWN;
    
    string guiText = "";
    private LineRenderer line;
    private List<GameObject> lpoints = new List<GameObject>();

    // More Strict: Property returns true when Status is Tracked and StatusInfo is Normal.
    public static bool TrackingStatusIsTrackedAndNormal
    {
        get
        {
            return
                (StatusCached == TrackableBehaviour.Status.TRACKED ||
                 StatusCached == TrackableBehaviour.Status.EXTENDED_TRACKED) &&
                StatusInfoCached == TrackableBehaviour.StatusInfo.NORMAL;
        }
    }

    // Less Strict: Property returns true when Status is Tracked/Normal or Limited/Unknown.
    public static bool TrackingStatusIsTrackedOrLimited
    {
        get
        {
            return
                ((StatusCached == TrackableBehaviour.Status.TRACKED ||
                 StatusCached == TrackableBehaviour.Status.EXTENDED_TRACKED) &&
                 StatusInfoCached == TrackableBehaviour.StatusInfo.NORMAL) ||
                (StatusCached == TrackableBehaviour.Status.LIMITED &&
                 StatusInfoCached == TrackableBehaviour.StatusInfo.UNKNOWN);
        }
    }

    bool SurfaceIndicatorVisibilityConditionsMet
    {
        // The Surface Indicator should only be visible if the following conditions
        // are true:
        // 1. Tracking Status is Tracked or Limited (sufficient for Hit Test Anchors
        // 2. Ground Plane Hit was received for this frame   
        get
        {
            return
                (TrackingStatusIsTrackedOrLimited &&
                 GroundPlaneHitReceived && Input.touchCount == 0);
        }
    }

    Timer timer;
    bool timerFinished;
    #endregion // PRIVATE_MEMBERS
    #region MONOBEHAVIOUR_METHODS
    void Start()
    {
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
        VuforiaARController.Instance.RegisterOnPauseCallback(OnVuforiaPaused);
        DeviceTrackerARController.Instance.RegisterTrackerStartedCallback(OnTrackerStarted);
        DeviceTrackerARController.Instance.RegisterDevicePoseStatusChangedCallback(OnDevicePoseStatusChanged);

        this.planeFinder.HitTestMode = HitTestMode.AUTOMATIC;
        this.planeAnchor = this.planeAugmentation.GetComponentInParent<AnchorBehaviour>();
        this.touchManager = FindObjectOfType<TouchManager>();
        this.groundPlaneUI = FindObjectOfType<MyGroundPlaneUI>();
        this.prd = FindObjectOfType<MyProductPlacement>();

        // Setup a timer to restart the DeviceTracker if tracking does not receive
        // status change from StatusInfo.RELOCALIZATION after 10 seconds.
        this.timer = new Timer(10000);
        this.timer.Elapsed += TimerFinished;
        this.timer.AutoReset = false;

        line = GetComponent<LineRenderer>();
    }

    void Update()
    {
        // The timer runs on a separate thread and we need to ResetTrackers on the main thread.
        if (this.timerFinished)
        {
            ResetTrackers();
            ResetScene();
            this.timerFinished = false;
        }
        
    }

    void LateUpdate()
    {
        // The AutomaticHitTestFrameCount is assigned the Time.frameCount in the
        // HandleAutomaticHitTest() callback method. When the LateUpdate() method
        // is then called later in the same frame, it sets GroundPlaneHitReceived
        // to true if the frame number matches. For any code that needs to check
        // the current frame value of GroundPlaneHitReceived, it should do so
        // in a LateUpdate() method.
        GroundPlaneHitReceived = (this.automaticHitTestFrameCount == Time.frameCount);

        // Surface Indicator visibility conditions rely upon GroundPlaneHitReceived,
        // so we will move this method into LateUpdate() to ensure that it is called
        // after GroundPlaneHitReceived has been updated in Update().
        SetSurfaceIndicatorVisible(SurfaceIndicatorVisibilityConditionsMet);
    }

    void OnDestroy()
    {
        VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
        VuforiaARController.Instance.UnregisterOnPauseCallback(OnVuforiaPaused);
        DeviceTrackerARController.Instance.UnregisterTrackerStartedCallback(OnTrackerStarted);
        DeviceTrackerARController.Instance.UnregisterDevicePoseStatusChangedCallback(OnDevicePoseStatusChanged);
    }

    #endregion // MONOBEHAVIOUR_METHODS


    #region GROUNDPLANE_CALLBACKS

    public void HandleAutomaticHitTest(HitTestResult result)
    {
        this.automaticHitTestFrameCount = Time.frameCount;
    }

    public void HandleInteractiveHitTest(HitTestResult result)
    {
        if (result == null)
        {
            return;
        }
        if (!groundPlaneUI.IsCanvasButtonPressed())
        {         
            // If the PlaneFinderBehaviour's Mode is Automatic, then the Interactive HitTestResult will be centered.
            // PlaneMode.Ground and PlaneMode.Placement both use PlaneFinder's ContentPositioningBehaviour
            this.contentPositioningBehaviour = this.planeFinder.GetComponent<ContentPositioningBehaviour>();
            this.contentPositioningBehaviour.DuplicateStage = true;
            
            // With each tap, the object is moved to the position of the
            // newly created anchor. Before we set any anchor, we first want
            // to verify that the Status=TRACKED/EXTENDED_TRACKED and StatusInfo=NORMAL.
            if (TrackingStatusIsTrackedAndNormal)
            {               
                //this.planeAugmentation.transform.localPosition = Vector3.zero;
                //UtilityHelper.RotateTowardCamera(this.planeAugmentation);
                this.contentPositioningBehaviour.AnchorStage = this.planeAnchor;
                this.contentPositioningBehaviour.PositionContentAtPlaneAnchor(result);
                 lpoints = prd.getPoints();
                 lpoints.Add(this.contentPositioningBehaviour.AnchorStage.gameObject);
                 if (lpoints.Count > 1)
                 {
                    for (int i = 1; i < lpoints.ToArray().Length; i++)
                    {                              
                        guiText = (Vector3.Distance(lpoints[i].transform.position, lpoints[i - 1].transform.position) * 100).ToString("F2");
                        OnGUI();                             
                    }
                 }                
            }
        }
    }


    #endregion // GROUNDPLANE_CALLBACKS

    #region PUBLIC_BUTTON_METHODS
    //Display distance on screen
    void OnGUI()
    {
        GUIStyle localStyle = new GUIStyle();
        localStyle.normal.textColor = Color.white;
        localStyle.fontSize = 80;
        GUI.Label(new Rect(90, 200, Screen.width - 20, 30), " " + guiText + " " + "cm", localStyle);
    }
 
    public static bool IsEmpty<T>(List<T> list)
    {
        if (list == null)
        {
            return true;
        }

        return !list.Any();
    }
    public void SetGroundMode()
    {
            SetMode(PlaneMode.GROUND);       
    }

    /// <summary>
    /// This method resets the augmentations and scene elements.
    /// It is called by the UI Reset Button and also by OnVuforiaPaused() callback.
    /// </summary>
    public void ResetScene()
    {
        // reset augmentations
        this.contentPositioningBehaviour.AnchorStage.gameObject.transform.position = Vector3.zero;
        this.contentPositioningBehaviour.AnchorStage.gameObject.transform.localEulerAngles = Vector3.zero;
        UtilityHelper.EnableRendererColliderCanvas(this.contentPositioningBehaviour.AnchorStage.gameObject, false);

        lpoints.Clear();
        this.groundPlaneUI.Reset();
        this.touchManager.enableRotation = false;

        guiText = $"Distance: {0:F2}";
    }

    /// <summary>
    /// This method stops and restarts the PositionalDeviceTracker.
    /// It is called by the UI Reset Button and when RELOCALIZATION status has
    /// not changed for 10 seconds.
    /// </summary>
    public void ResetTrackers()
    {
        this.smartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();
        this.positionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();

        // Stop and restart trackers
        this.smartTerrain.Stop(); // stop SmartTerrain tracker before PositionalDeviceTracker
        this.positionalDeviceTracker.Reset();
        this.smartTerrain.Start(); // start SmartTerrain tracker after PositionalDeviceTracker
    }

    #endregion // PUBLIC_BUTTON_METHODS


    #region PRIVATE_METHODS

    /// <summary>
    /// This private method is called by the UI Button handler methods.
    /// </summary>
    /// <param name="mode">PlaneMode</param>
    void SetMode(PlaneMode mode)
    {
        this.groundPlaneUI.UpdateTitle();
        this.planeFinder.enabled = (mode == PlaneMode.GROUND);
    }

    /// <summary>
    /// This method can be used to set the Ground Plane surface indicator visibility.
    /// This sample will display it when the Status=TRACKED and StatusInfo=Normal.
    /// </summary>
    /// <param name="isVisible">bool</param>
    void SetSurfaceIndicatorVisible(bool isVisible)
    {
        Renderer[] renderers = this.planeFinder.PlaneIndicator.GetComponentsInChildren<Renderer>(true);
        Canvas[] canvas = this.planeFinder.PlaneIndicator.GetComponentsInChildren<Canvas>(true);

        foreach (Canvas c in canvas)
            c.enabled = isVisible;

        foreach (Renderer r in renderers)
            r.enabled = isVisible;
    }

    /// <summary>
    /// This is a C# delegate method for the Timer:
    /// ElapsedEventHandler(object sender, ElapsedEventArgs e)
    /// </summary>
    /// <param name="source">System.Object</param>
    /// <param name="e">ElapsedEventArgs</param>
    void TimerFinished(System.Object source, ElapsedEventArgs e)
    {
        this.timerFinished = true;
    }
    #endregion // PRIVATE_METHODS


    #region VUFORIA_CALLBACKS

    void OnVuforiaStarted()
    {
      

        stateManager = TrackerManager.Instance.GetStateManager();

        // Check trackers to see if started and start if necessary
        this.positionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();
        this.smartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();

        if (this.positionalDeviceTracker != null && this.smartTerrain != null)
        {
            if (!this.positionalDeviceTracker.IsActive)
            {
                return;
            }

            if (this.positionalDeviceTracker.IsActive && !this.smartTerrain.IsActive)
                this.smartTerrain.Start();
        }
        else
        {
            if (this.positionalDeviceTracker == null)
            if (this.smartTerrain == null)
            MessageBox.DisplayMessageBox(UnsupportedDeviceTitle, UnsupportedDeviceBody, false, null);
        }
    }

    void OnVuforiaPaused(bool paused)
    {
        Debug.Log("OnVuforiaPaused(" + paused.ToString() + ") called.");
    }

    #endregion // VUFORIA_CALLBACKS


    #region DEVICE_TRACKER_CALLBACKS

    void OnTrackerStarted()
    {
        this.positionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();
        this.smartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();

        if (this.positionalDeviceTracker != null && this.smartTerrain != null)
        {
            if (!this.positionalDeviceTracker.IsActive)
            {
                return;
            }

            if (!this.smartTerrain.IsActive)
                this.smartTerrain.Start();
        }
    }

    void OnDevicePoseStatusChanged(TrackableBehaviour.Status status, TrackableBehaviour.StatusInfo statusInfo)
    {
        StatusCached = status;
        StatusInfoCached = statusInfo;

        // If the timer is running and the status is no longer Relocalizing, then stop the timer
        if (statusInfo != TrackableBehaviour.StatusInfo.RELOCALIZING && this.timer.Enabled)
        {
            this.timer.Stop();
        }

        switch (statusInfo)
        {
            case TrackableBehaviour.StatusInfo.NORMAL:
                break;
            case TrackableBehaviour.StatusInfo.UNKNOWN:
                break;
            case TrackableBehaviour.StatusInfo.INITIALIZING:
                break;
            case TrackableBehaviour.StatusInfo.EXCESSIVE_MOTION:
                break;
            case TrackableBehaviour.StatusInfo.INSUFFICIENT_FEATURES:
                break;
            case TrackableBehaviour.StatusInfo.INSUFFICIENT_LIGHT:
                break;
            case TrackableBehaviour.StatusInfo.RELOCALIZING:
                // Start a 10 second timer to Reset Device Tracker
                if (!this.timer.Enabled)
                {
                    this.timer.Start();
                }
                break;
            default:
                break;
        }
    }

    #endregion // DEVICE_TRACKER_CALLBACK_METHODS
}
