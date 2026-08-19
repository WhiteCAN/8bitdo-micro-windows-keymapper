using System.Windows;
using System.Windows.Input;
using MicroKeyStudio.App.Input;
using MicroKeyStudio.App.ViewModels;

namespace MicroKeyStudio.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _viewModel.DisposeAsync();
        base.OnClosed(e);
    }

    private void OnKeyboardCapturePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (KeyboardChordFormatter.TryFormat(key, Keyboard.Modifiers, out string action))
        {
            _viewModel.PendingAction = action;
            e.Handled = true;
        }
    }
}
