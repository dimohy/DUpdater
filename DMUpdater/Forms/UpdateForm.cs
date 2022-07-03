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
    public partial class UpdateForm : Form
    {
        private IUpdateCommand? _updateCommand;


        public Updater? Updater { get; set; }
        public UpdateComparison? UpdateComparison { get; set; }

        public bool IsFinished { get; private set; }

        public UpdateForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = $"{UpdateComparison!.Name} ({UpdateComparison!.Current.Version} -> {UpdateComparison!.Lastest.Version}) Update";

            progressLabel.Text = "";

            _updateCommand = Updater!.Update(state =>
            {
                Invoke(() =>
                {
                    // To get around the progressive animation, we need to move the 
                    // progress bar backwards.
                    updateProgressBar.Maximum = state.Totals;
                    if (state.Current == state.Totals)
                    {
                        updateProgressBar.Maximum = state.Current + 1;
                        updateProgressBar.Value = state.Current + 1;
                        updateProgressBar.Maximum = state.Current;
                    }
                    else
                        updateProgressBar.Value = state.Current + 1;
                    updateProgressBar.Value = state.Current;

                    if (state.Current == state.Totals)
                        progressLabel.Text = $"({state.Current}/{state.Totals}) Finished.";
                    else
                        progressLabel.Text = $"({state.Current}/{state.Totals}) Updating {state.Filename}...";
                });

                if (state.isFinished is true)
                {
                    IsFinished = true;
                    
                    Invoke(() =>
                    {
                        if (_updateCommand!.CanRestart is true)
                            commandButton.Text = "Restart";
                        else
                            commandButton.Text = "Finish";
                    });
                }
            });
        }

        private void commandButton_Click(object sender, EventArgs e)
        {
            if (IsFinished is false)
            {
                _updateCommand!.Pause();
                var result = MessageBox.Show("Are you sure you want to cancel the update?", $"{UpdateComparison!.Name} Update", MessageBoxButtons.YesNo);
                if (result is DialogResult.Yes)
                {
                    _updateCommand.Cancel();
                    Close();
                    return;
                }

                _updateCommand.Resume();
            }
            else if (IsFinished is true)
            {
                if (_updateCommand!.CanRestart is true)
                    _updateCommand.Restart();

                Close();
            }
        }
    }
}
