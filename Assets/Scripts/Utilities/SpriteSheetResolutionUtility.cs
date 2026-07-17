using UnityEngine;

public static class SpriteSheetResolutionUtility
{
    public const float ReferenceMaxTextureSize = 2048f;

    public static float GetSizeCompensatedPixelsPerUnit(Texture2D spriteSheet, float basePixelsPerUnit)
    {
        if (spriteSheet == null)
        {
            return Mathf.Max(1f, basePixelsPerUnit);
        }

        return GetSizeCompensatedPixelsPerUnit(
            spriteSheet.width,
            spriteSheet.height,
            basePixelsPerUnit);
    }

    public static float GetSizeCompensatedPixelsPerUnit(
        int textureWidth,
        int textureHeight,
        float basePixelsPerUnit)
    {
        float safePixelsPerUnit = Mathf.Max(1f, basePixelsPerUnit);
        float largestDimension = Mathf.Max(textureWidth, textureHeight);
        float resolutionScale = Mathf.Max(1f, largestDimension / ReferenceMaxTextureSize);
        return safePixelsPerUnit * resolutionScale;
    }
}
