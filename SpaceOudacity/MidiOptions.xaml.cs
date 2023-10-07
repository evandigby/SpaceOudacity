using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SpaceOudacity
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MidiOptions : Page
    {
        /// <summary>
        /// Device watchers for MIDI in and out ports
        /// </summary>
        private readonly MidiDeviceWatcher midiInDeviceWatcher;
        private readonly MidiDeviceWatcher midiOutDeviceWatcher;

        /// <summary>
        /// Main Page 
        /// <summary>
        /// Constructor: Empty device lists, start the device watchers and
        /// set initial states for buttons
        /// </summary>
        public MidiOptions()
        {
            InitializeComponent();

            // Start with a clean slate
            ClearAllDeviceValues();

            // Ensure Auto-detect devices toggle is on
            deviceAutoDetectToggle.IsOn = true;

            // Set up the MIDI input and output device watchers
            midiInDeviceWatcher = new MidiDeviceWatcher(MidiInPort.GetDeviceSelector(), DispatcherQueue, inputDevices);
            midiOutDeviceWatcher = new MidiDeviceWatcher(MidiOutPort.GetDeviceSelector(), DispatcherQueue, outputDevices);

            // Start watching for devices
            midiInDeviceWatcher.Start();
            midiOutDeviceWatcher.Start();

            // Disable manual enumeration buttons
            listInputDevicesButton.IsEnabled = false;
            listOutputDevicesButton.IsEnabled = false;
        }

        /// <summary>
        /// Clear all input and output MIDI device lists and properties
        /// </summary>
        private void ClearAllDeviceValues()
        {
            // Clear input devices
            inputDevices.Items.Clear();
            inputDevices.Items.Add("Click button to list input MIDI devices");
            inputDevices.IsEnabled = false;

            // Clear output devices
            outputDevices.Items.Clear();
            outputDevices.Items.Add("Click button to list output MIDI devices");
            outputDevices.IsEnabled = false;

            // Clear input device properties
            inputDeviceProperties.Items.Clear();
            inputDeviceProperties.Items.Add("Select a MIDI input device to view its properties");
            inputDeviceProperties.IsEnabled = false;

            // Clear output device properties
            outputDeviceProperties.Items.Clear();
            outputDeviceProperties.Items.Add("Select a MIDI output device to view its properties");
            outputDeviceProperties.IsEnabled = false;
        }

        /// <summary>
        /// Input button click handler
        /// </summary>
        /// <param name="sender">Element that fired the event</param>
        /// <param name="e">Event arguments</param>
        private async void listInputDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            // Enumerate input devices
            await EnumerateMidiInputDevices();
        }

        /// <summary>
        /// Query DeviceInformation class for Midi Input devices
        /// </summary>
        private async Task EnumerateMidiInputDevices()
        {
            // Clear input devices
            inputDevices.Items.Clear();
            inputDeviceProperties.Items.Clear();
            inputDeviceProperties.IsEnabled = false;

            // Find all input MIDI devices
            string midiInputQueryString = MidiInPort.GetDeviceSelector();
            DeviceInformationCollection midiInputDevices = await DeviceInformation.FindAllAsync(midiInputQueryString);

            // Return if no external devices are connected
            if (midiInputDevices.Count == 0)
            {
                inputDevices.Items.Add("No MIDI input devices found!");
                inputDevices.IsEnabled = false;

                return;
            }

            // Else, add each connected input device to the list
            foreach (DeviceInformation deviceInfo in midiInputDevices)
            {
                inputDevices.Items.Add(deviceInfo.Name);
                inputDevices.IsEnabled = true;
            }
        }

        /// <summary>
        /// Output button click handler
        /// </summary>
        /// <param name="sender">Element that fired the event</param>
        /// <param name="e">Event arguments</param>
        private async void listOutputDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            // Enumerate output devices
            await EnumerateMidiOutputDevices();
        }

        /// <summary>
        /// Query DeviceInformation class for Midi Output devices
        /// </summary>
        private async Task EnumerateMidiOutputDevices()
        {
            // Clear output devices
            outputDevices.Items.Clear();
            outputDeviceProperties.Items.Clear();
            outputDeviceProperties.IsEnabled = false;

            // Find all output MIDI devices
            string midiOutputQueryString = MidiOutPort.GetDeviceSelector();
            DeviceInformationCollection midiOutputDevices = await DeviceInformation.FindAllAsync(midiOutputQueryString);

            // Return if no external devices are connected, and GS synth is not detected
            if (midiOutputDevices.Count == 0)
            {
                outputDevices.Items.Add("No MIDI output devices found!");
                outputDevices.IsEnabled = false;

                return;
            }

            // List specific device information for each output device
            foreach (DeviceInformation deviceInfo in midiOutputDevices)
            {
                outputDevices.Items.Add(deviceInfo.Name);
                outputDevices.IsEnabled = true;
            }

        }

        /// <summary>
        /// Detect the toggle state of the Devicewatcher button.
        /// If auto-detect is on, disable manual enumeration buttons and start device watchers
        /// If auto-detect is off, enable manual enumeration buttons and stop device watchers
        /// </summary>
        /// <param name="sender">Element that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void DeviceAutoDetectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (deviceAutoDetectToggle.IsOn)
            {
                listInputDevicesButton.IsEnabled = false;
                listOutputDevicesButton.IsEnabled = false;

                if (midiInDeviceWatcher != null)
                {
                    midiInDeviceWatcher.Start();
                }
                if (midiOutDeviceWatcher != null)
                {
                    midiOutDeviceWatcher.Start();
                }
            }
            else
            {
                listInputDevicesButton.IsEnabled = true;
                listOutputDevicesButton.IsEnabled = true;

                if (midiInDeviceWatcher != null)
                {
                    midiInDeviceWatcher.Stop();
                }
                if (midiOutDeviceWatcher != null)
                {
                    midiOutDeviceWatcher.Stop();
                }
            }
        }

        /// <summary>
        /// Change the active input MIDI device
        /// </summary>
        /// <param name="sender">Element that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void inputDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the selected input MIDI device
            int selectedInputDeviceIndex = inputDevices.SelectedIndex;

            // Try to display the appropriate device properties
            if (selectedInputDeviceIndex < 0)
            {
                // Clear input device properties
                inputDeviceProperties.Items.Clear();
                inputDeviceProperties.Items.Add("Select a MIDI input device to view its properties");
                inputDeviceProperties.IsEnabled = false;
                return;
            }

            DeviceInformationCollection devInfoCollection = midiInDeviceWatcher.GetDeviceInformationCollection();
            if (devInfoCollection == null)
            {
                inputDeviceProperties.Items.Clear();
                inputDeviceProperties.Items.Add("Device not found!");
                inputDeviceProperties.IsEnabled = false;
                return;
            }

            DeviceInformation devInfo = devInfoCollection[selectedInputDeviceIndex];
            if (devInfo == null)
            {
                inputDeviceProperties.Items.Clear();
                inputDeviceProperties.Items.Add("Device not found!");
                inputDeviceProperties.IsEnabled = false;
                return;
            }

            // Display the found properties
            DisplayDeviceProperties(devInfo, inputDeviceProperties);
        }

        /// <summary>
        /// Change the active output MIDI device
        /// </summary>
        /// <param name="sender">Element that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void outputDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the selected output MIDI device
            int selectedOutputDeviceIndex = outputDevices.SelectedIndex;

            // Try to display the appropriate device properties
            if (selectedOutputDeviceIndex < 0)
            {
                // Clear output device properties
                outputDeviceProperties.Items.Clear();
                outputDeviceProperties.Items.Add("Select a MIDI output device to view its properties");
                outputDeviceProperties.IsEnabled = false;
                return;
            }

            DeviceInformationCollection devInfoCollection = midiOutDeviceWatcher.GetDeviceInformationCollection();
            if (devInfoCollection == null)
            {
                outputDeviceProperties.Items.Clear();
                outputDeviceProperties.Items.Add("Device not found!");
                outputDeviceProperties.IsEnabled = false;
                return;
            }

            DeviceInformation devInfo = devInfoCollection[selectedOutputDeviceIndex];
            if (devInfo == null)
            {
                outputDeviceProperties.Items.Clear();
                outputDeviceProperties.Items.Add("Device not found!");
                outputDeviceProperties.IsEnabled = false;
                return;
            }

            // Display the found properties
            DisplayDeviceProperties(devInfo, outputDeviceProperties);
        }

        /// <summary>
        /// Display the properties of the MIDI device to the user
        /// </summary>
        /// <param name="devInfo"></param>
        /// <param name="propertiesList"></param>
        private void DisplayDeviceProperties(DeviceInformation devInfo, ListBox propertiesList)
        {
            propertiesList.Items.Clear();
            propertiesList.Items.Add("Id: " + devInfo.Id);
            propertiesList.Items.Add("Name: " + devInfo.Name);
            propertiesList.Items.Add("IsDefault: " + devInfo.IsDefault);
            propertiesList.Items.Add("IsEnabled: " + devInfo.IsEnabled);
            propertiesList.Items.Add("EnclosureLocation: " + devInfo.EnclosureLocation);

            // Add device interface information
            propertiesList.Items.Add("----Device Interface----");
            foreach (var deviceProperty in devInfo.Properties)
            {
                propertiesList.Items.Add(deviceProperty.Key + ": " + deviceProperty.Value);
            }

            propertiesList.IsEnabled = true;
        }
    }
}
