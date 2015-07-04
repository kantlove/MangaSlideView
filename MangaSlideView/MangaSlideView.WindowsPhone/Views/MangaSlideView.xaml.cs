using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace MangaSlideView.Views {
    public sealed partial class MangaSlideView : UserControl, INotifyPropertyChanged {
        #region Private Properties
        private int currentItemIndex;
        private Point firstPoint;
        private ScrollViewer listScrollviewer = null;
        private Orientation _orientation;
        private ObservableCollection<ImageModel> Children;
        private List<SafeImage> managedImages;
        #endregion

        public int VerticalBufferRange { get; set; }
        public int HorizontalBufferRange { get; set; }
        public Orientation Orientation {
            get { return _orientation; }
            set {
                if (value != _orientation)
                    ChangeOrientation(value);
                _orientation = value;
                NotifyPropertyChanged();
            }
        }
        public IEnumerable<object> ItemsSource {
            get { return (IEnumerable<object>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }
        public DataTemplate ItemTemplate { get; set; }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        "ItemsSource", typeof(IEnumerable<object>), typeof(MangaSlideView), new PropertyMetadata(null, ItemsSourceChangedCallback));

        #region Events
        public delegate void SelectionChangedEventHandler(object sender, int currentSelection);
        public event SelectionChangedEventHandler SelectionChanged;
        #endregion

        public ListBox GetList() {
            return MainListBox;
        }

        public MangaSlideView() {
            this.InitializeComponent();

            managedImages = new List<SafeImage>();
            Children = new ObservableCollection<ImageModel>();
            Orientation = Orientation.Horizontal;

            this.MainListBox.DataContext = this;
            this.MainListBox.ItemsSource = Children;

            this.VerticalBufferRange = 3;
            this.HorizontalBufferRange = 1;

            Loaded += MangaSlideView_Loaded;
        }

        public bool ScrollToCurrentItem(Orientation orientation = Orientation.Horizontal) {
            if (this.listScrollviewer != null) {
                if (orientation == Orientation.Horizontal) {
                    this.listScrollviewer.ChangeView(this.currentItemIndex * this.ActualWidth, null, null);
                }
                else {
                    this.listScrollviewer.ChangeView(null, this.currentItemIndex * this.ActualHeight, null);
                }
                return true;
            }
            return false;
        }

        public void ChangeOrientation(Orientation target) {
            switch (target) {
                case Orientation.Horizontal:
                    this.GridTouchPad.IsHitTestVisible = true;

                    if (this.listScrollviewer != null) {
                        this.currentItemIndex = (int)(this.listScrollviewer.VerticalOffset / this.ActualHeight);
                        this.listScrollviewer.ViewChanged -= ListScrollviewer_ViewChanged;
                    }
                    break;
                case Orientation.Vertical:
                    this.GridTouchPad.IsHitTestVisible = false;

                    if (this.listScrollviewer != null)
                        this.listScrollviewer.ViewChanged += ListScrollviewer_ViewChanged;

                    break;
            }

            ScrollToCurrentItem(target);
        }

        public void Reload() {
            this.MainListBox.ItemsSource = null;
            this.MainListBox.ItemsSource = Children;

            ScrollToCurrentItem();
        }

        public void SetStatus(string text = "") {
            this.TbStatus.Text = text;
        }

        /// <summary>
        /// Custom behavior when adding, removing items
        /// </summary>
        private static void ItemsSourceChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue == null || e.NewValue == e.OldValue) {
                return;
            }

            MangaSlideView view = d as MangaSlideView;

            if (view == null)
                return;

            var obsList = e.NewValue as INotifyCollectionChanged;

            if (obsList != null) {
                obsList.CollectionChanged += (sender, eventArgs) => {
                    switch (eventArgs.Action) {
                        case NotifyCollectionChangedAction.Remove:
                            foreach (var oldItem in eventArgs.OldItems) {
                                for (int i = 0; i < view.Children.Count; i++) {
                                    var fxElement = view.Children[i] as ImageModel;
                                    if (fxElement == null || !fxElement.Equals(oldItem)) continue;
                                    view.RemoveAt(i);
                                }
                            }

                            break;

                        case NotifyCollectionChangedAction.Add:
                            foreach (var newItem in eventArgs.NewItems)
                                view.CreateItem(newItem);
                            break;
                    }
                };
            }

            view.Bind();
        }

        /// <summary>
        /// Create and add items from their DataTemplate
        /// </summary>
        private void Bind() {
            if (this.ItemsSource == null)
                return;

            this.Children.Clear();

            foreach (object item in this.ItemsSource)
                this.CreateItem(item);
        }

        private ImageModel CreateItem(object url) {
            if (url == null)
                return null;

            /*
             * Create real data model from url string
             */
            ExtendedUri uri = new ExtendedUri((string)url);
            if (this.Children.Count == 0)
                uri.IsUsing = true; // Mark the first image as Using
            ImageModel model = new ImageModel(this.Children.Count, uri);

            this.Children.Add(model);

            return model;
        }

        private void RemoveAt(int index) {
            this.Children.RemoveAt(index);
        }

        private void MangaSlideView_Loaded(object sender, RoutedEventArgs e) {
            this.listScrollviewer = Library.GetScrollViewer(this.MainListBox);

            if (Orientation == Orientation.Vertical)
                this.listScrollviewer.ViewChanged += ListScrollviewer_ViewChanged;

            this.GridTouchPad.ManipulationStarted += GridTouchPad_ManipulationStarted;
            this.GridTouchPad.ManipulationDelta += GridTouchPad_ManipulationDelta;
            this.GridTouchPad.ManipulationCompleted += GridTouchPad_ManipulationCompleted;
        }

        private void ListScrollviewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) {
            SetStatus("Scrolled " + this.listScrollviewer.VerticalOffset);

            this.currentItemIndex = (int)(this.listScrollviewer.VerticalOffset / this.ActualHeight);

            OptimizeMemory(VerticalBufferRange);
        }

        private void OptimizeMemory(int range = 1) {
            int left = this.currentItemIndex - range / 2, right = this.currentItemIndex + range / 2;

            FreeUnseenImage(left, right);

            // Reload a viewing page
            for (int i = left; i <= right; ++i) {
                if (i > 0 && i < this.Children.Count) {
                    ImageModel model = this.Children.ElementAt(i) as ImageModel;
                    Image target = this.managedImages[i].Content as Image;

                    // Rebind data
                    Binding myBinding = new Binding();
                    target.DataContext = model;
                    myBinding.Path = new PropertyPath("ImageSource");
                    myBinding.Mode = BindingMode.OneWay;
                    myBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                    myBinding.Converter = Resources["UriToBitmapConverter"] as UriToBitmapConverter;
                    target.SetBinding(Image.SourceProperty, myBinding);

                    model.ImageSource.IsUsing = true;
                    model.NotifyPropertyChanged("ImageSource");
                }
            }

            if(SelectionChanged != null) {
                SelectionChanged(this, this.currentItemIndex);
            }
        }

        private void FreeUnseenImage(int left, int right) {
            if (left > 0) {
                ImageModel model = this.Children.ElementAt(left - 1) as ImageModel;
                model.ImageSource.IsUsing = false;
                managedImages[left - 1].DisposeImage();
            }
            if (right < this.Children.Count() - 1) {
                ImageModel model = this.Children.ElementAt(right + 1) as ImageModel;
                model.ImageSource.IsUsing = false;
                managedImages[right + 1].DisposeImage();
            }
        }

        private void GridTouchPad_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e) {
            double horizontalDiff = e.Cumulative.Translation.X;
            double percent = 0.3;
            bool selectionChanged = false;

            // Scroll if the difference is large enough
            if (Math.Abs(horizontalDiff) / this.ActualWidth > percent) {
                if (horizontalDiff < 0) {
                    // Scroll to the right item
                    this.currentItemIndex = Math.Min(this.Children.Count() - 1, this.currentItemIndex + 1);
                }
                else {
                    // Left item
                    this.currentItemIndex = Math.Max(0, this.currentItemIndex - 1);
                }
                selectionChanged = true;
            }
            ScrollToCurrentItem();
            SetStatus("Selected" + this.currentItemIndex);

            if (selectionChanged) {
                OptimizeMemory(HorizontalBufferRange);
            }
        }

        private void GridTouchPad_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e) {
            double horizontalDiff = e.Cumulative.Translation.X;
            double finalPosition = this.currentItemIndex * this.ActualWidth - horizontalDiff;

            this.listScrollviewer.ChangeView(finalPosition, null, null);
            //SetStatus(string.Format("{0} - {1}", horizontalDiff, finalPosition));
        }

        private void GridTouchPad_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e) {
            //SetStatus("Start!");

            if (this.listScrollviewer == null) {
                this.listScrollviewer = Library.GetScrollViewer(this.MainListBox);
            }
            this.firstPoint = e.Position;
        }

        /// <summary>
        /// Update all ListBoxItem size to fit the size of the control
        /// </summary>
        private void ParentGrid_Loaded(object sender, RoutedEventArgs e) {
            FrameworkElement parent = sender as FrameworkElement;
            Image mainImage = Library.FindChild<Image>(parent, "MainImage");

            parent.Width = this.ActualWidth;
            parent.Height = this.ActualHeight;
            mainImage.Width = this.ActualWidth;
            mainImage.Height = this.ActualHeight;
        }

        private void SafeImage_Loaded(object sender, RoutedEventArgs e) {
            SafeImage img = (SafeImage)sender;
            ImageModel data = (ImageModel)img.DataContext;
            this.managedImages.Insert(data.Index, img);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyname = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }
    }

    #region Support Classes

    public sealed class UriToBitmapConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            ExtendedUri uri = (ExtendedUri)value;
            if (!uri.IsUsing) // not in use, ignore
                return null;

            BitmapImage img = new BitmapImage(uri);
            return img;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }

    public sealed class Library {
        public static T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null) {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName)) {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName) {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                }
                else {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }

        public static ScrollViewer GetScrollViewer(DependencyObject depObj) {
            if (depObj is ScrollViewer) return depObj as ScrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
    }

    public class SafeImage : ContentControl {
        /// <summary>
        /// Release memory used by the child Image
        /// </summary>
        public void DisposeImage() {
            Image image = this.Content as Image;

            if (image != null) {
                BitmapImage bitmapImage = image.Source as BitmapImage;

                if (bitmapImage != null) {
                    bitmapImage.UriSource = null;
                    image.Source = null;

                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    //image.Source = null;
                }
            }
        }
    }

    public class ExtendedUri : Uri {
        public bool IsUsing;

        public ExtendedUri(string uriString) : base(uriString) {
            IsUsing = false;
        }
    }

    public class ImageModel : INotifyPropertyChanged {
        private ExtendedUri _imageSource;

        public ExtendedUri ImageSource {
            get {
                return _imageSource;
            }

            set {
                _imageSource = value;
                NotifyPropertyChanged();
            }
        }
        public int Index { get; set; }

        public ImageModel() { }

        public ImageModel(int id, ExtendedUri uri) {
            Index = id;
            ImageSource = uri;
        }

        public override bool Equals(object obj) {
            return ImageSource.OriginalString.Equals(((ImageModel)obj).ImageSource.OriginalString);
        }

        public override int GetHashCode() {
            return ImageSource.GetHashCode() ^ Index;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyname = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }
    }

    #endregion
}
