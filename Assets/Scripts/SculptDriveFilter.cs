using UnityEngine;

public static class SculptDriveFilter
{
    public const float LatchReleaseDelay = 0.18f;

    public static float ExpLerp(float current, float target, float speed)
    {
        return Mathf.Lerp(
            current,
            target,
            1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    public static float HoldAtLeast(float heldValue, float candidate)
    {
        return Mathf.Max(heldValue, candidate);
    }

    public static float DeadZone(float delta, float deadZone)
    {
        return Mathf.Abs(delta) < deadZone ? 0f : delta;
    }

    public static bool ShouldClearLatch(bool inMode, ref float timeOutOfMode)
    {
        if (inMode)
        {
            timeOutOfMode = 0f;
            return false;
        }

        timeOutOfMode += Time.deltaTime;
        return timeOutOfMode >= LatchReleaseDelay;
    }
}
