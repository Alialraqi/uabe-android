using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace UABEAvalonia.ViewModels.Tools
{
    /// <summary>
    /// Image preview sub ViewModel. Responsible for zoom, background switching, size / and format info display.
    /// Adapted from ImagePreviewViewModel of UABEANext4, held by PreviewerToolViewModel.
    /// </summary>
    public partial class ImagePreviewViewModel : ViewModelBase
    {
        /// <summary>The bitmap currently previewed (obtained by the host converting BGRA pixels).</summary>
        [ObservableProperty]
        private Bitmap? _image;

 /// <summary>Size info, for example "256 x 256 px". </summary>
        [ObservableProperty]
        private string _imageInfo = "No image";

        /// <summary>Zoom level, 1.0 is the original size.</summary>
        [ObservableProperty]
        private double _zoomLevel = 1.0;

        /// <summary>Whether to fit the available area (reserved for computing when the view loads the image later).</summary>
        [ObservableProperty]
        private bool _fitToSize = true;

        /// <summary>Texture format description.</summary>
        [ObservableProperty]
        private string _textureFormat = string.Empty;

        /// <summary>Current background index (0-4).</summary>
        [ObservableProperty]
        private int _backgroundIndex = 0;

        /// <summary>Current background brush.</summary>
        [ObservableProperty]
        private IBrush _previewBackground = Brushes.Transparent;

        // 5 kinds of backgrounds: transparent / checkerboard / black / white / gray
        private readonly IBrush[] _backgrounds =
        {
            Brushes.Transparent,
            CreateCheckerboardBrush(),
            Brushes.Black,
            Brushes.White,
            Brushes.Gray
        };

        // Background label, used for the button to show the current background type
        private readonly string[] _bgLabels = { "A", "C", "B", "W", "G" };

        /// <summary>Current background label (transparent/checkerboard/black/white/gray).</summary>
        public string CurrentBgLabel => _bgIndex < _bgLabels.Length ? _bgLabels[_bgIndex] : "?";

        // Internal index used for switching (synced with BackgroundIndex)
        private int _bgIndex = 0;

        // Y-axis flip flag when rendering, used with the view's ScaleTransform
        public double RenderScaleY => -1.0;

        /// <summary>Actual display width = pixel width * zoom.</summary>
        public double DisplayWidth => Image != null ? Image.PixelSize.Width * ZoomLevel : 0;

        /// <summary>Actual display height = pixel height * zoom.</summary>
        public double DisplayHeight => Image != null ? Image.PixelSize.Height * ZoomLevel : 0;

        public ImagePreviewViewModel()
        {
            // Sync the initial background
            _previewBackground = _backgrounds[0];
        }

        /// <summary>
        /// Update the preview image and additional info.
        /// </summary>
        /// <param name="bitmap">New bitmap (may be null meaning no image).</param>
        /// <param name="textureFormat">Texture format description, may be empty.</param>
        public void UpdateImage(Bitmap? bitmap, string? textureFormat = null)
        {
            if (Image is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Image = bitmap;
            if (bitmap != null)
            {
                ImageInfo = $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height} px";
                TextureFormat = string.IsNullOrEmpty(textureFormat) ? "Unknown Format" : $"Format: {textureFormat}";
            }
            else
            {
                ImageInfo = "No image";
                TextureFormat = string.Empty;
                ZoomLevel = 1.0;
                OnPropertyChanged(nameof(DisplayWidth));
                OnPropertyChanged(nameof(DisplayHeight));
            }
        }

        /// <summary>Scale to fit the available area (not exceeding the original size).</summary>
        public void FitToAvailableSize(double availableWidth, double availableHeight)
        {
            if (Image == null || availableWidth <= 0 || availableHeight <= 0)
            {
                return;
            }

            double ratioX = availableWidth / Image.PixelSize.Width;
            double ratioY = availableHeight / Image.PixelSize.Height;

            ZoomLevel = Math.Min(ratioX, ratioY) * 0.95;
            if (ZoomLevel > 1.0)
            {
                ZoomLevel = 1.0;
            }
        }

        /// <summary>Cycle through the 5 backgrounds (transparent/checkerboard/black/white/gray).</summary>
        [RelayCommand]
        private void CycleBackground()
        {
            _bgIndex = (_bgIndex + 1) % _backgrounds.Length;
            BackgroundIndex = _bgIndex;
            PreviewBackground = _backgrounds[_bgIndex];
            OnPropertyChanged(nameof(CurrentBgLabel));
        }

        /// <summary>Zoom in / Zoom out (increase=true zoom in).</summary>
        public void AdjustZoom(bool increase)
        {
            double step = 1.1;
            if (increase)
            {
                ZoomLevel *= step;
            }
            else
            {
                ZoomLevel /= step;
            }

            ZoomLevel = Math.Clamp(ZoomLevel, 0.05, 20.0);
        }

        // Refresh derived size properties when zoom changes
        partial void OnZoomLevelChanged(double value)
        {
            OnPropertyChanged(nameof(DisplayWidth));
            OnPropertyChanged(nameof(DisplayHeight));
        }

        // Build the checkerboard brush
        private static IBrush CreateCheckerboardBrush()
        {
            return new DrawingBrush
            {
                TileMode = TileMode.Tile,
                SourceRect = new RelativeRect(0, 0, 20, 20, RelativeUnit.Absolute),
                DestinationRect = new RelativeRect(0, 0, 20, 20, RelativeUnit.Absolute),
                Drawing = new GeometryDrawing
                {
                    Brush = Brushes.LightGray,
                    Geometry = Geometry.Parse("M0,0 H10 V10 H0 Z M10,10 H20 V20 H10 Z")
                }
            };
        }
    }
}
