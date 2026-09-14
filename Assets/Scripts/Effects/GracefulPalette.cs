using UnityEngine;

/// <summary>
/// The one pastel spectrum every graceful effect is built from: the trail layers, the ribbon
/// texture, and the point gain that flies into the gauge all read as the same material.
/// </summary>
public static class GracefulPalette
{
    /// <summary>
    /// The colour a ribbon carries at a given level, swept along the stroke (0 = newest end).
    /// Level one is pure white and must stay that way; above it a faint blue-violet appears,
    /// pink and cyan join, and the top levels spread a pale rainbow. Colour is always added
    /// to white, never substituted for it.
    /// </summary>
    public static Color RibbonTint(float sweep, float level)
    {
        float colour = Mathf.Pow(Mathf.Clamp01((level - 1f) / 3f), 1.6f);
        if (colour <= 0f) return Color.white;
        float span = Mathf.Lerp(0.08f, 0.62f, colour);
        float hue = Mathf.Repeat(0.72f - (span * 0.5f) + (Mathf.Clamp01(sweep) * span), 1f);
        return Color.Lerp(Color.white, Color.HSVToRGB(hue, 1f, 1f), colour * 0.24f);
    }

    public static Gradient CreateSpectrum(float alpha = 1f)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.98f), 0f),
                new GradientColorKey(new Color(0.96f, 0.95f, 1f), 0.2f),
                new GradientColorKey(new Color(0.93f, 0.98f, 1f), 0.4f),
                new GradientColorKey(new Color(0.95f, 1f, 0.98f), 0.58f),
                new GradientColorKey(new Color(1f, 0.99f, 0.94f), 0.76f),
                new GradientColorKey(new Color(1f, 0.96f, 0.97f), 0.9f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
        return gradient;
    }
}
