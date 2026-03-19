using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MXFConverter.Converters
{
    /// <summary>
    /// Retourne Collapsed quand la valeur est true, Visible quand elle est false.
    /// Utilisé pour afficher le fallback icône quand aucune miniature n'est disponible.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v != Visibility.Visible;
        }
    }
}
