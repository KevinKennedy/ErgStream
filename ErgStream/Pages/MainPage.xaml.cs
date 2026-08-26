using ErgComm;
using ErgComm.Models;
using System.Collections.ObjectModel;

namespace ErgStream.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly ErgCommService _ergCommService;
        private readonly ErgRecorder ergRecorder;
        private CancellationTokenSource? _discoveryCts;

        public ObservableCollection<ErgInfo> Items { get; set; }

        public MainPage(ErgCommService ergComService, ErgRecorder ergRecorder)
        {
            InitializeComponent();
            
            _ergCommService = ergComService;
            this.ergRecorder = ergRecorder;
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
                            UpdateItemsInPlace(ergList);
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

        // AI code, not the best...
        private void UpdateItemsInPlace(IEnumerable<ErgInfo> ergList)
        {
            var newItems = ergList.ToDictionary(e => e.Id);
            var existingIds = Items.Select(e => e.Id).ToHashSet();

            // Remove items that no longer exist
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!newItems.ContainsKey(Items[i].Id))
                {
                    Items.RemoveAt(i);
                }
            }

            // Update existing items and add new ones
            foreach (var newErg in newItems.Values)
            {
                var existingItem = Items.FirstOrDefault(e => e.Id == newErg.Id);
                if (existingItem != null)
                {
                    // Update existing item properties
                    existingItem.Name = newErg.Name;
                    existingItem.SignalStrength = newErg.SignalStrength;
                    existingItem.ErgType = newErg.ErgType;
                    existingItem.IsMockErg = newErg.IsMockErg;
                }
                else
                {
                    // Add new item
                    Items.Add(newErg);
                }
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
                _ = Task.Run(() => ergRecorder.ConnectToErgAsync(selectedErg.Id));
                
                //await Shell.Current.GoToAsync($"///datastream");
                await Shell.Current.GoToAsync($"///ergprogram");

                // Deselect the item
                ((CollectionView)sender).SelectedItem = null;
            }
        }
    }
}