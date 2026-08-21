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

        _mutex = new Mutex(
            true,
            mutexName,
            out bool createdNew);

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


        /*
         * App.xaml używa StaticResource.
         *
         * Dlatego kolory wydania muszą zostać ustawione
         * przed utworzeniem MainWindow.
         */
        ApplyIssuedFeedbackColorsBeforeWindowCreation();


        /*
         * Tworzenie aplikacji / MainWindow.
         */
        base.OnStartup(e);


        /*
         * MainWindow.xaml posiada zasoby lokalne:
         *
         * AppReturnSurfaceBrush
         * AppReturnBorderBrush
         *
         * dlatego kolory zwrotu ustawiamy po utworzeniu okna.
         */
        ApplyReturnedFeedbackColorsToMainWindow();
    }


    private static void ApplyIssuedFeedbackColorsBeforeWindowCreation()
    {
        try
        {
            var settings =
                new ScannerFeedbackColorService().Load();


            /*
             * TŁO WYDANIA
             */
            if (Current.Resources["AppSuccessSurfaceBrush"]
                is SolidColorBrush surfaceBrush)
            {
                surfaceBrush.Color =
                    (Color)ColorConverter.ConvertFromString(
                        settings.IssuedBackground);
            }


            /*
             * RAMKA WYDANIA
             */
            if (Current.Resources["AppSuccessBorderBrush"]
                is SolidColorBrush borderBrush)
            {
                borderBrush.Color =
                    (Color)ColorConverter.ConvertFromString(
                        settings.IssuedBorder);
            }
        }
        catch
        {
            /*
             * Brak dostępu do bazy lub błędne dane
             * nie mogą zablokować uruchomienia programu.
             *
             * W takim przypadku zostają wartości
             * domyślne z App.xaml.
             */
        }
    }


    private static void ApplyReturnedFeedbackColorsToMainWindow()
    {
        try
        {
            var settings =
                new ScannerFeedbackColorService().Load();

            var window = Current.MainWindow;

            if (window == null)
                return;


            /*
             * TŁO ZWROTU
             */
            if (window.Resources["AppReturnSurfaceBrush"]
                is SolidColorBrush surfaceBrush)
            {
                surfaceBrush.Color =
                    (Color)ColorConverter.ConvertFromString(
                        settings.ReturnedBackground);
            }


            /*
             * RAMKA ZWROTU
             */
            if (window.Resources["AppReturnBorderBrush"]
                is SolidColorBrush borderBrush)
            {
                borderBrush.Color =
                    (Color)ColorConverter.ConvertFromString(
                        settings.ReturnedBorder);
            }
        }
        catch
        {
            /*
             * Pozostawiamy wartości domyślne
             * z MainWindow.xaml.
             */
        }
    }


    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}