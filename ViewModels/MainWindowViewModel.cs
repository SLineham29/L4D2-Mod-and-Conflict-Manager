using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
    private ObservableCollection<string> _fileList = new();

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

    private async Task GetGameDirectory()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var directoryFolders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Please select your Left 4 Dead 2 Game Folder",
                AllowMultiple = false,
                SuggestedFileName = "workshop"
            });

            if (directoryFolders.Count != 0)
            {
                GameDirectory = directoryFolders[0].Path.LocalPath;
                File.WriteAllText("GameDirectory.txt", GameDirectory);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Could not find this folder.");
                return;
            }
        }
    }

    [RelayCommand]
    public void TestingVpk()
    {
        FileList.Clear();
        
        using var package = new Package();
        package.Read(@"E:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2\addons\workshop\827842553.vpk");
        
        foreach (var entry in package.Entries["vtf"])
        {
            System.Diagnostics.Debug.WriteLine($"Found: {entry.FileName}");
            FileList.Add($"{entry.FileName}.{entry.TypeName}");
        }
    }

    [RelayCommand]
    public async Task CheckForConflicts()
    {
        await GetGameDirectory();
        
        using var package = new Package();
    }
}
    