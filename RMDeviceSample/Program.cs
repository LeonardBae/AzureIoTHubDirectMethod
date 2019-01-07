using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System.Dynamic;
using System.Threading;

namespace RMDeviceSample
{
    public class RemoteMonitorTelemetryDataClass
    {
        public string DeviceId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }

    public class RemoteMonitorTelemetryMetaClass
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
    }

    public class SystemPropertiesClass
    {
        public string Manufacturer { get; set; }
        public string FirmwareVersion { get; set; }
        public string InstalledRAM { get; set; }
        public string ModelNumber { get; set; }
        public string Platform { get; set; }
        public string Processor { get; set; }
        public string SerialNumber { get; set; }
    }

    public class LocaltionPropertiesClass
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class ReportedDevicePropertiesClass
    {
        public string DeviceState { get; set; }
        public LocaltionPropertiesClass Location { get; set; }
    }

    public class ConifgPropertiesClass
    {
        public double TemperatureMeanValue { get; set; }
        public double TelemetryInterval { get; set; }
    }

    public class DevicePropertiesClass
    {
        public string DeviceID { get; set; }
        public bool HubEnabledState { get; set; }
    }

    public class DeviceMetaDataClass
    {
        private string objectType = "DeviceInfo";
        public string ObjectType
        {
            get
            {
                return objectType;
            }

            set
            {
                objectType = value;
            }
        }

        private bool isSimulatedDevice = false;
        public bool IsSimulatedDevice
        {
            get
            {
                return isSimulatedDevice;
            }

            set
            {
                isSimulatedDevice = value;
            }
        }

        private string version = "1.0";
        public string Version
        {
            get
            {
                return version;
            }

            set
            {
                version = value;
            }
        }
        public DevicePropertiesClass DeviceProperties { get; set; }
        public List<RemoteMonitorTelemetryMetaClass> Telemetry { get; set; }
    }


    class Program
    {
        // String containing Hostname, Device Id & Device Key in one of the following formats:
        //  "HostName=<iothub_host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>"
        //  "HostName=<iothub_host_name>;CredentialType=SharedAccessSignature;DeviceId=<device_id>;SharedAccessSignature=SharedAccessSignature sr=<iot_host>/devices/<device_id>&sig=<token>&se=<expiry_time>";
        //private static string HostName = "<replace>";
        //private static string DeviceID = "<replace>";
        //private static string PrimaryAuthKey = "<replace>";
        private static string ObjectTypePrefix = "";// Replace with your prefix
        private static string HostName = "";
        private static string DeviceID = "";
        private static string PrimaryAuthKey = "";

        private static DeviceClient Client = null;
        private static CancellationTokenSource cts = new CancellationTokenSource();

        private static bool TelemetryActive = true;
        private static uint _telemetryIntervalInSeconds = 15;
        public static uint TelemetryIntervalInSeconds
        {
            get
            {
                return _telemetryIntervalInSeconds;
            }
            set
            {
                _telemetryIntervalInSeconds = value;
                TelemetryActive = _telemetryIntervalInSeconds > 0;
            }
        }

        private static Random rnd = new Random();

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine("desired property change:");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

            Console.WriteLine("Sending current time as reported property");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now;

            await Client.UpdateReportedPropertiesAsync(reportedProperties);
        }


        private static async Task<MethodResponse> TurnOnTheLight(MethodRequest request, object userContext)
        {
            // Implement actual logic here.
            Console.WriteLine("Simulated turn on the light...");

            // Complete the response
            return await Task.FromResult(BuildMethodRespose("\"Turned light on\""));
        }

        private static async Task<MethodResponse> TurnOffTheLight(MethodRequest request, object userContext)
        {
            // Implement actual logic here.
            Console.WriteLine("Simulated turn off the light...");

            // Complete the response
            return await Task.FromResult(BuildMethodRespose("\"Turned light off\""));
        }

        private static MethodResponse BuildMethodRespose(object response, int status = 200)
        {
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), status);
        }

        private static async Task SendDeviceInfoAsync(CancellationToken token, Func<object, Task> sendMessageAsync)
        {
            DeviceMetaDataClass device = new DeviceMetaDataClass();
            // Device basic properties
            device.DeviceProperties = new DevicePropertiesClass()
            {
                DeviceID = DeviceID,
                HubEnabledState = true
            };            
            device.IsSimulatedDevice = false;
            device.Version = "1.0";
            device.ObjectType = "DeviceInfo";

            // Telemery data descriptor
            device.Telemetry = new List<RemoteMonitorTelemetryMetaClass>();
            device.Telemetry.Add(new RemoteMonitorTelemetryMetaClass (){ Name = "Temperature", DisplayName = "Temperature", Type = "double" });
            device.Telemetry.Add(new RemoteMonitorTelemetryMetaClass (){ Name = "Humidity", DisplayName = "Humidity", Type = "double" });

            if (!token.IsCancellationRequested)
            {
                await sendMessageAsync(device);
            }
        }

        public static async Task SendMonitorDataAsync(CancellationToken token, Func<object, Task> sendMessageAsync)
        {
            var monitorData = new RemoteMonitorTelemetryDataClass();
            while (!token.IsCancellationRequested)
            {
                if (TelemetryActive)
                {
                    // Build simlated telemerty data.
                    monitorData.DeviceId = DeviceID;
                    monitorData.Temperature = Math.Round(rnd.NextDouble() * 100, 1);
                    monitorData.Humidity = Math.Round(rnd.NextDouble() * 100, 1);

                    await sendMessageAsync(monitorData);
                }
                await Task.Delay(TimeSpan.FromSeconds(TelemetryIntervalInSeconds), token);
            }
        }

        public static async Task SendEventAsync(Guid eventId, dynamic eventData)
        {
            string objectType = GetObjectType(eventData);
            if (!string.IsNullOrWhiteSpace(objectType) && !string.IsNullOrEmpty(ObjectTypePrefix))
            {
                eventData.ObjectType = ObjectTypePrefix + objectType;
            }

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventData));
            var message = new Microsoft.Azure.Devices.Client.Message(bytes);
            message.Properties["EventId"] = eventId.ToString();

            if (Client != null)
            {
                try
                {
                    await Client.SendEventAsync(message);
                }
                catch (Exception ex)
                {
                    Console.Write($"Exception raised while device {DeviceID} trying to send events: {ex.Message}");
                }

            }
        }

        private static string GetObjectType(dynamic eventData)
        {
            if (eventData == null)
            {
                throw new ArgumentNullException("eventData");
            }

            var propertyInfo = eventData.GetType().GetProperty("ObjectType");
            if (propertyInfo == null)
            {
                return string.Empty;
            }

            var value = propertyInfo.GetValue(eventData, null);
            return value == null ? string.Empty : value.ToString();
        }

        static void Main(string[] args)
        {
            string DeviceConnectionString;
            string environmentConnectionString = Environment.GetEnvironmentVariable("");
            if (!String.IsNullOrEmpty(environmentConnectionString))
            {
                DeviceConnectionString = environmentConnectionString;
            }
            else
            {
                var authMethod = new Microsoft.Azure.Devices.Client.DeviceAuthenticationWithRegistrySymmetricKey(DeviceID, PrimaryAuthKey);
                DeviceConnectionString = Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder.Create(HostName, authMethod).ToString();
            }

            try
            {
                Console.WriteLine("Checking for TransportType");
                var websiteHostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
                TransportType transportType = websiteHostName == null ? TransportType.Mqtt : TransportType.Mqtt_WebSocket_Only;
                Console.WriteLine($"Use TransportType: {transportType.ToString()}");

                Console.WriteLine("Connecting to hub");
                Client = DeviceClient.CreateFromConnectionString(DeviceConnectionString, transportType);
                Client.SetDesiredPropertyUpdateCallback(OnDesiredPropertyChanged, null).Wait();
                // Register handlers for direct methods
                var method = Client.SetMethodHandlerAsync("TurnOnLight", TurnOnTheLight, null);
                method = Client.SetMethodHandlerAsync("TurnOffLight", TurnOffTheLight, null);

                Console.WriteLine("Send reported properties to IoT Hub");
                Twin reportedProperties = new Twin(DeviceID);
                TwinCollection reported = reportedProperties.Properties.Reported;
                {
                    reported["Device"] = new ReportedDevicePropertiesClass()
                    {
                        DeviceState = "normal",
                        Location = new LocaltionPropertiesClass()
                        {
                            Latitude = 47.659159,
                            Longitude = -122.141515
                        }
                    };
                    reported["Conifg"] = new ConifgPropertiesClass()
                    {
                        TelemetryInterval = 45,
                        TemperatureMeanValue = 56.7,
                    };
                    reported["System"] = new SystemPropertiesClass()
                    {
                        Manufacturer = "Contoso Inc.",
                        FirmwareVersion = "2.22",
                        InstalledRAM = "8 MB",
                        ModelNumber = "DB-14",
                        Platform = "Plat 9.75",
                        Processor = "i3-9",
                        SerialNumber = "SER99"
                    };
                    reported["Location"] = new LocaltionPropertiesClass()
                    {
                        Latitude = 47.659159,
                        Longitude = -122.141515
                    };
                    reported["SupportedMethods"] = new TwinCollection();
                    {
                        var SupportedMethods = reported["SupportedMethods"];
                        SupportedMethods["TurnOnLight"] = "Turn on the light.";
                        SupportedMethods["TurnOffLight"] = "Turn off the light.";
                        //SupportedMethods["InitiateFirmwareUpdate--FwPackageURI-string"] = "Updates device Firmware. Use parameter FwPackageURI to specifiy the URI of the firmware file";
                    }
                }
                Client.UpdateReportedPropertiesAsync(reported);

                Console.WriteLine("Sending device infomation to RM");
                var deviceTask = SendDeviceInfoAsync(cts.Token, async (object eventData) =>
                {
                    await SendEventAsync(Guid.NewGuid(), eventData);
                });
                deviceTask.Wait();

                var monitorTask = SendMonitorDataAsync(cts.Token, async (object eventData) =>
                {
                    await SendEventAsync(Guid.NewGuid(), eventData);
                });

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
            Console.WriteLine("Waiting for Events.  Press enter to exit...");

            Console.ReadLine();
            cts.Cancel();
            Console.WriteLine("Exiting...");

        }
    }
}
