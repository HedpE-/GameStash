using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GameStash.MVVM.GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var cachedNoCover = new BitmapImage();
                cachedNoCover.BeginInit();

                // Configuramos tudo ANTES do objeto ser finalizado/congelado
                cachedNoCover.UriSource = new Uri("https://images.igdb.com/igdb/image/upload/t_cover_big/nocover.png", UriKind.Absolute);
                cachedNoCover.DecodePixelWidth = 150;
                cachedNoCover.CacheOption = BitmapCacheOption.OnLoad;
                cachedNoCover.CreateOptions = BitmapCreateOptions.DelayCreation;

                cachedNoCover.EndInit(); // O WPF fecha o objeto aqui
                cachedNoCover.Freeze();  // E nós congelamos logo a seguir com segurança

                // Injeta nos recursos globais com a chave que tinhas definido
                this.Resources.Add("NoCoverBitmap", cachedNoCover);
            }
            catch
            {
                // Tratamento de erro básico
            }
        }
    }

}
