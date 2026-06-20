using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DesktopClock
{
    public sealed class OutlinedTextBlock : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(
                nameof(Fill),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    Brushes.White,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(
                nameof(Stroke),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    Brushes.Transparent,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(
                nameof(StrokeThickness),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    0.0,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(
                nameof(FontSize),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    24.0,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(
                nameof(FontWeight),
                typeof(FontWeight),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    FontWeights.Normal,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(
                nameof(FontFamily),
                typeof(FontFamily),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(
                    SystemFonts.MessageFontFamily,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            FormattedText formattedText = CreateFormattedText();

            double strokePadding = StrokeThickness * 2;

            return new Size(
                formattedText.WidthIncludingTrailingWhitespace + strokePadding,
                formattedText.Height + strokePadding);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            FormattedText formattedText = CreateFormattedText();

            Geometry textGeometry = formattedText.BuildGeometry(
                new Point(StrokeThickness, StrokeThickness));

            Pen? pen = StrokeThickness > 0
                ? new Pen(Stroke, StrokeThickness)
                : null;

            drawingContext.DrawGeometry(Fill, pen, textGeometry);
        }

        private FormattedText CreateFormattedText()
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            return new FormattedText(
                Text ?? "",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    FontFamily,
                    FontStyles.Normal,
                    FontWeight,
                    FontStretches.Normal),
                FontSize,
                Fill,
                pixelsPerDip);
        }
    }
}