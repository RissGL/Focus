using System.Collections.ObjectModel;
using System.Windows;
using WpfApp1.Models;

namespace WpfApp1.Views;

public partial class AppRemoveDialog : Window
{
    public AppWhitelistEntry? SelectedApp { get; private set; }

    public AppRemoveDialog(IEnumerable<AppWhitelistEntry> apps,
        string title, string cancelText, string removeText)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = title;
        CancelBtn.Content = cancelText;
        RemoveBtn.Content = removeText;
        AppListBox.ItemsSource = new ObservableCollection<AppWhitelistEntry>(apps);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RemoveBtn_Click(object sender, RoutedEventArgs e)
    {
        SelectedApp = AppListBox.SelectedItem as AppWhitelistEntry;
        DialogResult = SelectedApp != null;
        Close();
    }
}
