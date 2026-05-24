using ErgComm;
using ErgComm.Models;
using System.Text;
using ErgStream.ViewModels;
using System.ComponentModel;
using Syncfusion.Maui.DataGrid;
using System.Collections.Specialized;
using System.Diagnostics;

namespace ErgStream.Pages;

public partial class ErgProgramPage : ContentPage
{
    private readonly ErgProgramViewModel viewModel;

    public ErgProgramPage(ErgProgramViewModel viewModel)
	{
        InitializeComponent();

        this.viewModel = viewModel;
        BindingContext = this.viewModel;

        // Subscribe to property changes
        this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ErgProgramViewModel.IntervalTimeRemaining))
        {
        }
    }
}