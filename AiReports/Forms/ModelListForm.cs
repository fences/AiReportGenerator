using AiReports.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiReports.Forms
{
    public partial class ModelListForm : Form
    {
        public ModelListForm()
        {
            InitializeComponent();
        }

        private async void ModelListForm_Load(object sender, EventArgs e)
        {
            try
            {
                var info =   SettingsService.Load();
                toolStripProgressBar1.MarqueeAnimationSpeed = 30;
                toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
                if (info != null)
                {

                    var client = new AvalaiApiService(info.ApiKey, info.BaseUrl);
                    var chatModels = await client.GetChatModelsAsync();
                    var displayModels = chatModels.Select(ChatModelDisplay.FromModelInfo).ToList();
                    dataGridView1.DataSource = displayModels;
                }
                
            }
            catch
            {


            }
            finally 
            {
                toolStripProgressBar1.Style = ProgressBarStyle.Blocks;
            }
        }

        private void btnCalcel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        public string _model;
        public string Model
        {
            get { return _model; }
            set { _model = value; }
        }
        private void btnSelect_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                var item = dataGridView1.SelectedRows[0];
                _model = item.Cells["Id"].Value.ToString();
                DialogResult = DialogResult.OK;
            }
        }
    }
}
