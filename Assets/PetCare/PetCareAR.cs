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
    private static readonly int BreathingState = Animator.StringToHash("Base Layer.Breathing");
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
    public bool CanInteract => Ready && dogAnimator != null && action == null;
    public bool IsGreeting { get; private set; }
    public string TargetLabel => surfaceAnchor != null ? "Surface anchored" : "Card tracked";
    public event Action Changed;
    public event Action SurfacePlaced;

    private readonly List<ARRaycastHit> rayHits = new List<ARRaycastHit>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private ARAnchor surfaceAnchor;
    private Coroutine opening;
    private ARTrackedImage matchingCard;
    private GameObject dog, marker, foodBowl;
    private Animator dogAnimator;
    private Transform muzzle;
    private Quaternion dogHeading;
    private bool surfacePlacementReported;
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

    public bool PlayAction(DogAction reaction, Action completed = null)
    {
        if (!CanInteract || dogAnimator == null || !dogAnimator.isActiveAndEnabled) return false;
        if (reaction == DogAction.Feed) ShowFoodBowl();
        BeginReaction(reaction, reaction == DogAction.Feed ? 3.2f : 2.25f, completed);
        return true;
    }

    private void BeginReaction(DogAction reaction, float duration, Action completed = null)
    {
        dogAnimator.SetInteger(AnimationId, (int)reaction);
        action = StartCoroutine(ReturnToIdle(reaction, duration, completed));
        Changed?.Invoke();
    }

    private IEnumerator ReturnToIdle(DogAction reaction, float duration, Action completed)
    {
        yield return new WaitForSeconds(duration);
        SetIdle();
        if (reaction == DogAction.Feed)
        {
            float deadline = Time.time + 1.8f;
            while (Ready && Time.time < deadline &&
                   (dogAnimator.IsInTransition(0) ||
                    dogAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash != BreathingState))
                yield return null;
        }
        if (foodBowl != null) foodBowl.SetActive(false);
        IsGreeting = false;
        action = null;
        if (Ready) completed?.Invoke();
        Changed?.Invoke();
    }

    private void ShowFoodBowl()
    {
        if (foodBowl == null)
        {
            foodBowl = PetARProps.CreateFoodBowl();
            foodBowl.transform.SetParent(dog.transform, false);
        }
        Vector3 point = muzzle == null ? Vector3.forward * .14f : dog.transform.InverseTransformPoint(muzzle.position);
        foodBowl.transform.localPosition = new Vector3(point.x, 0f, Mathf.Clamp(point.z + .007f, .075f, .24f));
        foodBowl.SetActive(true);
    }

    private void CancelAction()
    {
        if (action == null) return;
        var activeAction = action;
        action = null;
        StopCoroutine(activeAction);
        IsGreeting = false;
        if (foodBowl != null) foodBowl.SetActive(false);
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
        surfacePlacementReported = false;
        if (marker != null) marker.SetActive(false);
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
        if (dog != null)
        {
            dog.SetActive(false);
            Destroy(dog);
        }
        dog = null;
        dogAnimator = null;
        muzzle = null;
        foodBowl = null;
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
            if (!TryGetSurface(touch.position, out var hit, out var plane)) continue;
            surfaceAnchor = anchors.AttachAnchor(plane, hit.pose);
            if (surfaceAnchor != null) return;
        }
    }

    private bool TryGetSurface(Vector2 position, out ARRaycastHit hit, out ARPlane plane)
    {
        if (raycaster.Raycast(position, rayHits, TrackableType.PlaneWithinPolygon))
        {
            foreach (var candidate in rayHits)
            {
                plane = planes.GetPlane(candidate.trackableId);
                if (plane == null || plane.trackingState != TrackingState.Tracking) continue;
                hit = candidate;
                return true;
            }
        }
        hit = default;
        plane = null;
        return false;
    }

    private bool UpdateMarker(bool searching)
    {
        if (searching && TryGetSurface(sceneCamera.pixelRect.center, out var hit, out _))
        {
            if (marker == null)
            {
                marker = PetARProps.CreatePlacementMarker();
                marker.transform.SetParent(transform, true);
            }
            marker.transform.SetPositionAndRotation(hit.pose.position + hit.pose.up * .008f, hit.pose.rotation);
            if (!marker.activeSelf) marker.SetActive(true);
            return true;
        }
        if (marker != null && marker.activeSelf) marker.SetActive(false);
        return false;
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

        bool markerVisible = UpdateMarker(hasPlane && !showDog && surfaceAnchor == null);

        if (Ready)
        {
            SetStatus(surfaceAnchor != null ? "Surface ready." : "Matching card found.");
            if (!showDog && surfaceAnchor != null && !surfacePlacementReported)
            {
                surfacePlacementReported = true;
                SurfacePlaced?.Invoke();
            }
            if (showDog) PositionDog();
        }
        else
        {
            CancelAction();
            if (dog != null && dog.activeSelf) dog.SetActive(false);
            if (showDog) SetStatus("Tracking lost. Move back toward the placement area.");
            else if (!ModelAvailable && (matchingCard != null || surfaceAnchor != null))
                SetStatus("Tracking ready. Assign the 3D model for this dog.");
            else if (surfaceAnchor != null) SetStatus("Holding the placement spot…");
            else if (markerVisible) SetStatus("Tap the ring to place your dog.");
            else if (hasPlane) SetStatus("Aim at the detected surface, or scan the matching card.");
            else if (wrongCard) SetStatus($"Show card {(char)('A' + selection)} for this dog.");
            else SetStatus("Move the phone slowly to find a flat surface or scan the matching card.");
        }
    }

    private void PositionDog()
    {
        var target = surfaceAnchor != null ? surfaceAnchor.transform : matchingCard.transform;
        bool firstPlacement = dog == null;
        if (firstPlacement)
        {
            dog = new GameObject("AR Dog");
            var model = Instantiate(dogModels[selection], dog.transform, false);
            dogAnimator = model.GetComponentInChildren<Animator>();
            if (dogAnimator != null) dogAnimator.applyRootMotion = false;
            FitModel(model.transform);
            foreach (var bone in model.GetComponentsInChildren<Transform>())
            {
                if (bone.name != "DEF-nose") continue;
                muzzle = bone;
                Vector3 forward = Vector3.ProjectOnPlane(bone.position, Vector3.up);
                if (forward.sqrMagnitude > .0001f)
                {
                    var turn = Quaternion.Euler(0f, Vector3.SignedAngle(forward, Vector3.forward, Vector3.up), 0f);
                    model.transform.localPosition = turn * model.transform.localPosition;
                    model.transform.localRotation = turn * model.transform.localRotation;
                }
                break;
            }
            Vector3 facing = Vector3.ProjectOnPlane(sceneCamera.transform.position - target.position, Vector3.up);
            dogHeading = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
        }
        dog.transform.SetPositionAndRotation(target.position + Vector3.up * .004f, dogHeading);
        if (!dog.activeSelf) dog.SetActive(true);
        if (firstPlacement && dogAnimator != null)
        {
            IsGreeting = true;
            BeginReaction(DogAction.Play, 1.5f);
        }
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
