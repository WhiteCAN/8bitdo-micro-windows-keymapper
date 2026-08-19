namespace MicroKeyStudio.App.Services;

public interface IUserConfirmationService
{
    Task<bool> ConfirmAsync(string title, string message);
}
