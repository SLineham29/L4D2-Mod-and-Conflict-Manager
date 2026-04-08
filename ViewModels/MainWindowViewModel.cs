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

namespace l4d2_mod_manager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _gameDirectory = "N/A";

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

    private Dictionary<string, List<string>> ParseVpkFiles()
    {
        var vpkFiles = Directory.EnumerateFiles(GameDirectory, "*.vpk", SearchOption.AllDirectories);

        var conflictMapper = new Dictionary<string, List<string>>();

        foreach (var vpkFile in vpkFiles)
        {
            using var package = new Package();
            package.Read(vpkFile);
            string packageName = Path.GetFileName(vpkFile);

            foreach (var fileType in package.Entries)
            {
                foreach (var entry in fileType.Value)
                {
                    string filePath = $"{entry.DirectoryName}/{entry.FileName}.{entry.TypeName}";
                    if (!conflictMapper.ContainsKey(filePath))
                    {
                        conflictMapper[filePath] = new List<string>();
                    }

                    conflictMapper[filePath].Add(packageName);
                }
            }
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
    