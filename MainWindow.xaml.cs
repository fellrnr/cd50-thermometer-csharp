using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace TemperatureMonitor
{
    public partial class MainWindow : Window
    {
        private CD50Thermometer? _thermometer;
        private bool _isPolling;
        private CancellationTokenSource? _pollingCts;
        private ObservableCollection<double> _channel1Data = new();
        private ObservableCollection<double> _channel2Data = new();
        private ObservableCollection<double> _channel3Data = new();
        private ObservableCollection<double> _channel4Data = new();
        private const int MaxDataPoints = 120; // Keep 120 data points (2 minutes at 500ms intervals)

        public MainWindow()
        {
            InitializeComponent();
            InitializeChart();
        }

        private void InitializeChart()
        {
            var series = new ISeries[]
            {
                new LineSeries<double> 
                { 
                    Values = _channel1Data,
                    Stroke = new SolidColorPaint(SKColor.Parse("#E74C3C")) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(SKColor.Parse("#E74C3C")) { Alpha = 50 },
                    Name = "Channel 1",
                    GeometrySize = 0
                },
                new LineSeries<double> 
                { 
                    Values = _channel2Data,
                    Stroke = new SolidColorPaint(SKColor.Parse("#F39C12")) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(SKColor.Parse("#F39C12")) { Alpha = 50 },
                    Name = "Channel 2",
                    GeometrySize = 0
                },
                new LineSeries<double> 
                { 
                    Values = _channel3Data,
                    Stroke = new SolidColorPaint(SKColor.Parse("#27AE60")) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(SKColor.Parse("#27AE60")) { Alpha = 50 },
                    Name = "Channel 3",
                    GeometrySize = 0
                },
                new LineSeries<double> 
                { 
                    Values = _channel4Data,
                    Stroke = new SolidColorPaint(SKColor.Parse("#3498DB")) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(SKColor.Parse("#3498DB")) { Alpha = 50 },
                    Name = "Channel 4",
                    GeometrySize = 0
                }
            };

            var xAxes = new[] { new Axis { MaxLimit = MaxDataPoints } };
            var yAxes = new[] { new Axis { MinLimit = -50, MaxLimit = 150 } };

            TemperatureChart.Series = series;
            TemperatureChart.XAxes = xAxes;
            TemperatureChart.YAxes = yAxes;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string port = PortTextBox.Text.Trim();
                if (string.IsNullOrEmpty(port))
                {
                    MessageBox.Show("Please enter a COM port name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _thermometer = new CD50Thermometer(port);
                if (_thermometer.Connect())
                {
                    UpdateStatus("Connected", "#27AE60");
                    ConnectButton.IsEnabled = false;
                    DisconnectButton.IsEnabled = true;
                    StartButton.IsEnabled = true;
                    PortTextBox.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Disconnected", "#E74C3C");
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isPolling)
                {
                    PausePolling();
                }

                _thermometer?.Disconnect();
                _thermometer = null;

                UpdateStatus("Disconnected", "#E74C3C");
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                StartButton.IsEnabled = false;
                PauseButton.IsEnabled = false;
                PortTextBox.IsEnabled = true;

                ClearDisplays();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Disconnection error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_thermometer?.Connected != true)
            {
                MessageBox.Show("Connect to the device first!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isPolling = true;
            _pollingCts = new CancellationTokenSource();
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            ConnectButton.IsEnabled = false;

            _ = Task.Run(() => PollingLoop(_pollingCts.Token));
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            PausePolling();
        }

        private void PausePolling()
        {
            _isPolling = false;
            _pollingCts?.Cancel();
            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            ConnectButton.IsEnabled = true;
        }

        private async Task PollingLoop(CancellationToken cancellationToken)
        {
            while (_isPolling && !cancellationToken.IsCancellationRequested && _thermometer?.Connected == true)
            {
                try
                {
                    double[]? temperatures = _thermometer.ReadTemperatures();
                    if (temperatures != null && temperatures.Length == 4)
                    {
                        Dispatcher.Invoke(() => UpdateDisplays(temperatures));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
                }

                await Task.Delay(500, cancellationToken);
            }
        }

        private void UpdateDisplays(double[] temperatures)
        {
            // Update digital displays
            Channel1Text.Text = FormatTemperature(temperatures[0]);
            Channel2Text.Text = FormatTemperature(temperatures[1]);
            Channel3Text.Text = FormatTemperature(temperatures[2]);
            Channel4Text.Text = FormatTemperature(temperatures[3]);

            // Add to graph data (keep only MaxDataPoints)
            AddDataPoint(_channel1Data, temperatures[0]);
            AddDataPoint(_channel2Data, temperatures[1]);
            AddDataPoint(_channel3Data, temperatures[2]);
            AddDataPoint(_channel4Data, temperatures[3]);

            // Update timestamp
            UpdateTimestamp.Text = $"Last update: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private void AddDataPoint(ObservableCollection<double> collection, double value)
        {
            collection.Add(value);
            if (collection.Count > MaxDataPoints)
            {
                collection.RemoveAt(0);
            }
        }

        private string FormatTemperature(double temp)
        {
            if (temp > 280)
                return "---.-°C"; // Sensor error or not connected
            return $"{temp:F1}°C";
        }

        private void UpdateStatus(string status, string color)
        {
            StatusTextBlock.Text = status;
            // Parse color hex and set foreground
            if (color == "#27AE60")
                StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60));
            else
                StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C));
        }

        private void ClearDisplays()
        {
            Channel1Text.Text = "--.-°C";
            Channel2Text.Text = "--.-°C";
            Channel3Text.Text = "--.-°C";
            Channel4Text.Text = "--.-°C";
            UpdateTimestamp.Text = "Last update: Never";

            _channel1Data.Clear();
            _channel2Data.Clear();
            _channel3Data.Clear();
            _channel4Data.Clear();
        }
    }
}