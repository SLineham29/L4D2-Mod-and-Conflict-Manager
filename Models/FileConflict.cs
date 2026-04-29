using System.Collections.Generic;

namespace l4d2_mod_manager.Models;

public class FileConflict
{
    public string FileName { get; set; } = string.Empty;

    public List<ModFile> ConflictingMods { get; set; } = [];
}