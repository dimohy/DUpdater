using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DMUpdater.Forms
{
    public partial class CheckUpdateForm : Form
    {
        private UpdateComparison? updateComparison;


        public Updater? Updater { get; set; }
        public bool IsSelectUpdate { get; private set; }

        public CheckUpdateForm()
        {
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            updateComparison = null;
            await Task.Run(() =>
            {
                updateComparison = Updater!.GetUpdateComparison();
            });

            nameLabel.Text = updateComparison!.Name;
            currentVersionLabel.Text = updateComparison!.Current.Version.ToString();
            lastestVersionLabel.Text = updateComparison!.Lastest.Version.ToString();

            if (updateComparison!.IsNeedUpdate is false)
            {
                updateButton.Visible = false;
                messageLabel.Text = "The current version is the latest version.";
            }
            else
                messageLabel.Text = "You can update the latest version.";
        }

        private void okayButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void updateButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Do you want to proceed with the update?", $"{updateComparison!.Name} ({updateComparison!.Current.Version} -> {updateComparison!.Lastest.Version}) Update", MessageBoxButtons.YesNo);
            if (result is DialogResult.No)
                return;

            IsSelectUpdate = true;

            Close();
        }
    }
}
