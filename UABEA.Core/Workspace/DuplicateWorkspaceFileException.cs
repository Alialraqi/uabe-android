using System;

namespace UABEAvalonia
{
    // Thrown when the same file is loaded repeatedly.
    // This implementation does not depend on the AssetsManager's FileLookup / GetFileLookupKey
 // (the currently referenced AssetsTools.NET version does not provide these APIs yet),
    // only keeps the key info of the duplicate file, making it easy for upper layers to prompt the user.
    public class DuplicateWorkspaceFileException : Exception
    {
        // Path (or virtual path) of the duplicate file
        public string FilePath { get; }

        // If the file comes from a bundle, records the bundle's path; otherwise an empty string
        public string BundlePath { get; }

        public DuplicateWorkspaceFileException(string filePath, string bundlePath = "")
        {
            FilePath = filePath;
            BundlePath = bundlePath ?? string.Empty;
        }

        public override string Message
        {
            get
            {
                if (string.IsNullOrEmpty(BundlePath))
                {
                    return $"File '{FilePath}' has already been loaded and cannot be loaded again.";
                }
                else
                {
                    return $"File '{FilePath}' (From bundle '{BundlePath}')has already been loaded and cannot be loaded again.";
                }
            }
        }
    }
}
