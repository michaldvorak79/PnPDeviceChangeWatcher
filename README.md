# PnPDeviceChangeWatcher
A simple command line Windows application that can execute a command whenever a specific Plug and Play device is connected or disconnected.

Uses [WMI](https://en.wikipedia.org/wiki/Windows_Management_Instrumentation) to monitor the devices.

## Requirements
- [.NET Framework 4.0 or higher](https://dotnet.microsoft.com/).
- Windows 7 or newer. (Possibly runs on older Windows, but this is untested.)

## Purpose
Useful for running automated tasks when a specific PnP device (like usb switch, flash drive, keyboard, smart card, etc.) is connected to or disconnected from a Windows computer.

## Usage
`PnPDeviceChangeWatcher [options]`

Options:
- `/deviceId <device id>`         - specify the ID of the device to watch
- `/onConnect <command>`          - program or shell command to run when the device is connected
- `/onDisconnect <command>`       - program or shell command to run when the device is disconnected
- `/hide`                         - run the program in background, hidden
- `/stop`                         - stop all running instances of this program
- `/help`                         - display usage

It's best to enclose the `<device id>` part and the `<command>` parts in quotes, as they can contain many special characters and the Windows shell might otherwise mess them up. If your command contains a quotes (") character, replace it with double quotes ("").

## Example
This is a real-life example and my motivation for writing this application. I use two computers at home which share the same keyboard and mouse (using a USB switch) and are connected to the same display - one to the DisplayPort input, the other to the HDMI input. When switching from one computer to another, I could switch the keyboard and mouse quickly by pushing the button on the USB switch. To switch the display however, I had to activate its on-screen menu, navigate to the Input submenu, and select the proper input. That was slow and annoying.

Hence the following command:

`PnPDeviceChangeWatcher.exe /deviceId "USB\VID_1A40&PID_0101\8&E4790EA&0&3" /onConnect "ControlMyMonitor.exe /SetValue ""\\.\DISPLAY1\Monitor0"" 60 15" /onDisconnect "ControlMyMonitor.exe /SetValue ""\\.\DISPLAY1\Monitor0"" 60 17" /hide`

What it does:
- runs in background
- monitors the `USB\VID_1A40&PID_0101\8&E4790EA&0&3` device (my USB switch)
- whenever the switch is connected to main computer, command `ControlMyMonitor.exe /SetValue "\\.\DISPLAY1\Monitor0" 60 15` is executed, which switches the display input to DisplayPort (using NirSoft's excellent [ControlMyMonitor](https://www.nirsoft.net/utils/control_my_monitor.html) application).
- when the switch is disconnected (i.e. connected to the other computer), command `ControlMyMonitor.exe /SetValue "\\.\DISPLAY1\Monitor0" 60 17` is run to activate the HDMI display input

Thus I can use a simple USB switch as a full-fledged KVM.

## How to obtain your device's ID
Either check the Device Manager, or use the included helper app `GetDeviceIDs`. While it's running, it will monitor the computer for any PnP device connection or disconnection, outputting the device's name and ID. Plug and unplug the device you're interested in and you should see its name and device ID in the console.

## Troubleshooting
If you don't specify the `/hide` flag, the application will run in the console outputting various information about what exactly it's doing. You can use it to doublecheck that the device ID and the commands are correct and that the device changes are properly detected.
