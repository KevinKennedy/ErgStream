using ErgStream.Services;
using ErgStream.ViewModels;
using System.ComponentModel;
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

		this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ErgProgramViewModel.IntervalTimeRemaining))
		{
			//Debug.WriteLine("IntervalTimeRemaining changed: " + viewModel.IntervalTimeRemaining);
		}
	}

}