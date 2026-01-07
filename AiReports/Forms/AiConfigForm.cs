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
    public partial class AiConfigForm : Form
    {
        public AiConfigForm()
        {
            InitializeComponent();
        }

        private void AiConfigForm_Load(object sender, EventArgs e)
        {
           var info =  SettingsService.Load();
            if (info != null)
            {
                txtUrl.Text = info.ModelUrl;
                txtBase.Text = info.BaseUrl;
                txtKey.Text = info.ApiKey;
                txtMaxToken.Text = info.MaxToken.ToString();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtUrl.Text) || string.IsNullOrEmpty(txtKey.Text) || string.IsNullOrEmpty(txtMaxToken.Text))
                return;

            if (int.TryParse(txtMaxToken.Text, out int token))
            {
                SettingsService.Save(new ApiSettings() { ApiKey = txtKey.Text, MaxToken = token ,
                    ModelUrl = txtUrl.Text, BaseUrl = txtBase.Text});
                this.DialogResult = DialogResult.OK;
            }
            
        }
    }
}
