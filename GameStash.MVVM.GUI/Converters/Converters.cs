using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using IgdbSharp.Models;
using MaterialDesignThemes.Wpf;

namespace GameStash.MVVM.GUI.Converters
{
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrWhiteSpace(url))
            {
                string pattern = @"t_(\w+)";

                // Replace "thumb" by "medium" or other value in the parameter input
                string updatedUrl = Regex.Replace(url, pattern, "t_" + parameter.ToString());
                // Add "https:" if the URL starts with "//"

                updatedUrl = updatedUrl.StartsWith("//") ? $"https:{updatedUrl}" : updatedUrl;

                return updatedUrl;
            }

            return DependencyProperty.UnsetValue; // Return a placeholder or null if URL is invalid
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UrlToBitmapConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrWhiteSpace(url))
                return DependencyProperty.UnsetValue;

            string igdbSize = "cover_big";
            int decodeWidth = 150;

            // Lógica de Split robusta
            if (parameter != null)
            {
                string paramStr = parameter.ToString();
                if (paramStr.Contains("-"))
                {
                    string[] parts = paramStr.Split('-');
                    if (parts.Length > 0) igdbSize = parts[0];
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedWidth))
                    {
                        decodeWidth = parsedWidth;
                    }
                }
                else
                {
                    igdbSize = paramStr;
                }
            }

            try
            {
                string pattern = @"t_(\w+)";
                string updatedUrl = Regex.Replace(url, pattern, "t_" + igdbSize);
                updatedUrl = updatedUrl.StartsWith("//") ? $"https:{updatedUrl}" : updatedUrl;

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(updatedUrl, UriKind.Absolute);

                bitmap.DecodePixelWidth = decodeWidth > 0 ? decodeWidth : 150;

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.None;
                bitmap.EndInit();

                // 🟢 CRUCIAL: Só congelamos se o bitmap já tiver concluído a transição básica
                if (!bitmap.IsDownloading)
                {
                    bitmap.Freeze();
                }
                else
                {
                    // Se ainda estiver a descarregar da web, registamos um evento rápido para congelar 
                    // assim que o download terminar, garantindo performance máxima na UI.
                    bitmap.DownloadCompleted += (s, e) =>
                    {
                        try { ((BitmapImage)s)?.Freeze(); } catch { /* Silencioso */ }
                    };
                }

                return bitmap;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class VideoToThumbnailImageConverter : IValueConverter
    {
        private string _baseUrl = "https://i.ytimg.com/vi/_videoId_/hqdefault.jpg";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string videoId && !string.IsNullOrEmpty(videoId))
            {
                return _baseUrl.Replace("_videoId_", videoId);
            }

            return DependencyProperty.UnsetValue; // Return a placeholder or null if URL is invalid
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FormatBytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes && bytes >= 0)
            {
                string[] units = { "Bytes", "KB", "MB", "GB", "TB", "PB" };
                double size = bytes;
                int unitIndex = 0;

                while (size >= 1024 && unitIndex < units.Length - 1)
                {
                    size /= 1024;
                    unitIndex++;
                }

                return $"{size:0.##} {units[unitIndex]}";
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RatingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double rating)
            {
                // Convertendo o valor de 0-100 para 0-5
                double convertedRating = rating / 20;
                // Arredondando para uma casa decimal
                var result = Math.Round(convertedRating, 1);
                return result;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ListToStringConverter : IValueConverter
    {
        public bool Abbreviation { get; set; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<Genre> genres)
            {
                if (genres.Any())
                {
                    var result = string.Join(", ", genres.Select(g => g.Name));
                    return result;
                }
            }
            else if (value is IEnumerable<Platform> platforms)
            {
                if (platforms.Any())
                {
                    var result = string.Join(", ", platforms.Select(p => (Abbreviation ? p.Abbreviation : p.Name)));
                    return result;
                }
            }
            else if (value is IEnumerable<IgdbSharp.Models.Theme> themes)
            {
                if (themes.Any())
                {
                    var result = string.Join(", ", themes.Select(t => t.Name));
                    return result;
                }
            }
            else if (value is IEnumerable<PlayerPerspective> playerperspectives)
            {
                if (playerperspectives.Any())
                {
                    var result = string.Join(", ", playerperspectives.Select(pp => pp.Name));
                    return result;
                }
            }
            else if (value is IEnumerable<GameMode> gamemodes)
            {
                if (gamemodes.Any())
                {
                    var result = string.Join(", ", gamemodes.Select(gm => gm.Name));
                    return result;
                }
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class WindowStateToPackIconConverter : IValueConverter
    {
        public double Size { get; set; } = 20;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Check the current window state and return the appropriate PackIconKind
            if (value is WindowState windowState)
            {
                if (windowState == WindowState.Maximized)
                    return new PackIcon { Kind = PackIconKind.WindowRestore, Width = Size, Height = Size };  // Window is maximized, show restore icon
                else
                    return new PackIcon { Kind = PackIconKind.WindowMaximize, Width = Size, Height = Size };  // Window is normal, show maximize icon
            }

            return new PackIcon { Kind = PackIconKind.WindowMaximize };  // Default icon if state is unknown
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // We don't need to implement ConvertBack in this case
            throw new NotImplementedException();
        }
    }

    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return value;

            // Convert the value to a double (size)
            double size = (double)value;

            // Ensure the parameter is a valid double, even if it's passed as a string with commas or periods
            string parameterString = parameter.ToString();

            // If parameter is not a valid double, attempt to parse it safely
            double percentage;
            if (double.TryParse(parameterString, NumberStyles.Any, CultureInfo.InvariantCulture, out percentage))
            {
                var result = size * percentage;
                return result;
            }
            else
                // If parsing fails, return the original value
                return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value; // Not needed for this scenario
        }
    }

    public class DateFormatterConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is DateTimeOffset date && values[1] is string format)
            {
                try
                {
                    return date.ToString(format, culture);
                }
                catch
                {
                    return date.ToString(culture); // Fallback para formato padrão
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not implemented.");
        }
    }
}
