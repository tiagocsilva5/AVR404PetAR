using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public sealed class PetCareAR : MonoBehaviour
{
    public XRReferenceImageLibrary cards;
    public GameObject[] dogModels = new GameObject[5];
    public string[] breedNames = new string[5];
    public enum DogAction { Idle = 0, Play = 1, Feed = 5, Rest = 7 }
    private static readonly int AnimationId = Animator.StringToHash("AnimationID");
    public Camera sceneCamera;
    public Camera nonARCamera;
    public ARSession session;
    public ARTrackedImageManager tracker;
    public ARPlaneManager planes;
    public ARRaycastManager raycaster;
    public ARAnchorManager anchors;
    public GameObject startupMode, scanMode, placeMode, mainMode, nonARMode;
    public static bool IsPhone => !Application.isEditor && Application.isMobilePlatform;
    public string Status { get; private set; }
    public bool CameraOpen { get; private set; }
    public bool Ready => ModelAvailable && (surfaceAnchor != null
        ? surfaceAnchor.trackingState == TrackingState.Tracking : matchingCard != null);
    public bool CanInteract => Ready && action == null;
    public string TargetLabel => surfaceAnchor != null ? "Surface anchored" : "Card tracked";
    public event Action Changed;

    private readonly List<ARRaycastHit> rayHits = new List<ARRaycastHit>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private ARAnchor surfaceAnchor;
    private Coroutine opening;
    private ARTrackedImage matchingCard;
    private GameObject dog;
    private Animator dogAnimator;
    private Coroutine action;
    private int selection;
    private bool showDog;
    private bool ModelAvailable => selection >= 0 && selection < dogModels.Length && dogModels[selection] != null;

    public void Open(int selected)
    {
        Close();
        selection = selected;
        SetMode(startupMode);
        opening = StartCoroutine(OpenCamera());
    }

    private IEnumerator OpenCamera()
    {
        SetStatus("Checking AR availability…");
        yield return ARSession.CheckAvailability();
        if (ARSession.state == ARSessionState.NeedsInstall)
        {
            SetStatus("Google Play Services for AR is required.");
            yield return ARSession.Install();
        }
        if (ARSession.state != ARSessionState.Ready && ARSession.state != ARSessionState.SessionInitializing
            && ARSession.state != ARSessionState.SessionTracking)
        {
            SetMode(nonARMode);
            SetStatus("AR is unavailable on this device.");
            opening = null;
            yield break;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            bool replied = false;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => replied = true;
            callbacks.PermissionDenied += _ => replied = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => replied = true;
            SetStatus("Allow camera access to scan the card.");
            Permission.RequestUserPermission(Permission.Camera, callbacks);
            var deadline = Time.realtimeSinceStartup + 60f;
            yield return new WaitUntil(() => replied || Time.realtimeSinceStartup >= deadline
                || Permission.HasUserAuthorizedPermission(Permission.Camera));
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                SetStatus("Camera access is off. Allow it in device settings and try again.");
                opening = null;
                yield break;
            }
        }
#endif
        session.gameObject.SetActive(true);
        sceneCamera.gameObject.SetActive(true);
        nonARCamera.gameObject.SetActive(false);
        planes.enabled = raycaster.enabled = anchors.enabled = tracker.enabled = true;
        if (planes.subsystem == null && tracker.subsystem == null)
        {
            opening = null;
            Close();
            SetStatus("This device does not support plane or card tracking.");
            yield break;
        }
        SetMode(scanMode);
        CameraOpen = true;
        opening = null;
        SetStatus("Move the phone slowly to find a flat surface or scan the matching card.");
    }

    public void ShowDog(bool value)
    {
        showDog = value;
        if (value) SetMode(mainMode);
        if (dog != null) dog.SetActive(value && Ready);
    }

    public bool PlayAction(DogAction reaction)
    {
        if (!CanInteract || dogAnimator == null || !dogAnimator.isActiveAndEnabled) return false;
        dogAnimator.SetInteger(AnimationId, (int)reaction);
        action = StartCoroutine(ReturnToIdle());
        Changed?.Invoke();
        return true;
    }

    private IEnumerator ReturnToIdle()
    {
        yield return new WaitForSeconds(2.25f);
        action = null;
        SetIdle();
        Changed?.Invoke();
    }

    private void CancelAction()
    {
        if (action == null) return;
        var activeAction = action;
        action = null;
        StopCoroutine(activeAction);
        SetIdle();
    }

    private void SetIdle()
    {
        if (dogAnimator != null) dogAnimator.SetInteger(AnimationId, (int)DogAction.Idle);
    }

    public void Close()
    {
        CancelAction();
        if (opening != null)
        {
            StopCoroutine(opening);
            opening = null;
        }
        CameraOpen = false;
        matchingCard = null;
        showDog = false;
        if (surfaceAnchor != null) Destroy(surfaceAnchor.gameObject);
        surfaceAnchor = null;
        if (tracker != null) tracker.enabled = false;
        if (planes != null) planes.enabled = false;
        if (raycaster != null) raycaster.enabled = false;
        if (anchors != null) anchors.enabled = false;
        if (sceneCamera != null) sceneCamera.gameObject.SetActive(false);
        if (session != null) session.gameObject.SetActive(false);
        if (nonARCamera != null) nonARCamera.gameObject.SetActive(true);
        SetMode(nonARMode);
        if (dog != null) Destroy(dog);
        dog = null;
        dogAnimator = null;
        Status = "";
    }

    private GameObject currentMode;

    private void SetMode(GameObject active)
    {
        if (currentMode == active) return;
        if (currentMode != null) currentMode.SetActive(false);
        else nonARMode.SetActive(false);
        currentMode = active;
        currentMode.SetActive(true);
    }

    private void TryPlaceOnTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended || TouchOverUI(touch.position)) continue;
            if (!raycaster.Raycast(touch.position, rayHits, TrackableType.PlaneWithinPolygon)) continue;
            ARRaycastHit hit = rayHits[0];
            ARPlane plane = planes.GetPlane(hit.trackableId);
            if (plane == null || plane.trackingState != TrackingState.Tracking) continue;
            surfaceAnchor = anchors.AttachAnchor(plane, hit.pose);
            if (surfaceAnchor != null) return;
        }
    }

    private bool TouchOverUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
        foreach (var hit in uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void LateUpdate()
    {
        if (!CameraOpen) return;

        matchingCard = null;
        bool wrongCard = false;
        string target = CardName(selection);
        foreach (var tracked in tracker.trackables)
        {
            if (tracked.trackingState != TrackingState.Tracking) continue;
            if (tracked.referenceImage.name == target)
            {
                matchingCard = tracked;
                break;
            }
            wrongCard = true;
        }

        bool hasPlane = false;
        foreach (var plane in planes.trackables)
            if (plane.trackingState == TrackingState.Tracking) { hasPlane = true; break; }
        if (!showDog)
        {
            SetMode(hasPlane || matchingCard != null || surfaceAnchor != null ? placeMode : scanMode);
            if (hasPlane && surfaceAnchor == null) TryPlaceOnTouch();
        }

        if (Ready)
        {
            SetStatus(surfaceAnchor != null ? "Surface selected. Ready to place." : "Matching card found.");
            if (showDog) PositionDog();
        }
        else
        {
            CancelAction();
            if (dog != null && dog.activeSelf) dog.SetActive(false);
            if (showDog) SetStatus("Tracking lost. Move back toward the placement area.");
            else if (!ModelAvailable && (matchingCard != null || surfaceAnchor != null))
                SetStatus("Tracking ready. Assign the 3D model for this dog.");
            else if (hasPlane) SetStatus("Tap a detected surface, or scan the matching card.");
            else if (wrongCard) SetStatus($"Show card {(char)('A' + selection)} for this dog.");
            else SetStatus("Move the phone slowly to find a flat surface or scan the matching card.");
        }
    }

    private void PositionDog()
    {
        var target = surfaceAnchor != null ? surfaceAnchor.transform : matchingCard.transform;
        if (dog == null)
        {
            dog = new GameObject("AR Dog");
            var model = Instantiate(dogModels[selection], dog.transform, false);
            dogAnimator = model.GetComponentInChildren<Animator>();
            if (dogAnimator != null) dogAnimator.applyRootMotion = false;
            FitModel(model.transform);
        }
        var facing = Vector3.ProjectOnPlane(sceneCamera.transform.position - target.position, Vector3.up);
        dog.transform.SetPositionAndRotation(target.position + Vector3.up * .012f,
            facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : Quaternion.identity);
        if (!dog.activeSelf) dog.SetActive(true);
    }

    private static Bounds BoundsOf(Renderer[] renderers)
    {
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static void FitModel(Transform model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        var bounds = BoundsOf(renderers);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > .001f) model.localScale *= .28f / largest;
        bounds = BoundsOf(renderers);
        model.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private void SetStatus(string message)
    {
        if (Status == message) return;
        Status = message;
        Changed?.Invoke();
    }

    public static string CardName(int index) => $"Caregiver-{(char)('A' + index)}";
}

