using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace WPF_Typing
{
    /// <summary>
    /// 计算文本宽度的标记扩展
    /// </summary>
    public class TextWidthExtension : MarkupExtension
    {
        public string Text { get; set; } = string.Empty;
        public double FontSize { get; set; } = 14;
        public double Padding { get; set; } = 10;

        public TextWidthExtension()
        {
        }

        public TextWidthExtension(string text)
        {
            Text = text;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Text))
                return 100.0;

            try
            {
                var fontFamily = new FontFamily("Google Sans Code, Consolas, HarmonyOS Sans SC, 微软雅黑");
                
                var formattedText = new FormattedText(
                    Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    FontSize,
                    Brushes.Black,
                    96.0); // 使用96 DPI作为默认值

                return formattedText.Width + Padding;
            }
            catch
            {
                return 100.0;
            }
        }
    }
}

