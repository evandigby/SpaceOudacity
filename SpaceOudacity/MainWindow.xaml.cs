using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using System;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
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
        private Color currentColor;
        private readonly DeviceWatcher deviceWatcher;
        private readonly string deviceFilter = MidiOutPort.GetDeviceSelector();
        private IMidiOutPort midiOutPort;
        private readonly double minHueWavelength = HueToWavelength(0);
        private readonly double minMaxHueWaveLengthDelta = HueToWavelength(maxHue) - HueToWavelength(0);

        public MainWindow()
        {
            InitializeComponent();
            UpdateColor(colorPicker.Color);

            deviceWatcher = DeviceInformation.CreateWatcher(deviceFilter);
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Start();
            colorPicker.Color = Colors.Red;
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {

        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            var devices = await DeviceInformation.FindAllAsync(deviceFilter);
            if (devices.Count == 0)
            {
                return;
            }

            var currentOutPort = await MidiOutPort.FromIdAsync(devices[0].Id);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (midiOutPort != null)
                {
                    midiOutPort.Dispose();
                }
                midiOutPort = currentOutPort;
            });
        }

        private void canvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            const float rectWidth = 200;
            const float rectHeight = 200;

            var brush = new CanvasSolidColorBrush(sender.Device, currentColor);
            args.DrawingSession.FillRectangle((float)(sender.ActualWidth / 2) - (rectWidth / 2), (float)(sender.ActualHeight / 2) - (rectHeight / 2), rectWidth, rectHeight, brush);
        }

        private void colorPicker_ColorChanged(Microsoft.UI.Xaml.Controls.ColorPicker sender, Microsoft.UI.Xaml.Controls.ColorChangedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() => UpdateColor(args.NewColor));
        }


        private MidiNoteOnMessage ColorToMidiNote(Color color)
        {
            var (h, _, v) = RGBToHSV(color);
            var wave = HueToWavelength(h); ;
            var note = LightWaveToMidiNote(wave);
            return new MidiNoteOnMessage(10, note, (byte)(v * 127));
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

        private void UpdateColor(Color newColor)
        {
            currentColor = newColor;

            var (h, _, _) = RGBToHSV(currentColor);

            var midiNote = ColorToMidiNote(newColor);
            canvas.Invalidate();
            if (midiOutPort != null)
            {
                midiOutPort.SendMessage(midiNote);
            }
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
    }
}
