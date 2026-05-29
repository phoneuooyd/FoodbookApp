namespace Foodbook.Views.Components;

public class PieChartDrawable : IDrawable
{
    public float[] Percentages { get; set; } = [55f, 25f, 20f];

    public Color[] SegmentColors { get; set; } = [];

    public bool IsDonut { get; set; } = true;

    public Color HoleColor { get; set; } = Color.FromArgb("#FFFFFF");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Percentages.Length == 0)
        {
            return;
        }

        var segmentColors = SegmentColors.Length > 0 ? SegmentColors : ResolveThemeMacroColors();
        if (segmentColors.Length == 0)
        {
            return;
        }

        canvas.Antialias = true;

        float cx = dirtyRect.Width / 2f;
        float cy = dirtyRect.Height / 2f;

        float radius = Math.Min(cx, cy) * 0.88f;
        float innerRadius = IsDonut ? radius * 0.48f : 0f;

        float sum = Percentages.Sum();
        if (sum <= 0)
        {
            Percentages = [33.3f, 33.3f, 33.4f];
            sum = 100f;
        }

        float currentAngle = -90f;

        for (int i = 0; i < Percentages.Length; i++)
        {
            float percent = Math.Max(0f, Percentages[i]);
            if (percent <= 0f)
            {
                continue;
            }

            float sweep = 360f * (percent / sum);
            DrawPieSegment(
                canvas,
                (cx, cy, radius, innerRadius),
                currentAngle,
                sweep,
                segmentColors[i % segmentColors.Length]);
            currentAngle += sweep;
        }

        if (IsDonut)
        {
            canvas.FillColor = HoleColor;
            canvas.FillCircle(cx, cy, innerRadius);
        }

        currentAngle = -90f;
        for (int i = 0; i < Percentages.Length; i++)
        {
            float percent = Math.Max(0f, Percentages[i]);
            if (percent <= 0f)
            {
                continue;
            }

            float sweep = 360f * (percent / sum);

            if (percent / sum * 100f > 8f)
            {
                float midAngle = (currentAngle + sweep / 2f) * MathF.PI / 180f;
                float labelR = (radius + innerRadius) / 2f;
                float lx = cx + labelR * MathF.Cos(midAngle);
                float ly = cy + labelR * MathF.Sin(midAngle);

                var segmentColor = segmentColors[i % segmentColors.Length];

                canvas.FontColor = GetReadableLabelColor(segmentColor);
                canvas.FontSize = 9f;
                canvas.DrawString(
                    $"{percent / sum * 100f:F0}%",
                    lx - 12,
                    ly - 7,
                    24,
                    14,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }

            currentAngle += sweep;
        }
    }

    private static Color[] ResolveThemeMacroColors()
    {
        var resources = Application.Current?.Resources;
        return
        [
            TryGetThemeColor(resources, "DietStatsCarbsColor", Color.FromArgb("#00C9A7")),
            TryGetThemeColor(resources, "DietStatsFatColor", Color.FromArgb("#FFB347")),
            TryGetThemeColor(resources, "DietStatsProteinColor", Color.FromArgb("#8B72FF"))
        ];
    }

    private static Color TryGetThemeColor(ResourceDictionary? resources, string key, Color fallback)
    {
        if (resources != null && resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    private static Color GetReadableLabelColor(Color background)
    {
        static double ToLinear(double c)
            => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        var luminance = (0.2126 * ToLinear(background.Red)) +
                        (0.7152 * ToLinear(background.Green)) +
                        (0.0722 * ToLinear(background.Blue));

        return luminance > 0.52 ? Colors.Black : Colors.White;
    }

    private static void DrawPieSegment(
        ICanvas canvas,
        (float cx, float cy, float outerR, float innerR) geometry,
        float startDeg,
        float sweepDeg,
        Color color)
    {
        var (cx, cy, outerR, innerR) = geometry;
        const int steps = 36;
        float stepAngle = sweepDeg / steps;

        var path = new PathF();
        float startRad = startDeg * MathF.PI / 180f;

        path.MoveTo(
            cx + outerR * MathF.Cos(startRad),
            cy + outerR * MathF.Sin(startRad));

        for (int s = 1; s <= steps; s++)
        {
            float a = (startDeg + s * stepAngle) * MathF.PI / 180f;
            path.LineTo(cx + outerR * MathF.Cos(a), cy + outerR * MathF.Sin(a));
        }

        if (innerR <= 0f)
        {
            path.LineTo(cx, cy);
        }
        else
        {
            float endRad = (startDeg + sweepDeg) * MathF.PI / 180f;
            path.LineTo(
                cx + innerR * MathF.Cos(endRad),
                cy + innerR * MathF.Sin(endRad));

            for (int s = steps; s >= 0; s--)
            {
                float a = (startDeg + s * stepAngle) * MathF.PI / 180f;
                path.LineTo(cx + innerR * MathF.Cos(a), cy + innerR * MathF.Sin(a));
            }
        }

        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }
}
