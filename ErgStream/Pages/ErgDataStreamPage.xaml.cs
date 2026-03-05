using ErgComm;
using ErgComm.Models;
using System.Text;
using ErgStream.ViewModels;
using System.ComponentModel;
using Syncfusion.Maui.DataGrid;
using System.Collections.Specialized;

namespace ErgStream.Pages
{
    public partial class ErgDataStreamPage : ContentPage
    {
        private const string ColumnWidthsPreferenceKey = "ErgDataGrid_ColumnWidths";
        private readonly ErgDataStreamViewModel _viewModel;

        public ErgDataStreamPage(ErgDataStreamViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            BindingContext = _viewModel;
            
            // Subscribe to property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Subscribe to DataRows collection changes for auto-scroll
            _viewModel.DataRows.CollectionChanged += OnDataRowsCollectionChanged;

            // Subscribe to column resizing event
            DataGrid.ColumnResizing += OnDataGridColumnResized;
            
            // Restore column widths after columns are generated
            DataGrid.Loaded += OnDataGridLoaded;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ErgDataStreamViewModel.DataText))
            {
                // Scroll to bottom when DataText changes
                DataScrollView.ScrollToAsync(DataLabel, ScrollToPosition.End, false);
            }
        }

        private void OnDataRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Scroll to the last item when new data is added
            if (e.Action == NotifyCollectionChangedAction.Add && _viewModel.DataRows.Count > 0)
            {
                var lastItem = _viewModel.DataRows[_viewModel.DataRows.Count - 1];
                DataGrid.ScrollToRowIndex(_viewModel.DataRows.Count - 1, ScrollToPosition.MakeVisible, canAnimate: true);
            }
        }

        private void OnDataGridLoaded(object? sender, EventArgs e)
        {
            RestoreColumnWidths();
        }

        private void OnDataGridColumnResized(object? sender, EventArgs e)
        {
            // Save column widths when any column is resized
            SaveColumnWidths();
        }

        private void SaveColumnWidths()
        {
            var widths = new List<string>();
            
            foreach (var column in DataGrid.Columns)
            {
                widths.Add($"{column.MappingName}:{column.ActualWidth}");
            }
            
            Preferences.Set(ColumnWidthsPreferenceKey, string.Join(";", widths));
        }

        private void RestoreColumnWidths()
        {
            var savedWidths = Preferences.Get(ColumnWidthsPreferenceKey, string.Empty);
            
            if (string.IsNullOrEmpty(savedWidths))
                return;
            
            var widthDict = new Dictionary<string, double>(){
            };
            
            foreach (var pair in savedWidths.Split(';'))
            {
                var parts = pair.Split(':');
                if (parts.Length == 2 && double.TryParse(parts[1], out var width))
                {
                    widthDict[parts[0]] = width;
                }
            }
            
            foreach (var column in DataGrid.Columns)
            {
                if (widthDict.TryGetValue(column.MappingName, out var savedWidth))
                {
                    column.Width = savedWidth;
                }
            }
        }

        public void OnResetColumnWidths(object sender, EventArgs e)
        {
            Preferences.Remove(ColumnWidthsPreferenceKey);
            
            // Reset to default widths - you can adjust these as needed
            var defaultWidths = new Dictionary<string, double>
            {
                { "TimeStamp", 150 },
                { "ElapsedTime", 100 },
                { "Distance", 110 },
                { "WorkoutType", 110 },
                { "WorkoutState", 120 },
                { "StrokeState", 120 },
                { "DragFactor", 100 },
                { "Speed", 100 },
                { "StrokeRate", 100 },
                { "HeartRate", 90 },
                { "Pace", 110 },
                { "AveragePace", 130 },
                { "Power", 90 },
                { "Calories", 80 }
            };
            
            foreach (var column in DataGrid.Columns)
            {
                if (defaultWidths.TryGetValue(column.MappingName, out var defaultWidth))
                {
                    column.Width = defaultWidth;
                }
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
            _viewModel.DataRows.CollectionChanged -= OnDataRowsCollectionChanged;
            
            if (BindingContext is ErgDataStreamViewModel viewModel)
            {
                viewModel.Disconnect();
            }
        }
    }
}