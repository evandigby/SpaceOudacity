//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Devices.Enumeration;

namespace SpaceOudacity
{
    /// <summary>
    /// DeviceWatcher class to monitor adding/removing MIDI devices on the fly
    /// </summary>
    internal class MidiDeviceWatcher
    {
        internal DeviceWatcher deviceWatcher = null;
        internal DeviceInformationCollection deviceInformationCollection = null;
        private bool enumerationCompleted = false;
        private readonly ListBox portList = null;
        private readonly string midiSelector = string.Empty;
        private readonly DispatcherQueue dispatcherQueue = null;

        /// <summary>
        /// Constructor: Initialize and hook up Device Watcher events
        /// </summary>
        /// <param name="midiSelectorString">MIDI Device Selector</param>
        /// <param name="dispatcher">CoreDispatcher instance, to update UI thread</param>
        /// <param name="portListBox">The UI element to update with list of devices</param>
        internal MidiDeviceWatcher(string midiSelectorString, DispatcherQueue dispatcher, ListBox portListBox)
        {
            deviceWatcher = DeviceInformation.CreateWatcher(midiSelectorString);
            portList = portListBox;
            midiSelector = midiSelectorString;
            dispatcherQueue = dispatcher;

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
        }

        /// <summary>
        /// Destructor: Remove Device Watcher events
        /// </summary>
        ~MidiDeviceWatcher()
        {
            deviceWatcher.Added -= DeviceWatcher_Added;
            deviceWatcher.Removed -= DeviceWatcher_Removed;
            deviceWatcher.Updated -= DeviceWatcher_Updated;
            deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
        }

        /// <summary>
        /// Start the Device Watcher
        /// </summary>
        internal void Start()
        {
            if (deviceWatcher.Status != DeviceWatcherStatus.Started)
            {
                deviceWatcher.Start();
            }
        }

        /// <summary>
        /// Stop the Device Watcher
        /// </summary>
        internal void Stop()
        {
            if (deviceWatcher.Status != DeviceWatcherStatus.Stopped)
            {
                deviceWatcher.Stop();
            }
        }

        /// <summary>
        /// Get the DeviceInformationCollection
        /// </summary>
        /// <returns></returns>
        internal DeviceInformationCollection GetDeviceInformationCollection()
        {
            return deviceInformationCollection;
        }

        /// <summary>
        /// Add any connected MIDI devices to the list
        /// </summary>
        private async void UpdateDevices()
        {
            // Get a list of all MIDI devices
            deviceInformationCollection = await DeviceInformation.FindAllAsync(midiSelector);

            // If no devices are found, update the ListBox
            if ((deviceInformationCollection == null) || (deviceInformationCollection.Count == 0))
            {
                // Start with a clean list
                portList.Items.Clear();

                portList.Items.Add("No MIDI ports found");
                portList.IsEnabled = false;
            }
            // If devices are found, enumerate them and add them to the list
            else
            {
                // Start with a clean list
                portList.Items.Clear();

                foreach (var device in deviceInformationCollection)
                {
                    portList.Items.Add(device.Name);
                }

                portList.IsEnabled = true;
            }
        }

        /// <summary>
        /// Update UI on device added
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            // If all devices have been enumerated
            if (enumerationCompleted)
            {
                dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                {
                    // Update the device list
                    UpdateDevices();
                });
            }
        }

        /// <summary>
        /// Update UI on device removed
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // If all devices have been enumerated
            if (enumerationCompleted)
            {
                dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                {
                    // Update the device list
                    UpdateDevices();
                });
            }
        }

        /// <summary>
        /// Update UI on device updated
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // If all devices have been enumerated
            if (enumerationCompleted)
            {
                dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                {
                    // Update the device list
                    UpdateDevices();
                });
            }
        }

        /// <summary>
        /// Update UI on device enumeration completed.
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            enumerationCompleted = true;
            dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
            {
                // Update the device list
                UpdateDevices();
            });
        }
    }
}