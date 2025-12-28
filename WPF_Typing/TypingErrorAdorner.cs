using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WPF_Typing
{
    public sealed class TypingErrorAdorner : Adorner
    {
        private readonly RichTextBox _typingDisplay;
        private readonly Func<string, Run?> _runProvider;
        private readonly Func<Rect> _visibleRectProvider;

        private static readonly Brush OverlayBrush = CreateOverlayBrush();

        private static Brush CreateOverlayBrush()
        {
            var brush = new SolidColorBrush(Color.FromArgb(0x60, 0xCD, 0x5C, 0x5C));
            brush.Freeze();
            return brush;
        }

        private sealed class CachedGlyph
        {
            public string Key { get; }
            public char ActualChar { get; set; }
            public Rect Rect { get; set; }
            public int LayoutGeneration { get; set; }

            public CachedGlyph(string key, char actualChar)
            {
                Key = key;
                ActualChar = actualChar;
                Rect = Rect.Empty;
            }
        }

        private readonly Dictionary<string, CachedGlyph> _cache = new();
        private int _layoutGeneration = 0;

        public TypingErrorAdorner(
            RichTextBox typingDisplay,
            Func<string, Run?> runProvider,
            Func<Rect> visibleRectProvider) : base(typingDisplay)
        {
            _typingDisplay = typingDisplay;
            _runProvider = runProvider;
            _visibleRectProvider = visibleRectProvider;

            IsHitTestVisible = false;
        }

        public void Invalidate() => InvalidateVisual();

        public void UpsertError(string key, ErrorCharInfo info)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (_cache.TryGetValue(key, out var existing))
            {
                existing.ActualChar = info.ActualChar;
                // mark dirty for this layout
                existing.LayoutGeneration = -1;
            }
            else
            {
                _cache[key] = new CachedGlyph(key, info.ActualChar) { LayoutGeneration = -1 };
            }
        }

        public void RemoveError(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _cache.Remove(key);
        }

        public void ClearErrors()
        {
            _cache.Clear();
        }

        public void NotifyLayoutChanged()
        {
            _layoutGeneration++;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_cache.Count == 0) return;

            Rect visibleRect;
            try
            {
                visibleRect = _visibleRectProvider();
            }
            catch
            {
                visibleRect = Rect.Empty;
            }

            if (visibleRect.IsEmpty) return;

            // Expand a bit so we don't pop in/out at edges.
            visibleRect.Inflate(40, 80);

            var dpi = VisualTreeHelper.GetDpi(this);
            double pixelsPerDip = dpi.PixelsPerDip;

            var fontFamily = _typingDisplay.FontFamily ?? new FontFamily("Consolas");
            var typeface = new Typeface(fontFamily, _typingDisplay.FontStyle, _typingDisplay.FontWeight, _typingDisplay.FontStretch);
            double fontSize = _typingDisplay.FontSize > 0 ? _typingDisplay.FontSize : 24.0;

            // Slightly smaller than the main text to fit into the extra LineHeight space.
            double overlayFontSize = fontSize;

            // Clip within the visible viewport (in adorned-element coordinates).
            drawingContext.PushClip(new RectangleGeometry(visibleRect));

            foreach (var glyph in _cache.Values)
            {
                // If the user typed a space incorrectly, don't draw any overlay text.
                if (glyph.ActualChar == ' ')
                {
                    continue;
                }

                // Fast reject using cached rect.
                if (!glyph.Rect.IsEmpty && !glyph.Rect.IntersectsWith(visibleRect))
                {
                    continue;
                }

                // Recompute rect when layout changed or we never computed it.
                if (glyph.Rect.IsEmpty || glyph.LayoutGeneration != _layoutGeneration)
                {
                    var run = _runProvider(glyph.Key);
                    if (run == null) continue;

                    try
                    {
                        glyph.Rect = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                        glyph.LayoutGeneration = _layoutGeneration;
                    }
                    catch
                    {
                        continue;
                    }

                    if (glyph.Rect.IsEmpty || double.IsNaN(glyph.Rect.X) || double.IsNaN(glyph.Rect.Y)) continue;

                    if (!glyph.Rect.IntersectsWith(visibleRect))
                    {
                        continue;
                    }
                }

                string overlayText = ToVisibleChar(glyph.ActualChar);
                if (string.IsNullOrEmpty(overlayText))
                {
                    continue;
                }

                var formatted = new FormattedText(
                    overlayText,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    overlayFontSize,
                    OverlayBrush,
                    pixelsPerDip);

                double x = glyph.Rect.X + Math.Max(0, (glyph.Rect.Width - formatted.Width) / 2.0);
                double y = glyph.Rect.Top + fontSize + 4;

                drawingContext.DrawText(formatted, new Point(x, y));
            }

            drawingContext.Pop();
        }

        private static string ToVisibleChar(char c)
        {
            return c switch
            {
                ' ' => string.Empty,
                '\t' => "⇥",
                '\r' => "␍",
                '\n' => "␊",
                _ => c.ToString()
            };
        }
    }
}
