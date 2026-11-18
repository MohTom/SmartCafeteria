using System.Windows;

namespace SmartCafeteria.Views;

public partial class FirstRunCredentialsWindow : Window
{
    private readonly string _password;

    public FirstRunCredentialsWindow(string username, string password)
    {
        InitializeComponent();
        _password = password;
        UsernameBox.Text = username;
        PasswordBox.Text = password;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_password);
        }
        catch (Exception ex)
        {
            // Clipboard can be locked by another process; the password stays
            // selectable in the read-only box, so this is not fatal.
            System.Diagnostics.Trace.TraceWarning("Clipboard copy failed: {0}", ex);
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
