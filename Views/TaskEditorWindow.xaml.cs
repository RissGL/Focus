using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views;

public class AbilityPointItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Points { get; set; }
}

public partial class TaskEditorWindow : Window
{
    private readonly ObservableCollection<Ability> _abilities;
    private readonly ObservableCollection<AbilityPointItem> _pointItems = new();
    private TodoType _taskType = TodoType.ShortTerm;
    private readonly bool _isDarkMode;

    public TodoItem? Result { get; private set; }

    public TaskEditorWindow(ObservableCollection<Ability> abilities, TodoItem? existing, bool isDarkMode)
    {
        InitializeComponent();
        _abilities = abilities;
        _isDarkMode = isDarkMode;
        ApplyTheme();

        foreach (var ab in abilities)
        {
            var pts = 0;
            if (existing != null)
            {
                var existingBoost = existing.Boosts.FirstOrDefault(b => b.AbilityName == ab.Name);
                if (existingBoost != null) pts = existingBoost.Points;
            }
            _pointItems.Add(new AbilityPointItem { Name = ab.Name, Icon = ab.Icon, Points = pts });
        }
        AbilityPointsList.ItemsSource = _pointItems;

        if (existing != null)
        {
            TaskNameBox.Text = existing.Text;
            _taskType = existing.Type;
        }

        UpdateTypeButtons();
        ApplyLocale();
    }

    private void ApplyTheme()
    {
        var dark = _isDarkMode;
        Resources["WindowBg"] = new SolidColorBrush(ParseColor(dark ? "#111827" : "#F9FAFB"));
        Resources["BgCard"] = new SolidColorBrush(ParseColor(dark ? "#1F2937" : "#FFFFFF"));
        Resources["ItemBg"] = new SolidColorBrush(ParseColor(dark ? "#374151" : "#F9FAFB"));
        Resources["BtnSecondaryBg"] = new SolidColorBrush(ParseColor(dark ? "#374151" : "#FFFFFF"));
        Resources["BtnSecondaryFg"] = new SolidColorBrush(ParseColor(dark ? "#F9FAFB" : "#1F2937"));
        Resources["TextPrimary"] = new SolidColorBrush(ParseColor(dark ? "#F9FAFB" : "#1F2937"));
        Resources["TextSecondary"] = new SolidColorBrush(ParseColor(dark ? "#9CA3AF" : "#6B7280"));
        Resources["BorderColor"] = new SolidColorBrush(ParseColor(dark ? "#374151" : "#E5E7EB"));
    }

    private void ApplyLocale()
    {
        var zh = LocaleManager.Current == Locale.ZH;
        Title = zh ? "任务编辑" : "Task Editor";
        EditorTitle.Text = zh ? "任务编辑" : "Task Editor";
        NameLabel.Text = zh ? "任务名称" : "Task Name";
        TypeLabel.Text = zh ? "任务类型" : "Task Type";
        PointsLabel.Text = zh ? "完成时获得的能力点数" : "Points awarded on completion";
        CancelBtn.Content = zh ? "取消" : "Cancel";
        SaveBtn.Content = zh ? "保存" : "Save";
        UpdateTypeButtons();
    }

    private void UpdateTypeButtons()
    {
        var zh = LocaleManager.Current == Locale.ZH;
        var activeBg = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        var inactiveBg = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        var activeFg = new SolidColorBrush(Colors.White);
        var inactiveFg = new SolidColorBrush(Color.FromRgb(107, 114, 128));

        void Style(Button btn, bool active)
        {
            btn.Background = active ? activeBg : inactiveBg;
            btn.Foreground = active ? activeFg : inactiveFg;
            btn.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        Style(ShortTermBtn, _taskType == TodoType.ShortTerm);
        Style(DailyBtn, _taskType == TodoType.Daily);
        Style(LongTermBtn, _taskType == TodoType.LongTerm);

        ShortTermBtn.Content = zh ? "短期" : "Short";
        DailyBtn.Content = zh ? "🔄 每日" : "🔄 Daily";
        LongTermBtn.Content = zh ? "🎯 长期" : "🎯 Long";
    }

    private void TypeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _taskType = tag switch
            {
                "Daily" => TodoType.Daily,
                "LongTerm" => TodoType.LongTerm,
                _ => TodoType.ShortTerm
            };
            UpdateTypeButtons();
        }
    }

    private void PlusBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AbilityPointItem item)
        {
            item.Points++;
            AbilityPointsList.Items.Refresh();
        }
    }

    private void MinusBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AbilityPointItem item)
        {
            if (item.Points > 0) item.Points--;
            AbilityPointsList.Items.Refresh();
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var text = TaskNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            var zh = LocaleManager.Current == Locale.ZH;
            MessageBox.Show(
                zh ? "请输入任务名称。" : "Please enter a task name.",
                zh ? "需要输入" : "Input Required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var boosts = _pointItems
            .Where(p => p.Points > 0)
            .Select(p => new AbilityBoost
            {
                AbilityName = p.Name,
                Points = p.Points,
                Icon = p.Icon
            })
            .ToList();

        Result = new TodoItem
        {
            Text = text,
            Type = _taskType,
            Boosts = boosts,
            LastResetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static Color ParseColor(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex);
}
