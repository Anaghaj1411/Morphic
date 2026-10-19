using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LanternShelfController : MonoBehaviour
{
    public const string ShelfRootName = "LanternShelf";
    public const string SlotName = "LanternShelfSlot";

    private static readonly int FirstPiece = 49;
    private static readonly int LastPiece = 59;

    [SerializeField] private Transform[] shelfPieces;
    [SerializeField] private Transform lanternSlot;
    [SerializeField] private Transform placedLantern;

    private Vector3 storedPosition;
    private Quaternion storedRotation;
    private bool hasStoredPose;
    private bool lanternDeleted;
    private Camera viewCamera;
    private Vector3 storedCameraPosition;
    private Quaternion storedCameraRotation;
    private float storedCameraFieldOfView;
    private float storedCameraNearClip;
    private bool hasStoredCamera;
    private Coroutine cameraMove;

    public bool HasLantern => placedLantern != null && placedLantern.gameObject.activeInHierarchy;
    public bool IsDeleted => lanternDeleted;
    public Transform Slot => lanternSlot;
    public int BoundPieceCount => shelfPieces != null ? shelfPieces.Length : 0;

    public void BindPieces()
    {
        Transform manual = FindNamedSlot();
        if (manual != null)
        {
            lanternSlot = manual;
            return;
        }

        List<Transform> found = FindShelfPieces();
        shelfPieces = found.ToArray();
        EnsureSlot();
        if (found.Count > 0)
            AlignSlotToShelf();
    }

    public bool PlaceLantern(Transform lantern)
    {
        if (lantern == null)
            return false;

        BindPiecesIfNeeded();
        if (lanternSlot == null)
            return false;

        storedPosition = lantern.position;
        storedRotation = lantern.rotation;
        hasStoredPose = true;
        lanternDeleted = false;
        placedLantern = lantern;
        lantern.gameObject.SetActive(true);

        Bounds lanternBounds = GetWorldBounds(lantern);
        Vector3 currentBottom = new Vector3(
            lanternBounds.center.x,
            lanternBounds.min.y,
            lanternBounds.center.z);
        lantern.position += lanternSlot.position - currentBottom;
        PanToShelfView();
        return true;
    }

    public bool TakeBack()
    {
        if (placedLantern == null || !hasStoredPose)
            return false;

        placedLantern.gameObject.SetActive(true);
        placedLantern.SetPositionAndRotation(storedPosition, storedRotation);
        placedLantern = null;
        lanternDeleted = false;
        RestoreSculptView();
        return true;
    }

    public void PanToShelfView()
    {
        Camera camera = ResolveCamera();
        if (camera == null || lanternSlot == null)
            return;

        CacheCamera(camera);
        Vector3 targetPosition;
        Quaternion targetRotation;
        float targetFieldOfView;
        GetShelfCameraPose(camera, out targetPosition, out targetRotation, out targetFieldOfView);
        StartCameraMove(camera, targetPosition, targetRotation, targetFieldOfView, 0.01f);
    }

    public void RestoreSculptView()
    {
        Camera camera = ResolveCamera();
        if (camera == null || !hasStoredCamera)
            return;

        StartCameraMove(
            camera,
            storedCameraPosition,
            storedCameraRotation,
            storedCameraFieldOfView,
            storedCameraNearClip);
        hasStoredCamera = false;
    }

    private Camera ResolveCamera()
    {
        if (viewCamera != null)
            return viewCamera;

        MainMenuStartTransition transition =
            FindFirstObjectByType<MainMenuStartTransition>(FindObjectsInactive.Include);
        if (transition != null && transition.ViewCamera != null)
        {
            viewCamera = transition.ViewCamera;
            return viewCamera;
        }

        GameObject named = GameObject.Find("MainMenuViewCamera");
        if (named != null)
            viewCamera = named.GetComponent<Camera>();
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera != null)
            return viewCamera;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (cameras != null && cameras.Length > 0)
            viewCamera = cameras[0];
        return viewCamera;
    }

    private void CacheCamera(Camera camera)
    {
        if (hasStoredCamera)
            return;

        storedCameraPosition = camera.transform.position;
        storedCameraRotation = camera.transform.rotation;
        storedCameraFieldOfView = camera.fieldOfView;
        storedCameraNearClip = camera.nearClipPlane;
        hasStoredCamera = true;
    }

    private void GetShelfCameraPose(
        Camera camera,
        out Vector3 position,
        out Quaternion rotation,
        out float fieldOfView)
    {
        Bounds focusBounds = placedLantern != null
            ? GetWorldBounds(placedLantern)
            : new Bounds(lanternSlot.position, Vector3.one * 0.4f);
        focusBounds.Encapsulate(lanternSlot.position);
        focusBounds.Expand(new Vector3(0.85f, 0.55f, 0.85f));

        Vector3 focus = focusBounds.center;
        Vector3 fromCamera = focus - camera.transform.position;
        Vector3 sculptEuler = storedCameraRotation.eulerAngles;
        Vector3 planarLook = fromCamera;
        planarLook.y = 0f;
        float yaw = planarLook.sqrMagnitude > 0.002f
            ? Quaternion.LookRotation(planarLook).eulerAngles.y
            : sculptEuler.y;
        rotation = Quaternion.Euler(sculptEuler.x, yaw, sculptEuler.z);

        fieldOfView = Mathf.Clamp(
            storedCameraFieldOfView > 1f ? storedCameraFieldOfView * 0.88f : camera.fieldOfView,
            48f,
            68f);
        float halfSize = Mathf.Max(focusBounds.extents.x, Mathf.Max(focusBounds.extents.y, focusBounds.extents.z));
        float framingDistance = halfSize / Mathf.Tan(0.5f * fieldOfView * Mathf.Deg2Rad);
        float distance = Mathf.Clamp(framingDistance * 1.15f, 2.4f, 5.2f);
        position = focus - rotation * Vector3.forward * distance;
    }

    private void StartCameraMove(
        Camera camera,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float targetFieldOfView,
        float targetNearClip)
    {
        if (cameraMove != null)
            StopCoroutine(cameraMove);
        cameraMove = StartCoroutine(
            MoveCamera(camera, targetPosition, targetRotation, targetFieldOfView, targetNearClip));
    }

    private IEnumerator MoveCamera(
        Camera camera,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float targetFieldOfView,
        float targetNearClip)
    {
        Transform cameraTransform = camera.transform;
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        float startFieldOfView = camera.fieldOfView;
        float startNearClip = camera.nearClipPlane;
        const float duration = 1.45f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, eased);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            camera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, eased);
            camera.nearClipPlane = Mathf.Lerp(startNearClip, targetNearClip, eased);
            yield return null;
        }

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
        camera.fieldOfView = targetFieldOfView;
        camera.nearClipPlane = targetNearClip;
        cameraMove = null;
    }

    public bool DeleteLantern()
    {
        if (placedLantern == null)
            return false;

        placedLantern.gameObject.SetActive(false);
        placedLantern = null;
        lanternDeleted = true;
        return true;
    }

    private void Awake()
    {
        BindPiecesIfNeeded();
    }

    private void BindPiecesIfNeeded()
    {
        if (shelfPieces == null || shelfPieces.Length == 0 || lanternSlot == null)
            BindPieces();
    }

    private void EnsureSlot()
    {
        if (lanternSlot != null)
            return;

        lanternSlot = transform.Find(SlotName);
        if (lanternSlot == null)
            lanternSlot = FindNamedSlot();
    }

    private static Transform FindNamedSlot()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t].name == SlotName)
                        return transforms[t];
                }
            }
        }

        return null;
    }

    private void AlignSlotToShelf()
    {
        if (lanternSlot == null)
            return;

        Bounds bounds = GetShelfBounds();
        if (bounds.size.sqrMagnitude <= 0.0001f)
            return;

        Bounds rest = GetRestBounds(bounds);
        lanternSlot.position = new Vector3(
            rest.center.x,
            rest.max.y + 0.02f,
            rest.center.z);
    }

    private Bounds GetRestBounds(Bounds shelfBounds)
    {
        Bounds rest = new Bounds();
        bool started = false;
        if (shelfPieces != null)
        {
            for (int i = 0; i < shelfPieces.Length; i++)
            {
                if (shelfPieces[i] == null)
                    continue;

                Renderer renderer = shelfPieces[i].GetComponent<Renderer>();
                if (renderer == null)
                    renderer = shelfPieces[i].GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                    continue;

                Vector3 size = renderer.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > 0.18f)
                    continue;

                if (!started)
                {
                    rest = renderer.bounds;
                    started = true;
                }
                else
                {
                    rest.Encapsulate(renderer.bounds);
                }
            }
        }

        return started ? rest : shelfBounds;
    }

    private Bounds GetShelfBounds()
    {
        Bounds bounds = new Bounds();
        bool started = false;
        if (shelfPieces == null)
            return bounds;

        for (int i = 0; i < shelfPieces.Length; i++)
        {
            if (shelfPieces[i] == null)
                continue;

            Renderer renderer = shelfPieces[i].GetComponent<Renderer>();
            if (renderer == null)
                renderer = shelfPieces[i].GetComponentInChildren<Renderer>(true);
            if (renderer == null)
                continue;

            if (!started)
            {
                bounds = renderer.bounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static Bounds GetWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        bool started = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            if (!started)
            {
                bounds = renderers[i].bounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return bounds;
    }

    public static List<Transform> FindShelfPieces()
    {
        List<Transform> found = new List<Transform>();
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                CollectFromRoot(roots[i].transform, found);
        }

        return found;
    }

    private static void CollectFromRoot(Transform root, List<Transform> found)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (IsShelfPieceName(transforms[i].name) && !found.Contains(transforms[i]))
                found.Add(transforms[i]);
        }
    }

    public static bool IsShelfPieceName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string trimmed = objectName.Trim();
        int separator = trimmed.LastIndexOfAny(new[] { '.', '_' });
        if (separator < 0)
            return false;

        string prefix = trimmed.Substring(0, separator);
        string suffix = trimmed.Substring(separator + 1);
        int number;
        if (!int.TryParse(suffix, out number))
            return false;
        if (number < FirstPiece || number > LastPiece)
            return false;

        if (prefix.Equals("Cubee", StringComparison.OrdinalIgnoreCase))
            return true;

        return prefix.Equals("Cube", StringComparison.OrdinalIgnoreCase) && suffix.Length == 4;
    }
}
