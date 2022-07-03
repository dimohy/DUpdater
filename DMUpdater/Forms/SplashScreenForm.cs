using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DMUpdater.Forms;

public partial class SplashScreenForm : Form
{
    public string? ImageFilename { get; set; }


    public SplashScreenForm()
    {
        InitializeComponent();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (ImageFilename is null)
        {
            Close();
            return;
        }

        pictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
        pictureBox.ImageLocation = ImageFilename;
        pictureBox.Load();

        var screen = Screen.FromControl(this);
        Size = pictureBox.Image.Size;
        Location = new(screen.WorkingArea.X + screen.WorkingArea.Width / 2 - Size.Width / 2, screen.WorkingArea.Y + screen.WorkingArea.Height / 2 - Size.Height / 2);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        await Task.Delay(3000);

        Close();
    }
}