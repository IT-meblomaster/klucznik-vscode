using CommunityToolkit.Mvvm.ComponentModel;

namespace Klucznik.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private bool successfulScanFeedbackVisible;

    [ObservableProperty]
    private bool issuedScanFeedbackVisible;

    [ObservableProperty]
    private bool returnedScanFeedbackVisible;

    partial void OnStatusChanged(string value)
    {
        IssuedScanFeedbackVisible =
            value.StartsWith("Wydano klucz:", StringComparison.OrdinalIgnoreCase);

        ReturnedScanFeedbackVisible =
            value.StartsWith("Zwrócono klucz:", StringComparison.OrdinalIgnoreCase);

        SuccessfulScanFeedbackVisible =
            IssuedScanFeedbackVisible ||
            ReturnedScanFeedbackVisible;
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
            IssuedScanFeedbackVisible = false;
            ReturnedScanFeedbackVisible = false;
        }
    }
}
