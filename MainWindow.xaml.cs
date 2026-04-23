using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace IconvApp
{
    public class IconResolutionItem : INotifyPropertyChanged
    {
        private bool _isIncluded = true;
        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; OnPropertyChanged(nameof(IsIncluded)); }
        }

        public int Size { get; set; }
        public string SizeText => $"{Size}x{Size}";

        private BitmapSource? _image;
        public BitmapSource? Image
        {
            get => _image;
            set { _image = value; OnPropertyChanged(nameof(Image)); }
        }

        private bool _isCustom;
        public bool IsCustomImage
        {
            get => _isCustom;
            set
            {
                _isCustom = value;
                OnPropertyChanged(nameof(IsCustomImage));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText => IsCustomImage ? "Custom Image" : "Auto Generated";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private BitmapSource? _currentImage;
        public ObservableCollection<IconResolutionItem> ResolutionItems { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeResolutions();
            ResolutionItemsControl.ItemsSource = ResolutionItems;
        }

        private void InitializeResolutions()
        {
            foreach (var res in IconBuilder.DefaultResolutions)
            {
                ResolutionItems.Add(new IconResolutionItem { Size = res });
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    LoadImage(files[0]);
                }
            }
        }

        private void LoadImage(string path)
        {
            try
            {
                var image = LoadBitmapSource(path);
                if (image != null)
                {
                    _currentImage = image;
                    SourcePreview.Source = _currentImage;
                    SourcePreview.Visibility = Visibility.Visible;
                    DropText.Visibility = Visibility.Collapsed;
                    ExportButton.IsEnabled = true;
                    StatusLabel.Text = $"Loaded: {Path.GetFileName(path)} ({_currentImage.PixelWidth}x{_currentImage.PixelHeight})";

                    foreach (var item in ResolutionItems)
                    {
                        if (!item.IsCustomImage)
                        {
                            item.Image = IconBuilder.PrepareImage(_currentImage, item.Size);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private BitmapSource? LoadBitmapSource(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".ico")
            {
                var decoder = new IconBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
            }
            else
            {
                return new BitmapImage(new Uri(path));
            }
        }

        private void ResolutionItem_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void ResolutionItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is IconResolutionItem item)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        try
                        {
                            var customImage = LoadBitmapSource(files[0]);
                            if (customImage != null)
                            {
                                item.Image = IconBuilder.PrepareImage(customImage, item.Size);
                                item.IsCustomImage = true;
                                item.IsIncluded = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error loading custom image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                e.Handled = true;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFrames = ResolutionItems
                .Where(item => item.IsIncluded && item.Image != null)
                .Select(item => (item.Size, Image: item.Image!))
                .ToList();

            if (!selectedFrames.Any())
            {
                MessageBox.Show("Please select at least one resolution and ensure images are loaded.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Icon files (*.ico)|*.ico",
                FileName = "app.ico"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    IconBuilder.BuildIcon(selectedFrames, sfd.FileName);
                    StatusLabel.Text = "Successfully exported icon!";
                    MessageBox.Show("Icon exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting icon: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}