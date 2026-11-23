using System.IO;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Printing;
using System.Text;
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        try { Database.Initialize(); RefreshRows(); }
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
        if (sender == NameBox) NumberBox.Focus();
        else SaveButton_Click(sender, new RoutedEventArgs());
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) { ClearForm(); NameBox.Focus(); }
    private void ClearForm()
    {
        _editId = null; NameBox.Clear(); NumberBox.Clear();
        FormTitle.Text = "إضافة بطاقة جديدة"; SaveButton.Content = "حفظ البيانات";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) RefreshRows(); }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Person p) return;
        _editId = p.Id; NameBox.Text = p.FullName; NumberBox.Text = p.CardNumber;
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

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _dark = !_dark;
        var page = (SolidColorBrush)FindResource("PageBg");
        var surface = (SolidColorBrush)FindResource("Surface");
        var text = (SolidColorBrush)FindResource("TextMain");
        var muted = (SolidColorBrush)FindResource("TextMuted");
        var border = (SolidColorBrush)FindResource("BorderLine");
        if (_dark)
        {
            page.Color = Color.FromRgb(17, 26, 43); surface.Color = Color.FromRgb(29, 42, 63);
            text.Color = Color.FromRgb(235, 241, 250); muted.Color = Color.FromRgb(160, 174, 196);
            border.Color = Color.FromRgb(51, 67, 91); ThemeButton.Content = "☀  الوضع النهاري";
        }
        else
        {
            page.Color = Color.FromRgb(243, 246, 251); surface.Color = Colors.White;
            text.Color = Color.FromRgb(23, 36, 58); muted.Color = Color.FromRgb(116, 129, 152);
            border.Color = Color.FromRgb(229, 234, 242); ThemeButton.Content = "☾  الوضع الليلي";
        }
    }
}