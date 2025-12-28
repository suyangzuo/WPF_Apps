using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WPF_Typing
{
    /// <summary>
    /// 将进度百分比转换为圆形进度条的PathGeometry
    /// </summary>
    public class ProgressToArcConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return new PathGeometry();

            double progress = TryToDouble(values[0]);
            double width = values.Length > 1 ? TryToDouble(values[1]) : 0;
            double height = values.Length > 2 ? TryToDouble(values[2]) : 0;
            double strokeThickness = values.Length > 3 ? TryToDouble(values[3]) : 0;

            if (width <= 0 || height <= 0)
                return new PathGeometry();

            // 限制进度范围
            progress = Math.Max(0, Math.Min(100, progress));

            // 如果进度为0或非常接近0，返回空的PathGeometry（不显示任何路径）
            if (progress <= 0.01)
            {
                return new PathGeometry();
            }

            // 以Path自身的尺寸为基准计算中心点与半径，避免尺寸调整后出现偏移。
            // padding 用来给弧线留一点点内缩，避免与外圈描边/圆角端点产生叠加或裁切。
            double size = Math.Min(width, height);
            double centerX = width / 2.0;
            double centerY = height / 2.0;
            const double padding = 2.0;
            double radius = (size / 2.0) - padding - (strokeThickness / 2.0);
            if (radius <= 0)
                return new PathGeometry();

            // 将进度百分比转换为角度（0-360度），从顶部（-90度）开始
            double angle = (progress / 100.0) * 360.0;
            
            // 创建PathGeometry
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(centerX, centerY - radius), // 从顶部开始
                IsClosed = false
            };
            
            // 如果进度达到或超过100%，绘制完整的圆
            if (progress >= 99.99)
            {
                // 绘制完整的圆：使用两个180度的弧段
                var arcSegment1 = new ArcSegment
                {
                    Point = new Point(centerX, centerY + radius), // 底部点
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = false
                };
                
                var arcSegment2 = new ArcSegment
                {
                    Point = new Point(centerX, centerY - radius), // 回到顶部
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = false
                };
                
                figure.Segments.Add(arcSegment1);
                figure.Segments.Add(arcSegment2);
            }
            else
            {
                // 转换为弧度（从顶部开始，所以减去90度）
                double radians = (angle - 90) * Math.PI / 180.0;
                
                // 计算终点坐标
                double endX = centerX + radius * Math.Cos(radians);
                double endY = centerY + radius * Math.Sin(radians);
                
                var arcSegment = new ArcSegment
                {
                    Point = new Point(endX, endY),
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = angle > 180
                };
                
                figure.Segments.Add(arcSegment);
            }
            
            geometry.Figures.Add(figure);
            
            return geometry;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static double TryToDouble(object value)
        {
            if (value is double d)
                return d;
            if (value is float f)
                return f;
            if (value is int i)
                return i;
            if (value is long l)
                return l;
            if (value is decimal m)
                return (double)m;

            if (value == null)
                return 0;

            return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : 0;
        }
    }
}

