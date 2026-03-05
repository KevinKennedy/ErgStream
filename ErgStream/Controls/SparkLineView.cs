using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ErgStream.Controls
{
    /// <summary>
    /// A spark line view that displays an array of integer values as a simple line chart.
    /// Uses SkiaSharp for rendering
    /// </summary>
    public class SparkLineView : SKCanvasView
    {
        private const int MaxPoints = 200;
        private const float PointWidth = 2f;

        public static readonly BindableProperty DataProperty = BindableProperty.Create(
            nameof(Data),
            typeof(int[]),
            typeof(SparkLineView),
            null,
            propertyChanged: OnDataChanged);

        public static readonly BindableProperty LineColorProperty = BindableProperty.Create(
            nameof(LineColor),
            typeof(Color),
            typeof(SparkLineView),
            Colors.DodgerBlue,
            propertyChanged: OnVisualPropertyChanged);

        public static readonly BindableProperty LineThicknessProperty = BindableProperty.Create(
            nameof(LineThickness),
            typeof(float),
            typeof(SparkLineView),
            1.5f,
            propertyChanged: OnVisualPropertyChanged);
        
        public static readonly BindableProperty MaxValueProperty = BindableProperty.Create(
            nameof(MaxValue),
            typeof(int?),
            typeof(SparkLineView),
            null,
            propertyChanged: OnMaxValueChanged);

        public int[]? Data
        {
            get => (int[]?)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public Color LineColor
        {
            get => (Color)GetValue(LineColorProperty);
            set => SetValue(LineColorProperty, value);
        }

        public float LineThickness
        {
            get => (float)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        public int? MaxValue
        {
            get => (int?)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public SparkLineView()
        {
            // Set a default height for the spark line
            HeightRequest = 30;
        }

        private static void OnDataChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (SparkLineView)bindable;
            view.UpdateWidth();
            view.InvalidateSurface();
        }

        private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (SparkLineView)bindable;
            view.InvalidateSurface();
        }

        private static void OnMaxValueChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (SparkLineView)bindable;
            view.InvalidateSurface();
        }

        private void UpdateWidth()
        {
            var data = Data;
            if (data == null || data.Length == 0)
            {
                WidthRequest = 0;
            }
            else
            {
                // Limit to MaxPoints and calculate width based on point count
                int pointCount = Math.Min(data.Length, MaxPoints);
                WidthRequest = pointCount * PointWidth;
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            var data = Data;
            if (data == null || data.Length < 2)
                return;

            // Limit to MaxPoints
            int pointCount = Math.Min(data.Length, MaxPoints);

            // Find min and max for scaling
            int minValue = int.MaxValue;
            int maxValue = int.MinValue;
            if (MaxValue.HasValue)
            {
                minValue = 0;
                maxValue = MaxValue.Value;
            }
            else
            {
                for (int i = 0; i < pointCount; i++)
                {
                    if (data[i] < minValue) minValue = data[i];
                    if (data[i] > maxValue) maxValue = data[i];
                }
            }

            // Avoid division by zero
            float range = maxValue - minValue;
            if (range == 0) range = 1;

            var info = e.Info;
            float width = info.Width;
            float height = info.Height;

            // Add some padding
            float paddingY = 2f;
            float drawHeight = height - (paddingY * 2);

            // Calculate x step based on actual point count
            float xStep = width / (pointCount - 1);

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = LineColor.ToSKColor(),
                StrokeWidth = LineThickness,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            using var path = new SKPath();

            for (int i = 0; i < pointCount; i++)
            {
                float x = i * xStep;
                // Invert Y so higher values are at the top
                float normalizedValue = (data[i] - minValue) / range;
                float y = paddingY + drawHeight - (normalizedValue * drawHeight);

                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }

            canvas.DrawPath(path, paint);
        }
    }
}