using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace DMUpdater;


[XmlRoot("Update")]
public class UpdateInfo
{
    /// <summary>
    /// Program name
    /// </summary>
    [XmlAttribute]
    public string Name { get; set; } = "";

    /// <summary>
    /// Version information
    /// </summary>
    [XmlIgnore]
    public Version Version { get; set; } = Version.Parse("0.1");

    [XmlAttribute(nameof(Version))]
    public string VersionText
    {
        get => Version.ToString();
        set
        {
            Version = Version.Parse(value);
        }
    }

    /// <summary>
    /// Remote Uri with update files
    /// </summary>
    [XmlIgnore]
    public Uri Uri { get; set; } = new Uri("https://test.com/update");

    [XmlAttribute(nameof(Uri))]
    public string UriText
    {
        get => Uri.ToString();
        set
        {
            Uri = new Uri(value);
        }
    }

    /// <summary>
    /// List of files to keep
    /// </summary>
    public List<File> KeepFiles { get; set; } = new();
    /// <summary>
    /// Update file list
    /// </summary>
    public List<File> Files { get; set; } = new();
    /// <summary>
    /// List of files to be excluded from the list of update files
    /// </summary>
    public List<File> IgnoreFiles { get; set; } = new();

    /// <summary>
    /// Returns true if it is lower than the remote version.
    /// </summary>
    /// <param name="remote"></param>
    /// <returns></returns>
    public bool IsLessVersionThan(UpdateInfo remote)
    {
        if (Version < remote.Version)
            return true;

        return false;
    }

    /// <summary>
    /// Returns an IEnumerable<File> with the file behavior set compared to the remote file.
    /// </summary>
    /// <param name="remote"></param>
    /// <returns></returns>
    public IEnumerable<File> GetFiles(UpdateInfo remote)
    {
        foreach (var file in remote.KeepFiles)
        {
            file.Action = ActionKind.Keep;
            yield return file;
        }

        foreach (var file in remote.Files)
        {
            var localFile = Files.FirstOrDefault(x => x.Name == file.Name);
            if (localFile is null || localFile.Hash != file.Hash)
                file.Action = ActionKind.Update;
            else
                file.Action = ActionKind.Keep;

            yield return file;
        }

        foreach (var file in Files)
        {
            var remoteFile = remote.Files.FirstOrDefault(x => x.Name == file.Name);
            if (remoteFile is null)
            {
                file.Action = ActionKind.Remove;
                yield return file;
            }
        }
    }

    /// <summary>
    /// Gets the hash code of the file in the path and reflects it in the file list.
    /// </summary>
    /// <param name="path"></param>
    public void ApplyFiles(string path, string publishPath)
    {
        if (Directory.Exists(publishPath) is false)
            Directory.CreateDirectory(publishPath);

        Files.Clear();

        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relativeFilename = file.Substring(path.Length + 1);
            // Exclude directories or files that should be ignored from the list of files.
            if (IgnoreFiles.Any(x => x.Name == relativeFilename || relativeFilename.StartsWith($@"{x.Name}{Path.DirectorySeparatorChar}")) is true)
                continue;

            var hashCode = GetHashCode(file);

            Files.Add(new()
            {
                Name = relativeFilename,
                Hash = hashCode
            });

            // Compress with gzip.
            using var targetFileStream = System.IO.File.OpenRead(file);
            using var compressedStream = System.IO.File.OpenWrite(Path.Combine(publishPath, $"{relativeFilename}.gz"));
            using var compressor = new GZipStream(compressedStream, CompressionMode.Compress);
            targetFileStream.CopyTo(compressor);
        }
    }

    /// <summary>
    /// Get the hash code of the file.
    /// </summary>
    /// <param name="filepath"></param>
    /// <returns></returns>
    private string GetHashCode(string filepath)
    {
        using var sha1 = SHA1.Create();
        using var s = System.IO.File.OpenRead(filepath);
        return BitConverter.ToString(sha1.ComputeHash(s)).Replace("-", "");
    }

    /// <summary>
    /// Save the instance to a file.
    /// </summary>
    /// <param name="filename"></param>
    public void ToFile(string filename)
    {
        var serializer = new XmlSerializer(typeof(UpdateInfo));
        using var xw = XmlWriter.Create(filename, new() { Indent = true });
        serializer.Serialize(xw, this);
    }

    /// <summary>
    /// Create an instance from a file.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static UpdateInfo FromFile(string filename)
    {
        var serializer = new XmlSerializer(typeof(UpdateInfo));
        using var xr = XmlReader.Create(filename);
        return (UpdateInfo)serializer.Deserialize(xr);
    }

    /// <summary>
    /// Creates an instance through a Uri address.
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    public static UpdateInfo FromUri(string uri)
    {
        var serializer = new XmlSerializer(typeof(UpdateInfo));
        using var webClient = new WebClient();
        var data = webClient.DownloadData(uri);
        using var s = new MemoryStream(data);
        return (UpdateInfo)serializer.Deserialize(s);
    }


    public class File
    {
        /// <summary>
        /// file name. A path relative to the top-level path.
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; } = default!;

        /// <summary>
        /// hash code of the file
        /// </summary>
        [XmlAttribute]
        public string Hash { get; set; } = default!;

        /// <summary>
        /// File processing action kind
        /// </summary>
        [XmlIgnore]
        public ActionKind Action { get; set; }
    }


    /// <summary>
    /// File processing action kind
    /// </summary>
    public enum ActionKind
    {
        /// <summary>
        /// The file persists regardless of the remote file.
        /// </summary>
        Keep,

        /// <summary>
        /// The file will be removed.
        /// </summary>
        Remove,

        /// <summary>
        /// The file will be updated as a remote file.
        /// </summary>
        Update
    }
}

