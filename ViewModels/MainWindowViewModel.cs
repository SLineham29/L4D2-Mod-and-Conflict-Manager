using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamDatabase.ValvePak;
using Avalonia.Platform.Storage;
using l4d2_mod_manager.Models;
using l4d2_mod_manager.Views;
using ValveKeyValue;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace l4d2_mod_manager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ModListView _modListView = new();
    private readonly ModConflictView _modConflictView = new();
    
    [ObservableProperty]
    private UserControl _currentView;

    [ObservableProperty] private bool _goToConflictView;

    partial void OnGoToConflictViewChanged(bool value)
    {
        CurrentView = value ? _modConflictView : _modListView;
    }
    
    [ObservableProperty]
    private string _gameDirectory = "N/A";

    [ObservableProperty]
    private ObservableCollection<ModFile> _modCollection = new();

    [ObservableProperty]
    private ModFile? _currentMod;

    partial void OnCurrentModChanged(ModFile? value)
    {
        if (value == null)
        {
            return;
        }
    }

    [ObservableProperty]
    private ObservableCollection<FileConflict> _conflictList = new();

    public MainWindowViewModel()
    {
        string filepath = "GameDirectory.txt";
        
        GameDirectory = File.Exists(filepath) ? File.ReadAllText(filepath) : "N/A";
        
        _currentView = _modListView;
    }

    [RelayCommand]
    private void ShowModList()
    {
        CurrentView = _modListView;
    }

    [RelayCommand]
    private void ShowConflictList()
    {
        CurrentView = _modConflictView;
    }

    [RelayCommand]
    private void ViewModDetails(ModFile chosenMod)
    {
        CurrentMod =  chosenMod;
        CurrentView = _modListView;
        GoToConflictView = false;
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
                if (!directoryFolders[0].Path.LocalPath.Contains(@"left4dead2\addons"))                
                {
                    var popup = MessageBoxManager.GetMessageBoxStandard("Wrong Directory",
                        "Selected folder is not the Left 4 Dead 2 addons folder (located in Left 4 Dead 2/left4dead2/addons), please choose another directory.",
                        ButtonEnum.Ok, Icon.Warning);
                    var popupResult = await popup.ShowAsync();
                    return;
                }                
                GameDirectory = directoryFolders[0].Path.LocalPath;
                string txtFileLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameDirectory.txt");
                File.WriteAllText(txtFileLocation, GameDirectory);
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
            Console.WriteLine("Error in this mod, need to read manually.");
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
                        mod.ModDescription = child.Value.ToString().Trim() != "" ? child.Value.ToString() : "No description given.";
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
            // If the addoninfo file is messed up in any way, we can still try and get the title through a regex search.
            string txtInfo = System.Text.Encoding.UTF8.GetString(data).Trim();
            var findTitle = Regex.Match(txtInfo, @"(?i)""?AddonTitle""?\s+""([^""]+)""");
            mod.ModName = findTitle.Success ? findTitle.Groups[1].Value : "Corrupt AddonInfo";
            mod.ModDescription = "No description given.";
        }
        return mod;
    }

    private void ParseVpkFiles()
    {
        var vpkFiles = Directory.EnumerateFiles(GameDirectory, "*.vpk", SearchOption.AllDirectories);
        if (vpkFiles.Count() == 0)
        {
            Console.WriteLine("No VPK files found in this directory.");
            return;
        }
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
                    ModDescription = "No description given.",   
                    FileName = Path.GetFileName(vpkFile)
                };
            }
            
            // steam://openurl/ opens the link directly in Steam if it's installed.
            currentMod.ModWorkshopLink = "steam://openurl/https://steamcommunity.com/sharedfiles/filedetails/?id=" +
                                         Path.GetFileNameWithoutExtension(vpkFile);
            
            string imageLocation = Path.GetDirectoryName(vpkFile) + @"\" + Path.GetFileNameWithoutExtension(vpkFile) + ".jpg";
            if (File.Exists(imageLocation))
            {
                currentMod.ImageLink = imageLocation;
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
                    currentMod.FileList.Add(filePath);
                }
            }
            ModCollection.Add(currentMod);
        }
    }

    private List<FileConflict> CreateConflictMapper(List<ModFile> modCollection)
    {
        var tempConflictCollection = new Dictionary<string, FileConflict>(StringComparer.OrdinalIgnoreCase);
        
        foreach(var mod in modCollection)
        {
            if (!mod.IsMap)
            {
                foreach (var file in mod.FileList)
                {
                    if (!tempConflictCollection.ContainsKey(file))
                    {
                        tempConflictCollection[file] = new FileConflict
                        {
                            FileName = file,
                            ConflictingMods = new List<ModFile>()
                        };
                    }
                    tempConflictCollection[file].ConflictingMods.Add(mod);
                }
            }
        }
        return tempConflictCollection.Values.Where(x => x.ConflictingMods.Count > 1).ToList();
    }

    [RelayCommand]
    private async Task CheckForConflicts()
    {
        if (!Directory.Exists(GameDirectory))
        {
            await SetGameDirectory();
            return;
        }
        
        ConflictList.Clear();
        ModCollection.Clear();
        
        ParseVpkFiles();
        var conflictMapper = CreateConflictMapper(ModCollection.ToList());
        ConflictList = new ObservableCollection<FileConflict>(conflictMapper);
    }
}
    