#pragma warning disable CS8321 // 로컬 함수가 선언되었지만 사용되지 않음

using DMUpdater;

using System.Reflection;


//var s = new UpdateInfo();
//var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
//s.ApplyFiles(currentPath);


var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
var updater = new Updater(rootPath);
//updater.GenerateFilesHash();
var updateCompariso = updater.GetUpdateComparison();
Console.WriteLine(updateCompariso.Name);
Console.WriteLine(updateCompariso.Current.Version);
Console.WriteLine(updateCompariso.Lastest.Version);
Console.WriteLine(updateCompariso.IsNeedUpdate);






// 버젼 구조 확인

void VersionTest()
{
    var v1 = Version.Parse("1.2");
    var v2 = Version.Parse("1.2.2022");
    var v3 = Version.Parse("1.2.2022.2");

    Console.WriteLine($"Version: {v3}");
    Console.WriteLine($"Major : {v3.Major}");
    Console.WriteLine($"Minor : {v3.Minor}");
    Console.WriteLine($"Build : {v3.Build}");
    Console.WriteLine($"Revision : {v3.Revision}");
    Console.WriteLine($"MajorRevision : {v3.MajorRevision}");
    Console.WriteLine($"MinorRevision : {v3.MinorRevision}");

    Console.WriteLine();

    Console.WriteLine($"v1 = {v1}");
    Console.WriteLine($"v2 = {v2}");
    Console.WriteLine($"v3 = {v3}");

    Console.WriteLine($"v1 >= v2 = {v1 >= v2}");
    Console.WriteLine($"v1 >= v3 = {v1 >= v3}");
    Console.WriteLine($"v2 >= v1 = {v2 >= v1}");
    Console.WriteLine($"v2 >= v3 = {v2 >= v3}");
    Console.WriteLine($"v3 >= v1 = {v3 >= v1}");
    Console.WriteLine($"v3 >= v2 = {v3 >= v2}");

    Console.WriteLine();

    var v4 = Version.Parse("1.2");
    var v5 = Version.Parse("1.2.0");

    Console.WriteLine($"v4 = {v4}");
    Console.WriteLine($"v5 = {v5}");
    Console.WriteLine($"v4 >= v5 = {v4 >= v5}");
    Console.WriteLine($"v5 >= v4 = {v5 >= v4}");
}

void MakeLastupdateFileTest()
{

    var remoteUpdateInfo = new UpdateInfo
    {
        Version = Version.Parse("1.0.0"),
    };
    remoteUpdateInfo.Files.Add(new()
    {
        Name = "test.txt",
        Hash = "1213131321352512341234555",
    });

    var nowUpdateInfo = new UpdateInfo
    {
        Version = Version.Parse("0.9.9")
    };


    var result = nowUpdateInfo.IsLessVersionThan(remoteUpdateInfo);
    Console.WriteLine(result);

    var tempPath = Path.GetTempPath();
    var filename = Path.Combine(tempPath, "lastupdate.xml");

    remoteUpdateInfo.ToFile(filename);
    Console.WriteLine(filename);
}