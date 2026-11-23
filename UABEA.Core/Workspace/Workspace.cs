using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    // Multi-bundle workspace. Compared with the old BundleWorkspace / AssetWorkspace coexists as a transition solution.
    //
    // Design follows AssetWorkspace.Workspace of UABEANext4, with the following simplifications:
    //   - Save logic is not implemented (left for later P1)
    //   - Parallel loading is not implemented (kept serial)
    //   - Does not depend on AssetsManager.FileLookup / GetFileLookupKey
 // (the currently referenced AssetsTools.NET version does not provide these APIs yet),
    //     De-duplication uses this class's own ItemLookup dictionary instead.
    //
    // The workspace is organized as a tree: RootItems holds all root-level files; sub-files inside a bundle
    // are the Children of that bundle item, with Parent set to the parent bundle.
    public class Workspace
    {
        // Assets manager, responsible for loading bundle / assets files and maintaining dependencies
        public AssetsManager am { get; }

 // All root-level files (standalone bundle / assets / resource)
        public List<WorkspaceItem> RootItems { get; }

        // Quickly look up workspace items by path (key is the normalized path)
        public Dictionary<string, WorkspaceItem> ItemLookup { get; }

        // File item change event, for the UI layer to subscribe to
        public delegate void WorkspaceItemEvent(WorkspaceItem item);
        public event WorkspaceItemEvent? ItemAdded;
        public event WorkspaceItemEvent? ItemRemoved;
        public event WorkspaceItemEvent? ItemUpdated;

        // Notify subscribers that a workspace item has been updated (for example after editing). For UI / later save logic to call.
        public void NotifyItemUpdated(WorkspaceItem item)
        {
            ItemUpdated?.Invoke(item);
        }

        public Workspace()
        {
            am = new AssetsManager();
            RootItems = new List<WorkspaceItem>();
            ItemLookup = new Dictionary<string, WorkspaceItem>();

            // Load classdata.tpk (if present), so the class database can later be loaded by Unity version
            string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
            {
                am.LoadClassPackage(classDataPath);
            }
        }

        // Normalized path used as the ItemLookup key: unify separators and lowercase
        private static string NormalizeKey(string path)
        {
            return path.Replace('\\', '/').ToLowerInvariant();
        }

        // Check whether the file at the given path has already been loaded (for de-duplication)
        public bool HasFile(string path)
        {
            string key = NormalizeKey(path);
            return ItemLookup.ContainsKey(key);
        }

        // Automatically determine the type and load any file, returning the created WorkspaceItem.
        // Returns null for unrecognized files.
        public WorkspaceItem? LoadAnyFile(string path)
        {
            DetectedFileType detectedType = FileTypeDetector.DetectFileType(path);
            if (detectedType == DetectedFileType.BundleFile)
            {
                return LoadBundle(path);
            }
            else if (detectedType == DetectedFileType.AssetsFile)
            {
                return LoadAssets(path);
            }
            else if (path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
            {
                return LoadResource(path);
            }

            return null;
        }

        // Load a bundle file and iterate over its internal directory info, creating a WorkspaceItem for each sub-file.
        // bundle Serialized sub-files inside are registered with am, making later asset reading possible.
        public WorkspaceItem LoadBundle(string path)
        {
            string key = NormalizeKey(path);
            if (ItemLookup.ContainsKey(key))
            {
                throw new DuplicateWorkspaceFileException(path);
            }

            // Unpack into memory so internal sub-files can be read directly through SegmentStream
            BundleFileInstance bunInst = am.LoadBundleFile(path, true);
            TryLoadClassDatabase(bunInst.file);

            WorkspaceItem item = new WorkspaceItem(bunInst);

            // Iterate over the bundle's internal directory info and create one for each sub-file WorkspaceItem
            var dirInfs = bunInst.file.BlockAndDirInfo.DirectoryInfos;
            foreach (var dirInf in dirInfs)
            {
                string childName = dirInf.Name;
                string childKey = NormalizeKey(Path.Combine(bunInst.path, childName));
                if (ItemLookup.ContainsKey(childKey))
                {
                    throw new DuplicateWorkspaceFileException(childName, bunInst.path);
                }

                // Wrap the range of this sub-file in the bundle data with SegmentStream,
                // following the approach of the existing BundleWorkspace.PopulateFilesList
                long startAddress = dirInf.Offset;
                long length = dirInf.DecompressedSize;
                SegmentStream segStream = new SegmentStream(
                    bunInst.file.DataReader.BaseStream, startAddress, length);

                WorkspaceItem child;
                bool isSerialized = (dirInf.Flags & 0x04) != 0;
                if (isSerialized)
                {
                    // Register with am, making later asset reading possible; the parent bundle is set to the current bundle
                    string childVirtualPath = Path.Combine(bunInst.path, childName);
                    AssetsFileInstance fileInst = am.LoadAssetsFile(
                        segStream, childVirtualPath, false, bunInst);
                    TryLoadClassDatabase(fileInst.file);

                    child = new WorkspaceItem(childName, fileInst);
                }
                else
                {
                    WorkspaceItemType type = IsResourceName(childName)
                        ? WorkspaceItemType.ResourceFile
                        : WorkspaceItemType.OtherFile;
                    child = new WorkspaceItem(childName, segStream, type);
                }

                child.Parent = item;
                item.Children.Add(child);
                ItemLookup[childKey] = child;
            }

            RootItems.Add(item);
            ItemLookup[key] = item;
            ItemAdded?.Invoke(item);
            return item;
        }

        // Load a standalone .assets file
        public WorkspaceItem LoadAssets(string path)
        {
            string key = NormalizeKey(path);
            if (ItemLookup.ContainsKey(key))
            {
                throw new DuplicateWorkspaceFileException(path);
            }

            AssetsFileInstance fileInst = am.LoadAssetsFile(path, true);
            TryLoadClassDatabase(fileInst.file);

            WorkspaceItem item = new WorkspaceItem(fileInst);
            RootItems.Add(item);
            ItemLookup[key] = item;
            ItemAdded?.Invoke(item);
            return item;
        }

        // Load a resource file (.resS / .resource etc.)
        public WorkspaceItem LoadResource(string path)
        {
            string key = NormalizeKey(path);
            if (ItemLookup.ContainsKey(key))
            {
                throw new DuplicateWorkspaceFileException(path);
            }

            string name = Path.GetFileName(path);
            FileStream fs = File.OpenRead(path);
            WorkspaceItem item = new WorkspaceItem(name, fs, WorkspaceItemType.ResourceFile);
            RootItems.Add(item);
            ItemLookup[key] = item;
            ItemAdded?.Invoke(item);
            return item;
        }

        // Remove the file at the specified path. If it is a bundle, all its sub-files are removed together.
        public void RemoveFile(string path)
        {
            string key = NormalizeKey(path);
            if (!ItemLookup.TryGetValue(key, out WorkspaceItem? item))
            {
                return;
            }

            // Handle sub-files first (inside the bundle)
            if (item.Children.Count > 0)
            {
                string basePath = item.BundleFileInstance != null
                    ? item.BundleFileInstance.path
                    : item.Name;
                List<WorkspaceItem> childrenCopy = new List<WorkspaceItem>(item.Children);
                foreach (WorkspaceItem child in childrenCopy)
                {
                    string childKey = NormalizeKey(Path.Combine(basePath, child.Name));
                    ItemLookup.Remove(childKey);
                    ItemRemoved?.Invoke(child);
                }
                item.Children.Clear();
            }

            // Unload resources held by the root-level file
            if (item.ItemType == WorkspaceItemType.BundleFile && item.BundleFileInstance != null)
            {
                am.UnloadBundleFile(item.BundleFileInstance);
            }
            else if (item.ItemType == WorkspaceItemType.AssetsFile &&
                     item.AssetsFileInstance != null && item.Parent == null)
            {
                am.UnloadAssetsFile(item.AssetsFileInstance);
            }
            else if ((item.ItemType == WorkspaceItemType.ResourceFile ||
                      item.ItemType == WorkspaceItemType.OtherFile) && item.Stream != null)
            {
                item.Stream.Close();
            }

            // Remove from the parent item or the root list
            if (item.Parent != null)
            {
                item.Parent.Children.Remove(item);
                item.Parent = null;
            }
            else
            {
                RootItems.Remove(item);
            }

            ItemLookup.Remove(key);
            ItemRemoved?.Invoke(item);
        }

        // Get the list of all loaded AssetsFileInstance in the workspace
 // (including root-level assets files and serialized sub-files inside bundles)
        public List<AssetsFileInstance> GetAssetsFileInstances()
        {
            List<AssetsFileInstance> result = new List<AssetsFileInstance>();
            foreach (WorkspaceItem item in RootItems)
            {
                if (item.ItemType == WorkspaceItemType.AssetsFile && item.AssetsFileInstance != null)
                {
                    result.Add(item.AssetsFileInstance);
                }
                else if (item.ItemType == WorkspaceItemType.BundleFile)
                {
                    foreach (WorkspaceItem child in item.Children)
                    {
                        if (child.ItemType == WorkspaceItemType.AssetsFile && child.AssetsFileInstance != null)
                        {
                            result.Add(child.AssetsFileInstance);
                        }
                    }
                }
            }
            return result;
        }

        // If the class database is not loaded yet, load it from classdata.tpk according to the Unity version of the assets file
        private void TryLoadClassDatabase(AssetsFile file)
        {
            if (am.ClassDatabase == null)
            {
                string version = file.Metadata.UnityVersion;
                if (!string.IsNullOrEmpty(version) && version != "0.0.0")
                {
                    am.LoadClassDatabaseFromPackage(version);
                }
            }
        }

        // If the class database is not loaded yet, load it from classdata.tpk according to the engine version of the bundle file
        private void TryLoadClassDatabase(AssetBundleFile file)
        {
            if (am.ClassDatabase == null)
            {
                string version = file.Header.EngineVersion;
                if (!string.IsNullOrEmpty(version) && version != "0.0.0")
                {
                    am.LoadClassDatabaseFromPackage(version);
                }
            }
        }

        // Determine whether the file name belongs to a resource file
        private static bool IsResourceName(string name)
        {
            return name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase);
        }

        // ============ Asset field reading / PPtr Parsing helper methods ============
        // For plugin previewers (such as MeshPlugin) to load asset base fields on demand and resolve component references.
        // Design follows the same-named method of the old AssetWorkspace, but without the LoadedAssets cache,
        // constructing an AssetContainer on demand each time, keeping stateless query semantics for the new workspace.

        /// <summary>
        /// Get the base value field of an asset. If the container has not loaded the field yet, use <see cref="am"/> to decode on demand.
        /// Non-MonoBehaviour assets do not need a RefTypeManager.
        /// </summary>
        public AssetTypeValueField? GetBaseField(AssetContainer cont)
        {
            if (cont == null)
                return null;

            if (cont.HasValueField)
                return cont.BaseValueField;

            try
            {
                AssetTypeTemplateField tempField = am.GetTemplateBaseField(
                    cont.FileInstance, cont.FileReader, cont.FilePosition,
                    cont.ClassId, cont.MonoId, AssetReadFlags.None);
                return tempField.MakeValue(cont.FileReader, cont.FilePosition);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolve the target asset container by PPtr (m_FileID + m_PathID).
        /// m_FileID 0 means the same file, otherwise it is a 1-based dependency index.
        /// The returned container only contains info (BaseValueField is empty); call <see cref="GetBaseField"/> to decode.
        /// </summary>
        public AssetContainer? GetAssetContainer(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
        {
            int fileId = pptrField["m_FileID"].AsInt;
            long pathId = pptrField["m_PathID"].AsLong;

            AssetsFileInstance targetInst = fileInst;
            if (fileId != 0)
            {
                targetInst = fileInst.GetDependency(am, fileId - 1);
                if (targetInst == null)
                    return null;
            }

            AssetFileInfo info = targetInst.file.GetAssetInfo(pathId);
            if (info == null)
                return null;

            return new AssetContainer(info, targetInst);
        }
    }
}
