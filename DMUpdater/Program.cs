using DMUpdater.Forms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DMUpdater;

internal static class Program
{
    private static readonly string RootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        var mode = ActionMode.CheckUpdate;
        if (args.Length > 0)
            Enum.TryParse<ActionMode>(args[0], out mode);

        var action = new Dictionary<ActionMode, Func<string[], int>>
        {
            [ActionMode.CheckUpdate] = CheckUpdate,
            [ActionMode.SplashScreen] = RunSplashScreen,
            [ActionMode.Generate] = Generate,
            [ActionMode.Update] = Update,
            [ActionMode.UpdateNext] = UpdateNext,
            [ActionMode.Restore] = Restore,
            [ActionMode.RestoreNext] = RestoreNext,
        };

        //mode = ActionMode.SplashScreen;
        //args = new[] { "", "W:\\Works\\ERgrin\\ERgrin.SmartUpdater\\ergrin_splashscreen.png" };
        return action[mode](args);
    }

    private static int CheckUpdate(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var updater = new Updater(RootPath);
        var form = new CheckUpdateForm();
        form.Updater = updater;

        Application.Run(form);

        if (form.IsSelectUpdate is false)
            return (int)ActionMode.CheckUpdate;

        Update(args);

        return (int)ActionMode.Update;
    }

    private static int RunSplashScreen(string[] args)
    {
        if (args.Length < 2)
            return -1;

        var splashScreenImage = args[1];

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var form = new SplashScreenForm();
        form.ImageFilename = splashScreenImage;

        Application.Run(form);

        return (int)ActionMode.SplashScreen;
    }

    private static int Generate(string[] args)
    {
        var updater = new Updater(RootPath);
        updater.GenerateFiles();

        return (int)ActionMode.Generate;
    }

    /// <summary>
    /// After copying the update program to a temporary directory, run it again with admin privileges.
    /// </summary>
    /// <param name="args"></param>
    private static int Update(string[] args)
    {
        var updater = new Updater(RootPath);
        var updateComparison = updater.GetUpdateComparison();
        if (updateComparison.IsNeedUpdate is false)
            return 0;

        var updaterExe = Assembly.GetExecutingAssembly().Location;
        var updaterExeConfig = $"{updaterExe}.config";
        var tempPath = Path.GetTempPath();

        var tempExeFilename = Path.Combine(tempPath, Path.GetFileName(updaterExe));
        File.Copy(updaterExe, tempExeFilename, true);

        var tempExeConfigFilename = Path.Combine(tempPath, Path.GetFileName(updaterExeConfig));
        File.Copy(updaterExeConfig, tempExeConfigFilename, true);

        var p = new Process();
        p.StartInfo.FileName = tempExeFilename;
        p.StartInfo.Arguments = $"UpdateNext {RootPath}";
        p.StartInfo.Verb = "runas"; // Elevate Admin Privileges
        p.Start();

        return (int)ActionMode.UpdateNext;
    }

    /// <summary>
    /// Proceed with the update.
    /// </summary>
    /// <param name="args"></param>
    private static int UpdateNext(string[] args)
    {
        var rootPath = args[1];

        var updater = new Updater(rootPath);
        var versionInfo = updater.GetUpdateComparison();
        if (versionInfo.IsNeedUpdate is false)
            return 0;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var form = new UpdateForm
        {
            Updater = updater,
            UpdateComparison = versionInfo
        };

        Application.Run(form);

        return (int)ActionMode.UpdateNext;
    }

    private static int Restore(string[] args)
    {
        return (int)ActionMode.Restore;
    }

    private static int RestoreNext(string[] args)
    {
        return (int)ActionMode.RestoreNext;
    }
}

enum ActionMode
{
    CheckUpdate = 0,
    SplashScreen,
    Generate,
    Update,
    UpdateNext,
    Restore,
    RestoreNext,
}

enum ActionOptions
{
    Screen,
    Callback,
}
