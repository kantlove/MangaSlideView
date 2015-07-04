using System;
using System.Collections.ObjectModel;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace MangaSlideView {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer dispatcherTimer;

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            Loaded += MainPage_Loaded;
        }

        private void DispatcherTimer_Tick(object sender, object e) {
            ulong AppMemoryUsageUlong = MemoryManager.AppMemoryUsage;
            AppMemoryUsageUlong /= 1024; // convert to KB
            this.TbMemory.Text = AppMemoryUsageUlong.ToString("N") + " KB";
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e) {
            LoadImages();
        }

        public void LoadImages() {
            ObservableCollection<string> data = new ObservableCollection<string>();
            int n = 100, availableImages = 19;

            for(int i = 0; i < n; ++i) {
                int imgId = i % availableImages + 1;
                data.Add(string.Format(@"http://a.mfcdn.net/store/manga/15684/01-010.0/compressed/q{0}.jpg", imgId.ToString("D3"))); 
            }

            this.MainSlideView.ItemsSource = data;
        }

        private void BtnSwitch_Click(object sender, RoutedEventArgs e) {
            MainSlideView.Orientation = 1 - MainSlideView.Orientation;
        }
    }
}
