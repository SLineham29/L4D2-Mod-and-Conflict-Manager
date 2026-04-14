using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamDatabase.ValvePak;
using Avalonia.Platform.Storage;
using l4d2_mod_manager.Models;
using ValveKeyValue;

namespace l4d2_mod_manager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _gameDirectory = "N/A";

    [ObservableProperty] private ObservableCollection<ModFile> _modCollection = new();

    [ObservableProperty]
    private ObservableCollection<string> _conflictList = new();

    public MainWindowViewModel()
    {
        string filepath = "GameDirectory.txt";
        
        if(File.Exists(filepath))
        {
                GameDirectory = File.ReadAllText(filepath);
        }
        else
        {
            GameDirectory = "N/A";
        }        
    }

    [RelayCommand]
    private async Task SetGameDirectory()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var directoryFolders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Please select your Left 4 Dead 2 addons folder (Stored in Left 4 Dead 2/left4dead2)",
                AllowMultiple = false,
                SuggestedFileName = "addons"
            });

            if (directoryFolders.Count != 0)
            {
                GameDirectory = directoryFolders[0].Path.LocalPath;
                File.WriteAllText("GameDirectory.txt", GameDirectory);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Could not find this folder.");
            }
        }
    }

    private ModFile CreateNewModFile(Package package, PackageEntry addonInfo)
    {
        package.ReadEntry(addonInfo, out byte[] data);
        var ms = new MemoryStream(data);
        var kvs = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        KVObject convertedInfo;
        try
        {
            convertedInfo = kvs.Deserialize(ms);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            convertedInfo = null;
        }

        ModFile mod = new ModFile();
        
        if (convertedInfo != null)
        {
            foreach (var child in convertedInfo.Children)
            {
                string key = child.Key;
                switch (key.ToLower())
                {
                    case "addontitle":
                        mod.ModName = child.Value.ToString();
                        break;
                    case "addondescription":
                        mod.ModDescription = child.Value.ToString();
                        break;
                    case "addonauthor":
                        mod.ModAuthor = child.Value.ToString();
                        break;
                    case "addoncontent_campaign":
                    case "addoncontent_map":
                        if (mod.IsMap == false)
                        {
                            mod.IsMap = String.Equals(child.Value.ToString(), "1");
                        }
                        break;
                }
            }
        }
        else
        {
            mod.FileName = "Corrupt AddonInfo";
        }
        return mod;
    }

    private Dictionary<string, List<string>> ParseVpkFiles()
    {
        var vpkFiles = Directory.EnumerateFiles(GameDirectory, "*.vpk", SearchOption.AllDirectories);
        var conflictMapper = new Dictionary<string, List<string>>();

        foreach (var vpkFile in vpkFiles)
        {
            Console.WriteLine($"Reading {Path.GetFileName(vpkFile)}");
            ModFile currentMod;            
            using var package = new Package();
            package.Read(vpkFile);
            var infoFile = package.FindEntry("addoninfo.txt");
            if (infoFile != null)
            {
                currentMod = CreateNewModFile(package, infoFile);
                currentMod.FileName = Path.GetFileName(vpkFile);
            }
            else
            {
                currentMod = new ModFile
                {
                    ModName = "Invalid Mod Info",
                    FileName = Path.GetFileName(vpkFile)
                };
            }
            string imageLocation = Path.GetDirectoryName(vpkFile) + @"\" + Path.GetFileNameWithoutExtension(vpkFile) + ".jpg";
            if (Directory.Exists(imageLocation))
            {
                currentMod.ImageLink = imageLocation;
            }
            
            if (currentMod.IsMap)
            {
                continue;
            }

            foreach (var fileType in package.Entries)
            {
                foreach (var entry in fileType.Value)
                {
                    if(entry.TypeName == "txt" || entry.TypeName == "jpg" || entry.TypeName == "cache")
                    {
                        continue;
                    }
                    string filePath = $"{entry.DirectoryName}/{entry.FileName}.{entry.TypeName}";
                    if (!conflictMapper.ContainsKey(filePath))
                    {
                        conflictMapper[filePath] = new List<string>();
                    }
                    conflictMapper[filePath].Add(currentMod.ModName);
                    currentMod.FileList.Add(filePath);
                }
            }
            ModCollection.Add(currentMod);
        }
        return conflictMapper;
    }

    [RelayCommand]
    private void CheckForConflicts()
    {
        if (!Directory.Exists(GameDirectory))
        {
            return;
        }
        
        ConflictList.Clear();

        var conflictMapper = ParseVpkFiles();

        foreach (var entry in conflictMapper)
        {
            if (entry.Value.Count > 1)
            {
                string conflictingMods = string.Join(", ", entry.Value);
                ConflictList.Add($"{entry.Key} - {conflictingMods}");
            }
        }
    }
}
    