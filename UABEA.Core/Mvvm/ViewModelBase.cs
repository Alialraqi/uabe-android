using CommunityToolkit.Mvvm.ComponentModel;

namespace UABEAvalonia
{
    /// <summary>
    /// Base class of all ViewModels, providing INotifyPropertyChanged support.
    /// Located in the platform-independent UABEA.Core, shared by the main project and Android.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
    }
}
