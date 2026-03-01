using ErgComm;
using ErgComm.Models;
using System.Collections.ObjectModel;

namespace ErgStream.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly ErgCommService _ergCommService;
        private CancellationTokenSource? _discoveryCts;

        public ObservableCollection<ErgInfo> Items { get; set; }

        public MainPage()
        {
            InitializeComponent();
            
            _ergCommService = new ErgCommService();
            Items = new ObservableCollection<ErgInfo>();
            
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Start discovering ergs when page becomes visible
            _discoveryCts = new CancellationTokenSource();
            
            try
            {
                await _ergCommService.StartFindErgsAsync(
                    ergList =>
                    {
                        // Update UI on main thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Items.Clear();
                            foreach (var erg in ergList)
                            {
                                Items.Add(erg);
                            }
                        });
                    },
                    _discoveryCts.Token,
                    includeMockErg: true); // Set to false in production
            }
            catch (OperationCanceledException)
            {
                // Expected when page disappears
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Stop discovery when page is not visible
            _discoveryCts?.Cancel();
            _discoveryCts?.Dispose();
            _discoveryCts = null;
        }

        private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is ErgInfo selectedErg)
            {
                // Navigate using absolute route with ///
                await Shell.Current.GoToAsync($"///datastream?ergId={selectedErg.Id}");
                
                // Deselect the item
                ((CollectionView)sender).SelectedItem = null;
            }
        }
    }
}