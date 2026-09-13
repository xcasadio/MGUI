namespace MGUI.Core.UI.Docking.Controls;

internal static class DockSplitSizing
{
    public static float ClampRatioToMinSizes(float ratio, int availableSize, int minFirstSize, int minSecondSize)
    {
        float boundedRatio = Math.Clamp(ratio, 0f, 1f);
        if (availableSize <= 0)
        {
            return boundedRatio;
        }

        int safeMinFirstSize = Math.Max(0, minFirstSize);
        int safeMinSecondSize = Math.Max(0, minSecondSize);

        float minRatio = (float)safeMinFirstSize / availableSize;
        float maxRatio = (float)(availableSize - safeMinSecondSize) / availableSize;
        if (minRatio <= maxRatio)
        {
            return Math.Clamp(boundedRatio, minRatio, maxRatio);
        }

        int totalMinSize = safeMinFirstSize + safeMinSecondSize;
        if (totalMinSize <= 0)
        {
            return boundedRatio;
        }

        return Math.Clamp((float)safeMinFirstSize / totalMinSize, 0f, 1f);
    }

    public static void ComputeChildSizes(float ratio, int availableSize, int minFirstSize, int minSecondSize, out int firstSize, out int secondSize)
    {
        if (availableSize <= 0)
        {
            firstSize = 0;
            secondSize = 0;
            return;
        }

        int safeMinFirstSize = Math.Max(0, minFirstSize);
        int safeMinSecondSize = Math.Max(0, minSecondSize);

        if (safeMinFirstSize + safeMinSecondSize > availableSize)
        {
            int totalMinSize = safeMinFirstSize + safeMinSecondSize;
            if (totalMinSize <= 0)
            {
                firstSize = (int)Math.Round(availableSize * Math.Clamp(ratio, 0f, 1f), MidpointRounding.ToEven);
            }
            else
            {
                firstSize = (int)Math.Round((double)availableSize * safeMinFirstSize / totalMinSize, MidpointRounding.ToEven);
            }

            firstSize = Math.Clamp(firstSize, 0, availableSize);
            secondSize = availableSize - firstSize;
            return;
        }

        float clampedRatio = ClampRatioToMinSizes(ratio, availableSize, safeMinFirstSize, safeMinSecondSize);
        firstSize = (int)(availableSize * clampedRatio);
        secondSize = availableSize - firstSize;

        if (firstSize < safeMinFirstSize)
        {
            firstSize = safeMinFirstSize;
            secondSize = availableSize - firstSize;
        }

        if (secondSize < safeMinSecondSize)
        {
            secondSize = safeMinSecondSize;
            firstSize = availableSize - secondSize;
        }
    }
}