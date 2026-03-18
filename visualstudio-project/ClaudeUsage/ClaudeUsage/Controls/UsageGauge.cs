using System.Windows;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ClaudeUsage.Controls;

public class UsageGauge : System.Windows.Controls.UserControl
{
    // 270° arc with wider bottom gap (looks better)
    // Labels at 20/80 are placed on a horizontal line for aesthetics
    private const float StartAngle = 135f;
    private const float SweepAngle = 270f;

    // Gradient colors defined in GetGradientForValue()

    #region Dependency Properties

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(UsageGauge),
            new PropertyMetadata(0.0, OnRedraw));

    public static readonly DependencyProperty TimeElapsedPercentProperty =
        DependencyProperty.Register(nameof(TimeElapsedPercent), typeof(double?), typeof(UsageGauge),
            new PropertyMetadata(null, OnRedraw));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(UsageGauge),
            new PropertyMetadata("", OnRedraw));

    public static readonly DependencyProperty ResetTextProperty =
        DependencyProperty.Register(nameof(ResetText), typeof(string), typeof(UsageGauge),
            new PropertyMetadata("", OnRedraw));

    public static readonly DependencyProperty IsDarkThemeProperty =
        DependencyProperty.Register(nameof(IsDarkTheme), typeof(bool), typeof(UsageGauge),
            new PropertyMetadata(true, OnRedraw));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double? TimeElapsedPercent { get => (double?)GetValue(TimeElapsedPercentProperty); set => SetValue(TimeElapsedPercentProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ResetText { get => (string)GetValue(ResetTextProperty); set => SetValue(ResetTextProperty, value); }
    public bool IsDarkTheme { get => (bool)GetValue(IsDarkThemeProperty); set => SetValue(IsDarkThemeProperty, value); }

    #endregion

    private readonly SKElement _skElement;

    public UsageGauge()
    {
        _skElement = new SKElement();
        _skElement.PaintSurface += OnPaintSurface;
        Content = _skElement;
    }

    private static void OnRedraw(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UsageGauge g) g._skElement.InvalidateVisual();
    }

    private float GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        return (float)(source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Transparent);

        var dark = IsDarkTheme;
        var value = Math.Clamp(Value, 0, 100);
        var dpi = GetDpiScale();

        var w = (float)info.Width;
        var h = (float)info.Height;
        var cx = w / 2f;

        // --- Layout ---
        var labelFontSize = 13f * dpi;
        var labelAreaH = labelFontSize + 16f * dpi;      // Label + bigger padding
        var bottomTextH = 36f * dpi;                      // Space for value + reset text

        // Gauge must fit between label and bottom text
        var availableH = h - labelAreaH - bottomTextH;
        var maxRadiusFromH = (availableH) / 2.0f;
        var maxRadiusFromW = w * 0.40f;
        var radius = MathF.Min(maxRadiusFromH, maxRadiusFromW);
        var arcThick = radius * 0.26f;
        var tickGap = 8f;

        var cy = labelAreaH + radius + arcThick / 2f;

        // Percentage sits inside the arc opening (bottom gap)
        var valueTextY = cy + radius * 0.7f;
        var resetTextY = valueTextY + 14f * dpi;

        // --- Draw ---
        DrawLabel(canvas, cx, labelFontSize, dark, labelFontSize);
        DrawBackgroundArc(canvas, cx, cy, radius, arcThick, dark);
        DrawTickRing(canvas, cx, cy, radius, arcThick, tickGap, dark);
        DrawFillArc(canvas, cx, cy, radius, arcThick, value);

        if (TimeElapsedPercent.HasValue)
            DrawTimeMarker(canvas, cx, cy, radius, arcThick, TimeElapsedPercent.Value);

        DrawNeedle(canvas, cx, cy, radius, arcThick, tickGap, value, dark);
        DrawScaleLabels(canvas, cx, cy, radius, arcThick, dark, dpi);
        DrawValueText(canvas, cx, valueTextY, value, dark, dpi);
        DrawResetText(canvas, cx, resetTextY, dark, dpi);
    }

    private static void DrawTextNew(SKCanvas canvas, string text, float x, float y, SKTextAlign align, SKFont font, SKPaint paint)
    {
        canvas.DrawText(text, x, y, align, font, paint);
    }

    private void DrawLabel(SKCanvas canvas, float cx, float y, bool dark, float fontSize)
    {
        if (string.IsNullOrEmpty(Label)) return;
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), fontSize);
        using var paint = new SKPaint { Color = dark ? new SKColor(210, 210, 210) : new SKColor(50, 50, 50), IsAntialias = true };
        DrawTextNew(canvas, Label, cx, y, SKTextAlign.Center, font, paint);
    }

    private static void DrawBackgroundArc(SKCanvas canvas, float cx, float cy, float r, float thick, bool dark)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = thick,
            StrokeCap = SKStrokeCap.Round,
            Color = dark ? new SKColor(55, 55, 55) : new SKColor(225, 225, 225),
            IsAntialias = true
        };
        var rect = new SKRect(cx - r, cy - r, cx + r, cy + r);
        canvas.DrawArc(rect, StartAngle, SweepAngle, false, paint);
    }

    private static void DrawFillArc(SKCanvas canvas, float cx, float cy, float r, float thick, double value)
    {
        if (value <= 0) return;

        var (startColor, endColor) = GetGradientForValue(value);
        var sweep = (float)(SweepAngle * value / 100.0);

        // Linear gradient from arc start point to arc fill end point
        var startRad = StartAngle * MathF.PI / 180f;
        var endRad = (StartAngle + sweep) * MathF.PI / 180f;

        var startPt = new SKPoint(cx + MathF.Cos(startRad) * r, cy + MathF.Sin(startRad) * r);
        var endPt = new SKPoint(cx + MathF.Cos(endRad) * r, cy + MathF.Sin(endRad) * r);

        using var shader = SKShader.CreateLinearGradient(
            startPt, endPt,
            new[] { startColor, endColor },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = thick,
            StrokeCap = SKStrokeCap.Round,
            Shader = shader,
            IsAntialias = true
        };
        var rect = new SKRect(cx - r, cy - r, cx + r, cy + r);
        canvas.DrawArc(rect, StartAngle, sweep, false, paint);
    }

    private static void DrawTickRing(SKCanvas canvas, float cx, float cy, float r, float arcThick, float gap, bool dark)
    {
        // Separate tick ring INSIDE the main arc with a clear gap
        var arcInner = r - arcThick / 2f;
        var tickOuterR = arcInner - gap;
        var tickThick = arcThick * 0.4f;
        var tickInnerR = tickOuterR - tickThick;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = dark ? new SKColor(95, 95, 95) : new SKColor(170, 170, 170),
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round  // Rounded tick ends
        };

        // Cosmetic tick distribution: 20=180°, 50=270°, 80=360°
        // Minor ticks interpolate evenly between anchor points
        for (float pct = 0; pct <= 100.01f; pct += 2.5f)
        {
            var i = (int)MathF.Round(pct);
            bool major = i == 20 || i == 50 || i == 80;

            var angle = CosmeticAngle(pct);
            var rad = angle * MathF.PI / 180f;
            var cos = MathF.Cos(rad);
            var sin = MathF.Sin(rad);

            paint.StrokeWidth = major ? 2.8f : 1.5f;
            var inner = major ? tickInnerR : tickOuterR - tickThick * 0.5f;

            canvas.DrawLine(
                cx + cos * inner, cy + sin * inner,
                cx + cos * tickOuterR, cy + sin * tickOuterR,
                paint);
        }
    }

    private static void DrawScaleLabels(SKCanvas canvas, float cx, float cy, float r, float thick, bool dark, float dpi)
    {
        var fontSize = 9f * dpi;
        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var paint = new SKPaint { Color = dark ? new SKColor(150, 150, 150) : new SKColor(120, 120, 120), IsAntialias = true };

        var labelR = r - thick / 2f - 36f;
        var labelY = cy + fontSize * 0.35f;

        var angle20 = StartAngle + SweepAngle * 20f / 100f;
        var angle80 = StartAngle + SweepAngle * 80f / 100f;
        var x20 = cx + MathF.Cos(angle20 * MathF.PI / 180f) * labelR;
        var x80 = cx + MathF.Cos(angle80 * MathF.PI / 180f) * labelR;

        DrawTextNew(canvas, "20", x20, labelY, SKTextAlign.Center, font, paint);
        DrawTextNew(canvas, "80", x80, labelY, SKTextAlign.Center, font, paint);
    }

    private static void DrawTimeMarker(SKCanvas canvas, float cx, float cy, float r, float thick, double pct)
    {
        pct = Math.Clamp(pct, 0, 100);
        var angle = StartAngle + SweepAngle * (float)pct / 100f;
        var rad = angle * MathF.PI / 180f;

        var innerR = r - thick / 2f - 3f;
        var outerR = r + thick / 2f + 3f;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawLine(
            cx + MathF.Cos(rad) * innerR, cy + MathF.Sin(rad) * innerR,
            cx + MathF.Cos(rad) * outerR, cy + MathF.Sin(rad) * outerR,
            paint);
    }

    private static void DrawNeedle(SKCanvas canvas, float cx, float cy, float r, float arcThick, float tickGap, double value, bool dark)
    {
        var angle = StartAngle + SweepAngle * (float)value / 100f;
        var rad = angle * MathF.PI / 180f;

        var arcInner = r - arcThick / 2f;
        var tipLen = arcInner - tickGap - 12f;  // 10px shorter, inside tick ring
        var tailLen = r * 0.10f;

        var tipX = cx + MathF.Cos(rad) * tipLen;
        var tipY = cy + MathF.Sin(rad) * tipLen;

        var needleColor = dark ? new SKColor(190, 190, 190) : new SKColor(70, 70, 70);

        // 1. Background circle FIRST (behind needle)
        using var outerDot = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = dark ? new SKColor(155, 155, 155) : new SKColor(140, 140, 140),
            IsAntialias = true
        };
        canvas.DrawCircle(cx, cy, 11f, outerDot);

        // 2. Needle: tapered from base (wide) to tip (narrow), no tail behind center
        var perp = rad + MathF.PI / 2f;
        var baseHalfW = 7f;
        var tipHalfW = 2.5f;

        var bx1 = cx + MathF.Cos(perp) * baseHalfW;
        var by1 = cy + MathF.Sin(perp) * baseHalfW;
        var bx2 = cx - MathF.Cos(perp) * baseHalfW;
        var by2 = cy - MathF.Sin(perp) * baseHalfW;

        var tx1 = tipX + MathF.Cos(perp) * tipHalfW;
        var ty1 = tipY + MathF.Sin(perp) * tipHalfW;
        var tx2 = tipX - MathF.Cos(perp) * tipHalfW;
        var ty2 = tipY - MathF.Sin(perp) * tipHalfW;

        using var needlePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = needleColor,
            IsAntialias = true
        };

        // Needle from center base to tip — no tail
        using var path = new SKPath();
        path.MoveTo(tx1, ty1);
        path.LineTo(bx1, by1);
        path.LineTo(bx2, by2);
        path.LineTo(tx2, ty2);
        path.Close();
        canvas.DrawPath(path, needlePaint);

        // Rounded tip cap
        canvas.DrawCircle(tipX, tipY, tipHalfW, needlePaint);

        // 3. Small inner circle ON TOP of needle
        using var innerDot = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = dark ? new SKColor(70, 70, 70) : new SKColor(230, 230, 230),
            IsAntialias = true
        };
        canvas.DrawCircle(cx, cy, 6.5f, innerDot);
    }

    private void DrawValueText(SKCanvas canvas, float cx, float y, double value, bool dark, float dpi)
    {
        using var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var font = new SKFont(typeface, 20f * dpi);
        using var paint = new SKPaint { Color = GetColorForValue(value), IsAntialias = true };
        DrawTextNew(canvas, $"{(int)value}%", cx, y, SKTextAlign.Center, font, paint);
    }

    private void DrawResetText(SKCanvas canvas, float cx, float y, bool dark, float dpi)
    {
        if (string.IsNullOrEmpty(ResetText)) return;
        using var font = new SKFont(SKTypeface.Default, 10f * dpi);
        using var paint = new SKPaint { Color = dark ? new SKColor(120, 130, 140) : new SKColor(120, 120, 120), IsAntialias = true };
        DrawTextNew(canvas, ResetText, cx, y, SKTextAlign.Center, font, paint);
    }

    /// <summary>
    /// Maps a percentage to a cosmetic angle so that 20%=180°, 50%=270°, 80%=360°.
    /// Minor ticks interpolate linearly between anchor points.
    /// </summary>
    private static float CosmeticAngle(float pct)
    {
        // Anchors: 0%=135°, 20%=180°, 50%=270°, 80%=360°, 100%=405°
        if (pct <= 20f)
            return 135f + (pct / 20f) * (180f - 135f);
        if (pct <= 50f)
            return 180f + ((pct - 20f) / 30f) * (270f - 180f);
        if (pct <= 80f)
            return 270f + ((pct - 50f) / 30f) * (360f - 270f);
        return 360f + ((pct - 80f) / 20f) * (405f - 360f);
    }

    private static (SKColor start, SKColor end) GetGradientForValue(double value)
    {
        if (value >= 90) return (new SKColor(0xFF, 0x92, 0x1F), new SKColor(0xEB, 0x48, 0x24));  // Red
        if (value >= 70) return (new SKColor(0xFF, 0xD3, 0x94), new SKColor(0xFF, 0xB3, 0x57));  // Orange
        return (new SKColor(0x52, 0xD1, 0x7C), new SKColor(0x22, 0x91, 0x8B));                    // Green
    }

    private static SKColor GetColorForValue(double value)
    {
        // Solid color for text — use the end (darker) gradient color
        if (value >= 90) return new SKColor(0xEB, 0x48, 0x24);
        if (value >= 70) return new SKColor(0xFF, 0xB3, 0x57);
        return new SKColor(0x52, 0xD1, 0x7C);
    }
}
