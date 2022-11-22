using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace TextureRescaler {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public static readonly DependencyProperty BlockSizeProperty
			= DependencyProperty.Register(
				nameof(BlockSize), typeof(int), typeof(MainWindow),
				new PropertyMetadata(4, OnBlockSizeChanged), ValidateBlockSize
			);
		public int BlockSize {
			get => (int)GetValue(BlockSizeProperty);
			set => SetValue(BlockSizeProperty, value);
		}
		static bool ValidateBlockSize(object value) {
			return value is int val && val > 0;
		}
		static void OnBlockSizeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
			(obj as MainWindow)?.ResizeImages();
		}

		public static readonly DependencyProperty PreviewBackgroundProperty
			= DependencyProperty.Register(
				nameof(PreviewBackground), typeof(Brush), typeof(MainWindow),
				new PropertyMetadata(Brushes.White, OnPreviewBackgroundChanged)
			);
		public Brush PreviewBackground {
			get => (Brush)GetValue(PreviewBackgroundProperty);
			set => SetValue(PreviewBackgroundProperty, value);
		}
		static void OnPreviewBackgroundChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
			if (obj is MainWindow window) {
				window.Border0.Background = args.NewValue as Brush;
				window.Border1.Background = args.NewValue as Brush;
				window.RenderImages();
			}
		}

		public static readonly DependencyProperty PreviewHintProperty
			= DependencyProperty.Register(
				nameof(PreviewHint), typeof(Brush), typeof(MainWindow),
				new PropertyMetadata(new SolidColorBrush(new Color { A = 0x90, R = 0x90, G = 0xff, B = 0x90 }), OnPreviewHintChanged)
			);
		public Brush PreviewHint {
			get => (Brush)GetValue(PreviewHintProperty);
			set => SetValue(PreviewHintProperty, value);
		}
		static void OnPreviewHintChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
			(obj as MainWindow)?.RenderImages();
		}

		public static readonly DependencyProperty HorizontalMethodProperty
			= DependencyProperty.Register(
				nameof(HorizontalMethod), typeof(TextureRescaleMethod), typeof(MainWindow),
				new PropertyMetadata(TextureRescaleMethod.AddTransparentEdge, OnHorizontalMethodChanged)
			);
		public TextureRescaleMethod HorizontalMethod {
			get => (TextureRescaleMethod)GetValue(HorizontalMethodProperty);
			set => SetValue(HorizontalMethodProperty, value);
		}
		static void OnHorizontalMethodChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
			(obj as MainWindow)?.RenderImages();
		}

		public static readonly DependencyProperty VerticalMethodProperty
			= DependencyProperty.Register(
				nameof(VerticalMethod), typeof(TextureRescaleMethod), typeof(MainWindow),
				new PropertyMetadata(TextureRescaleMethod.AddTransparentEdge, OnVerticalMethodChanged)
			);
		public TextureRescaleMethod VerticalMethod {
			get => (TextureRescaleMethod)GetValue(VerticalMethodProperty);
			set => SetValue(VerticalMethodProperty, value);
		}
		static void OnVerticalMethodChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
			(obj as MainWindow)?.RenderImages();
		}

		public MainWindow() {
			InitializeComponent();
			dv = new DrawingVisual();
			dpi = VisualTreeHelper.GetDpi(Image1);
		}

		DirectoryInfo? srcdir;
		DirectoryInfo? destdir;
		readonly List<(string, FileInfo)> files = new();
		int m_index = -1;
		int Index {
			get {
				return m_index;
			}
			set {
				m_index = value;
				if (Index >= 0) LoadImage();
			}
		}

		BitmapImage? srcimg;
		DpiScale dpi;
		readonly DrawingVisual dv;
		RenderTargetBitmap? rtb;
		void LoadImage() {
			Image0.Source = srcimg = new BitmapImage(new Uri(files[Index].Item2.FullName));
			ButtonOpenFolder.Content = string.Format("{0}/{1} {2}", Index + 1, files.Count, files[Index].Item1).Replace("_", "__");
			ProgressBar.Value = (double)Index / (files.Count - 1);
			ResizeImages();
		}

		void ResizeImages() {
			if (srcimg is null) return;

			var ax = Align(srcimg.PixelWidth);
			var ay = Align(srcimg.PixelHeight);
			if (ax == srcimg.PixelWidth && ay == srcimg.PixelHeight) {
				KeepImpl();
				Next(true);
				return;
			}

			rtb = new RenderTargetBitmap(
				ax, ay,
				dpi.PixelsPerInchX, dpi.PixelsPerInchY,
				PixelFormats.Pbgra32
			);
			Image1.Source = rtb;

			RenderImages();
		}

		public int Align(int pixel) => ((pixel - 1) / BlockSize + 1) * BlockSize;

		double ax = 0.5, ay = 0.5;
		void RenderImages(bool output = false) {
			if (srcimg is null || rtb is null) return;
			int ow = srcimg.PixelWidth, oh = srcimg.PixelHeight; // Original size
			int tw = rtb.PixelWidth, th = rtb.PixelHeight; // Target size
			var trect = new Rect(0, 0, tw, th);
			int pax = (int)Math.Round(ow * ax), pay = (int)Math.Round(oh * ay); // Pixel anchor
			int dw = (int)Math.Round((tw - ow) * ax), dh = (int)Math.Round((th - oh) * ay); // Content origin
			rtb.Clear();
			using (var ctx = dv.RenderOpen()) {
				// Draw global hint
				if (!output) {
					ctx.DrawRectangle(Brushes.White, null, trect);
					ctx.DrawRectangle(PreviewBackground, null, trect);
					ctx.DrawRectangle(PreviewHint, null, trect);
				}

				// Calculate clip positions
				int x0 = HorizontalMethod == TextureRescaleMethod.AddTransparentEdge ? dw : 0,
					y0 = VerticalMethod == TextureRescaleMethod.AddTransparentEdge ? dh : 0,
					w0 = pax,
					h0 = pay;
				int x1 = HorizontalMethod == TextureRescaleMethod.AddTransparentEdge ? dw + pax : tw - ow + pax,
					y1 = VerticalMethod == TextureRescaleMethod.AddTransparentEdge ? dh + pay : th - oh + pay,
					w1 = ow - pax,
					h1 = oh - pay;

				// Draw top left
				ctx.PushClip(new RectangleGeometry(new Rect(x0, y0, w0, h0)));
				if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x0, y0, ow, oh));
				ctx.DrawImage(srcimg, new Rect(x0, y0, ow, oh));
				ctx.Pop();

				// Draw top right
				ctx.PushClip(new RectangleGeometry(new Rect(x1, y0, w1, h0)));
				if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x1, y0, w1, oh));
				ctx.DrawImage(srcimg, new Rect(x1 - pax, y0, ow, oh));
				ctx.Pop();

				// Draw bottom left
				ctx.PushClip(new RectangleGeometry(new Rect(x0, y1, w0, h1)));
				if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x0, y1, w0, h1));
				ctx.DrawImage(srcimg, new Rect(x0, y1 - pay, ow, oh));
				ctx.Pop();

				// Draw bottom right
				ctx.PushClip(new RectangleGeometry(new Rect(x1, y1, w1, h1)));
				if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x1, y1, w1, h1));
				ctx.DrawImage(srcimg, new Rect(x1 - pax, y1 - pay, ow, oh));
				ctx.Pop();

				// Fill columns
				if (HorizontalMethod == TextureRescaleMethod.StretchBody) {
					for (int x = pax; x < x1; x++) {
						ctx.PushClip(new RectangleGeometry(new Rect(x, y0, 1, h0)));
						if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x, y0, 1, h0));
						ctx.DrawImage(srcimg, new Rect(x - pax, y0, ow, oh));
						ctx.Pop();

						ctx.PushClip(new RectangleGeometry(new Rect(x, y1, 1, oh - h0)));
						if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x, y1, 1, oh - h0));
						ctx.DrawImage(srcimg, new Rect(x - pax, y1 - pay, ow, oh));
						ctx.Pop();
					}
				}

				// Fill rows
				if (VerticalMethod == TextureRescaleMethod.StretchBody) {
					for (int y = pay; y < y1; y++) {
						ctx.PushClip(new RectangleGeometry(new Rect(x0, y, w0, 1)));
						if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x0, y, w0, 1));
						ctx.DrawImage(srcimg, new Rect(x0, y - pay, ow, oh));
						ctx.Pop();

						ctx.PushClip(new RectangleGeometry(new Rect(x1, y, ow - w0, 1)));
						if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x1, y, ow - w0, 1));
						ctx.DrawImage(srcimg, new Rect(x1 - pax, y - pay, ow, oh));
						ctx.Pop();
					}
				}

				// Fill center
				if (HorizontalMethod == TextureRescaleMethod.StretchBody &&
					VerticalMethod == TextureRescaleMethod.StretchBody) {
					for (int x = pax; x < x1; x++) {
						for (int y = pay; y < y1; y++) {
							ctx.PushClip(new RectangleGeometry(new Rect(x, y, 1, 1)));
							if (!output) ctx.DrawRectangle(PreviewBackground, null, new Rect(x, y, 1, 1));
							ctx.DrawImage(srcimg, new Rect(x - pax, y - pay, ow, oh));
							ctx.Pop();
						}
					}
				}
			}
			rtb.Render(dv);
		}

		private void Keep(object sender, RoutedEventArgs e) {
			KeepImpl();
			Next();
		}
		void KeepImpl() {
			if (Index < 0 || Index >= files.Count) return;
			var src = files[Index];
			var dest = GetDestination();
			if (dest is null) return;
			src.Item2.CopyTo(dest.FullName, true);
		}
		private void Process(object sender, RoutedEventArgs e) {
			var dest = GetDestination();
			if (dest is null) return;
			RenderImages(true);
			Image1.Arrange(new Rect(Image1.RenderSize));
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(rtb));
			using var stream = dest.OpenWrite();
			encoder.Save(stream);
			Next();
		}
		FileInfo? GetDestination() {
			if (destdir is null) return null;
			var src = files[Index];
			var dest = new FileInfo(Path.Combine(destdir.FullName, src.Item1));
			var dir = dest.Directory;
			if (dir is null) {
				MessageBox.Show("Unknown error: Cannot get directory", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
			if (!dir.Exists) dir.Create();
			return dest;
		}

		void Next(bool discardCurrent = false) {
			if (discardCurrent) {
				files.RemoveAt(Index);
				if (Index >= files.Count - 1) Index--;
				else LoadImage();
			}
			else if (Index < files.Count - 1) Index++;
		}

		private void Back(object sender, RoutedEventArgs e) {
			if (Index > 0) Index--;
		}

		private void OpenFolder(object sender, RoutedEventArgs e) {
			using var dialog = new FolderBrowserDialog();
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				srcdir = new DirectoryInfo(dialog.SelectedPath);
				if (srcdir.Parent is null) {
					MessageBox.Show("Cannot select a root path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				destdir = new DirectoryInfo(Path.Combine(srcdir.Parent.FullName, srcdir.Name + ".normalized"));
				files.Clear();
				Scan(srcdir);
				Index = -1;
				Next();
			}
		}

		void Scan(DirectoryInfo dir, string reldirname = "") {
			foreach (var file in dir.EnumerateFiles("*.png")) {
				files.Add((reldirname + file.Name, file));
			}
			foreach (var sdir in dir.GetDirectories()) {
				Scan(sdir, reldirname + sdir.Name + "/");
			}
		}

		private void SetAnchorOriginal(object sender, MouseButtonEventArgs e) {
			if (srcimg is null) return;
			SetAnchor(Image0.RenderSize, e.GetPosition(Image0));
		}
		private void SetAnchorProcessed(object sender, MouseButtonEventArgs e) {
			if (rtb is null) return;
			SetAnchor(Image1.RenderSize, e.GetPosition(Image1));
		}
		private void SetAnchor(Size cs, Point mpos) {
			ax = Math.Clamp(mpos.X / cs.Width, 0, 1);
			ay = Math.Clamp(mpos.Y / cs.Height, 0, 1);
			RenderImages();
		}
	}
}
