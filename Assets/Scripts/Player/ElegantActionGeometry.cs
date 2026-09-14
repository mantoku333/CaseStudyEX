using UnityEngine;

public static class ElegantActionGeometry
{
    public static bool PassedThrough(Bounds target, Vector2 start, Vector2 end, Vector2 bodyHalfSize)
    {
        Vector2 delta = end - start;
        if (delta.sqrMagnitude < 0.01f) return false;
        bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
        float a = horizontal ? start.x - target.center.x : start.y - target.center.y;
        float b = horizontal ? end.x - target.center.x : end.y - target.center.y;
        float extent = horizontal ? target.extents.x : target.extents.y;
        if (a * b >= 0f || Mathf.Abs(a) < extent || Mathf.Abs(b) < extent) return false;
        var expanded = target;
        expanded.Expand(new Vector3(bodyHalfSize.x * 2f, bodyHalfSize.y * 2f, 2f));
        Vector3 origin = new Vector3(start.x, start.y, expanded.center.z);
        return expanded.IntersectRay(new Ray(origin, delta.normalized), out float distance) && distance <= delta.magnitude;
    }

    public static bool CrossedOverHead(Bounds enemy, int facing, Vector2 previousFeet, Vector2 feet,
        float minimumClearance, float maximumClearance)
    {
        float startSide = previousFeet.x - enemy.center.x;
        float endSide = feet.x - enemy.center.x;
        if (startSide * facing <= 0f || endSide * facing > 0f || Mathf.Abs(feet.x - previousFeet.x) < 0.001f)
            return false;
        float t = (enemy.center.x - previousFeet.x) / (feet.x - previousFeet.x);
        float clearance = Mathf.Lerp(previousFeet.y, feet.y, t) - enemy.max.y;
        return clearance >= minimumClearance && clearance <= maximumClearance;
    }
}
