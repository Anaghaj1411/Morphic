using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LanternShelfController : MonoBehaviour
{
    public const string ShelfRootName = "LanternShelf";
    public const string SlotName = "LanternShelfSlot";
    public const int SlotCount = 9;

    private static readonly int FirstPiece = 49;
    private static readonly int LastPiece = 59;

    [SerializeField] private Transform[] shelfPieces;
    [SerializeField] private Transform lanternSlot;
    [SerializeField] private Transform placedLantern;
    [SerializeField] private Transform collectionRoot;
    [SerializeField] private Transform[] displayMarks;
    [SerializeField] private float collectionSpacing;

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

    public bool HasLantern =>
        placedLantern != null && placedLantern.gameObject.activeInHierarchy ||
        CollectionCount > 0;
    public bool IsDeleted => lanternDeleted;
    public Transform Slot => lanternSlot;
    public Transform CollectionRoot => EnsureCollectionRoot();
    public int BoundPieceCount => shelfPieces != null ? shelfPieces.Length : 0;
    public int CollectionCount => collectionRoot != null ? collectionRoot.childCount : 0;

    public void BindPieces()
    {
        Transform manual = FindNamedSlot();
        if (manual != null)
        {
            lanternSlot = manual;
            if (shelfPieces == null || shelfPieces.Length == 0)
                shelfPieces = FindShelfPieces().ToArray();
            CreateManualSlots();
            return;
        }

        List<Transform> found = FindShelfPieces();
        shelfPieces = found.ToArray();
        EnsureSlot();
        if (found.Count > 0 && lanternSlot == null)
            AlignSlotToShelf();
        CreateManualSlots();
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
        RestoreSculptView();
        lanternDeleted = false;
        placedLantern = null;
        return true;
    }

    public void ClearCollectionDisplays()
    {
        Transform root = EnsureCollectionRoot();
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        placedLantern = null;
        lanternDeleted = false;
    }

    public void RemoveLastDisplay()
    {
        Transform root = EnsureCollectionRoot();
        if (root.childCount == 0)
            return;

        Transform last = root.GetChild(root.childCount - 1);
        if (last != null)
            UnityEngine.Object.DestroyImmediate(last.gameObject);
        placedLantern = root.childCount > 0 ? root.GetChild(root.childCount - 1) : null;
    }

    public void SeatAtSlot(Transform lantern, int slotIndex)
    {
        if (lantern == null)
            return;

        EnsureSlot();
        lantern.SetParent(EnsureCollectionRoot(), true);
        CreateManualSlots();
        Transform seat = GetSlotTransform(slotIndex);
        lantern.rotation = seat != null ? seat.rotation : Quaternion.identity;

        Vector3 slotPos = GetMarkPosition(slotIndex);
        Bounds lanternBounds = GetWorldBounds(lantern);
        Vector3 currentBottom = new Vector3(
            lanternBounds.center.x,
            lanternBounds.min.y,
            lanternBounds.center.z);
        lantern.position += slotPos - currentBottom;
        placedLantern = lantern;
        lanternDeleted = false;
    }

    public Vector3 GetSlotPosition(Transform lantern, int slotIndex)
    {
        EnsureSlot();
        CreateManualSlots();
        return GetMarkPosition(slotIndex);
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
        EncapsulateCollection(ref focusBounds);
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

    public void CreateManualSlots()
    {
        if (displayMarks != null && displayMarks.Length >= SlotCount)
            return;

        EnsureSlot();
        List<Transform> slots = FindAllNamedSlots();
        if (lanternSlot != null && !slots.Contains(lanternSlot))
            slots.Insert(0, lanternSlot);

        slots.Sort((a, b) => string.CompareOrdinal(SlotSortKey(a), SlotSortKey(b)));

        Transform parent = lanternSlot != null ? lanternSlot.parent : transform;
        if (parent == null)
            parent = transform;
        Quaternion rotation = lanternSlot != null ? lanternSlot.rotation : Quaternion.identity;
        Vector3 origin = lanternSlot != null ? lanternSlot.position : transform.position;

        for (int i = slots.Count; i < SlotCount; i++)
        {
            GameObject slotObject = new GameObject(SlotObjectName(i));
            Transform slot = slotObject.transform;
            slot.SetParent(parent, true);
            slot.position = origin + Vector3.right * (0.45f * i);
            slot.rotation = rotation;
            slots.Add(slot);
        }

        displayMarks = slots.ToArray();
        if (displayMarks.Length > 0)
            lanternSlot = displayMarks[0];
    }

    private Vector3 GetMarkPosition(int slotIndex)
    {
        Transform slot = GetSlotTransform(slotIndex);
        if (slot != null)
            return slot.position;
        EnsureSlot();
        return lanternSlot != null ? lanternSlot.position : Vector3.zero;
    }

    private Transform GetSlotTransform(int slotIndex)
    {
        if (displayMarks == null || displayMarks.Length == 0)
            CreateManualSlots();
        if (displayMarks == null || displayMarks.Length == 0)
            return lanternSlot;
        int index = Mathf.Clamp(slotIndex, 0, displayMarks.Length - 1);
        return displayMarks[index];
    }

    private static string SlotObjectName(int index)
    {
        return index <= 0 ? SlotName : SlotName + "_" + (index + 1);
    }

    private static string SlotSortKey(Transform slot)
    {
        if (slot == null)
            return "99";
        if (slot.name == SlotName)
            return "1";
        return slot.name;
    }

    private static List<Transform> FindAllNamedSlots()
    {
        List<Transform> found = new List<Transform>();
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
                    if (transforms[t] != null &&
                        transforms[t].name.StartsWith(SlotName, StringComparison.Ordinal) &&
                        !found.Contains(transforms[t]))
                        found.Add(transforms[t]);
                }
            }
        }

        return found;
    }

    private Transform EnsureCollectionRoot()
    {
        if (collectionRoot != null)
            return collectionRoot;

        Transform existing = transform.Find("LanternCollection");
        if (existing != null)
        {
            collectionRoot = existing;
            return collectionRoot;
        }

        GameObject root = new GameObject("LanternCollection");
        collectionRoot = root.transform;
        collectionRoot.SetParent(transform, false);
        collectionRoot.localPosition = Vector3.zero;
        collectionRoot.localRotation = Quaternion.identity;
        collectionRoot.localScale = Vector3.one;
        return collectionRoot;
    }

    private Vector3 SlotOffset(Transform lantern, int slotIndex)
    {
        Vector3 along;
        float spacing;
        if (!TryGetRestLayout(out along, out spacing))
        {
            along = lanternSlot != null
                ? Vector3.ProjectOnPlane(lanternSlot.right, Vector3.up)
                : Vector3.right;
            if (along.sqrMagnitude < 0.01f)
                along = Vector3.right;
            along.Normalize();

            spacing = collectionSpacing;
            if (spacing <= 0.01f && lantern != null)
            {
                Bounds bounds = GetWorldBounds(lantern);
                spacing = Mathf.Max(bounds.size.x, bounds.size.z) * 1.35f + 0.12f;
            }

            if (spacing <= 0.01f)
                spacing = 0.55f;
        }

        return along * (slotIndex * spacing);
    }

    private bool TryGetRestLayout(out Vector3 along, out float spacing)
    {
        along = Vector3.right;
        spacing = 0f;
        if (shelfPieces == null || lanternSlot == null)
            return false;

        List<Vector3> pads = new List<Vector3>();
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

            pads.Add(renderer.bounds.center);
        }

        if (pads.Count < 2)
            return false;

        float minX = pads[0].x;
        float maxX = pads[0].x;
        float minZ = pads[0].z;
        float maxZ = pads[0].z;
        for (int i = 1; i < pads.Count; i++)
        {
            minX = Mathf.Min(minX, pads[i].x);
            maxX = Mathf.Max(maxX, pads[i].x);
            minZ = Mathf.Min(minZ, pads[i].z);
            maxZ = Mathf.Max(maxZ, pads[i].z);
        }

        bool useX = (maxX - minX) >= (maxZ - minZ);
        along = useX ? Vector3.right : Vector3.forward;
        if (Vector3.Dot(along, new Vector3(maxX, 0f, maxZ) - lanternSlot.position) < 0f)
            along = -along;

        float slotY = lanternSlot.position.y;
        List<float> row = new List<float>();
        for (int i = 0; i < pads.Count; i++)
        {
            if (Mathf.Abs(pads[i].y - slotY) > 0.12f)
                continue;
            row.Add(useX ? pads[i].x : pads[i].z);
        }

        if (row.Count < 2)
            return false;

        row.Sort();
        List<float> gaps = new List<float>();
        for (int i = 1; i < row.Count; i++)
        {
            float gap = row[i] - row[i - 1];
            if (gap > 0.08f)
                gaps.Add(gap);
        }

        if (gaps.Count == 0)
            return false;

        gaps.Sort();
        spacing = gaps[gaps.Count / 2];
        return spacing > 0.08f;
    }

    private void EncapsulateCollection(ref Bounds focusBounds)
    {
        if (collectionRoot == null)
            return;

        for (int i = 0; i < collectionRoot.childCount; i++)
        {
            Transform child = collectionRoot.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy)
                focusBounds.Encapsulate(GetWorldBounds(child));
        }
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

    private void OnDrawGizmos()
    {
        if (displayMarks == null)
            return;

        for (int i = 0; i < displayMarks.Length; i++)
        {
            if (displayMarks[i] == null)
                continue;

            Gizmos.color = i == 0
                ? new Color(1f, 0.25f, 0.75f, 0.9f)
                : new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(displayMarks[i].position, 0.1f);
        }
    }
}
