using Microsoft.Win32;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Implementations;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MQTTTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {

        IMqttClient client;
        MqttClientOptions options;
        private string topic;
        private static MqttServer server;
        private bool MqttReconnect;
        private static string UserName;
        private static string Password;
        private bool Logging;
        private string time;
        private static IPAddress ipAddress;
        private bool holded;

        //MqttClient mqttClient;
        public MainWindow()
        {
            InitializeComponent();
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            ipAddress = ipHostInfo.AddressList[15];
            cmbIPList.ItemsSource = ipHostInfo.AddressList;
            cmbIPList.Items.Filter = (item) =>
            {
                if(((IPAddress)item).AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return true;
                return false;
            };
        }
        public static async Task Run_Server(IPAddress ipAdress, int port, bool UseLogin, string _UserName, string _Password)
        {

            var serverOptions = new MqttServerOptionsBuilder()
                                    .WithDefaultEndpointBoundIPAddress(ipAdress)
                                    .WithDefaultEndpointBoundIPV6Address(IPAddress.None)
                                    .WithDefaultEndpoint()
                                    .WithDefaultEndpointPort(port);
            //.WithApplicationMessageInterceptor(OnNewMessage);//
            
               
            server = new MqttFactory().CreateMqttServer(serverOptions.Build());
            if (UseLogin)
            {
                // Setup connection validation before starting the server so that there is 
                // no change to connect without valid credentials.
                UserName = _UserName;
                Password = _Password;
                server.ValidatingConnectionAsync -= Server_ValidatingConnectionAsync;
                server.ValidatingConnectionAsync += Server_ValidatingConnectionAsync;
            }
            
            await server.StartAsync();
        }

        private static Task Server_ValidatingConnectionAsync(ValidatingConnectionEventArgs arg)
        {
            if (arg.UserName != UserName)
            {
                arg.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            }
            
            if (arg.Password != Password)
            {
                arg.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            }

            return Task.CompletedTask;
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            MqttReconnect = false;
            if (client != null && client.IsConnected)
            {
                await client.DisconnectAsync();
                client.Dispose();
            }
            if (server != null && server.IsStarted)
            {
                await server.StopAsync();
                server.Dispose();
            }
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            MqttReconnect = false;
            if (chkCreateServer.IsChecked == true)
                _ = Run_Server((IPAddress)cmbIPList.SelectedItem,Convert.ToInt16(txtBrokerPort.Text),(bool)chkUseLogin.IsChecked,txtUserName.Text,txtPasword.Password);
            topic = $"{txtClientId.Text}/{txtTopic.Text}";
            if (client != null && client.IsConnected)
            {
                await client.DisconnectAsync(MqttClientDisconnectReason.NormalDisconnection);//,"New connection");
                client.Dispose();
            }
            await Connect();
        }
        async Task Connect()
        {
            //var server = "test.mosquitto.org";
            //server = "broker.hivemq.com";
            var serveradress = (bool)chkCreateServer.IsChecked ? cmbIPList.SelectedItem.ToString() : txtBrokerAdress.Text;
            var serverport = Convert.ToInt16(txtBrokerPort.Text);
            var mqttFactory = new MqttFactory();
            client = mqttFactory.CreateMqttClient();
            var tlsoption = new MqttClientOptionsBuilderTlsParameters
            {
                SslProtocol = (bool)isEncrypted.IsChecked ? System.Security.Authentication.SslProtocols.Ssl3 : System.Security.Authentication.SslProtocols.None
            };
            var t_options = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer(serveradress, serverport)
                .WithTls(tlsoption)
                .WithCleanSession();
            if ((bool)chkUseLogin.IsChecked)
                t_options.WithCredentials(txtUserName.Text,txtPasword.Password);
            options=t_options.Build();
            client.ConnectedAsync -= Client_ConnectedAsync;
            client.DisconnectedAsync -= Client_DisconnectedAsync;
            client.ApplicationMessageReceivedAsync -= Client_ApplicationMessageReceivedAsync;
            client.ConnectedAsync += Client_ConnectedAsync;
            client.DisconnectedAsync += Client_DisconnectedAsync;
            client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;


            try
            {
                await client.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor=ConsoleColor.Red;
                WriteLog("Connection error with " + ex.Message);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Dispatcher.Invoke(delegate
                {              // we need this construction because the receiving code in the library and the UI with textbox run on different threads
                    MQTTnet.Client.MqttClientOptions options = client.Options;
                    listBox1.Items.Add("Connection Error with " + ex.Message + "on Port : " + ((MqttClientTcpOptions)options.ChannelOptions).Port + " Protocol Version : " + options.ProtocolVersion);
                    listBox1.ScrollIntoView(listBox1.Items[listBox1.Items.Count - 1]);
                });
            }
            var msg = "connect,server=" + serveradress + ",port=" + serverport.ToString();
            WriteLog(msg);
        }

        private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var msg = $"received: {Encoding.UTF8.GetString(arg.ApplicationMessage.Payload)} from Topic {arg.ApplicationMessage.Topic}";
            Console.WriteLine(msg);
            var message = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
            Dispatcher.Invoke(delegate
            {              // we need this construction because the receiving code in the library and the UI with textbox run on different threads
                listBox1.Items.Add($"Message: ( {message} ) from Topic ( {arg.ApplicationMessage.Topic} ) at {DateTime.Now}");

                listBox1.ScrollIntoView(listBox1.Items[listBox1.Items.Count - 1]);
            });
            if (message.Substring(0, 1) == "{" && message.Substring(message.Length - 1) == "}")
            {
                try
                {
                    Dictionary<string, JsonElement> user = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);
                    if (user.ContainsKey("Status") && user["Status"].GetString() == "Connected" && user["UseMAC"].GetString() == "True")
                    {


                        Dispatcher.Invoke(delegate
                        {              // we need this construction because the receiving code in the library and the UI with textbox run on different threads
                            Subscribe($"{txtClientId.Text}/{user["MAC"].GetString()}/{txtTopic.Text}");
                            if (!comboBox1.Items.Contains(user["MAC"].GetString())) comboBox1.Items.Add(user["MAC"].GetString());
                            if (!comboBox2.Items.Contains(user["MAC"].GetString())) comboBox2.Items.Add(user["MAC"].GetString());
                            listBox1.Items.Add($"Topic Subscribed : {txtClientId.Text}/{user["MAC"].GetString()}/{txtTopic.Text} at {DateTime.Now}");
                            listBox1.ScrollIntoView(listBox1.Items[listBox1.Items.Count - 1]);
                        });
                    }
                    else
                    {

                        Dispatcher.Invoke(delegate
                        {              // we need this construction because the receiving code in the library and the UI with textbox run on different threads

                            if (!holded)
                            {
                                if (comboBox1.SelectedItem != null && arg.ApplicationMessage.Topic == $"{txtClientId.Text}/{comboBox1.SelectedItem}/{txtTopic.Text}")
                                {
                                    lblValue1.Content = user["ValueS"].GetString();
                                    lblSymbol1.Content = user["Range"].GetString();
                                    lblACDC1.Content = user["Current"].GetString();
                                }
                                if (comboBox2.SelectedItem != null && arg.ApplicationMessage.Topic == $"{txtClientId.Text}/{comboBox2.SelectedItem}/{txtTopic.Text}")
                                {
                                    lblValue2.Content = user["ValueS"].GetString();
                                    lblSymbol2.Content = user["Range"].GetString();
                                    lblACDC2.Content = user["Current"].GetString();
                                } 
                            }
                        });
                        if (Logging)
                            File.AppendAllText("Log_" + time + ".csv", $"{user["Time"].GetString()}, {user["ValueS"].GetString()} , {user["Range"].GetString()}" + System.Environment.NewLine,Encoding.Default);
                    }
                }
                catch (Exception ex)
                { Console.WriteLine(ex.Message); }
            }

            Debug.WriteLine(msg);
            
            return Task.CompletedTask;
        }

        private Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            var msg = "Disconnected from broker!";
            WriteLog(msg);
            Task.Delay(25);
            if (MqttReconnect)
                client.ConnectAsync(options);
            return Task.CompletedTask;
        }

        private Task Client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            MqttReconnect = true;
            var msg = "connected to the broker!";
            WriteLog(msg);
            Dispatcher.Invoke(delegate
            {              // we need this construction because the receiving code in the library and the UI with textbox run on different threads
                if (client.IsConnected)
                {
                    MQTTnet.Client.MqttClientOptions options = client.Options;
                    listBox1.Items.Add("Connection Port : " + ((MQTTnet.Client.MqttClientTcpOptions)options.ChannelOptions).Port + " Protocol Version : " + options.ProtocolVersion + " SSL version : " + ((MQTTnet.Client.MqttClientTcpOptions)options.ChannelOptions).TlsOptions.SslProtocol);
                    listBox1.ScrollIntoView(listBox1.Items[listBox1.Items.Count - 1]);
                }
            });
            Subscribe(topic);
            return Task.CompletedTask;
        }

        void Subscribe(string stopic)
        {
            var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic(stopic+ "/#")
                    .Build();
            client.SubscribeAsync(topicFilter);
            var subscribeMsg = "Subscribed topic=" + stopic;
            WriteLog(subscribeMsg);
        }

        void WriteLog(string msg)
        {
            Console.WriteLine(msg);
        }

        private void btnPublish_Click(object sender, RoutedEventArgs e)
        {
            var msg = "{\"Publish\": \"Test\"}";
            _ = Publish(msg);

        }

        async Task Publish(string msg)
        {

            //var userId = txtUser.Text;
            var topic = $"BT_DMM/Values";
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(msg)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            if (client.IsConnected)
            {
                await client.PublishAsync(message);
            }
            var doneMsg = "Message Published:Topic=" + topic + ",Message=" + msg;
            WriteLog(doneMsg);
        }
        async Task RunAsync()
        {
            // MqttNetConsoleLogger.ForwardToConsole();

            // For most of these connections to work, set output target to Net5.0.            

#if NET5_0_OR_GREATER
            // TLS13 is only available in Net5.0
            var unsafeTls13 = new MqttClientOptionsBuilderTlsParameters
            {
                UseTls = true,
                SslProtocol = SslProtocols.Tls13,
                // Don't use this in production code. This handler simply allows any invalid certificate to work.
                CertificateValidationHandler = w => true
            };
#endif

            // Also defining TLS12 for servers that don't seem no to support TLS13.
            var unsafeTls12 = new MqttClientOptionsBuilderTlsParameters
            {
                UseTls = true,
                SslProtocol = SslProtocols.Tls12,
                // Don't use this in production code. This handler simply allows any invalid certificate to work.
                CertificateValidationHandler = w => true
            };

            // mqtt.eclipseprojects.io
            await ExecuteTestAsync("mqtt.eclipseprojects.io TCP",
                    new MqttClientOptionsBuilder().WithTcpServer("mqtt.eclipseprojects.io", 1883)
                        .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("mqtt.eclipseprojects.io WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("mqtt.eclipseprojects.io:80/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

#if NET5_0_OR_GREATER
            await ExecuteTestAsync("mqtt.eclipseprojects.io WS TLS13",
                new MqttClientOptionsBuilder().WithWebSocketServer("mqtt.eclipseprojects.io:443/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls13).Build());
#endif

            // test.mosquitto.org
            await ExecuteTestAsync("test.mosquitto.org TCP",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 1883)
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("test.mosquitto.org TCP - Authenticated",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 1884)
                    .WithCredentials("rw", "readwrite")
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("test.mosquitto.org TCP TLS12",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 8883)
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls12).Build());

#if NET5_0_OR_GREATER
            await ExecuteTestAsync("test.mosquitto.org TCP TLS13",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 8883)
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls13).Build());
#endif

            await ExecuteTestAsync("test.mosquitto.org TCP TLS12 - Authenticated",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 8885)
                    .WithCredentials("rw", "readwrite")
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls12).Build());

            await ExecuteTestAsync("test.mosquitto.org WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("test.mosquitto.org:8080/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("test.mosquitto.org WS TLS12",
                new MqttClientOptionsBuilder().WithWebSocketServer("test.mosquitto.org:8081/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls12).Build());

            // broker.emqx.io
            await ExecuteTestAsync("broker.emqx.io TCP",
                new MqttClientOptionsBuilder().WithTcpServer("broker.emqx.io", 1883)
                     .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("broker.emqx.io TCP TLS12",
                new MqttClientOptionsBuilder().WithTcpServer("broker.emqx.io", 8883)
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls12).Build());

#if NET5_0_OR_GREATER
            await ExecuteTestAsync("broker.emqx.io TCP TLS13",
                new MqttClientOptionsBuilder().WithTcpServer("broker.emqx.io", 8883)
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls13).Build());
#endif

            await ExecuteTestAsync("broker.emqx.io WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("broker.emqx.io:8083/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("broker.emqx.io WS TLS12",
                new MqttClientOptionsBuilder().WithWebSocketServer("broker.emqx.io:8084/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).WithTls(unsafeTls12).Build());


            // broker.hivemq.com
            await ExecuteTestAsync("broker.hivemq.com TCP",
                new MqttClientOptionsBuilder().WithTcpServer("broker.hivemq.com", 1883)
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("broker.hivemq.com WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("broker.hivemq.com:8000/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V311).Build());

            // mqtt.swifitch.cz: Does not seem to operate any more

            // cloudmqtt.com: Cannot test because it does not offer a free plan any more.

            Write("Finished.", ConsoleColor.White);
        }

        async Task ExecuteTestAsync(string name, MqttClientOptions options)
        {
            try
            {
                Write("Testing '" + name + "'... ", ConsoleColor.Gray);
                var factory = new MqttFactory();
                //factory.UseWebSocket4Net();
                var client = factory.CreateMqttClient();
                var topic = Guid.NewGuid().ToString();

                MqttApplicationMessage receivedMessage = null;
                client.ApplicationMessageReceivedAsync += e =>
                {
                    receivedMessage = e.ApplicationMessage;
                    return PlatformAbstractionLayer.CompletedTask;
                };

                await client.ConnectAsync(options);
                await client.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);
                await client.PublishStringAsync(topic, "Hello_World", MqttQualityOfServiceLevel.AtLeastOnce);

                SpinWait.SpinUntil(() => receivedMessage != null, 5000);

                if (receivedMessage?.Topic != topic || receivedMessage?.ConvertPayloadToString() != "Hello_World")
                {
                    throw new Exception("Message invalid.");
                }

                await client.UnsubscribeAsync(topic);
                await client.DisconnectAsync();

                Write("[OK]", ConsoleColor.Green);
            }
            catch (Exception e)
            {
                Write("[FAILED] " + e.Message, ConsoleColor.Red);
            }
        }

        private void Write(string message, ConsoleColor color)
        {
            Dispatcher.Invoke(delegate
            {              // we need this construction because the receiving code in the library and the UI with textbox run on different threads
                listBox1.Items.Add(message);
                listBox1.ScrollIntoView(listBox1.Items[listBox1.Items.Count - 1]);
            });
        }

        private async void btnTestGlobal_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            Logging = !Logging;
            if (Logging)
            {
                time = DateTime.Now.ToString("dd-MM-yyyy_HH_mm_ss");
                File.AppendAllText("Log_" + time + ".csv", "Time, Value, Range" + System.Environment.NewLine, Encoding.Default);
                btnLog.Content = "Stop Loging";
                txtLog.Text = "Logging started...";
                txtLog.Background = new SolidColorBrush(Colors.GreenYellow);
                txtLog.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                btnLog.Content = "Start Loging";
                txtLog.Text = "Logging stopped...";
                txtLog.Background = new SolidColorBrush(Colors.Red);
                txtLog.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void btnHold_Click(object sender, RoutedEventArgs e)
        {
            holded = !holded;
            btnHold.Content = holded ? "UN-HOLD" : "HOLD";
        }

        private void btnPick_Click(object sender, RoutedEventArgs e)
        {
            DataItem item = new DataItem
            {
                ID = grdData.Items.Count,
                Value1 = lblValue1.Content.ToString(),
                Value2 = lblValue2.Content.ToString(),
                Symbol1 = lblSymbol1.Content.ToString(),
                Symbol2 = lblSymbol2.Content.ToString()
            };
            grdData.Items.Add(item);

        }

        private void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            grdSettings.Visibility = grdSettings.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            grdDatas.Visibility = grdSettings.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            btnToggle.Content = grdSettings.Visibility == Visibility.Visible ? "View Datas" : "View Settings";
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                // Create the CSV file to which grid data will be exported.  
                StreamWriter sw = new StreamWriter(new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write));


                var sb = new StringBuilder();
                string delimiter = ",";
                var headers = grdData.Columns.Cast<DataGridColumn>();
                sb.AppendLine(string.Join(delimiter, headers.Select(column => "\"" + column.Header.ToString() + "\"").ToArray()));

                foreach (DataItem row in grdData.Items)
                {
                    Console.WriteLine(row);
                    //var cells = row.Cells.Cast<DataGridViewCell>();
                    sb.AppendLine(row.ToString());
                }
                //return sb.ToString();
                sw.Write(sb.ToString());
                sw.Close();
            }
        }
    }
    public class DataItem
    {
        public int ID { get; set; }
        public string Value1 { get; set; }
        public string Symbol1 { get; set; }
        public string Value2 { get; set; }
        public string Symbol2 { get; set; }
        public override string ToString()
        {
            return ID + "," + Value1 + "," + Symbol1 + "," + Value2 + "," + Symbol2;
        }
    }
}
