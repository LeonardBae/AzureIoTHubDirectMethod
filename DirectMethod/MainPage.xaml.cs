using System;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Gpio;
using Microsoft.Azure.Devices.Client;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Shared;
using System.Text;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DirectMethod
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const int LEDPinNumber = 5;
        private static GpioPin pin;
        private static GpioPinValue pinValue;
        private DeviceClient deviceClient;

        public MainPage()
        {
            this.InitializeComponent();
            InitGPIO();
            deviceClient = DeviceClient.CreateFromConnectionString("HostName=mdsrmsolutions.azure-devices.net;DeviceId=rpmethod;SharedAccessKey=vKZ/g7AmG229c2n/xR6OU/xZhAqSW676seVORpIfpZk=", TransportType.Mqtt);
            var method = deviceClient.SetMethodHandlerAsync("TurnOnLight", TurnOnTheLight, null);
            method = deviceClient.SetMethodHandlerAsync("TurnOffLight", TurnOffTheLight, null);
            Twin reportedProperties = new Twin("rpmethod");
            TwinCollection reported = reportedProperties.Properties.Reported;
            {
                reported["SupportedMethods"] = new TwinCollection();
                {
                    var SupportedMethods = reported["SupportedMethods"];
                    SupportedMethods["TurnOnLight"] = "Turn on the light.";
                    SupportedMethods["TurnOffLight"] = "Turn off the light.";
                }
            }
            deviceClient.UpdateReportedPropertiesAsync(reported);
        }
        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pin = null;
                return;
            }

            pin = gpio.OpenPin(LEDPinNumber);
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            pin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private static MethodResponse BuildMethodRespose(object response, int status = 200)
        {
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), status);
        }
        private static async Task<MethodResponse> TurnOnTheLight(MethodRequest request, object userContext)
        {
            // Implement actual logic here.
            Console.WriteLine("Simulated turn on the light...");
            pinValue = GpioPinValue.Low;
            pin.Write(pinValue);
            // Complete the response
            return await Task.FromResult(BuildMethodRespose("\"Turned light on\""));
        }

        private static async Task<MethodResponse> TurnOffTheLight(MethodRequest request, object userContext)
        {
            // Implement actual logic here.
            Console.WriteLine("Simulated turn off the light...");
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            // Complete the response
            return await Task.FromResult(BuildMethodRespose("\"Turned light off\""));
        }
        
    }
}
