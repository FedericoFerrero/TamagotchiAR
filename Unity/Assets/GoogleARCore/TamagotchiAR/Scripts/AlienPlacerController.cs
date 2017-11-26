using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using GoogleARCore;
using GoogleARCore.HelloAR;

public class AlienPlacerController : MonoBehaviour
{
    /// <summary>
    /// The first-person camera being used to render the passthrough camera.
    /// </summary>
    public Camera m_firstPersonCamera;

    /// <summary>
    /// Our alien
    /// </summary>
    public GameObject AlienPrefab;

    /// <summary>
    /// A prefab for tracking and visualizing detected planes.
    /// </summary>
    public GameObject m_trackedPlanePrefab;

    /// <summary>
    /// A gameobject parenting UI for displaying the "searching for planes" snackbar.
    /// </summary>
    public GameObject m_searchingForPlaneUI;

    private List<TrackedPlane> m_newPlanes = new List<TrackedPlane>();

    private List<TrackedPlane> m_allPlanes = new List<TrackedPlane>();

    private bool isAlienInstantiated = false;

    /// <summary>
    /// The Unity Update() method.
    /// </summary>
    public void Update()
    {
        _QuitOnConnectionErrors();

        // The tracking state must be FrameTrackingState.Tracking in order to access the Frame.
        if (Frame.TrackingState != FrameTrackingState.Tracking)
        {
            const int LOST_TRACKING_SLEEP_TIMEOUT = 15;
            Screen.sleepTimeout = LOST_TRACKING_SLEEP_TIMEOUT;
            return;
        }

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Frame.GetNewPlanes(ref m_newPlanes);


        // Iterate over planes found in this frame and instantiate corresponding GameObjects to visualize them.
        for (int i = 0; i < m_newPlanes.Count; i++)
        {
            // Instantiate a plane visualization prefab and set it to track the new plane. The transform is set to
            // the origin with an identity rotation since the mesh for our prefab is updated in Unity World
            // coordinates.
            GameObject planeObject = Instantiate(m_trackedPlanePrefab, Vector3.zero, Quaternion.identity,
                transform);
            planeObject.GetComponent<TrackedPlaneVisualizer>().SetTrackedPlane(m_newPlanes[i]);

            // Apply a random grid rotation.
            planeObject.GetComponent<Renderer>().material.SetFloat("_UvRotation", Random.Range(0.0f, 360.0f));
        }

        // Disable the snackbar UI when no planes are valid.
        bool showSearchingUI = true;
        Frame.GetAllPlanes(ref m_allPlanes);
        for (int i = 0; i < m_allPlanes.Count; i++)
        {
            if (m_allPlanes[i].IsValid)
            {
                showSearchingUI = false;
                break;
            }
        }

        m_searchingForPlaneUI.SetActive(showSearchingUI);

        //  Button reset = ResetButton.GetComponent<Button>();
        //  reset.onClick.AddListener(
        //      () => { isAlienInstantiated = false; }            
        //   );



        Touch touch;
        if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
        {
            return;
        }

        TrackableHit hit;
        TrackableHitFlag raycastFilter = TrackableHitFlag.PlaneWithinBounds | TrackableHitFlag.PlaneWithinPolygon;
        // if(Session.Raycast(m_firstPersonCamera.ScreenPointToRay(touch.position),raycastFilter, out hit)&&!(isAlienInstantiated)){
        if (Session.Raycast(m_firstPersonCamera.ScreenPointToRay(touch.position), raycastFilter, out hit))
        {
            //Create an anchor to allow ARCore to track the hitpoint as understandinf of the physical world evolves
            var anchor = Session.CreateAnchor(hit.Point, Quaternion.identity);

            //Instantiate our alien as a child of the anchor; it's transform will be relative from the anchor's tracking
            var alienObject = Instantiate(AlienPrefab, hit.Point, Quaternion.identity, anchor.transform);

            //Our Alien should look at the camera, but still be flush with the plane
            alienObject.transform.LookAt(m_firstPersonCamera.transform);
            Vector3 dist = new Vector3(0, 0.08f, 0);
            alienObject.transform.Translate(dist);
            alienObject.transform.rotation = Quaternion.Euler(-90.0f, -45.0f, alienObject.transform.rotation.z);

            //Use a plane attachment component to mantain the Alien y-offset from the plane
            //Occur after anchor updates
            alienObject.GetComponent<PlaneAttachment>().Attach(hit.Plane);

            isAlienInstantiated = true;
        }
    }

    /// <summary>
    /// Quit the application if there was a connection error for the ARCore session.
    /// </summary>
    private void _QuitOnConnectionErrors()
    {
        // Do not update if ARCore is not tracking.
        if (Session.ConnectionState == SessionConnectionState.DeviceNotSupported)
        {
            _ShowAndroidToastMessage("This device does not support ARCore.");
            Application.Quit();
        }
        else if (Session.ConnectionState == SessionConnectionState.UserRejectedNeededPermission)
        {
            _ShowAndroidToastMessage("Camera permission is needed to run this application.");
            Application.Quit();
        }
        else if (Session.ConnectionState == SessionConnectionState.ConnectToServiceFailed)
        {
            _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
            Application.Quit();
        }
    }

    /// <summary>
    /// Show an Android toast message.
    /// </summary>
    /// <param name="message">Message string to show in the toast.</param>
    /// <param name="length">Toast message time length.</param>
    private static void _ShowAndroidToastMessage(string message)
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        if (unityActivity != null)
        {
            AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
            unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                    message, 0);
                toastObject.Call("show");
            }));
        }
    }
}