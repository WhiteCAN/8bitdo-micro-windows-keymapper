using System.Windows;

namespace MicroKeyStudio.App.Services;

public sealed class MessageBoxConfirmationService : IUserConfirmationService
{
    public Task<bool> ConfirmAsync(string title, string message)
    {
        MessageBoxResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
