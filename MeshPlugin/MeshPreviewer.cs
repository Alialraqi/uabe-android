using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using UABEAvalonia;
using UABEAvalonia.Mesh;
using UABEAvalonia.Plugins;

namespace MeshPlugin
{
    /// <summary>
 /// Mesh Previewer plugin. Implements <see cref="IUavPluginPreviewer"/>, Provides 3D mesh preview to the unified Previewer panel.
    /// Adapted from MeshPlugin.MeshPreviewer in UABEANext4, fitted to this project's
    /// <see cref="AssetContainer"/> / <see cref="Workspace"/> types.
    ///
    /// Two entry points are supported:
    ///   - Select a Mesh asset directly (ClassId 43)
    ///   - Select a GameObject (ClassId 1), going through GameObject -> MeshFilter -> Mesh parsing
    /// </summary>
    public class MeshPreviewer : IUavPluginPreviewer
    {
        public string Name => "Mesh Previewer";
        public string Description => "Preview Meshes";

        // Check whether the GameObject has a MeshFilter component (ClassId 33)
        private static bool IsGameObjectWithMeshFilter(Workspace workspace, AssetContainer goAsset)
        {
            if (goAsset.ClassId != (int)AssetClassID.GameObject)
                return false;

            var goBase = workspace.GetBaseField(goAsset);
            if (goBase is null)
                return false;

            var goComponents = goBase["m_Component.Array"];
            foreach (var componentPair in goComponents)
            {
                // The last child field of the component pair is a PPtr (field names differ between versions; taking the last one is the safest)
                var component = componentPair[componentPair.Children.Count - 1];
                var componentCont = workspace.GetAssetContainer(goAsset.FileInstance, component);
                if (componentCont is not null && componentCont.ClassId == (int)AssetClassID.MeshFilter)
                {
                    return true;
                }
            }

            return false;
        }

        // Resolve the Mesh asset container from the GameObject's MeshFilter component
        private static AssetContainer? GetMeshFromGameObject(Workspace workspace, AssetContainer goAsset)
        {
            var goBase = workspace.GetBaseField(goAsset);
            if (goBase is null)
                return null;

            var goComponents = goBase["m_Component.Array"];
            foreach (var componentPair in goComponents)
            {
                var component = componentPair[componentPair.Children.Count - 1];
                var componentCont = workspace.GetAssetContainer(goAsset.FileInstance, component);
                if (componentCont is not null && componentCont.ClassId == (int)AssetClassID.MeshFilter)
                {
                    var mfiltBase = workspace.GetBaseField(componentCont);
                    if (mfiltBase is null)
                        return null;

                    // MeshFilter.m_Mesh Points to the actual Mesh asset
                    var meshCont = workspace.GetAssetContainer(componentCont.FileInstance, mfiltBase["m_Mesh"]);
                    if (meshCont is null)
                        return null;

                    return meshCont;
                }
            }

            return null;
        }

        public UavPluginPreviewerType SupportsPreview(Workspace workspace, List<AssetContainer> selection)
        {
            if (selection == null || selection.Count == 0)
                return UavPluginPreviewerType.None;

            var asset = selection[0];
            return asset.ClassId == (int)AssetClassID.Mesh || IsGameObjectWithMeshFilter(workspace, asset)
                ? UavPluginPreviewerType.Mesh
                : UavPluginPreviewerType.None;
        }

        public MeshObj? ExecuteMesh(Workspace workspace, List<AssetContainer> selection)
        {
            if (selection == null || selection.Count == 0)
                return null;

            try
            {
                var asset = selection[0];

                // When a GameObject is selected, first go through the GameObject -> MeshFilter -> Mesh chain
                if (asset.ClassId == (int)AssetClassID.GameObject)
                {
                    var maybeMeshAsset = GetMeshFromGameObject(workspace, asset);
                    if (maybeMeshAsset is null)
                        return null;

                    asset = maybeMeshAsset;
                }

                var meshBf = workspace.GetBaseField(asset);
                if (meshBf == null)
                    return null;

                return MeshObj.FromBaseField(meshBf, asset.FileInstance);
            }
            catch
            {
                return null;
            }
        }

        // This previewer only supports Mesh; text / and image entries are not handled
        public string ExecuteText(Workspace workspace, List<AssetContainer> selection)
            => throw new NotSupportedException("MeshPreviewer only supports Mesh preview.");

        public byte[] ExecuteImage(Workspace workspace, List<AssetContainer> selection, out int width, out int height)
            => throw new NotSupportedException("MeshPreviewer only supports Mesh preview.");

        public void Cleanup() { }
    }
}
