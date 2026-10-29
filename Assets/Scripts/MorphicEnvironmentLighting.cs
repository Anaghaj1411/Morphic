using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// Interior lamps that live in the scene so you can drag them in the Scene view.
[ExecuteAlways]
public sealed class MorphicEnvironmentLighting : MonoBehaviour
{
    public const string RootName = "Morphic Environment Lights";

    private static readonly Color WarmKey = new Color(1f, 0.78f, 0.52f);
    private static readonly Color WarmFill = new Color(1f, 0.88f, 0.72f);
    private static readonly Color CoolFill = new Color(0.72f, 0.84f, 1f);
    private static readonly Color ExtraWarm = new Color(1f, 0.55f, 0.28f);

    [SerializeField]
    [Tooltip("Project → Resources → MorphicLightingSettings.")]
    private MorphicLightingSettings settings;

    private static readonly string[] WorldLightNames =
    {
        "World Light",
        "World Light 2",
        "World Light 3",
        "World Light 4",
        "World Light 5",
        "World Light 6",
        "World Light 7",
        "World Light 8"
    };

    private static readonly Vector3[] WorldLightOffsets =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(14f, 0f, 8f),
        new Vector3(-14f, 1f, 8f),
        new Vector3(14f, 1f, -8f),
        new Vector3(-14f, 0f, -8f),
        new Vector3(0f, 4f, 12f),
        new Vector3(18f, 2f, 0f),
        new Vector3(-18f, 2f, 2f)
    };

    [SerializeField]
    [Tooltip("Select these in Hierarchy. Intensity, Range, and Transform are edited on each Light itself.")]
    private Light[] worldLights;
    [SerializeField] private bool worldLightReady;
    [SerializeField] private Light restaurantKey;
    [SerializeField] private Light restaurantFill;
    [SerializeField] private Light potteryShop;
    [SerializeField] private Light workbench;
    [SerializeField] private Light street;
    [SerializeField] private Light cloud;
    [SerializeField] private Light cloud2;
    [SerializeField] private Light cloud3;
    [SerializeField] private Light cloud4;
    [SerializeField] private Light cloud5;

    [SerializeField] private float restaurantKeyBase = 2.6f;
    [SerializeField] private float restaurantFillBase = 1.1f;
    [SerializeField] private float potteryShopBase = 2.4f;
    [SerializeField] private float workbenchBase = 2.7f;
    [SerializeField] private float streetBase = 0.8f;
    [SerializeField] private float cloudBase = 18f;

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorInstall()
    {
        EditorApplication.delayCall -= EnsureLoadedScenes;
        EditorApplication.delayCall += EnsureLoadedScenes;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => EnsureScene(scene);
    }

    private static void EnsureLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            EnsureScene(SceneManager.GetSceneAt(i));
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PlayInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;
        EnsureScene(scene);
    }

    private static void EnsureScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

#if UNITY_EDITOR
        if (EditorSceneManager.IsPreviewScene(scene))
            return;
#endif

        MorphicEnvironmentLighting existing = FindRig(scene);
        if (existing != null)
        {
            existing.BindChildren();
            existing.EnsureWorldLights();
            existing.EnsureCloudLights();
            existing.ApplySettings();
            return;
        }

        Transform restaurantView = FindInScene(scene, "RestaurantCameraTarget");
        Transform studioView = FindInScene(scene, "PotteryStudioCameraTarget");
        Transform shopView = FindInScene(scene, "PotteryShopCameraTarget");
        if (restaurantView == null && studioView == null && shopView == null)
            return;

        MorphicLightingSettings lightingSettings =
            Resources.Load<MorphicLightingSettings>("MorphicLightingSettings");

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        MorphicEnvironmentLighting rig = root.AddComponent<MorphicEnvironmentLighting>();
        rig.settings = lightingSettings;

        float sun = ReadSunIntensity();
        float key = Mathf.Clamp(sun * 26f, 1.6f, 4.2f);
        float fill = Mathf.Clamp(sun * 11f, 0.6f, 1.8f);
        bool saved = lightingSettings != null && lightingSettings.useSavedPositions;

        if (restaurantView != null)
        {
            rig.restaurantKeyBase = key;
            rig.restaurantFillBase = fill;
            rig.restaurantKey = CreatePoint(root.transform, "Restaurant Key",
                SavedOrAuto(saved, lightingSettings != null ? lightingSettings.restaurantKeyPosition : Vector3.zero,
                    InFront(restaurantView, 3.2f, 0.85f, 0.2f)),
                WarmKey, key, 11f, true);
            rig.restaurantFill = CreatePoint(root.transform, "Restaurant Fill",
                SavedOrAuto(saved, lightingSettings != null ? lightingSettings.restaurantFillPosition : Vector3.zero,
                    InFront(restaurantView, 1.4f, 1.6f, -1.8f)),
                WarmFill, fill, 10f, false);
        }

        if (shopView != null)
        {
            rig.potteryShopBase = key * 0.92f;
            rig.potteryShop = CreatePoint(root.transform, "Pottery Shop Key",
                SavedOrAuto(saved, lightingSettings != null ? lightingSettings.potteryShopPosition : Vector3.zero,
                    InFront(shopView, 2.4f, 1.1f, 0.15f)),
                WarmKey, rig.potteryShopBase, 9f, true);
        }

        if (studioView != null)
        {
            rig.workbenchBase = key * 1.05f;
            rig.workbench = CreatePoint(root.transform, "Workbench Key",
                SavedOrAuto(saved, lightingSettings != null ? lightingSettings.workbenchPosition : Vector3.zero,
                    InFront(studioView, 1.7f, 0.7f, 0.25f)),
                WarmKey, rig.workbenchBase, 6.5f, false);
        }

        Transform streetA = restaurantView != null ? restaurantView : studioView;
        Transform streetB = shopView != null ? shopView : studioView;
        if (streetA != null && streetB != null)
        {
            Vector3 mid = (streetA.position + streetB.position) * 0.5f;
            mid.y = Mathf.Max(streetA.position.y, streetB.position.y) + 2.4f;
            rig.streetBase = fill * 0.7f;
            rig.street = CreatePoint(root.transform, "Street Fill",
                SavedOrAuto(saved, lightingSettings != null ? lightingSettings.streetPosition : Vector3.zero, mid),
                CoolFill, rig.streetBase, 14f, false);
        }

        rig.EnsureWorldLights();
        rig.EnsureCloudLights();

        rig.ApplySettings();
        rig.StorePositions(markAsUserPlaced: false);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(root, "Add Morphic Environment Lights");
            EditorSceneManager.MarkSceneDirty(scene);
        }
#endif
    }

    private void Update()
    {
        BindChildren();
        EnsureWorldLights();
        ApplySettings();
        StorePositions(markAsUserPlaced: true);
    }

    private void BindChildren()
    {
        if (settings == null)
            settings = Resources.Load<MorphicLightingSettings>("MorphicLightingSettings");

        if (restaurantKey == null)
            restaurantKey = ChildLight("Restaurant Key");
        if (restaurantFill == null)
            restaurantFill = ChildLight("Restaurant Fill");
        if (potteryShop == null)
            potteryShop = ChildLight("Pottery Shop Key");
        if (workbench == null)
            workbench = ChildLight("Workbench Key");
        if (street == null)
            street = ChildLight("Street Fill");
        if (cloud == null)
            cloud = ChildLight("Cloud Light");
        if (cloud2 == null)
            cloud2 = ChildLight("Cloud Light 2");
        if (cloud3 == null)
            cloud3 = ChildLight("Cloud Light 3");
        if (cloud4 == null)
            cloud4 = ChildLight("Cloud Light 4");
        if (cloud5 == null)
            cloud5 = ChildLight("Cloud Light 5");
    }

    public void EnsureWorldLights()
    {
        if (worldLights == null || worldLights.Length != WorldLightNames.Length)
            System.Array.Resize(ref worldLights, WorldLightNames.Length);

        Vector3 origin = new Vector3(-24.6f, 16f, -7.5f);
        bool created = false;

        for (int i = 0; i < WorldLightNames.Length; i++)
        {
            if (worldLights[i] != null && worldLights[i].name == "Directional Light")
                worldLights[i] = null;
            if (worldLights[i] == null)
                worldLights[i] = ChildLight(WorldLightNames[i]);

            if (worldLights[i] == null)
            {
                worldLights[i] = CreatePoint(
                    transform,
                    WorldLightNames[i],
                    origin + WorldLightOffsets[i],
                    Color.white,
                    80f,
                    100f,
                    false);
                created = true;
                continue;
            }

            if (worldLights[i].type != LightType.Directional)
                continue;

            worldLights[i].type = LightType.Point;
            worldLights[i].color = Color.white;
            worldLights[i].intensity = 80f;
            worldLights[i].range = 100f;
            worldLights[i].shadows = LightShadows.None;
            worldLights[i].transform.rotation = Quaternion.identity;
            created = true;
        }

        worldLightReady = true;
        if (created)
            MarkLightingDirty();
    }

    public void EnsureCloudLights()
    {
        BindChildren();
        cloudBase = 18f;
        float range = settings != null ? settings.cloudRange : 180f;
        Vector3 cluster = FindCloudCluster(gameObject.scene);

        cloud = EnsureNamedCloud("Cloud Light", cloud, CloudSaved(1), cluster);
        cloud2 = EnsureNamedCloud("Cloud Light 2", cloud2, CloudSaved(2), cluster + new Vector3(14f, 4f, 10f));
        cloud3 = EnsureNamedCloud("Cloud Light 3", cloud3, CloudSaved(3), cluster + new Vector3(-14f, 3f, 10f));
        cloud4 = EnsureNamedCloud("Cloud Light 4", cloud4, CloudSaved(4), cluster + new Vector3(12f, 5f, -12f));
        cloud5 = EnsureNamedCloud("Cloud Light 5", cloud5, CloudSaved(5), cluster + new Vector3(-12f, 4f, -11f));
    }

    private Light EnsureNamedCloud(string name, Light existing, Vector3 savedPos, Vector3 autoPos)
    {
        if (existing == null)
        {
            bool saved = settings != null && settings.useSavedPositions && savedPos.sqrMagnitude > 0.01f;
            float range = settings != null ? settings.cloudRange : 180f;
            existing = CreatePoint(transform, name, SavedOrAuto(saved, savedPos, autoPos),
                CoolFill, cloudBase, range, true);
        }

        ConfigureSkyLight(existing);
        return existing;
    }

    private Vector3 CloudSaved(int index)
    {
        if (settings == null)
            return Vector3.zero;
        switch (index)
        {
            case 1: return settings.cloudPosition;
            case 2: return settings.cloudPosition2;
            case 3: return settings.cloudPosition3;
            case 4: return settings.cloudPosition4;
            case 5: return settings.cloudPosition5;
            default: return Vector3.zero;
        }
    }

    private Light ChildLight(string name)
    {
        Transform child = transform.Find(name);
        return child != null ? child.GetComponent<Light>() : null;
    }

    private void ApplySettings()
    {
        if (settings == null)
            return;

        float global = Mathf.Max(0f, settings.globalBrightness);
        ApplyLight(restaurantKey, restaurantKeyBase * settings.restaurantKey * global, WarmKey, settings.warmth);
        ApplyLight(restaurantFill, restaurantFillBase * settings.restaurantFill * global, WarmFill, settings.warmth);
        ApplyLight(potteryShop, potteryShopBase * settings.potteryShop * global, WarmKey, settings.warmth);
        ApplyLight(workbench, workbenchBase * settings.workbench * global, WarmKey, settings.warmth);
        ApplyLight(street, streetBase * settings.street * global, CoolFill,
            Mathf.Lerp(1.15f, 0.35f, Mathf.Clamp01(settings.warmth)));
        float cloudIntensity = cloudBase * settings.clouds * global;
        ApplyLight(cloud, cloudIntensity, CoolFill, 0.35f);
        ApplyLight(cloud2, cloudIntensity, CoolFill, 0.35f);
        ApplyLight(cloud3, cloudIntensity, CoolFill, 0.35f);
        ApplyLight(cloud4, cloudIntensity, CoolFill, 0.35f);
        ApplyLight(cloud5, cloudIntensity, CoolFill, 0.35f);
        ConfigureSkyLight(cloud);
        ConfigureSkyLight(cloud2);
        ConfigureSkyLight(cloud3);
        ConfigureSkyLight(cloud4);
        ConfigureSkyLight(cloud5);

        // Give the sky light the shadow atlas so its shadows actually show.
        SetShadows(restaurantKey, LightShadows.None);
        SetShadows(restaurantFill, LightShadows.None);
        SetShadows(potteryShop, LightShadows.None);
        SetShadows(workbench, LightShadows.None);
        SetShadows(street, LightShadows.None);
    }

    private void MarkLightingDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void ConfigureSkyLight(Light light)
    {
        if (light == null)
            return;

        if (light.type != LightType.Spot)
        {
            light.type = LightType.Spot;
            Vector3 target = new Vector3(-24.6f, 0.4f, -7.5f);
            Vector3 toGround = target - light.transform.position;
            if (toGround.sqrMagnitude > 0.01f)
                light.transform.rotation = Quaternion.LookRotation(toGround.normalized);
        }

        light.range = settings != null ? Mathf.Max(40f, settings.cloudRange) : 180f;
        light.spotAngle = 110f;
        light.innerSpotAngle = 70f;
        light.shadows = LightShadows.Hard;
        light.shadowStrength = 1f;
        light.shadowBias = 0.012f;
        light.shadowNormalBias = 0.06f;
        light.shadowNearPlane = 0.4f;

        UniversalAdditionalLightData extra = light.GetUniversalAdditionalLightData();
        extra.usePipelineSettings = false;
        extra.softShadowQuality = SoftShadowQuality.Low;
    }

    private static void SetShadows(Light light, LightShadows mode)
    {
        if (light != null)
            light.shadows = mode;
    }

    private void StorePositions(bool markAsUserPlaced)
    {
        if (settings == null)
            return;

        bool moved = false;
        moved |= WritePos(ref settings.restaurantKeyPosition, restaurantKey);
        moved |= WritePos(ref settings.restaurantFillPosition, restaurantFill);
        moved |= WritePos(ref settings.potteryShopPosition, potteryShop);
        moved |= WritePos(ref settings.workbenchPosition, workbench);
        moved |= WritePos(ref settings.streetPosition, street);
        moved |= WritePos(ref settings.cloudPosition, cloud);
        moved |= WritePos(ref settings.cloudPosition2, cloud2);
        moved |= WritePos(ref settings.cloudPosition3, cloud3);
        moved |= WritePos(ref settings.cloudPosition4, cloud4);
        moved |= WritePos(ref settings.cloudPosition5, cloud5);
        if (!moved)
            return;

        if (markAsUserPlaced)
            settings.useSavedPositions = true;

#if UNITY_EDITOR
        EditorUtility.SetDirty(settings);
#endif
    }

    private static bool WritePos(ref Vector3 stored, Light light)
    {
        if (light == null)
            return false;
        Vector3 pos = light.transform.position;
        if ((stored - pos).sqrMagnitude < 0.0001f)
            return false;
        stored = pos;
        return true;
    }

    private static void ApplyLight(Light light, float intensity, Color baseColor, float warmth)
    {
        if (light == null)
            return;

        light.intensity = Mathf.Max(0f, intensity);
        light.color = WarmthColor(baseColor, warmth);
    }

    private static Color WarmthColor(Color original, float warmth)
    {
        warmth = Mathf.Clamp(warmth, 0f, 2f);
        if (warmth <= 1f)
            return Color.Lerp(CoolFill, original, warmth);
        return Color.Lerp(original, ExtraWarm, warmth - 1f);
    }

    private static Vector3 SavedOrAuto(bool saved, Vector3 savedPos, Vector3 autoPos)
    {
        return saved ? savedPos : autoPos;
    }

    private static MorphicEnvironmentLighting FindRig(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == RootName)
                return roots[i].GetComponent<MorphicEnvironmentLighting>();
        }

        return null;
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindRecursive(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindRecursive(t.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static float ReadSunIntensity()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
                return Mathf.Max(0.08f, lights[i].intensity);
        }

        return 0.1f;
    }

    private static Vector3 FindCloudCluster(Scene scene)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        MainMenuCloudMover[] clouds = FindObjectsByType<MainMenuCloudMover>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] == null || clouds[i].gameObject.scene != scene)
                continue;
            sum += clouds[i].transform.position;
            count++;
        }

        Vector3 center;
        if (count > 0)
            center = sum / count;
        else
            center = new Vector3(-24.6f, 14f, -7.5f);

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = cam.transform.position - center;
            if (toCamera.sqrMagnitude > 0.01f)
                center += toCamera.normalized * 6f;
        }

        return center;
    }

    private static Vector3 InFront(Transform view, float ahead, float up, float right)
    {
        Vector3 horizontal = view.forward;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude < 0.04f)
        {
            horizontal = -view.up;
            horizontal.y = 0f;
        }

        if (horizontal.sqrMagnitude < 0.0001f)
            horizontal = Vector3.forward;

        horizontal.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, horizontal);
        if (side.sqrMagnitude < 0.0001f)
            side = view.right;
        side.Normalize();

        return view.position + horizontal * ahead + Vector3.up * up + side * right;
    }

    private static Light CreatePoint(
        Transform parent,
        string name,
        Vector3 worldPosition,
        Color color,
        float intensity,
        float range,
        bool shadows)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPosition;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows ? LightShadows.Hard : LightShadows.None;
        light.shadowStrength = 0.55f;
        light.renderMode = LightRenderMode.Auto;
        return light;
    }
}
