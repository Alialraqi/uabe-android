using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    // Tree file item in the workspace.
 // Root-level file (standalone bundle / assets / resource)located in Workspace.RootItems;
    // bundle sub-files inside are the Children of that bundle item, with Parent set to the parent bundle.
    // Also keeps the original name OriginalName to track rename operations.
    public class WorkspaceItem
    {
        // Current display name (may have been renamed)
        public string Name { get; set; }

        // Original name when loaded, used to track renames
        public string OriginalName { get; set; }

        // File item type
        public WorkspaceItemType ItemType { get; }

 // File data stream. Mainly used for ResourceFile / OtherFile; 
        // For AssetsFile, the data stream is held by the AssetsFileInstance itself.
        public Stream? Stream { get; set; }

        // Sub-file list. Only bundle items have sub-files; other items have an empty list.
        public List<WorkspaceItem> Children { get; }

        // Marks whether modified (for later save logic, implemented in the P1 stage)
        public bool IsModified { get; set; }

        // Marks whether it is a newly added file
        public bool IsNew { get; set; }

        // Marks whether it is removed
        public bool IsRemoved { get; set; }

        // Parent item. Sub-files inside a bundle point to the bundle item they belong to; root-level files are null.
        public WorkspaceItem? Parent { get; set; }

        // If it is a serialized asset file, holds its AssetsFileInstance
        public AssetsFileInstance? AssetsFileInstance { get; set; }

        // If it is a bundle file, holds its BundleFileInstance
        public BundleFileInstance? BundleFileInstance { get; set; }

        // Root-level serialized asset file
        public WorkspaceItem(AssetsFileInstance fileInst)
        {
            Name = fileInst.name;
            OriginalName = Name;
            ItemType = WorkspaceItemType.AssetsFile;
            AssetsFileInstance = fileInst;
            Children = new List<WorkspaceItem>(0);
        }

        // Root-level bundle file (Children initially empty, filled by Workspace)
        public WorkspaceItem(BundleFileInstance bunInst)
        {
            Name = bunInst.name;
            OriginalName = Name;
            ItemType = WorkspaceItemType.BundleFile;
            BundleFileInstance = bunInst;
            Children = new List<WorkspaceItem>();
        }

        // bundle Sub serialized asset file inside
        public WorkspaceItem(string name, AssetsFileInstance fileInst)
        {
            Name = name;
            OriginalName = Name;
            ItemType = WorkspaceItemType.AssetsFile;
            AssetsFileInstance = fileInst;
            Children = new List<WorkspaceItem>(0);
        }

        // resource / Other files (can be root-level files or sub-files inside a bundle)
        public WorkspaceItem(string name, Stream stream, WorkspaceItemType type)
        {
            Name = name;
            OriginalName = Name;
            ItemType = type;
            Stream = stream;
            Children = new List<WorkspaceItem>(0);
        }

        public override string ToString()
        {
            return Name + (IsModified ? "*" : "");
        }
    }
}
