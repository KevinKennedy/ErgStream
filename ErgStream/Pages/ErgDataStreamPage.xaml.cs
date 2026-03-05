using ErgComm;
using ErgComm.Models;
using System.Text;
using ErgStream.ViewModels;
using System.ComponentModel;

namespace ErgStream.Pages
{
    public partial class ErgDataStreamPage : ContentPage
    {
        private readonly ErgDataStreamViewModel _viewModel;

        public ErgDataStreamPage(ErgDataStreamViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            BindingContext = _viewModel;
            
            // Subscribe to property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ErgDataStreamViewModel.DataText))
            {
                // Scroll to bottom when DataText changes
                DataScrollView.ScrollToAsync(DataLabel, ScrollToPosition.End, false);
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Disconnect();
        }
    }
}