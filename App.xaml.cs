
using System.Threading;
using System.Windows;
using Klucznik.Services;

namespace Klucznik;

public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = @"Global\Klucznik";

        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Program Klucznik jest już uruchomiony.",
                "Klucznik",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        LoadScannerFeedbackColors();

        base.OnStartup(e);

var window = new MainWindow();
MainWindow = window;

window.Show();

    }

    private static void LoadScannerFeedbackColors()
    {
        try
        {
            var settings = new ScannerFeedbackColorService().Load();

            SetBrushColor("AppSuccessSurfaceBrush", settings.IssuedBackground);
            SetBrushColor("AppSuccessBorderBrush", settings.IssuedBorder);
            SetBrushColor("AppReturnSurfaceBrush", settings.ReturnedBackground);
            SetBrushColor("AppReturnBorderBrush", settings.ReturnedBorder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Nie udało się wczytać kolorów skanera: {ex}");
        }
    }

 private static void SetBrushColor(string resourceKey, string colorValue)
{
    // Pędzle zdefiniowane w App.xaml i używane przez StaticResource w Setterach
    // są przez WPF automatycznie zamrażane (Freeze()) — nie da się zmienić ich
    // właściwości Color. Dlatego zamiast modyfikować istniejący obiekt,
    // podmieniamy cały wpis w słowniku zasobów na nowy, świeżo utworzony pędzel.
    // Wymaga to, aby MainWindow.xaml odwoływał się do tych kluczy przez
    // DynamicResource (nie StaticResource) — inaczej podmiana nie zostanie
    // zauważona przez elementy, których Style został już rozwiązany.
    try
    {
        Current.Resources[resourceKey] = ScannerFeedbackColorService.CreateBrush(colorValue);
    }
    catch
    {
        // Wartość domyślna pozostaje bez zmian.
    }
}

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
