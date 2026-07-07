using CommunityToolkit.Mvvm.ComponentModel;

namespace Klucznik.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private bool successfulScanFeedbackVisible;

    partial void OnStatusChanged(string value)
    {
        SuccessfulScanFeedbackVisible =
            value.StartsWith("Wydano klucz:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("Zwrócono klucz:", StringComparison.OrdinalIgnoreCase);
    }

    partial void OnFirstNameChanged(string value)
    {
        ClearSuccessfulScanFeedbackIfScannerPanelsAreEmpty();
    }

    partial void OnCurrentKeyNameChanged(string value)
    {
        ClearSuccessfulScanFeedbackIfScannerPanelsAreEmpty();
    }

    private void ClearSuccessfulScanFeedbackIfScannerPanelsAreEmpty()
    {
        if (string.IsNullOrWhiteSpace(FirstName) &&
            string.IsNullOrWhiteSpace(LastName) &&
            string.IsNullOrWhiteSpace(EmployeeCardDisplay) &&
            string.IsNullOrWhiteSpace(CurrentKeyName) &&
            string.IsNullOrWhiteSpace(CurrentKeyBuilding) &&
            string.IsNullOrWhiteSpace(CurrentKeyDescription) &&
            string.IsNullOrWhiteSpace(CurrentKeyRfidStatus))
        {
            SuccessfulScanFeedbackVisible = false;
        }
    }
}

