using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using System;

namespace UABEAvalonia
{
    /// <summary>
    /// ViewModel -> View automatic mapping.
    /// Mapped by the naming convention XxxViewModel to XxxView (in the same assembly UABEAvalonia).
    /// Matching rules:
    ///   1. ViewModel inheriting ViewModelBase (old rule)
    ///   2. Dock dockable implementing Dock.Model.Core.IDockable (introduced in P1)
    ///      including Tool / Document / ToolDock / DocumentDock and other Dock.Model.Mvvm.Controls subclasses.
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        public bool SupportsRecycling => false;

        public Control? Build(object? data)
        {
            if (data == null)
            {
                return null;
            }

 // By naming convention:XxxViewModel -> XxxView. 
            // Note Replace("ViewModel", "View") will replace in the namespace "ViewModels"
 // all with "Views", so the View must be placed in the corresponding Views.{sub-namespace} under.
            string name = data.GetType().FullName!.Replace("ViewModel", "View");
            Type? type = Type.GetType(name);

            if (type != null)
            {
                return (Control?)Activator.CreateInstance(type);
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            // Matches both the old ViewModelBase and Dock's IDockable
            return data is ViewModelBase || data is IDockable;
        }
    }
}
