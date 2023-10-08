using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using NAudio.Midi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SpaceOudacity
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const int maxHue = 359;
        private readonly MidiOut midiOutPort;
        private readonly double minHueWavelength = HueToWavelength(0);
        private readonly double minMaxHueWaveLengthDelta = HueToWavelength(maxHue) - HueToWavelength(0);
        private readonly WaveOutEvent waveOut = new WaveOutEvent();
        private MixingSampleProvider mixer;

        private CanvasBitmap canvasBitmap;


        public MainWindow()
        {
            InitializeComponent();
            //UpdateColorPosition(colorPicker.Color);

            colorPicker.Color = Colors.Red;

            midiOutPort = new(0);
        }

        private async void canvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            if (canvasBitmap != null)
            {
                var scaleWidth = (float)sender.ActualWidth / canvasBitmap.SizeInPixels.Width;
                args.DrawingSession.Transform = Matrix3x2.CreateScale(scaleWidth);
                args.DrawingSession.DrawImage(canvasBitmap);
                args.DrawingSession.Transform = Matrix3x2.CreateScale(1f);
            }
        }

        //private void colorPicker_ColorChanged(Microsoft.UI.Xaml.Controls.ColorPicker sender, Microsoft.UI.Xaml.Controls.ColorChangedEventArgs args)
        //{
        //    DispatcherQueue.TryEnqueue(() => UpdateColorPosition(args.NewColor));
        //}


        private NoteOnEvent ColorToNote(Color color)
        {
            var (h, s, v) = RGBToHSV(color);
            var wave = HueToWavelength(h); ;
            var note = LightWaveToMidiNote(wave);
            return new NoteOnEvent(0, 1, note, (int)((s + v) / 2 * 127), 1);
        }

        private ISampleProvider ColorToSignal(Color color)
        {
            var (h, s, v) = RGBToHSV(color);
            var wave = HueToWavelength(h); ;
            return LightWaveToSignal(wave);
        }

        private ISampleProvider LightWaveToSignal(double wavelength)
        {
            var minimized = wavelength - minHueWavelength;

            var waveRatio = minimized / minMaxHueWaveLengthDelta;

            const double pianoMinFrequency = 27.5d;
            const double pianoMaxFrequency = 4186.009d;
            const double pianoDelta = pianoMaxFrequency - pianoMinFrequency;

            var frequency = (waveRatio * pianoDelta) - pianoMinFrequency;

            return new SignalGenerator()
            {
                Gain = 0.2,
                Frequency = frequency,
                Type = SignalGeneratorType.Sin,
            }.Take(TimeSpan.FromSeconds(5));
        }

        private byte LightWaveToMidiNote(double wavelength)
        {
            var minimized = wavelength - minHueWavelength;

            var waveRatio = minimized / minMaxHueWaveLengthDelta;

            const int piano88Key = 108;
            const int piano1Key = 21;
            const int pianoDelta = piano88Key - piano1Key;

            return (byte)(((double)pianoDelta * waveRatio) + piano1Key);
        }

        private static double HueToWavelength(double hue) => 650d - (250d / maxHue * hue);

        private void UpdateColorPosition(Color newColor, Point point, Point maxPosition)
        {
            colorPicker.Color = newColor;

            var signal = ColorToSignal(newColor);

            var scaleWidth = point.X / maxPosition.X;
            var stereo = new PanningSampleProvider(signal.ToMono())
            {
                Pan = (2 * (float)scaleWidth) - 1
            };

            mixer ??= new MixingSampleProvider(stereo.WaveFormat);

            mixer.AddMixerInput(stereo);

            if (waveOut.PlaybackState != PlaybackState.Playing)
            {
                waveOut.Init(mixer);
                waveOut.Play();
            }

            //if (midiOutPort != null)
            //{
            //    var midiNote = ColorToNote(newColor);
            //    var scaleWidth = point.X / maxPosition.X;
            //    var pan = scaleWidth * 127;

            //    var control = new ControlChangeEvent(0, 1, MidiController.Pan, (int)pan);

            //    midiOutPort.Send(control.GetAsShortMessage());
            //    midiOutPort.Send(midiNote.GetAsShortMessage());
            //}
            canvas.Invalidate();
        }
        public static (double, double, double) RGBToHSV(Color rgb)
        {
            double delta, min;
            double h = 0, s, v;

            min = Math.Min(Math.Min(rgb.R, rgb.G), rgb.B);
            v = Math.Max(Math.Max(rgb.R, rgb.G), rgb.B);
            delta = v - min;

            if (v == 0.0)
                s = 0;
            else
                s = delta / v;

            if (s == 0)
                h = 0.0;

            else
            {
                if (rgb.R == v)
                    h = (rgb.G - rgb.B) / delta;
                else if (rgb.G == v)
                    h = 2 + ((rgb.B - rgb.R) / delta);
                else if (rgb.B == v)
                    h = 4 + ((rgb.R - rgb.G) / delta);

                h *= 60;

                if (h < 0.0)
                    h = h + maxHue;
            }

            return (h, s, v / 255);
        }

        private async void canvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            var path = @"E:\SpaceApps\Nebula\Orion00108552.tif";

            // using IRandomAccessStream fileStream = await ImageFile.OpenReadAsync();

            // var decoder = await BitmapDecoder.CreateAsync(fileStream);

            // var pixelData = await decoder.GetPixelDataAsync(
            //    BitmapPixelFormat.Bgra8, // WriteableBitmap uses BGRA format 
            //    BitmapAlphaMode.Straight,
            //    new BitmapTransform(),
            //    ExifOrientationMode.IgnoreExifOrientation, // This sample ignores Exif orientation 
            //    ColorManagementMode.DoNotColorManage
            //);

            var img = await StorageFile.GetFileFromPathAsync(path);
            using IRandomAccessStream stream = await img.OpenReadAsync();
            canvasBitmap = await CanvasBitmap.LoadAsync(canvas.Device, stream);
            DispatcherQueue.TryEnqueue(() => canvas.Invalidate());

        }

        private void canvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var canvasSender = sender as CanvasControl;

            var point = e.GetCurrentPoint(sender as UIElement);

            var scaleWidth = canvasBitmap.Bounds.Width / canvasSender.ActualWidth;

            var pixelColors = canvasBitmap.GetPixelColors((int)(point.Position.X * scaleWidth), (int)(point.Position.Y * scaleWidth), 1, 1);

            var pixelColor = pixelColors[0];

            DispatcherQueue.TryEnqueue(() => UpdateColorPosition(pixelColors[0], point.Position, new Point(canvasSender.ActualWidth, canvasSender.ActualHeight)));
        }
    }
}
