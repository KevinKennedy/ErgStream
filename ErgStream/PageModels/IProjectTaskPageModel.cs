using CommunityToolkit.Mvvm.Input;
using ErgStream.Models;

namespace ErgStream.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}