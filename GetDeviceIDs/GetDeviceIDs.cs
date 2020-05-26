using System;
using System.Collections.Generic;
using System.Management;

/*
 * A helper program - watches for a device connection and disconnection events and prints out names and IDs of the (dis)connected devices.
 */
namespace GetDeviceIDs {
    class Program {
        private static List<Tuple<string, string>> devices;
        static void Main(string[] args) {
            Console.WriteLine("Listening for PnP device additions and removals...\n(press any key to stop)");

            devices = GetDevices();
            // Create event query to be notified of device additions and removals
            WqlEventQuery query = new WqlEventQuery("Win32_DeviceChangeEvent", "EventType = '3' or EventType = '2'");

            // Initialize an event watcher and subscribe to events that match this query
            using (var watcher = new ManagementEventWatcher()) {
                watcher.Query = query;
                watcher.EventArrived += ProcessEvent;
                watcher.Start();

                //wait for keypress
                Console.ReadKey(true);

                watcher.Stop();
            }
        }

        private static void ProcessEvent(object sender, EventArrivedEventArgs e) {
            List<Tuple<string, string>> newDevices = GetDevices();
            foreach (Tuple<string, string> device in newDevices) {
                if (!devices.Contains(device)) {
                    Console.WriteLine(String.Format("\nAdded device \"{0}\"\nDeviceID: \"{1}\"", device.Item1, device.Item2));
                }
            }
            foreach (Tuple<string, string> device in devices) {
                if (!newDevices.Contains(device)) {
                    Console.WriteLine(String.Format("\nRemoved device \"{0}\"\nDeviceID: \"{1}\"", device.Item1, device.Item2));
                }
            }

            devices = newDevices;
        }

        private static List<Tuple<string, string>> GetDevices() {
            List<Tuple<string, string>> devices = new List<Tuple<string, string>>();

            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
            using (var collection = searcher.Get()) {
                foreach (ManagementObject device in collection) {
                    devices.Add(Tuple.Create(device.Properties["DeviceID"].Value?.ToString(), device.Properties["Description"].Value?.ToString()));
                }
            }

            return devices;
        }
    }
}
