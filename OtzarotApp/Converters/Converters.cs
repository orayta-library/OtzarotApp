using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace OtzarotApp.Converters;

/// <summary>הופך bool ל-Visibility</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, string lang) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, string lang) =>
        value is Visibility.Visible;
}

/// <summary>הופך bool ל-Visibility הפוך</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, string lang) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, string lang) =>
        value is not Visibility.Visible;
}

/// <summary>שולל bool</summary>
public class BoolNegConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, string lang) =>
        value is not true;

    public object ConvertBack(object value, Type t, object p, string lang) =>
        value is not true;
}

/// <summary>אם רשימה לא ריקה → Visible</summary>
public class ListToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, string lang)
    {
        if (value is System.Collections.ICollection col)
            return col.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object p, string lang) =>
        throw new NotImplementedException();
}

/// <summary>int → bool (0 = false)</summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, string lang) =>
        value is int i && i > 0;

    public object ConvertBack(object value, Type t, object p, string lang) =>
        throw new NotImplementedException();
}

/// <summary>float → string עם 2 ספרות עשרוניות</summary>
public class FloatToStringConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, string lang) =>
        value is float f ? f.ToString("F2") : "0.00";

    public object ConvertBack(object value, Type t, object p, string lang) =>
        throw new NotImplementedException();
}
