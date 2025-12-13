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
    public class ProgressToArcConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double progress = 0;
            if (value is double d)
            {
                progress = d;
            }
            else if (value != null && double.TryParse(value.ToString(), out double parsed))
            {
                progress = parsed;
            }

            // 限制进度范围
            progress = Math.Max(0, Math.Min(100, progress));

            // 如果进度为0或非常接近0，返回空的PathGeometry（不显示任何路径）
            if (progress <= 0.01)
            {
                return new PathGeometry();
            }

            // 圆形半径计算：
            // - 外圆Ellipse: 50x50，半径25，StrokeThickness=2，内边缘在半径24
            // - 内圆Ellipse: 40x40，半径20，外边缘在半径20
            // - 缝隙中间位置: (20 + 24) / 2 = 22
            // - Path的StrokeThickness=2，所以中心线应该在半径22
            const double radius = 27;
            const double centerX = 30;
            const double centerY = 30;

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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

