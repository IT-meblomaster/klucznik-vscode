using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
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

        ApplyScannerColorsToWindowResources(window);

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
        if (Current.Resources[resourceKey] is not SolidColorBrush brush)
            return;

        try
        {
            brush.Color = (Color)ColorConverter.ConvertFromString(colorValue);
        }
        catch
        {
            // Wartość domyślna pozostaje bez zmian.
        }
    }

    private static void ApplyScannerColorsToWindowResources(MainWindow window)
    {
        try
        {
            CopyBrushColor(
                window.Resources["AppReturnSurfaceBrush"] as SolidColorBrush,
                Current.Resources["AppReturnSurfaceBrush"] as SolidColorBrush);

            CopyBrushColor(
                window.Resources["AppReturnBorderBrush"] as SolidColorBrush,
                Current.Resources["AppReturnBorderBrush"] as SolidColorBrush);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Nie udało się zastosować kolorów do zasobów MainWindow: {ex}");
        }
    }

    private static void CopyBrushColor(
        SolidColorBrush? target,
        SolidColorBrush? source)
    {
        if (target is null || source is null)
            return;

        target.Color = source.Color;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
