using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TexiService;

namespace TexiServiceClient
{
    public partial class MainWindow : Window
    {
        private SerializableEmployee employee;
        private Rectangle recTexiPosition;
        private Image imgEmployeePosition;
        private Socket socket;
        private IPEndPoint endPoint;
        private LayoutSize size;
        private BackgroundWorker bw;
        private Timer timer;
        private Dispatcher dispatcher;

        public MainWindow()
        {
            this.dispatcher = Application.Current.Dispatcher;
            this.bw = new BackgroundWorker();

            this.bw.DoWork += (o, args) =>
            {
                this.SetupSocket(9999);
                this.GetLayoutSize();
                this.SendEmployeeDataToServer();
                this.SetupLayoutGrid();
                this.SetupListener();
            };

            this.InitializeComponent();

            this.bw.RunWorkerAsync();
        }

        private void SetupListener()
        {
            this.bw = new BackgroundWorker();

            this.bw.DoWork += (o, arg) =>
            {
                while(true)
                {
                    // If employee has reached his destination clear the texi rectangle.
                    if(this.employee.Destination != null)
                        if(this.employee.Destination.SameAs(this.employee.Location))
                        {
                            this.dispatcher?.Invoke(() =>
                                {
                                    this.recTexiPosition.Visibility = Visibility.Hidden;
                                    Application.Current.Dispatcher.Invoke(() => this.tbTexiLoc.Text = string.Empty);
                                    Application.Current.Dispatcher.Invoke(() => this.tbTexiStat.Text = string.Empty);
                                });
                        }

                    try
                    {
                        // Receive the data.
                        var data = this.ReceiveData();

                        // Continue according to data type.
                        if(data.GetType() == typeof(string))
                        {
                            // Hide the texi rectanlge in the layout view.
                            if((string)data == "Destination reached.")
                                this.dispatcher.Invoke(() => this.recTexiPosition.Visibility = Visibility.Hidden);
                        }
                        // Update employee position and data.
                        else if(data.GetType() == typeof(SerializableEmployee))
                        {
                            Location location = (data as SerializableEmployee).Location;

                            this.employee.Location = location;

                            Application.Current.Dispatcher.Invoke(() => Grid.SetRow(this.imgEmployeePosition, (location.Row)));
                            Application.Current.Dispatcher.Invoke(() => Grid.SetColumn(this.imgEmployeePosition, (location.Col)));
                        }
                        // Update texi position and data.
                        else if(data.GetType() == typeof(SerializableTexi))
                        {
                            this.dispatcher?.Invoke(() => this.recTexiPosition.Visibility = Visibility.Visible);

                            Location location = (data as SerializableTexi).Location;

                            Application.Current.Dispatcher.Invoke(() => Grid.SetRow(this.recTexiPosition, (location.Row)));
                            Application.Current.Dispatcher.Invoke(() => Grid.SetColumn(this.recTexiPosition, (location.Col)));
                            Application.Current.Dispatcher.Invoke(() => this.tbTexiLoc.Text = location.ToString());
                            Application.Current.Dispatcher.Invoke(() => this.tbTexiStat.Text = (data as SerializableTexi).Status.ToString());
                        }
                    }
                    catch(NullReferenceException)
                    {
                        // No data received from server.
                        this.ShowErrorMessage("Server connection error.");
                    }
                }
            };

            this.bw.RunWorkerAsync();
        }
        private void ShowErrorMessage(string msg)
        {
            this.dispatcher.Invoke(() =>
            {
                this.tbDest.Text = msg;

                this.tbDest.Visibility = Visibility.Visible;
                this.timer = new Timer(s => this.dispatcher.Invoke(() => this.tbDest.Visibility = Visibility.Hidden), null, 2000, Timeout.Infinite);
            });
        }
        private void SetupSocket(int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            this.endPoint = new IPEndPoint(ipAddress, port);

            this.socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Attampt to connect to server.
            //  If server does not responde wait 5 seconds and try again.
            do
            {
                try
                {
                    this.socket.Connect(ipAddress, port);
                }
                // Server not responding.
                catch(SocketException)
                {
                    int seconds = 5;
                    int s = seconds;

                    this.dispatcher.Invoke(() => this.tbError.Visibility = Visibility.Visible);

                    for(int i = 0; i <= s; i++)
                    {
                        this.dispatcher.Invoke(() => this.tbError.Text = $"Unable to connect to server, attampting to reconnect in {seconds--}");

                        Thread.Sleep(1000);
                    }

                    this.dispatcher.Invoke(() => this.tbError.Visibility = Visibility.Hidden);
                }
            } while(!this.socket.Connected);
        }
        private void GetLayoutSize()
        {
            if(this.socket.Connected) // Socket connection check.
            {
                this.socket.Send(this.SerializeData("Send layout size please."));
                this.size = (LayoutSize)this.ReceiveData();
            }
        }
        private void SendEmployeeDataToServer()
        {
            // Create a rendom employee.
            this.employee = RandomGenerator.SerializableEmployee(this.size);

            // Set the data context of the employeee info grid.
            this.dispatcher.Invoke(() => this.employeeInfo.DataContext = this.employee);

            // Send the employee info to the server.
            this.socket.Send(this.SerializeData(this.employee));
        }
        private void SetupLayoutGrid()
        {
            Rectangle gridRec;

            // If size is null (no connection to server) set grid to be 10 x 10
            int limit = this.size?.Row ?? 10;

            this.dispatcher.Invoke(() =>
            {
                this.recTexiPosition = new Rectangle
                {
                    Fill = new SolidColorBrush(Colors.Yellow),
                    Visibility = Visibility.Hidden
                }; ;
                this.imgEmployeePosition = new Image() { Source = new BitmapImage(new Uri(@"C:\Users\Geco\Documents\Visual Studio 2017\Projects\Sudoku\Sudoku\Img\Employee.png")) };

                Panel.SetZIndex(this.recTexiPosition, -1);

                // Build rows and columns.
                for(int i = 1; i < limit; i++)
                {
                    // Set employee icon row.
                    if(this.employee != null)
                        if(i == this.employee.Location.Row) Grid.SetRow(this.imgEmployeePosition, i);

                    for(int j = 1; j < limit; j++)
                    {
                        // Set employee icon column.
                        if(this.employee != null)
                            if(j == this.employee.Location.Col) Grid.SetColumn(this.imgEmployeePosition, j);

                        RowDefinition rd = new RowDefinition();
                        ColumnDefinition cd = new ColumnDefinition();
                        rd.Height = new GridLength(10);
                        cd.Width = new GridLength(10);

                        gridRec = new Rectangle
                        {
                            Stroke = new SolidColorBrush(Colors.Black),
                            StrokeThickness = 1
                        };
                        Panel.SetZIndex(gridRec, -1);
                        Grid.SetRow(gridRec, i);
                        Grid.SetColumn(gridRec, j);

                        this.layoutGrid.Children.Add(gridRec);
                        this.layoutGrid.RowDefinitions.Add(rd);
                        this.layoutGrid.ColumnDefinitions.Add(cd);
                    }
                }

                this.layoutGrid.Children.Add(this.recTexiPosition); // Add texi position to layout.
                this.layoutGrid.Children.Add(this.imgEmployeePosition); // Add employee position to layout.
            });
        }
        private object ReceiveData()
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms;
            byte[] data = new byte[8192];
            int len;

            // Receive the data.
            try
            {
                len = this.socket.Receive(data);
                Array.Resize(ref data, len);
            }
            catch(ArgumentNullException exp)
            {
                // TODO
            }
            catch(SocketException exp)
            {
                // TODO
            }
            catch(ObjectDisposedException exp)
            {
                // TODO
            }
            catch(SecurityException exp)
            {
                // TODO
            }
            catch(ArgumentOutOfRangeException exp)
            {
                // TODO
            }

            // Using a MemoryStram and binaryFormatter deserialize the data.
            using(ms = new MemoryStream(data))
            {
                try
                {
                    return bf.Deserialize(ms);
                }
                catch(ArgumentNullException exp)
                {
                    // TODO
                }
                catch(SerializationException exp)
                {
                    // TODO
                }
                catch(SecurityException exp)
                {
                    // TODO
                }
            }

            return null;
        }
        private byte[] SerializeData(object data)
        {
            BinaryFormatter bf = new BinaryFormatter();

            using(MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, data);
                return ms.ToArray();
            }
        }

        private void CallTexi(object sender, RoutedEventArgs e)
        {
            try
            {
                int row = int.Parse(this.tbRow.Text);
                int col = int.Parse(this.tbCol.Text);
                Location destination = new Location(row, col);

                this.socket.Send(this.SerializeData(destination));

                this.employee.Destination = destination;
                this.employee.Status = EmployeeStatus.Waiting;
            }
            catch(Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }
    }
}
