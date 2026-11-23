using System.IO;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Printing;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;

namespace CardArchive;

public partial class MainWindow : Window
{
    public ObservableCollection<Person> People { get; } = new();
    private long? _editId;
    private bool _dark;
    private List<Person> _all = new();
    private FontPreferences _fontPreferences = new();

    private sealed class FontPreferences
    {
        public string Family { get; set; } = "Segoe UI";
        public double Size { get; set; } = 14;
        public bool Bold { get; set; } = true;
        public string Color { get; set; } = "#203653";
    }

    private static string FontSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CardArchive", "font-settings.json");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        try { Database.Initialize(); LoadFontPreferences(); ApplyFontPreferences(); RefreshRows(); }
        catch (Exception ex) { MessageBox.Show("تعذر فتح قاعدة البيانات: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error); }
        Loaded += (_, _) => NameBox.Focus();
    }

    private void RefreshRows()
    {
        _all = Database.Search();
        var query = SearchBox?.Text?.Trim() ?? "";
        var rows = string.IsNullOrWhiteSpace(query) ? _all : _all.Where(p =>
            p.FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            p.CardNumber.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        People.Clear();
        foreach (var p in rows) People.Add(p);
        CountText.Text = Database.Count().ToString("N0", CultureInfo.CurrentCulture);
        ResultsText.Text = $"{People.Count:N0} نتيجة";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var number = NumberBox.Text.Trim();
        if (AutoNumberCheck.IsChecked == true && !_editId.HasValue)
            number = Database.NextAvailableSequence().ToString(CultureInfo.InvariantCulture);
        if (name.Length == 0 || number.Length == 0)
        {
            MessageBox.Show("يرجى إدخال اسم صاحب البطاقة ورقم البطاقة.", "بيانات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (name.Length == 0) NameBox.Focus(); else NumberBox.Focus();
            return;
        }
        try
        {
            if (_editId.HasValue) Database.Update(_editId.Value, name, number);
            else Database.Add(name, number);
            ClearForm();
            RefreshRows();
            NameBox.Focus();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            MessageBox.Show("رقم البطاقة مسجل مسبقاً. لا يمكن تكراره.", "رقم مكرر", MessageBoxButton.OK, MessageBoxImage.Warning);
            NumberBox.Focus(); NumberBox.SelectAll();
        }
        catch (Exception ex) { MessageBox.Show("تعذر الحفظ: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (sender == NameBox && AutoNumberCheck.IsChecked != true) NumberBox.Focus();
        else SaveButton_Click(sender, new RoutedEventArgs());
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) { ClearForm(); NameBox.Focus(); }
    private void ClearForm()
    {
        _editId = null; NameBox.Clear(); NumberBox.Clear();
        FormTitle.Text = "إضافة بطاقة جديدة"; SaveButton.Content = "حفظ البيانات";
        AutoNumberCheck.IsEnabled = true;
        NumberBox.IsReadOnly = AutoNumberCheck.IsChecked == true;
        NumberBox.Text = AutoNumberCheck.IsChecked == true ? Database.NextAvailableSequence().ToString(CultureInfo.InvariantCulture) : "";
    }

    private void AutoNumberCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (NumberBox == null || _editId.HasValue) return;
        var enabled = AutoNumberCheck.IsChecked == true;
        NumberBox.IsReadOnly = enabled;
        NumberBox.Text = enabled ? Database.NextAvailableSequence().ToString(CultureInfo.InvariantCulture) : "";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) RefreshRows(); }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Person p) return;
        _editId = p.Id; NameBox.Text = p.FullName; NumberBox.Text = p.CardNumber;
        AutoNumberCheck.IsEnabled = false; NumberBox.IsReadOnly = false;
        FormTitle.Text = "تعديل بيانات البطاقة"; SaveButton.Content = "حفظ التعديل";
        NameBox.Focus(); NameBox.SelectAll();
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Person p) return;
        if (MessageBox.Show($"هل تريد حذف سجل ({p.FullName})؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { Database.Delete(p.Id); if (_editId == p.Id) ClearForm(); RefreshRows(); }
        catch (Exception ex) { MessageBox.Show("تعذر الحذف: " + ex.Message); }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CSV متوافق مع Excel (*.csv)|*.csv|كل الملفات (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var result = Database.ImportCsv(dialog.FileName); RefreshRows();
            MessageBox.Show($"اكتمل الاستيراد.\n\nالسجلات المضافة: {result.added}\nالأرقام المكررة: {result.duplicates}\nالصفوف غير الصالحة: {result.invalid}", "نتيجة الاستيراد", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("تعذر الاستيراد: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV متوافق مع Excel (*.csv)|*.csv", FileName = "فهرس_البطاقات.csv" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var output = new StringBuilder("الاسم,رقم البطاقة\r\n");
            foreach (var p in Database.Search()) output.AppendLine(Csv(p.FullName) + "," + Csv(p.CardNumber));
            File.WriteAllText(dialog.FileName, output.ToString(), new UTF8Encoding(true));
            MessageBox.Show("تم تصدير البيانات بنجاح.", "التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("تعذر التصدير: " + ex.Message); }
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "قاعدة بيانات SQLite (*.db)|*.db", FileName = $"CardArchive_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db" };
        if (dialog.ShowDialog() != true) return;
        try { Database.Backup(dialog.FileName); MessageBox.Show("تم إنشاء النسخة الاحتياطية بنجاح.", "النسخ الاحتياطي", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception ex) { MessageBox.Show("تعذر إنشاء النسخة الاحتياطية: " + ex.Message); }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        var rows = Database.Search(SearchBox.Text);
        if (rows.Count == 0) { MessageBox.Show("لا توجد سجلات للطباعة."); return; }
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 12, FlowDirection = FlowDirection.RightToLeft, PagePadding = new Thickness(48) };
        document.Blocks.Add(new Paragraph(new Run("فهرس البطاقات الوطنية")) { FontSize = 22, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 18) });
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
        var group = new TableRowGroup(); table.RowGroups.Add(group);
        AddPrintRow(group, "ت", "اسم صاحب البطاقة", "رقم البطاقة", true);
        int i = 1;
        foreach (var p in rows) AddPrintRow(group, (i++).ToString(), p.FullName, p.CardNumber, false);
        document.Blocks.Add(table);
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true) { paginator.PageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight); printDialog.PrintDocument(paginator, "فهرس البطاقات الوطنية"); }
    }

    private static void AddPrintRow(TableRowGroup group, string n, string name, string number, bool header)
    {
        var row = new TableRow();
        foreach (var text in new[] { n, name, number })
        {
            var cell = new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(5), TextAlignment = TextAlignment.Center });
            cell.BorderBrush = Brushes.LightGray; cell.BorderThickness = new Thickness(0.5);
            if (header) { cell.Background = new SolidColorBrush(Color.FromRgb(232, 239, 249)); cell.FontWeight = FontWeights.Bold; }
            row.Cells.Add(cell);
        }
        group.Rows.Add(row);
    }

    private void LoadFontPreferences()
    {
        try
        {
            if (File.Exists(FontSettingsPath))
                _fontPreferences = JsonSerializer.Deserialize<FontPreferences>(File.ReadAllText(FontSettingsPath)) ?? new FontPreferences();
        }
        catch { _fontPreferences = new FontPreferences(); }
    }

    private void SaveFontPreferences()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FontSettingsPath)!);
        File.WriteAllText(FontSettingsPath, JsonSerializer.Serialize(_fontPreferences));
    }

    private void ApplyFontPreferences()
    {
        var family = new FontFamily(_fontPreferences.Family);
        var weight = _fontPreferences.Bold ? FontWeights.Bold : FontWeights.Normal;
        var brush = (Brush)new BrushConverter().ConvertFromString(_fontPreferences.Color)!;
        foreach (var box in new[] { NameBox, NumberBox, SearchBox })
        {
            box.FontFamily = family;
            box.FontSize = _fontPreferences.Size;
            box.FontWeight = weight;
            box.Foreground = brush;
        }
        PeopleGrid.FontFamily = family;
        PeopleGrid.FontSize = _fontPreferences.Size;
        PeopleGrid.FontWeight = weight;
        PeopleGrid.Foreground = brush;
    }

    private void FontSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "تنسيق الخط",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 390,
            Height = 440,
            ResizeMode = ResizeMode.NoResize,
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = new FontFamily("Segoe UI"),
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 253))
        };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "تخصيص شكل النصوص", FontSize = 21, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) });

        panel.Children.Add(new TextBlock { Text = "نوع الخط", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var familyBox = new ComboBox { Height = 36, Margin = new Thickness(0, 0, 0, 14), Padding = new Thickness(8) };
        foreach (var family in new[] { "Segoe UI", "Tahoma", "Arial", "Calibri" }) familyBox.Items.Add(family);
        familyBox.SelectedItem = _fontPreferences.Family;
        if (familyBox.SelectedIndex < 0) familyBox.SelectedIndex = 0;
        panel.Children.Add(familyBox);

        panel.Children.Add(new TextBlock { Text = "حجم الخط", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var sizeBox = new ComboBox { Height = 36, Margin = new Thickness(0, 0, 0, 14), Padding = new Thickness(8), IsEditable = true };
        foreach (var size in new[] { "11", "12", "13", "14", "15", "16", "18", "20", "22", "24", "26", "28", "32" }) sizeBox.Items.Add(size);
        sizeBox.Text = _fontPreferences.Size.ToString(CultureInfo.InvariantCulture);
        panel.Children.Add(sizeBox);

        var boldBox = new CheckBox { Content = "خط عريض (Bold)", IsChecked = _fontPreferences.Bold, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(boldBox);

        panel.Children.Add(new TextBlock { Text = "لون الخط", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var colorBox = new ComboBox { Height = 36, Margin = new Thickness(0, 0, 0, 18), Padding = new Thickness(8) };
        var colors = new (string Name, string Hex)[]
        {
            ("أزرق داكن", "#203653"), ("أسود", "#111827"), ("أزرق", "#2563EB"),
            ("أخضر داكن", "#166534"), ("خمري", "#9F1239"), ("رمادي", "#475569"),
            ("أبيض", "#FFFFFF")
        };
        foreach (var item in colors) colorBox.Items.Add(new ComboBoxItem { Content = item.Name, Tag = item.Hex });
        colorBox.SelectedItem = colorBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, _fontPreferences.Color)) ?? colorBox.Items[0];
        panel.Children.Add(colorBox);

        var preview = new TextBlock
        {
            Text = "معاينة النص العربي — حسن محمد علي ١٢٣٤٥",
            FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(32, 54, 83)),
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 18)
        };
        panel.Children.Add(preview);
        void UpdatePreview()
        {
            if (double.TryParse(sizeBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var s) && s >= 9 && s <= 36)
                preview.FontSize = s;
            preview.FontFamily = new FontFamily(familyBox.SelectedItem?.ToString() ?? "Segoe UI");
            preview.FontWeight = boldBox.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            if (colorBox.SelectedItem is ComboBoxItem item && item.Tag is string hex)
                preview.Foreground = (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
        familyBox.SelectionChanged += (_, _) => UpdatePreview();
        sizeBox.SelectionChanged += (_, _) => UpdatePreview();
        sizeBox.LostFocus += (_, _) => UpdatePreview();
        boldBox.Checked += (_, _) => UpdatePreview();
        boldBox.Unchecked += (_, _) => UpdatePreview();
        colorBox.SelectionChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var apply = new Button { Content = "تطبيق", MinWidth = 105, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "إلغاء", MinWidth = 90, Background = new SolidColorBrush(Color.FromRgb(229, 235, 245)), Foreground = new SolidColorBrush(Color.FromRgb(41, 69, 104)) };
        apply.Click += (_, _) =>
        {
            if (!double.TryParse(sizeBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var size) || size < 9 || size > 36)
            {
                MessageBox.Show(dialog, "اختر حجماً بين 9 و36.", "حجم غير صالح", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var selectedColor = (ComboBoxItem)colorBox.SelectedItem;
            _fontPreferences = new FontPreferences
            {
                Family = familyBox.SelectedItem?.ToString() ?? "Segoe UI",
                Size = size,
                Bold = boldBox.IsChecked == true,
                Color = selectedColor.Tag?.ToString() ?? "#203653"
            };
            try { SaveFontPreferences(); ApplyFontPreferences(); dialog.DialogResult = true; dialog.Close(); }
            catch (Exception ex) { MessageBox.Show(dialog, "تعذر حفظ إعدادات الخط: " + ex.Message); }
        };
        cancel.Click += (_, _) => dialog.Close();
        buttons.Children.Add(apply); buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.ShowDialog();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _dark = !_dark;
        var resources = Application.Current.Resources;
        if (_dark)
        {
            resources["PageBg"] = new SolidColorBrush(Color.FromRgb(17, 26, 43));
            resources["Surface"] = new SolidColorBrush(Color.FromRgb(29, 42, 63));
            resources["TextMain"] = new SolidColorBrush(Color.FromRgb(235, 241, 250));
            resources["TextMuted"] = new SolidColorBrush(Color.FromRgb(160, 174, 196));
            resources["BorderLine"] = new SolidColorBrush(Color.FromRgb(51, 67, 91));
            ThemeButton.Content = "☀  الوضع النهاري";
        }
        else
        {
            resources["PageBg"] = new SolidColorBrush(Color.FromRgb(243, 246, 251));
            resources["Surface"] = new SolidColorBrush(Colors.White);
            resources["TextMain"] = new SolidColorBrush(Color.FromRgb(23, 36, 58));
            resources["TextMuted"] = new SolidColorBrush(Color.FromRgb(116, 129, 152));
            resources["BorderLine"] = new SolidColorBrush(Color.FromRgb(229, 234, 242));
            ThemeButton.Content = "☾  الوضع الليلي";
        }
    }}