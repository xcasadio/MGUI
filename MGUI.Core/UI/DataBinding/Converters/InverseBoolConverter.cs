using System.Globalization;
using System.Windows.Data;
#if UseWPF
using System.Windows.Markup;

#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.DataBinding.Converters;

public class InverseBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool BoolValue)
        {
            return !BoolValue;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool BoolValue)
        {
            return !BoolValue;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private static readonly InverseBoolConverter Instance = new();
    public override object ProvideValue(IServiceProvider serviceProvider) => Instance;
}