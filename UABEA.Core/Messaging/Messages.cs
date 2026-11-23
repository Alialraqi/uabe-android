using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;

namespace UABEAvalonia
{
    /// <summary>
    /// Collection of message types for cross-ViewModel communication, based on CommunityToolkit WeakReferenceMessenger.
    /// Decouples components such as Workspace, Inspector, Previewer and Hierarchy, avoiding direct references.
    /// Adapted from Messages.cs of UABEANext.
    /// </summary>

    /// <summary>The set of selected assets has changed.</summary>
    public class AssetsSelectedMessage : ValueChangedMessage<List<AssetContainer>>
    {
        public AssetsSelectedMessage(List<AssetContainer> value) : base(value) { }
    }

    /// <summary>Request to open the edit data window.</summary>
    public class RequestEditAssetMessage : ValueChangedMessage<AssetContainer>
    {
        public RequestEditAssetMessage(AssetContainer value) : base(value) { }
    }

    /// <summary>Request to jump to the specified asset (by FileID + PathID).</summary>
    public class RequestVisitAssetMessage : ValueChangedMessage<AssetPPtr>
    {
        public RequestVisitAssetMessage(AssetPPtr value) : base(value) { }
    }

    /// <summary>Request to open the scene hierarchy view and locate the specified GameObject.</summary>
    public class RequestSceneViewMessage : ValueChangedMessage<AssetContainer>
    {
        public RequestSceneViewMessage(AssetContainer value) : base(value) { }
    }

    /// <summary>An asset was modified/added/deleted; notifies Inspector, Previewer, etc. to refresh.</summary>
    public class AssetsUpdatedMessage : ValueChangedMessage<List<AssetContainer>>
    {
        public AssetsUpdatedMessage(List<AssetContainer> value) : base(value) { }
    }

    /// <summary>The selected item in the workspace changed (file tree node).</summary>
    public class SelectedWorkspaceItemChangedMessage : ValueChangedMessage<object?>
    {
        public SelectedWorkspaceItemChangedMessage(object? value) : base(value) { }
    }

    /// <summary>Request to close the specified file/document.</summary>
    public class RequestCloseFileMessage : ValueChangedMessage<string>
    {
        public RequestCloseFileMessage(string value) : base(value) { }
    }

    /// <summary>The workspace is closing; notifies components to release resources.</summary>
    public class WorkspaceClosingMessage : ValueChangedMessage<bool>
    {
        public WorkspaceClosingMessage(bool value) : base(value) { }
    }
}
