using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DMUpdater;


/// <summary>
/// It generates a hash code and provides an update function when the current version and the published version are different.
/// </summary>
public class Updater : IUpdateCommand
{
    private const string UpdateInfoFilename = "lastupdate.xml";
    private const string BackupDirectory = "backup";
    private const string PublishDirectory = "publish";
    private const string LastestDirectory = "lastest";
    private const string PreviousDirectory = "previous";


    private readonly string _rootPath;
    private readonly string _updateInfoFilepath; 
    private UpdateInfo _localUpdateInfo;

    private Task? _updateTask;
    private CancellationTokenSource? _updateTaskCTS;
    private ManualResetEvent _updatePauseEvent = new ManualResetEvent(true);


    public bool CanRestart { get; private set; }



    public Updater(string rootPath)
    {
        _rootPath = rootPath;
        _updateInfoFilepath = Path.Combine(_rootPath, UpdateInfoFilename);

        // If there is no update file, it is created.
        if (File.Exists(_updateInfoFilepath) is false)
        {
            _localUpdateInfo = new()
            {
                IgnoreFiles = new() { new() {  Name = BackupDirectory }, new() { Name = PublishDirectory }, new() { Name = UpdateInfoFilename } }
            };

            try
            {
                _localUpdateInfo.ToFile(_updateInfoFilepath);
            }
            catch (Exception ex)
            {
                throw new UpdaterException(UpdaterError.FailedWriteUpdateFile, ex);
            }
        }

        _localUpdateInfo = UpdateInfo.FromFile(_updateInfoFilepath);

        // The backup directory is a mandatory exclusion directory, so if it is not listed, add it.
        if (_localUpdateInfo.IgnoreFiles.Any(x => x.Name == BackupDirectory) is false)
            _localUpdateInfo.IgnoreFiles.Add(new() { Name = BackupDirectory });

        if (_localUpdateInfo.IgnoreFiles.Any(x => x.Name == PublishDirectory) is false)
            _localUpdateInfo.IgnoreFiles.Add(new() { Name = PublishDirectory });

        if (_localUpdateInfo.IgnoreFiles.Any(x => x.Name == UpdateInfoFilename) is false)
            _localUpdateInfo.IgnoreFiles.Add(new() { Name = UpdateInfoFilename });
    }

    /// <summary>
    /// Generates hash information for all files in the current directory.
    /// </summary>
    public void GenerateFiles()
    {
        try
        {
            var publishDirectory = Path.Combine(_rootPath, PublishDirectory);
            _localUpdateInfo.ApplyFiles(_rootPath, publishDirectory);
            _localUpdateInfo.ToFile(_updateInfoFilepath);

            // Copy lastupdate.xml to the publish directory
            File.Copy(_updateInfoFilepath, Path.Combine(publishDirectory, UpdateInfoFilename), true);
        }
        catch (Exception ex)
        {
            throw new UpdaterException(UpdaterError.FailedGenerateHashCode, ex);
        }
    }

    /// <summary>
    /// Returns the current version and the latest version.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="UpdaterException"></exception>
    public UpdateComparison GetUpdateComparison()
    {
        try
        {
            var remoteUpdateInfo = UpdateInfo.FromUri($"{_localUpdateInfo.Uri}/{UpdateInfoFilename}");
            return new UpdateComparison(_localUpdateInfo, remoteUpdateInfo);
        }
        catch (Exception ex)
        {
            throw new UpdaterException(UpdaterError.RemoteAccessFailure, ex);
        }
    }

    /// <summary>
    /// Update to the latest version.
    /// </summary>
    public IUpdateCommand Update(Action<UpdateState> updateStateCallback)
    {
        _updateTaskCTS = new CancellationTokenSource();
        _updateTask = Task.Run(() =>
        {
            var sourcePath = _rootPath;
            var lastestPath = Path.Combine(_rootPath, BackupDirectory, LastestDirectory);
            if (Directory.Exists(lastestPath) is true)
                Directory.Delete(lastestPath, true);
            Directory.CreateDirectory(lastestPath);

            var updateComparison = GetUpdateComparison();

            var files = updateComparison.Current.GetFiles(updateComparison.Lastest);
            var totals = files.Count();
            var current = 0;

            // Save the latest lastupdate.xml
            updateComparison.Lastest.ToFile(Path.Combine(lastestPath, UpdateInfoFilename));

            using var webClient = new WebClient();

            foreach (var file in files)
            {
                _updatePauseEvent.WaitOne();
                if (_updateTaskCTS.IsCancellationRequested is true)
                    return;

                updateStateCallback?.Invoke(new UpdateState(totals, current, file.Name, false));

                try
                {
                    if (file.Action is UpdateInfo.ActionKind.Remove)
                        continue;

                    var targetFilename = Path.Combine(lastestPath, file.Name);
                    var targetFilepath = Path.GetDirectoryName(targetFilename);
                    if (Directory.Exists(targetFilepath) is false)
                        Directory.CreateDirectory(targetFilepath);

                    if (file.Action is UpdateInfo.ActionKind.Keep)
                    {
                        File.Copy(Path.Combine(sourcePath, file.Name), targetFilename, true);
                    }
                    else if (file.Action is UpdateInfo.ActionKind.Update)
                    {
                        var remoteUri = $"{updateComparison.Lastest.Uri}/{file.Name}.gz";
                        webClient.DownloadFile(remoteUri, $"{targetFilename}.gz");

                        var compressedStream = File.OpenRead($"{targetFilename}.gz");
                        using var targetFileStream = File.OpenWrite(targetFilename);
                        using var decompressor = new GZipStream(compressedStream, CompressionMode.Decompress);
                        decompressor.CopyTo(targetFileStream);

                        compressedStream.Dispose();
                        File.Delete($"{targetFilename}.gz");
                    }
                }
                finally
                {
                    current++;
                }
            }

            updateStateCallback?.Invoke(new UpdateState(totals, current, "", true));

            // Back up current files.
            MoveAll(sourcePath, Path.Combine(_rootPath, BackupDirectory, PreviousDirectory), true, false, updateComparison.Current.IgnoreFiles.Where(x => x.Name != UpdateInfoFilename).Select(x => x.Name));

            // Change to the latest files.
            MoveAll(lastestPath, sourcePath, false, true, Enumerable.Empty<string>());
        }, _updateTaskCTS.Token);

        return this as IUpdateCommand;
    }

    private void MoveAll(string sourcePath, string targetPath, bool isDeleteTarget, bool isCopy, IEnumerable<string> ignoreRelativeFiles)
    {
        var currentfiles = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories);
        if (isDeleteTarget is true && Directory.Exists(targetPath) is true)
            Directory.Delete(targetPath, true);
        foreach (var file in currentfiles)
        {
            var relativeFilename = file.Substring(sourcePath.Length + 1);
            if (ignoreRelativeFiles.Any(x => x == relativeFilename || relativeFilename.StartsWith($@"{x}{Path.DirectorySeparatorChar}")) is true)
                continue;

            var targetFilename = Path.Combine(targetPath, relativeFilename);
            var targetFilepath = Path.GetDirectoryName(targetFilename);
            if (Directory.Exists(targetFilepath) is false)
                Directory.CreateDirectory(targetFilepath);
            if (isCopy is false)
                File.Move(file, targetFilename);
            else
                File.Copy(file, targetFilename, true);
        }
    }

    /// <summary>
    /// Restore to a previous version.
    /// </summary>
    public void Restore()
    {

    }


    void IUpdateCommand.Pause()
    {
        _updatePauseEvent.Reset();
    }

    void IUpdateCommand.Resume()
    {
        _updatePauseEvent.Set();
    }

    void IUpdateCommand.Cancel()
    {
        _updatePauseEvent.Set();
        _updateTaskCTS?.Cancel();
    }

    void IUpdateCommand.Restart()
    {
    }
}

public class UpdateState
{
    public int Totals { get; }
    public int Current { get; }
    public string Filename { get; }
    public bool isFinished { get; }

    public UpdateState(int totals, int current, string filename, bool isFinished)
    {
        Totals = totals;
        Current = current;
        Filename = filename;
        this.isFinished = isFinished;
    }
}

public interface IUpdateCommand
{
    bool CanRestart { get; }

    void Pause();
    void Resume();
    void Cancel();
    void Restart();
}

public class UpdaterException : Exception
{
    private UpdaterError Error { get; }

    public UpdaterException(UpdaterError error, Exception innerException) : base(error.ToString(), innerException)
    {
        Error = error;
    }
}

public enum UpdaterError
{
    /// <summary>
    /// Remote access failure
    /// </summary>
    RemoteAccessFailure,

    /// <summary>
    /// No remote update information
    /// </summary>
    NoRemoteUpdateInformation,

    /// <summary>
    /// Failed to write update file
    /// </summary>
    FailedWriteUpdateFile,

    /// <summary>
    /// Failed to generate hash code.
    /// </summary>
    FailedGenerateHashCode
}

public class UpdateComparison
{
    public string Name => Current.Name;

    public UpdateInfo Current { get; }
    public UpdateInfo Lastest { get; }

    public bool IsNeedUpdate => Current.IsLessVersionThan(Lastest);

    public UpdateComparison(UpdateInfo current, UpdateInfo lastest)
    {
        Current = current;
        Lastest = lastest;
    }
}
