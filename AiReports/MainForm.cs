using AiReports.Forms;
using AiReports.Helper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace AiReports
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            ChangeLanguage("fa-IR");
        }


        private List<ExcelData> UserExcelDataTable = new List<ExcelData>();
        private GridLogger _logger;
        private CancellationTokenSource _cts;
        private string reportResponse = "";

        private void btnLeftToRight_Click(object sender, EventArgs e)
        {
            this.RightToLeft = RightToLeft.No;
            this.tabControl1.RightToLeft = RightToLeft.No;
            this.tabControl1.RightToLeftLayout = false;

            ChangeLanguage("en");
        }

        private void btnRightToLeft_Click(object sender, EventArgs e)
        {
            this.RightToLeft = RightToLeft.Yes;
            this.tabControl1.RightToLeft = RightToLeft.Yes;
            this.tabControl1.RightToLeftLayout = true;

            ChangeLanguage("fa-IR");
        }

        private  void MainForm_Load(object sender, EventArgs e)
        {
           
            txtSystemPropmts.Text = SettingsService.ReadSystemPrompt();
            txtUserPrompts.Text = SettingsService.ReadUserPrompt();


            _logger = new GridLogger(dataGridView1, maxLogEntries: 500);
            EnableControl(true);



        }

        private void btnAiConfig_Click(object sender, EventArgs e)
        {
            AiConfigForm aiConfigForm = new AiConfigForm();
            aiConfigForm.ShowDialog();
        }

        private void btnSaveUserPropmt_Click(object sender, EventArgs e)
        {
            SettingsService.SaveUserPrompt(txtUserPrompts.Text);
        }

        private void btnSaveSystemPrompt_Click(object sender, EventArgs e)
        {
            SettingsService.SaveSystemPrompt(txtSystemPropmts.Text);
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void btnLoadExcel_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "Excel File(*.xlsx)|*.xlsx";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                DataTable dataTable = ExcelHelper.ReadExcelFile(openFileDialog1.FileName);
                excelFileList.Items.Add(openFileDialog1.FileName);
                dgvExcel.DataSource = dataTable;
                UserExcelDataTable.Add(new ExcelData() { Data = dataTable, FileName = openFileDialog1.FileName });
                tabControl1.SelectedIndex = 3;
            }
        }

        private void excelFileList_DoubleClick(object sender, EventArgs e)
        {
            if (excelFileList.SelectedItem == null)
                return;

            string selectedText = excelFileList.SelectedItem.ToString();
            var data = UserExcelDataTable.FirstOrDefault(item => item.FileName == selectedText);
            if (data != null)
            {
                dgvExcel.DataSource = data.Data;
                tabControl1.SelectedIndex = 3;
            }

        }


        public void ChangeLanguage(string lang)
        {
            this.Visible = false;
            this.SuspendLayout();
            
            try
            {
                var culture = new CultureInfo(lang);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;

                var resources = new ComponentResourceManager(this.GetType());
                ApplyResources(this, resources, culture);
            }
            finally
            {
                this.Visible = true;
                this.ResumeLayout(true);
               
            }
        }

        private void ApplyResources(Control parent, ComponentResourceManager resources, CultureInfo culture)
        {
            if (parent is Form)
            {
                resources.ApplyResources(parent, "$this", culture);
            }
            else
            {
                resources.ApplyResources(parent, parent.Name, culture);
            }

            foreach (Control ctl in parent.Controls)
            {
                ApplyResources(ctl, resources, culture);

                if (ctl is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem item in toolStrip.Items)
                    {
                        ApplyResourcesToToolStripItems(item, resources, culture);
                    }
                }
            }
        }



        private void ApplyResourcesToToolStripItems(ToolStripItem item, ComponentResourceManager resources, CultureInfo culture)
        {
            resources.ApplyResources(item, item.Name, culture);

            if (item is ToolStripMenuItem menuItem)
            {
                foreach (ToolStripItem childItem in menuItem.DropDownItems)
                {
                    ApplyResourcesToToolStripItems(childItem, resources, culture);
                }
            }
        }

        private void btnSelectAiModel_Click(object sender, EventArgs e)
        {
            ModelListForm modelListForm = new ModelListForm();
            if (modelListForm.ShowDialog() == DialogResult.OK)
            { 
                txtModel.Text = modelListForm.Model;
            }
        }

        private void EnableControl(bool enable)
        {
            btnStart.Enabled = enable;
            btnStop.Enabled = !enable;
            btnSelectAiModel.Enabled = enable;
            menuStrip1.Enabled = enable;
            if (!enable)
            {
                tspBar.Style = ProgressBarStyle.Marquee;
                tspBar.MarqueeAnimationSpeed = 30;
            }
            else
            {
                tspBar.Style = ProgressBarStyle.Blocks;
            }

        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            txtModelOutput.Text = "";
            reportResponse = "";
            _logger.ClearLogs();
            tabControl1.SelectedIndex = 0;
           


            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Empty.html");
            webView21.Source = new Uri(htmlPath);


            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", "Vazirmatn-Regular.woff2").Replace("\\", "/");
            _cts = new CancellationTokenSource();
            if (string.IsNullOrEmpty(txtModel.Text))
            {
                _logger.LogError("مدل هوش مصنوعی انتخاب نشده است");
                return;
            }


            if (string.IsNullOrEmpty(txtUserPrompts.Text) || string.IsNullOrEmpty(txtSystemPropmts.Text))
            {
                _logger.LogError("دستورات کاربر/سیستم وجود ندارد");
                return;

            }

            if (UserExcelDataTable.Count == 0)
            {
                _logger.LogError("اطلاعات مربوط به چک لیستها وارد نشده است");
                return;
            }


            _logger.LogInformation("در حال در یافت اطلاعات جداول کیفیت...");
            List<DataTable> dataTables = new List<DataTable>();
            EnableControl(false);
          
            foreach (var item in UserExcelDataTable)
            {
                dataTables.Add(item.Data);

            }

            var info = SettingsService.Load();
            using (var aiClient = new AIClient(
                info.ApiKey,
                   info.ModelUrl, txtModel.Text, 
                   _logger, new AIClientConfiguration()
                   {
                       EnableDetailedLogging = true,
                       MaxRetries = 3,
                       MaxTokens = 16000,
                       Timeout = TimeSpan.FromMinutes(10)
                   }))

            {

                aiClient.OnError += (se) => { EnableControl(true); };



                var response = aiClient.CreateRequest(
                              prompt: txtUserPrompts.Text,
                              systemPropmts: txtSystemPropmts.Text,
                              imagePaths: null,
                              excelPaths: null,
                              dataTables: dataTables, _cts.Token);



                response.ChunkReceived += (s, chunk) =>
                {

                    this.BeginInvoke(new Action(() =>
                    {
                        AppendTextColorful(chunk);
                    }));
                };

                response.ResponseCompleted += (s, ex) =>
                {



                    var onePage = response.GetReport();
                    DisplayResult(onePage, fontPath);
                    EnableControl(true);

                };

                while (!response.IsCompleted)
                {
                    await Task.Delay(100);
                }
            }


        }

        private async void DisplayResult(string markdown, string font)
        {
            try
            {

                string htmlContent = MarkdownViewerHelper.ConvertToHtml(markdown, font);
                reportResponse = htmlContent;
                await webView21.EnsureCoreWebView2Async();
                var tcs = new TaskCompletionSource<bool>();

                void Handler(object s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView21.NavigationCompleted -= Handler;
                    tcs.TrySetResult(true);
                }

                if (webView21.CoreWebView2 != null)
                {
                    webView21.NavigationCompleted += Handler;
                    webView21.NavigateToString(htmlContent);
                 
                    await tcs.Task;
                }
                else
                {
                    _logger.LogInformation("مرورگر هنوز آماده نشده است، لطفاً چند لحظه صبر کنید.");
                }
            }
            catch (Exception ex)
            {
               
                _logger.LogInformation(ex.Message);
            }
            finally
            {

            }

        }


        private void AppendTextColorful(string text)
        {
            txtModelOutput.SelectionStart = txtModelOutput.TextLength;
            txtModelOutput.SelectionLength = 0;

            if (text.Contains("**"))
            {
                txtModelOutput.SelectionColor = Color.Cyan;
            }
            else
            {
                txtModelOutput.SelectionColor = Color.Black;
            }

            txtModelOutput.AppendText(text);
            txtModelOutput.SelectionColor = txtModelOutput.ForeColor;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {

                _cts.Cancel();
                _logger.LogError("درخواست لغو ارسال شد...");
                EnableControl(true);
            }
        }

        private void btnExportToWord_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(reportResponse))
            {
                _logger.LogError("گزارشی وجود ندارد");
                return;
            }
            saveFileDialog1.FileName = "Report.docx";
            saveFileDialog1.Filter = "Word File (.docx)|*.docx";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", "Vazirmatn-Regular.woff2").Replace("\\", "/");
                    HtmlReportToWord.ConvertHtmlToDocx(reportResponse, saveFileDialog1.FileName);

                    _logger.LogSuccess("اطلاعات با موفقیت ذخیره شد.");
                }
                catch (Exception ex) { _logger.LogError(ex.Message); }

            }
        }

        private void btnExportToHtml_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(reportResponse))
            {
                _logger.LogError("گزارشی وجود ندارد");
                return;
            }

            saveFileDialog1.FileName = "Report.html";
            saveFileDialog1.Filter = "HTML File (.html)|*.html";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", "Vazirmatn-Regular.woff2").Replace("\\", "/");
                    File.WriteAllText( saveFileDialog1.FileName, reportResponse);
                    _logger.LogSuccess("اطلاعات با موفقیت ذخیره شد.");
                }
                catch (Exception ex) { _logger.LogError(ex.Message); }


            }

        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();   
        }
    }

    public class ExcelData
    {
        public string FileName { get; set; }
        public DataTable Data {  get; set; }
    }


    public class ChatModelDisplay
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Owner { get; set; }
        public string PriceInfo { get; set; }
        public string RateLimits { get; set; }
        public string TokenLimits { get; set; }
        public string Features { get; set; }
        public ModelInfo OriginalModel { get; set; }

        public static ChatModelDisplay FromModelInfo(ModelInfo model)
        {
            var features = new List<string>();
            if (model.SupportsVision == true) features.Add("👁️ Vision");
            if (model.SupportsFunctionCalling == true) features.Add("🔧 Functions");
            if (model.SupportsWebSearch == true) features.Add("🔍 Search");
            if (model.SupportsPromptCaching == true) features.Add("💾 Caching");
            if (model.SupportsPdfInput == true) features.Add("📄 PDF");
            if (model.SupportsAudioOutput == true) features.Add("🔊 Audio");

            var tokenLimits = "";
            if (model.MaxInputTokens.HasValue)
            {
                tokenLimits = $"In: {FormatNumber(model.MaxInputTokens.Value)}";
                if (model.MaxOutputTokens.HasValue)
                    tokenLimits += $" | Out: {FormatNumber(model.MaxOutputTokens.Value)}";
            }
            else if (model.MaxTokens.HasValue)
            {
                tokenLimits = $"Max: {FormatNumber(model.MaxTokens.Value)}";
            }

            return new ChatModelDisplay
            {
                Id = model.Id,
                DisplayName = $"{model.Id} ({model.OwnedBy})",
                Owner = model.OwnedBy,
                PriceInfo = model.Pricing?.GetDisplayPrice() ?? "N/A",
                RateLimits = model.MaxRequestsPerMinute.HasValue
                    ? $"{FormatNumber(model.MaxRequestsPerMinute.Value)} req/min"
                    : "N/A",
                TokenLimits = tokenLimits,
                Features = features.Count > 0 ? string.Join(" ", features) : "Basic",
                OriginalModel = model
            };
        }

        private static string FormatNumber(double number)
        {
            if (number >= 1000000)
                return $"{number / 1000000:F1}M";
            if (number >= 1000)
                return $"{number / 1000:F0}K";
            return number.ToString("F0");
        }
    }


}
