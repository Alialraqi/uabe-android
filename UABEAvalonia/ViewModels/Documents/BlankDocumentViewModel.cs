using Dock.Model.Mvvm.Controls;

namespace UABEAvalonia.ViewModels.Documents;

/// <summary>
/// Blank placeholder document, the initial content of DocumentDock.
/// The base class Dock.Model.Mvvm.Controls.Document already inherits ObservableObject, so [ObservableProperty] can be used directly.
/// </summary>
public partial class BlankDocumentViewModel : Document
{
    private const string DocumentTitle = "New Tab";

    public BlankDocumentViewModel()
    {
        Id = "Blank";
        Title = DocumentTitle;
        // The placeholder document cannot be closed or floated, ensuring the document area always has at least one tab
        CanClose = false;
        CanFloat = false;
    }
}
