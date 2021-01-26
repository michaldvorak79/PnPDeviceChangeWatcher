using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Threading;

/*
 * The main program - watches for a device connection and disconnection events and runs specified commands.
 */
namespace PnpDeviceChangeWatcher {
    class PnPDeviceChangeWatcher {
        //command line options
        private static readonly string OPTION_HIDE = "/hide";
        private static readonly string OPTION_HIDE_SIMULATED = "/hideSimulated"; //for debugging
        private static readonly string OPTION_HELP = "/help";
        private static readonly string OPTION_HELP_ALT = "/?";
        private static readonly string OPTION_DEVICE_ID = "/deviceId";
        private static readonly string OPTION_ON_CONNECT = "/onConnect";
        private static readonly string OPTION_ON_DISCONNECT = "/onDisconnect";
        private static readonly string OPTION_STOP = "/stop";

        //the name of the global handle which can receive a stop signal
        private static readonly string STOP_SIGNAL_HANDLE_NAME = "DeviceChangeWatcherEventWaitHandle";

        //the ID of the PnP device to watch
        private static string deviceId;
        //command to run when the watched device is connected. It will be run in shell, so it can be either a program, or a shell command.
        private static string commandOnConnect;
        //command to run when the watched device is disconnected
        private static string commandOnDisconnect;

        //current connection status of the watched device
        private static bool connected;

        static void Main(string[] args) {
            ParseParams(args);

            if (deviceId == null) {
                Console.Error.WriteLine("\nError: device ID must be specified!");
                Environment.Exit(-1);
            }

            //output some debug info
            Console.WriteLine("\nConfiguration\n-------------");
            Console.WriteLine("Device ID to watch:        " + deviceId);
            Console.WriteLine("Command when connected:    " + (commandOnConnect != null ? commandOnConnect : "N/A"));
            Console.WriteLine("Command when disconnected: " + (commandOnDisconnect != null ? commandOnDisconnect : "N/A"));
            Console.WriteLine("\nStarted monitoring device state\n(press any key to stop)");

            connected = IsDevicePresent();

            // Create event query to be notified of device additions and removals
            WqlEventQuery query = new WqlEventQuery("Win32_DeviceChangeEvent", "EventType = '3' or EventType = '2'");

            // Initialize an event watcher and subscribe to events that match this query
            using (var watcher = new ManagementEventWatcher()) {
                watcher.Query = query;
                watcher.EventArrived += new EventArrivedEventHandler(ProcessEvent);
                watcher.Start();

                WaitForKeyOrStopSignal();

                watcher.Stop();
            }
        }

        /*
         * This method is called whenever a PnP device is connected or disconnected. Unfortunately it doesn't carry information about _which_ device
         * was connected or disconnected, so we have to run another WMI query to determine the status of our watched device, then compare it with 
         * the previous status. Only when the connection status changes can we conclude that the device has just been connected or disconnected.
         */
        private static void ProcessEvent(object sender, EventArrivedEventArgs e) {
            bool newStatus = IsDevicePresent();

            if (newStatus != connected) {
                ProcessStartInfo info = new ProcessStartInfo("cmd.exe") {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    Arguments = "/c "
                };
                if (newStatus) {
                    Console.WriteLine("\nDevice connected");
                    if (commandOnConnect != null) {
                        Console.WriteLine("Executing command: " + commandOnConnect);
                        info.Arguments += commandOnConnect;
                        Process.Start(info);
                    } else {
                        Console.WriteLine("No command specified, doing nothing");
                    }
                } else {
                    Console.WriteLine("\nDevice disconnected");
                    if (commandOnDisconnect != null) {
                        Console.WriteLine("Executing command: " + commandOnDisconnect);
                        info.Arguments += commandOnDisconnect;
                        Process.Start(info);
                    } else {
                        Console.WriteLine("No command specified, doing nothing");
                    }
                }
            }

            connected = newStatus;
        }

        /* 
         * Detect the presence of our watched device.
         */
        private static bool IsDevicePresent() {
            //query WMI to check if our device is present
            //(backslashes in our device ID have to be replaced with double backslashes, as WMI requires this. also quotes need to be backslashed.)
            using (var searcher = new ManagementObjectSearcher(String.Format("Select * From Win32_PnPEntity where DeviceID = \"{0}\"", deviceId.Replace(@"\", @"\\").Replace("\"", "\\\""))))
            using (var collection = searcher.Get())
                return collection.Count == 1;
        }

        /*
         * Run a new instance of this program inside a hidden window. 
         */
        private static void RunHidden(string[] args, bool simulate = false) {
            //get the path to our executable
            ProcessStartInfo info = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //convert arguments from array to single string
            StringBuilder argLine = new StringBuilder();
            foreach (string arg in args) {
                //don't pass the /hide parameter to the new hidden instance of the program, otherwise new instances would be created endlessly
                if (!IsArg(arg, OPTION_HIDE) && !IsArg(arg, OPTION_HIDE_SIMULATED)) {
                    if (argLine.Length > 0) argLine.Append(' ');
                    //enclose the arguments in quotes (replacing any single quotes inside the argument by double quotes, as per Windows shell standard), 
                    //because the arguments can contain spaces and other weird characters and we want to preserve those.
                    //Also backslashes at the end of the argument apparently need to be doubled. Weird.
                    string escapedArgument = arg;
                    int numEndingBackslashes = 0;
                    while (escapedArgument.EndsWith("\\")) {
                        numEndingBackslashes++;
                        escapedArgument = escapedArgument.Substring(0, escapedArgument.Length - 1);
                    }
                    for (int i = 0; i < numEndingBackslashes; i++) escapedArgument += @"\\";
                    argLine.Append("\"" + escapedArgument.Replace("\"", "\"\"") + "\"");
                }
            }
            info.Arguments = argLine.ToString();
            info.UseShellExecute = false;
            if (!simulate) {
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.CreateNoWindow = true;
            }

            Process.Start(info);
        }

        /*
         * Process command line parameters
         */
        private static void ParseParams(string[] args) {
            if (args.Length == 0) {
                ShowHelp();
                Environment.Exit(0);
            }
            for (int i = 0; i < args.Length; i++) {
                if (IsArg(args[i], OPTION_HIDE)) {
                    //run a new hidden instance of the program and exit
                    RunHidden(args);
                    Environment.Exit(0);
                } else if (IsArg(args[i], OPTION_HIDE_SIMULATED)) {
                    //simulate a new hidden instance of the program (for debug purposes) and exit
                    RunHidden(args, true);
                    Environment.Exit(0);
                } else if (IsArg(args[i], OPTION_STOP)) {
                    //send the stop signal to all other instances and exit
                    SendStopSignal();
                    Environment.Exit(0);
                } else if (IsArg(args[i], OPTION_HELP) || IsArg(args[i], OPTION_HELP_ALT)) {
                    ShowHelp();
                    Environment.Exit(0);
                } else if (i < args.Length - 1) {
                    if (IsArg(args[i], OPTION_DEVICE_ID)) {
                        deviceId = args[++i];
                    } else if (IsArg(args[i], OPTION_ON_CONNECT)) {
                        commandOnConnect = args[++i];
                    } else if (IsArg(args[i], OPTION_ON_DISCONNECT)) {
                        commandOnDisconnect = args[++i];
                    }
                }
            }
        }

        /*
         * compares a command line argument, case-insensitive
         */
        private static bool IsArg(string arg1, string arg2) {
            if (arg1 == null || arg2 == null) return arg1 == arg2;
            else return (arg1.ToLower().Equals(arg2.ToLower()));
        }

        private static void ShowHelp() {
            int tabPosition = 30;
            Console.WriteLine(String.Format("{0} v{1}\n",
                System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location),
                System.Reflection.Assembly.GetEntryAssembly().GetName().Version)
            );
            Console.WriteLine("Usage: " + System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location) + " [options]\n");
            Console.WriteLine("Options:");
            Console.WriteLine((OPTION_DEVICE_ID + " <device id>").PadRight(tabPosition) + "- specify the ID of the device to watch");
            Console.WriteLine((OPTION_ON_CONNECT + " <command>").PadRight(tabPosition) + "- program or shell command to run when the device is connected");
            Console.WriteLine((OPTION_ON_DISCONNECT + " <command>").PadRight(tabPosition) + "- program or shell command to run when the device is disconnected");
            Console.WriteLine(OPTION_HIDE.PadRight(tabPosition) + "- run the program in background, hidden");
            Console.WriteLine(OPTION_STOP.PadRight(tabPosition) + "- stop all running instances of this program");
            Console.WriteLine(OPTION_HELP.PadRight(tabPosition) + "- show this help message");
        }

        /*
         * Wait until a key is pressed or the stop signal is received
         */
        private static void WaitForKeyOrStopSignal() {
            EventWaitHandle keyPressed = new EventWaitHandle(false, EventResetMode.ManualReset);
            //we detect a keypress in a separate thread, so that the main thread is not blocked. When a key is pressed, an EventWaitHandle is triggered.
            //The thread is set as background, so that if the main thread finishes before a key is pressed, this thread will also finish instead of blocking the program.
            new Thread(() => {
                using(keyPressed) {
                    Console.ReadKey(true);
                    keyPressed.Set();
                }
            }) { IsBackground = true }.Start();

            //set up a global stop signal event
            using (var stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset, STOP_SIGNAL_HANDLE_NAME)) {
                stopSignal.Reset();
                //wait until either the stop signal or the keypress event is received
                WaitHandle.WaitAny(new WaitHandle[] { keyPressed, stopSignal });
            }
        }

        /*
         * Send the stop signal event to all listening instances
         */
        private static void SendStopSignal() {
            using (var ewh = new EventWaitHandle(false, EventResetMode.ManualReset, STOP_SIGNAL_HANDLE_NAME)) {
                ewh.Set();
            }
        }
    }
}

