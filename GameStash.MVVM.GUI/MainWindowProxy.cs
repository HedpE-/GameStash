using System.Windows;

namespace GameStash.MVVM.GUI
{
    public class MainWindowProxy : Freezable
    {
        protected override Freezable CreateInstanceCore() => new MainWindowProxy();

        public object Window => Application.Current.MainWindow;
    }
}
