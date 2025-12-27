using System;
using System.Globalization;
using System.Windows.Data;

namespace WPF_Typing
{
    /// <summary>
    /// 将数字拆分为整数部分和小数部分
    /// parameter: "Integer" 返回整数部分, "Dot" 返回小数点（如果存在）, "DecimalOnly" 返回小数部分（不包含小数点）
    /// </summary>
    public class NumberPartConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "0";

            double num = 0;
            if (value is double d)
            {
                num = d;
            }
            else if (double.TryParse(value.ToString(), out double parsed))
            {
                num = parsed;
            }

            string param = parameter?.ToString() ?? "Integer";

            switch (param)
            {
                case "Integer":
                    return ((int)Math.Floor(num)).ToString();
                case "Dot":
                    // 返回小数点（如果存在有效小数部分）
                    string formatted = num.ToString("F2", CultureInfo.InvariantCulture);
                    int dotIndex = formatted.IndexOf('.');
                    if (dotIndex < 0)
                        return ""; // 如果没有小数点，返回空字符串
                    string decimalPart = formatted.Substring(dotIndex + 1);
                    // 去除末尾的0
                    decimalPart = decimalPart.TrimEnd('0');
                    // 如果小数部分全为0，返回空字符串
                    if (string.IsNullOrEmpty(decimalPart))
                        return ""; // 如果没有有效小数部分，返回空字符串
                    return ".";
                case "DecimalOnly":
                    // 返回小数部分（不包含小数点），去除末尾的0
                    formatted = num.ToString("F2", CultureInfo.InvariantCulture);
                    dotIndex = formatted.IndexOf('.');
                    if (dotIndex < 0)
                        return ""; // 如果没有小数点，返回空字符串
                    decimalPart = formatted.Substring(dotIndex + 1);
                    // 去除末尾的0
                    decimalPart = decimalPart.TrimEnd('0');
                    // 如果小数部分全为0，返回空字符串
                    if (string.IsNullOrEmpty(decimalPart))
                        return ""; // 如果没有有效小数部分，返回空字符串
                    return decimalPart;
                default:
                    return num.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

