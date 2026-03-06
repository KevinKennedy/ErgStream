using ErgComm;
using ErgComm.Models;
using System.Text;
using ErgStream.ViewModels;
using System.ComponentModel;
using Syncfusion.Maui.DataGrid;
using System.Collections.Specialized;
using System.Diagnostics;

namespace ErgStream.Pages
{
    public partial class ErgDataStreamPage : ContentPage
    {
        private const string ColumnWidthsPreferenceKey = "ErgDataGrid_ColumnWidths";
        private readonly ErgDataStreamViewModel viewModel;

        public ErgDataStreamPage(ErgDataStreamViewModel viewModel)
        {
            InitializeComponent();
            
            this.viewModel = viewModel;
            BindingContext = this.viewModel;
            
            // Subscribe to property changes
            this.viewModel.PropertyChanging += OnViewModelPropertyChanging;
            this.viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Subscribe to DataRows collection changes for auto-scroll
            this.viewModel.DataRows.CollectionChanged += OnDataRowsCollectionChanged;

            // Subscribe to column resizing event
            DataGrid.ColumnResizing += OnDataGridColumnResized;
            
            // Restore column widths after columns are generated
            DataGrid.Loaded += OnDataGridLoaded;


        }

        private void OnViewModelPropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            if (e.PropertyName == nameof(ErgDataStreamViewModel.DataRows))
            {
                viewModel.DataRows.CollectionChanged -= OnDataRowsCollectionChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ErgDataStreamViewModel.DataRows))
            {
                viewModel.DataRows.CollectionChanged += OnDataRowsCollectionChanged;
                ScrollToBottomOfDataGrid();
            }
        }

        private void OnDataRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && viewModel.DataRows.Count > 0)
            {
                ScrollToBottomOfDataGrid();
            }
        }

        private void ScrollToBottomOfDataGrid()
        {
            if (viewModel.AutoScrollEnabled && viewModel.DataRows.Count > 0)
            {
                // Delay the scroll to allow the UI to complete its layout pass
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), async () =>
                {
                    int lastRowIndex = viewModel.DataRows.Count; // - 1 ;
                    await DataGrid.ScrollToRowIndex(lastRowIndex, ScrollToPosition.End, canAnimate: true);
                });
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
            this.viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            this.viewModel.DataRows.CollectionChanged -= OnDataRowsCollectionChanged;
            
            if (BindingContext is ErgDataStreamViewModel viewModel)
            {
                viewModel.Disconnect();
            }
        }
    }
}