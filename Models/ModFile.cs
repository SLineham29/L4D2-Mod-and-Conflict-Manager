using System.Collections.Generic;

namespace l4d2_mod_manager.Models;

public class ModFile
{
    public string ModName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ModDescription  { get; set; }  = string.Empty;
    public string ModAuthor  { get; set; }  = string.Empty;
    public bool IsMap  { get; set; }
    
    public List<string> FileList = [];
    public string ImageLink { get; set; } = string.Empty;
}