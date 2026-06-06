ZXing.Net.Maui & BarcodeScanning.Native.MauiType: Open Source (Free)Best for: Cross-platform mobile development (.NET MAUI).Details: If you are building native iOS and Android applications, these libraries use native APIs (Apple Vision Kit and Google ML Kit) to provide blazing-fast camera-based barcode scanning.Get it: View documentation on Scanbot SDK regarding top .NET MAUI options.

ZXing.Net.Maui 

ZXing.Net.Maui is the successor to the older ZXing.Net.Mobile library. It does not only provide controls for barcode scanning, but also APIs for QR Code generation. The plugin is built on ZXing.Net, a C# port of the original Java ZXing library.  

ZXing is officially in maintenance mode, meaning no updates or features are being released. Despite the challenges this introduces, the library is the base of many community ports and wrappers for a variety of platforms and frameworks. 
Key features and benefits 

ZXing.Net.Maui provides both QR Code generation and barcode scanning capabilities. It can scan from the live camera stream and static images. The scanner is able to detect multiple barcodes in one frame as well.  

Developers can configure switching between front/rear cameras and toggling the flashlight. However, the library lacks support for zoom controls and defining a region of interest (ROI).  
Supported platforms 

On Windows, only barcode generation is supported.  
Platform	Supported?
Android	✅ Yes
iOS	✅ Yes
macOS	⚠️ File-based scanning only 
Windows	⚠️ Generation only 
Supported barcodes 

ZXing.Net.Maui supports a broad range of 1D and 2D barcodes, including niche and postal symbologies like GS1 DataBar Limited and Intelligent Mail barcode. You can find the full list of supported symbologies in the table below. 
Limitations 

In addition to the original ZXing library no longer being actively maintained, ZXing.Net.MAUI has several other limitations that affect scanning performance and reliability: 

Slow scanning for 1D barcodes 

ZXing.Net.MAUI requires TryHarder = true to reliably scan one-dimensional barcodes on Android, which makes scanning very slow. Developers have flagged that scanning Code 128 barcodes takes up to 2.5 seconds on Android phones like the Google Pixel 3A.  

Difficulties scanning complex and small barcodes 

ZXing.Net.Maui can struggle with dense or high-complexity barcodes. Developers flag that the scanner fails to detect large QR codes, and that while simple PDF417 codes scan successfully, higher-density ones do not. Even high-resolution images frequently decode to null. 

Additionally, the scanner may face issues with scanning small barcodes, even on higher-end devices like the Samsung Galaxy S23+.  

Fails to scan long barcodes 

ZXing.Net.MAUI also fails to properly scan certain 26-character 1D barcodes, specifically Code 128. Users flag that these codes are either not detected at all or are decoded incorrectly, with one developer noting that no 1D barcode of this length scans successfully. 

Camera focus issues on iOS 

When used on iPhone 15 Pro, the camera struggles to focus on barcodes. ZXing.Net.Maui uses the main lens instead of switching to the macro lens, rendering the camera unable to focus at close range. As a result, barcodes that scan successfully on Android are blurry and unreadable on iOS, with no built-in way to select the correct lens.
Summary: ZXing.Net.Maui 
Supported platforms	Android, iOS
macOS (no camera support)
Supported barcodes	Codabar, Code 128, Code 39, Code 93, EAN-13, EAN-8, GS1 DataBar, GS1 DataBar Expanded, Intelligent Mail Barcode, ITF, MSI Plessey, UPC-A, UPC-E, UPC-A/EAN Extension, Aztec, Data Matrix, MaxiCode, PDF417, PharmaCode, QR Code 
Based on another library?	Yes, built on the ZXing.Net engine, a port of the original Java ZXing library 
Maintenance status	Actively maintained, but no active feature development 
Languages	C#
Offers UI?	No, developers must build their own UI around CameraBarcodeReaderView 
Input source?	Both live stream and static images
Developer resources	GitHub repository 
NuGet package 
Integration tutorial



TUTORIAL OF INTEGRATION

In this tutorial, you will learn how to use the ZXing.Net.Maui.Controls library to integrate a barcode scanner into your Android app using .NET MAUI.
Prerequisites

Before you begin developing a .NET MAUI application, you have to ensure your development environment is configured correctly.

First, you have to install the .NET SDK on your machine. We recommend installing .NET 8.0. You can check all the available versions by accessing the official .NET website.

You will create the application using Visual Studio. If you don’t have Visual Studio on your computer, access the Microsoft portal and download the Visual Studio Community version, which is free for individuals.

⚠️ A note for Mac users: Microsoft has ended support for Visual Studio on Mac. We’ve covered several other options in our blog post about Visual Studio alternatives.

During the installation, select the following workloads:

    ASP.NET and web development
    Mobile development with .NET
    Universal Windows Platform (UWP) development

Create a project

To create a new project, launch Visual Studio and follow these steps:

    Click on Create a new project.
    Search for .NET MAUI App using the search bar. Choose the .NET MAUI App template and click Next.

Visual Studio will generate the new project. If a Windows Security Alert regarding the firewall pops up, click Allow access to proceed.
Configure your MAUI app to scan barcodes

After creating the project, it’s time to add the ZXing.Net.Maui library to your project and configure it to add barcode scanning capabilities to your .NET MAUI app. 
Step 1: Install ZXing.Net.Maui.Controls

Begin by installing the ZXing.Net.Maui.Controls NuGet package into your project. To add the package, run the following command:

dotnet add package ZXing.Net.Maui.Controls --version 0.4.0

      
        
      
    

This package provides the necessary components to integrate barcode scanning functionality seamlessly into your app.
Step 2: Initialize the ZXing.Net.Maui plugin

Next, you need to initialize the ZXing.Net.Maui plugin within the MauiProgram.cs file. This will enable the barcode scanning feature across the application. First, add the following to the top of the file:

using ZXing.Net.Maui.Controls;

      
        
      
    

After that, within the Create method, add the UseBarcodeReader() method to integrate the ZXing plugin into the app’s configuration:

public static MauiApp Create()
{
    var builder = MauiApp.CreateBuilder();

    builder
        .UseMauiApp<App>()
        .UseBarcodeReader(); // This line initializes the barcode reader plugin

    return builder.Build();
}

      
        
      
    

Step 3: Provide camera access permissions

To scan barcodes, your app needs permission to access the device’s camera. You’ll need to add the appropriate permissions to the app’s metadata, depending on the platform you’re using. For Android, add the following permission to your AndroidManifest.xml file:

<uses-permission android:name="android.permission.CAMERA" />

      
        
      
    

If you’re using iOS, include the following entry in your info.plist file:

<key>NSCameraUsageDescription</key>
<string>This app uses the camera to scan barcodes.</string>

      
        
      
    

When adding permission for an iOS app, Ensure you enter a clear and valid reason for your app to access the camera. This message will be displayed to users when the app requests permission.
Step 4: Add the barcode reader

To start reading barcodes, you need to add the barcode reader control to your MainPage.xaml. This is done by including the appropriate XML namespace and adding the CameraBarcodeReaderView control. Your MainPage.xaml should look like this:

<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="MauiApp.MainPage"
             xmlns:zxing="clr-namespace:ZXing.Net.Maui.Controls;assembly=ZXing.Net.Maui.Controls">

    <zxing:CameraBarcodeReaderView
          x:Name="cameraBarcodeReaderView"
          IsDetecting="True"
          BarcodesDetected="BarcodesDetected" />

</ContentPage>

      
        
      
    

The CameraBarcodeReaderView component provides live barcode detection through the camera. In addition, setting IsDetecting to True ensures that barcode detection starts as soon as the app runs.
Step 5: Configure the barcode reader options

After setting up the UI, you have to configure the barcode reader’s behavior in MainPage.xaml.cs. In this step, you’ll specify which barcode formats your app should recognize and whether it should handle multiple barcodes simultaneously.

To define the barcode reader, use the x:Name from the code snippet in step 4 for the zxing:CameraBarcodeReaderView view. If you used the code example, that’s x:Name = cameraBarcodeReaderView. To specify the options, use the BarcodeReaderOptions class. The following code block presents a configuration example where the scanner is configured to detect EAN-13 barcodes, which are standard in many retail environments. The AutoRotate option ensures that barcodes are detected regardless of orientation, while Multiple allows for scanning more than one barcode at a time.

public MainPage()
{
    InitializeComponent();
    cameraBarcodeReaderView.Options = new ZXing.Net.Maui.BarcodeReaderOptions
    {
        Formats = ZXing.Net.Maui.BarcodeFormat.Ean13, // Set to recognize EAN-13 barcodes
        AutoRotate = true, // Automatically rotate the image to detect barcodes from different angles
        Multiple = true // Allow the detection of multiple barcodes at once
    };
}

      
        
      
    

Step 6: Handle barcode detection events

As the last step, you need to handle the event triggered when a barcode is detected. To add this event handler, use the BarcodesDetected method in your MainPage.xaml.cs file.

For this tutorial, we’ll set the code to listen for barcode detection events and, upon detecting a barcode, display a message in a pop-up alert. The message will inform the barcode was detected and will also display the barcode. The pop-up will also contain a button, which the user can press to close the alert. The following code block presents a configuration example.

protected void BarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
{
    var first = e.Results?.FirstOrDefault();
    if (first is null) {
        return;
    }

    Dispatcher.DispatchAsync(async () =>
    {
        await DisplayAlert("Barcode Detected", first.Value, "OK");
    });
}

      
        
      
    

In the above example, the Dispatcher.DispatchAsync method ensures that the UI is updated on the main thread, which is necessary for displaying alerts.

Since steps 5 and 6 change the MainPage.xaml.cs file, the following code block presents the complete code for the MainPage.xaml.cs to ensure your app is configured correctly.

namespace MauiApp
{
    public partial class MainPage : ContentPage
    {    	
        string lastDetectedBarcode = string.Empty;
        DateTime lastDetectedTime = DateTime.MinValue;
    	
        public MainPage()
        {
            InitializeComponent();
            cameraBarcodeReaderView.Options = new ZXing.Net.Maui.BarcodeReaderOptions
            {
               Formats = ZXing.Net.Maui.BarcodeFormat.Ean13, // Set to recognize EAN-13 barcodes
               AutoRotate = true, // Automatically rotate the image to detect barcodes from different angles
               Multiple = true // Allow the detection of multiple barcodes at once
            };
        }

        protected void BarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
        {
            var first = e.Results?.FirstOrDefault();
            if (first is null)
            {
                return;
            }
            
            // Check if the same barcode was detected within the last second
            if (first.Value == lastDetectedBarcode && (DateTime.Now - lastDetectedTime).TotalSeconds < 1)
            {
                return;
            }
            
            lastDetectedBarcode = first.Value;
            lastDetectedTime = DateTime.Now;

            Dispatcher.DispatchAsync(async () =>
            {
                await DisplayAlert("Barcode Detected", first.Value, "OK");
            });
        }
    }
}

      
        
      
    

Test the app

To test your finished app, you can connect your Android phone to your computer and enable Developer Options or use Virtual Devices (see Run on mobile device in the .NET MAUI docs to learn more).

⚠️ Not all Android devices work properly with Visual Studio when testing apps. We recommend trying the emulator if you face issues connecting your Android device.
Disadvantages of the ZXing library

ZXing provides decent performance for basic barcode scanning tasks but sometimes struggles with more challenging scenarios. Its most notable drawbacks as a barcode scanning solution include:

    Scanning accuracy and speed: ZXing struggles with scanning poorly lit or damaged barcodes. It often exhibits low recognition rates for smaller barcodes and can fail to decode them altogether.
    Compatibility issues: ZXing may not perform reliably across all devices, particularly newer models. This has led to frustration among developers who require consistent performance across different platforms.
    Lack of active development: As an open-source project, ZXing has seen limited updates and maintenance in recent years. This stagnation can lead to unresolved bugs and compatibility issues, making it less reliable for commercial applications.
    Integration complexity: Integrating ZXing into your app can be cumbersome, especially for developers who may not be familiar with its architecture. This can lead to longer development times and increased chances of bugs during implementation.

Try the Scanbot Barcode Scanner SDK for enhanced barcode scanning

While ZXing is a capable solution for basic barcode scanning, it may face limitations in terms of scanning accuracy and speed, especially in challenging environments. As an alternative, you can integrate the Scanbot Barcode Scanner SDK into your .NET MAUI app to enable enterprise-grade scanning performance with just a few lines of code – pre-built and configurable UI components included.
Step 1: Install the Scanbot Barcode Scanner SDK

To get started, add the Scanbot Barcode Scanner SDK to your project. Open your project’s .csproj file and add the following package references:

<ItemGroup>

    <PackageReference Include="ScanbotBarcodeSDK.MAUI" Version="5.1.0" />

</ItemGroup>

      
        
      
    

Run dotnet restore to install the SDK.
Step 2: Initialize the SDK

In your MauiProgram.cs, initialize the Scanbot SDK. Make sure to add the necessary initialization code:

using ScanbotSDK.MAUI;

namespace MauiApp;

public static class MauiProgram
{
    public const string LicenseKey = "";
// Without a license key, the Scanbot Barcode SDK will work for 1 minute.
  	// To scan longer, register for a trial license key here: https://docs.scanbot.io/trial/
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        ScanbotBarcodeSDK.Initialize(builder, new InitializationOptions
        {
            LicenseKey = LicenseKey,
            LoggingEnabled = true,
            ErrorHandler = (status, feature) =>
            {
                Console.WriteLine($"License error: {status}, {feature}");
            }
        });

        return builder.Build();
    }
}

      
        
      
    

This initializes the SDK. If you don’t have a license key, you can still try out the scanner. However, to test longer than 60 seconds, get a free trial license here: https://docs.scanbot.io/trial/
Step 3: Implement the barcode scanner

Now, let’s add single-barcode scanning functionality. Replace the content in your MainPage.xaml.cs with the following:

using ScanbotSDK.MAUI;
using ScanbotSDK.MAUI.Barcode;

namespace MauiApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void StartSingleScanning(object sender, EventArgs e)
    {
        try
        {
            if (!ScanbotBarcodeSDK.LicenseInfo.IsValid)
            {
                await DisplayAlert("License invalid", "Trial license expired.", "Dismiss");
                return;
            }

            var configuration = new BarcodeScannerConfiguration
            {
                UseCase = new SingleScanningMode
                {
                    ConfirmationSheetEnabled = true
                }
            };

            var result = await ScanbotBarcodeSDK.BarcodeScanner.OpenBarcodeScannerAsync(configuration);

            var barcode = result.Items.FirstOrDefault()?.Text ?? "No barcode found";
            await DisplayAlert("Barcode detected", barcode, "OK");
        }
        catch (TaskCanceledException)
        {
            // Handle the cancellation.
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

      
        
      
    

In your MainPage.xaml, add a button to trigger the Scanbot Barcode Scanner:

<Button
    x:Name="SingleScanningBtn"
    Text="Start single-barcode scanning"
    Clicked="StartSingleScanning"
    HorizontalOptions="Fill" />

      
        
      
    

Step 4: Test your app

That’s it! You can now build and run your app to try out the Scanbot Barcode Scanner SDK. The single scanning mode you implemented is just one of many available in the Scanbot SDK. It also offers advanced features like multi-barcode scanning, augmented reality (AR) overlays, and customizable confirmation sheets, giving you more options to enhance your app’s barcode scanning workflows. All UI colors and texts can be easily adjusted to fit your app’s design and branding needs.
Conclusion

By integrating the Scanbot SDK, you can provide your users with an improved barcode scanning experience. Try it out and see how it compares to ZXing. If you have any questions, feel free to contact us at sdksupport@scanbot.io

Happy scanning!
How to improve barcode scanning performance in real-world conditions

Once your barcode scanning functionality is up and running in .NET MAUI, the next step is to ensure everything works as expected in real-world scenarios. Your users won’t always have great lighting, crisp codes, or a single symbology on a flat surface. Understanding these variables – and how .NET MAUI and its ecosystem of tools can help mitigate them – is essential if you’re aiming to build a robust, production-grade scanner. Let’s walk through the most critical challenges and advanced considerations developers face after the initial implementation.
Scanning in low-light environments

Barcode scanning is highly sensitive to lighting conditions. In low-light environments, mobile and desktop cameras often produce grainy, high-noise images, degrading the contrast that barcode decoders rely on. Detecting poor lighting and automatically activating the device’s torch feature can be implemented using native platform APIs via .NET MAUI’s dependency injection.

Additionally, you can preprocess the camera feed using simple brightness/contrast adjustments or more advanced image normalization techniques – some developers even apply adaptive histogram equalization for dynamic contrast boosting. While not always necessary, this can substantially improve the scanner’s performance in suboptimal lighting.
Scanning damaged or worn-out barcodes

Real-world barcodes are rarely pristine. They may be torn, scratched, faded, or printed on curved or textured surfaces. Non-commercial scanning libraries often fail to decode if enough visual fidelity is lost, especially in the case of 1D barcodes like Code 39 or EAN-13, which are more sensitive to bar and gap proportions.

To improve the scanning success rate, preprocessing steps such as binarization, morphological filters (like dilation or closing), and contour-based barcode isolation can help. These enhancements are particularly valuable when using static image capture instead of live scanning.

For use cases that demand higher resilience – such as logistics, manufacturing, or healthcare – consider integrating ML-based approaches trained to recognize degraded barcodes, or switch to more fault-tolerant symbologies like Data Matrix or PDF417.
Scanning multiple barcodes in one frame

Scanning multiple barcodes within a single camera frame introduces complexity in detection, decoding, and UI feedback. To solve this, you’ll need to isolate barcode regions using image segmentation. This often involves edge detection or trained object detection models running on the camera feed to identify multiple barcode bounding boxes. Each region can then be processed separately.

You should also define a strategy for handling simultaneous reads. Will your app return all codes at once? Let the user tap on a region to select one? Or prioritize a specific format? Designing this interaction flow carefully is key to preventing confusion and ensuring usability.
Supporting many different barcode symbologies

Depending on your application’s domain, your barcode scanning app may need to support Code 128, EAN-13, ITF, Aztec, PDF417, or proprietary formats. Most barcode scanning libraries support at least a handful of the most common symbologies, but they don’t do equally well with every format.

It’s wise to explicitly configure which symbologies your app is expecting. This not only improves scan accuracy but also reduces CPU usage. For example, if your app only ever expects QR codes and EAN-13, there’s no need to have the decoder look for Code 93 or MaxiCode. Also, understand the pros and cons of each format. QR codes are robust and versatile but visually bulky. Data Matrix codes are excellent for small surfaces but not widely recognized by older scanners.

In some industries, regulations dictate barcode format usage. Being format-aware ensures compliance, enhances interoperability, and helps you future-proof your application.
Providing zoom and focus control

A poorly focused barcode is as good as unreadable. While many modern smartphone cameras offer auto-focus and digital zoom, these features are not exposed consistently across platforms in .NET MAUI out of the box. For high-precision scanning tasks, especially with small barcodes or scanning at a distance (e.g., warehouse inventory), you should give your users manual control. This is particularly valuable when your scanner is targeting a moving barcode or a code that’s not centered in the frame.
Implementing augmented reality (AR) overlays

One underutilized but powerful enhancement is the use of AR overlays during scanning. Using AR-like feedback – such as highlighting detected barcode regions in real time, displaying decoded data inline, or guiding users to align the camera – drastically improves the user experience.
Keeping accessibility in mind

Designing your app for accessibility is critical – especially in public-facing or healthcare applications. At a minimum, your app should support screen reader announcements of scan results, text-to-speech for visually impaired users, and high-contrast modes for easier barcode alignment. Ensure that all visual cues have spoken equivalents and that dynamic barcode overlays don’t interfere with screen reader focus. For AR-style overlays or complex scan feedback, consider providing accessible fallback UI modes with large text, voice guidance, or guided scanning instructions.




NUGET:
ZXing.Net.MAUI

The successor to ZXing.Net.Mobile: barcode scanning and generation for .NET MAUI applications

<img src="https://user-images.githubusercontent.com/271950/129272315-b3f5a468-c585-49f2-bbab-68a884618b94.png" width="300" />
Barcode Scanning
Install ZXing.Net.MAUI

    Install ZXing.Net.Maui.Controls NuGet package on your .NET MAUI application

    Make sure to initialize the plugin first in your MauiProgram.cs, see below

    // Add the using to the top
    using ZXing.Net.Maui.Controls;

    // ... other code 

    public static MauiApp Create()
    {
    	var builder = MauiApp.CreateBuilder();

    	builder
    		.UseMauiApp<App>()
    		.UseBarcodeReader(); // Make sure to add this line

    	return builder.Build();
    }

Now we just need to add the right permissions to our app metadata. Find below how to do that for each platform.
Check Device Support

Before using barcode scanning, you can check if the device supports it (i.e., has a camera available):

if (ZXing.Net.Maui.BarcodeScanning.IsSupported)
{
  // Device has a camera, safe to use barcode scanning
}
else
{
  // No camera available, show alternative UI or message
}

This is useful for handling devices without cameras gracefully, avoiding runtime exceptions.
Android

For Android go to your AndroidManifest.xml file (under the Platforms\Android folder) and add the following permissions inside of the manifest node:

<uses-permission android:name="android.permission.CAMERA" />

iOS

For iOS go to your info.plist file (under the Platforms\iOS folder) and add the following permissions inside of the dict node:

<key>NSCameraUsageDescription</key>
<string>This app uses barcode scanning to...</string>

Make sure that you enter a clear and valid reason for your app to access the camera. This description will be shown to the user.
Windows

Windows is not supported at this time for barcode scanning. You can however use the barcode generation. No extra permissions are required for that.

For more information on permissions, see the Microsoft Docs.
Using ZXing.Net.Maui

If you're using the controls from XAML, make sure to add the right XML namespace in the root of your file, e.g: xmlns:zxing="clr-namespace:ZXing.Net.Maui.Controls;assembly=ZXing.Net.MAUI.Controls"

<zxing:CameraBarcodeReaderView
  x:Name="cameraBarcodeReaderView"
  BarcodesDetected="BarcodesDetected" />

Configure Reader options

cameraBarcodeReaderView.Options = new BarcodeReaderOptions
{
  Formats = BarcodeFormats.OneDimensional,
  AutoRotate = true,
  Multiple = true
};

QR codes with international characters (e.g., £, €, ¥, or non-Latin scripts) are supported by default with UTF-8 encoding. You can override this if needed:

cameraBarcodeReaderView.Options = new BarcodeReaderOptions
{
  Formats = BarcodeFormats.TwoDimensional,
  CharacterSet = "ISO-8859-1"  // Override default UTF-8 if needed
};

Toggle Torch

cameraBarcodeReaderView.IsTorchOn = !cameraBarcodeReaderView.IsTorchOn;

Flip between Rear/Front cameras

cameraBarcodeReaderView.CameraLocation
  = cameraBarcodeReaderView.CameraLocation == CameraLocation.Rear ? CameraLocation.Front : CameraLocation.Rear;

Select a specific camera

// Get available cameras
var cameras = await cameraBarcodeReaderView.GetAvailableCameras();

// Select a specific camera by setting the SelectedCamera property
if (cameras.Count > 0)
{
  cameraBarcodeReaderView.SelectedCamera = cameras[0];
}

// Or loop through available cameras and select one by name
foreach (var camera in cameras)
{
  Console.WriteLine($"Camera: {camera.Name} ({camera.Location})");
  // Select the first rear camera found
  if (camera.Location == CameraLocation.Rear)
  {
    cameraBarcodeReaderView.SelectedCamera = camera;
    break;
  }
}

Handle detected barcode(s)

protected void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
{
  foreach (var barcode in e.Results)
    Console.WriteLine($"Barcodes: {barcode.Format} -> {barcode.Value}");
}

Barcode Generator View

<zxing:BarcodeGeneratorView
  HeightRequest="100"
  WidthRequest="100"
  ForegroundColor="DarkBlue"
  Value="https://dotnet.microsoft.com"
  Format="QrCode"
  Margin="3" />

Character Encoding

The BarcodeGeneratorView supports UTF-8 character encoding by default, which allows you to encode international characters including Chinese, Japanese, Arabic, and other non-ASCII characters. You can also specify a different character encoding if needed:

<zxing:BarcodeGeneratorView
  HeightRequest="100"
  WidthRequest="100"
  Value="测试中文 Test UTF-8"
  Format="QrCode"
  CharacterSet="UTF-8" />

The CharacterSet property defaults to "UTF-8" if not specified. Other common values include "ISO-8859-1", "Shift_JIS", etc., depending on your barcode format requirements.



CAMERA SECTION:
# Camera Selection Feature

The camera selection feature allows you to enumerate available cameras on a device and select a specific camera to use for barcode scanning or camera preview.

## Overview

This feature is useful in scenarios where:
- A device has multiple cameras (e.g., front, rear, external webcams on desktop)
- You want to give users control over which camera to use
- You need to remember and restore a user's camera preference
- You want to support external cameras on desktop platforms

## API Reference

### CameraInfo Class

Represents metadata about an available camera.

```csharp
public class CameraInfo
{
    public string DeviceId { get; set; }    // Unique identifier for the camera
    public string Name { get; set; }         // Human-readable name of the camera
    public CameraLocation Location { get; set; }  // Front or Rear
}
```

### GetAvailableCameras Method

Returns a list of all available cameras on the device.

```csharp
public async Task<IReadOnlyList<CameraInfo>> GetAvailableCameras()
```

Available on both `CameraView` and `CameraBarcodeReaderView` controls.

### SelectedCamera Property

Sets or gets the currently selected camera.

```csharp
public CameraInfo SelectedCamera { get; set; }
```

When set to `null`, the default camera for the specified `CameraLocation` is used.

## Usage Examples

### Example 1: List Available Cameras

```csharp
var cameras = await cameraBarcodeReaderView.GetAvailableCameras();

foreach (var camera in cameras)
{
    Console.WriteLine($"Camera: {camera.Name}");
    Console.WriteLine($"  ID: {camera.DeviceId}");
    Console.WriteLine($"  Location: {camera.Location}");
}
```

### Example 2: Select a Specific Camera

```csharp
var cameras = await cameraBarcodeReaderView.GetAvailableCameras();

// Select the first rear camera
var rearCamera = cameras.FirstOrDefault(c => c.Location == CameraLocation.Rear);
if (rearCamera != null)
{
    cameraBarcodeReaderView.SelectedCamera = rearCamera;
}
```

### Example 3: User Camera Selection UI

```csharp
async void SelectCameraButton_Clicked(object sender, EventArgs e)
{
    var cameras = await cameraBarcodeReaderView.GetAvailableCameras();
    
    if (cameras.Count == 0)
    {
        await DisplayAlert("No Cameras", "No cameras found.", "OK");
        return;
    }

    // Show action sheet to select camera
    var cameraNames = cameras.Select(c => c.Name).ToArray();
    var selectedName = await DisplayActionSheet("Select Camera", "Cancel", null, cameraNames);
    
    if (selectedName != null && selectedName != "Cancel")
    {
        var selectedCamera = cameras.FirstOrDefault(c => c.Name == selectedName);
        if (selectedCamera != null)
        {
            cameraBarcodeReaderView.SelectedCamera = selectedCamera;
        }
    }
}
```

### Example 4: Save and Restore Camera Preference

```csharp
// Save camera preference
var selectedCamera = cameraBarcodeReaderView.SelectedCamera;
if (selectedCamera != null)
{
    Preferences.Set("PreferredCameraId", selectedCamera.DeviceId);
}

// Restore camera preference
var cameras = await cameraBarcodeReaderView.GetAvailableCameras();
var preferredCameraId = Preferences.Get("PreferredCameraId", string.Empty);

var camera = cameras.FirstOrDefault(c => c.DeviceId == preferredCameraId);
if (camera != null)
{
    cameraBarcodeReaderView.SelectedCamera = camera;
}
```

### Example 5: XAML Binding

```xaml
<zxing:CameraBarcodeReaderView
    x:Name="barcodeView"
    SelectedCamera="{Binding SelectedCamera}"
    BarcodesDetected="BarcodesDetected" />
```

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    private CameraInfo _selectedCamera;
    
    public CameraInfo SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            _selectedCamera = value;
            OnPropertyChanged();
        }
    }
    
    public async Task LoadCamerasAsync()
    {
        var cameras = await barcodeView.GetAvailableCameras();
        // Select first camera
        if (cameras.Count > 0)
        {
            SelectedCamera = cameras[0];
        }
    }
}
```

## Platform-Specific Notes

### Android
- Uses AndroidX CameraX API to enumerate cameras
- Camera IDs are stable identifiers based on lens facing and enumeration order (e.g., "front-0", "rear-0", "rear-1")
- Camera names are descriptive (e.g., "Front Camera", "Rear Camera", "Rear Camera 2")
- All available cameras are returned, including external cameras
- Properly handles devices with multiple cameras of the same facing direction

### iOS/MacCatalyst
- Uses AVFoundation framework to enumerate cameras
- Camera names are taken from the device's localized name
- Supports built-in cameras and external cameras (on MacCatalyst)

### Windows
- Uses Windows MediaCapture and MediaFrameSourceGroup APIs
- Camera names are taken from device information
- Excellent support for multiple external cameras (webcams)
- Especially useful for desktop applications

### Other Platforms
- Returns an empty list if camera enumeration is not supported

## Important Notes

1. **Permission Required**: Camera permission must be granted before calling `GetAvailableCameras()`.

2. **Timing**: Call `GetAvailableCameras()` after the camera view is initialized and connected.

3. **Dynamic Changes**: On Windows, cameras can be connected/disconnected dynamically. Consider refreshing the camera list when needed.

4. **Default Behavior**: When `SelectedCamera` is `null`, the view uses the default camera for the specified `CameraLocation` (Front or Rear).

5. **Compatibility**: Setting `SelectedCamera` takes precedence over `CameraLocation`. If you want to use `CameraLocation` again, set `SelectedCamera` to `null`.

## Migration Guide

If you were using only `CameraLocation` before:

```csharp
// Old way (still works)
cameraBarcodeReaderView.CameraLocation = CameraLocation.Front;

// New way (more control)
var cameras = await cameraBarcodeReaderView.GetAvailableCameras();
var frontCamera = cameras.FirstOrDefault(c => c.Location == CameraLocation.Front);
if (frontCamera != null)
{
    cameraBarcodeReaderView.SelectedCamera = frontCamera;
}

// To go back to automatic selection
cameraBarcodeReaderView.SelectedCamera = null;
cameraBarcodeReaderView.CameraLocation = CameraLocation.Rear;
```

Both approaches work and can be used interchangeably based on your needs.



README.MD FRom Github::

# ZXing.Net.MAUI

The successor to ZXing.Net.Mobile: barcode scanning and generation for .NET MAUI applications

<img src="https://user-images.githubusercontent.com/271950/129272315-b3f5a468-c585-49f2-bbab-68a884618b94.png" width="300" />

## Barcode Scanning

### Install ZXing.Net.MAUI

1. Install [ZXing.Net.Maui.Controls](https://www.nuget.org/packages/ZXing.Net.Maui.Controls) NuGet package on your .NET MAUI application

1. Make sure to initialize the plugin first in your `MauiProgram.cs`, see below

    ```csharp
    // Add the using to the top
    using ZXing.Net.Maui.Controls;
    
    // ... other code 
    
    public static MauiApp Create()
    {
    	var builder = MauiApp.CreateBuilder();
    
    	builder
    		.UseMauiApp<App>()
    		.UseBarcodeReader(); // Make sure to add this line
    
    	return builder.Build();
    }
    ```

Now we just need to add the right permissions to our app metadata. Find below how to do that for each platform.

### Check Device Support

Before using barcode scanning, you can check if the device supports it (i.e., has a camera available):

```csharp
if (ZXing.Net.Maui.BarcodeScanning.IsSupported)
{
  // Device has a camera, safe to use barcode scanning
}
else
{
  // No camera available, show alternative UI or message
}
```

This is useful for handling devices without cameras gracefully, avoiding runtime exceptions.

#### Android

For Android go to your `AndroidManifest.xml` file (under the Platforms\Android folder) and add the following permissions inside of the `manifest` node:

```xml
<uses-permission android:name="android.permission.CAMERA" />
```

#### iOS

For iOS go to your `info.plist` file (under the Platforms\iOS folder) and add the following permissions inside of the `dict` node:

```xml
<key>NSCameraUsageDescription</key>
<string>This app uses barcode scanning to...</string>
```

Make sure that you enter a clear and valid reason for your app to access the camera. This description will be shown to the user.

#### Windows

Windows is not supported at this time for barcode scanning. You can however use the barcode generation. No extra permissions are required for that.

For more information on permissions, see the [Microsoft Docs](https://docs.microsoft.com/dotnet/maui/platform-integration/appmodel/permissions).

### Using ZXing.Net.Maui

If you're using the controls from XAML, make sure to add the right XML namespace in the root of your file, e.g: `xmlns:zxing="clr-namespace:ZXing.Net.Maui.Controls;assembly=ZXing.Net.MAUI.Controls"`

```xaml
<zxing:CameraBarcodeReaderView
  x:Name="cameraBarcodeReaderView"
  BarcodesDetected="BarcodesDetected" />
```

Configure Reader options
```csharp
cameraBarcodeReaderView.Options = new BarcodeReaderOptions
{
  Formats = BarcodeFormats.OneDimensional,
  AutoRotate = true,
  Multiple = true
};
```

QR codes with international characters (e.g., £, €, ¥, or non-Latin scripts) are supported by default with UTF-8 encoding. You can override this if needed:
```csharp
cameraBarcodeReaderView.Options = new BarcodeReaderOptions
{
  Formats = BarcodeFormats.TwoDimensional,
  CharacterSet = "ISO-8859-1"  // Override default UTF-8 if needed
};
```

Toggle Torch
```csharp
cameraBarcodeReaderView.IsTorchOn = !cameraBarcodeReaderView.IsTorchOn;
```

Flip between Rear/Front cameras
```csharp
cameraBarcodeReaderView.CameraLocation
  = cameraBarcodeReaderView.CameraLocation == CameraLocation.Rear ? CameraLocation.Front : CameraLocation.Rear;
```

Select a specific camera
```csharp
// Get available cameras
var cameras = await cameraBarcodeReaderView.GetAvailableCameras();

// Select a specific camera by setting the SelectedCamera property
if (cameras.Count > 0)
{
  cameraBarcodeReaderView.SelectedCamera = cameras[0];
}

// Or loop through available cameras and select one by name
foreach (var camera in cameras)
{
  Console.WriteLine($"Camera: {camera.Name} ({camera.Location})");
  // Select the first rear camera found
  if (camera.Location == CameraLocation.Rear)
  {
    cameraBarcodeReaderView.SelectedCamera = camera;
    break;
  }
}
```

Handle detected barcode(s)
```csharp
protected void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
{
  foreach (var barcode in e.Results)
    Console.WriteLine($"Barcodes: {barcode.Format} -> {barcode.Value}");
}
```

## Barcode Generator View
```xaml
<zxing:BarcodeGeneratorView
  HeightRequest="100"
  WidthRequest="100"
  ForegroundColor="DarkBlue"
  Value="https://dotnet.microsoft.com"
  Format="QrCode"
  Margin="3" />
```

### Character Encoding

The `BarcodeGeneratorView` supports UTF-8 character encoding by default, which allows you to encode international characters including Chinese, Japanese, Arabic, and other non-ASCII characters. You can also specify a different character encoding if needed:

```xaml
<zxing:BarcodeGeneratorView
  HeightRequest="100"
  WidthRequest="100"
  Value="测试中文 Test UTF-8"
  Format="QrCode"
  CharacterSet="UTF-8" />
```

The `CharacterSet` property defaults to "UTF-8" if not specified. Other common values include "ISO-8859-1", "Shift_JIS", etc., depending on your barcode format requirements.





