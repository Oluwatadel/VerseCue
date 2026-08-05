using System.Windows;

namespace Versecue.Wpf.Views;

public partial class PresenterWindow : Window
{
    public PresenterWindow()
    {
        InitializeComponent();
    }

    public void UpdateContent(string scriptureText, string reference)
    {
        TxtScripture.Text = scriptureText;
        TxtReference.Text = reference;
    }
}
