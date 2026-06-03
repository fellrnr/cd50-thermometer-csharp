using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;

namespace TemperatureMonitor
{
    public partial class MainWindow : Window
    {
        private CD50Thermometer? _thermometer;
        private bool _isPolling;
        private CancellationTokenSource? _pollingCts;
        private List<double> _channel1Data = new();
        private List<double> _channel2Data = new();
        private List<double> _channel3Data = new();
        private List<double> _channel4Data = new();
        private PlotModel? _plotModel;
        private int _dataPointCount = 0;
        private const int MaxDataPoints = 120; // Keep 120 data points (2 minutes at 500ms intervals)

        public MainWindow()
        {
            InitializeComponent();
            InitializeChart();
        }

        private void InitializeChart()
        {
            _plotModel = new PlotModel { Title = "" };
            _plotModel.Background = OxyColors.White;

            // X-Axis
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (seconds)",
                Minimum = 0,
                Maximum = MaxDataPoints / 2, // 60 seconds at 500ms intervals
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(200, 200, 200)
            };
            _plotModel.Axes.Add(xAxis);

            // Y-Axis
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Temperature (°C)",
                Minimum = -50,
                Maximum = 150,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(200, 200, 200)
            };
            _plotModel.Axes.Add(yAxis);

            // Create line series for each channel
            var series1 = new LineSeries { Title = "Channel 1", Color = OxyColor.FromRgb(231, 76, 60), StrokeThickness = 2 };
            var series2 = new LineSeries { Title = "Channel 2", Color = OxyColor.FromRgb(243, 156, 18), StrokeThickness = 2 };
            var series3 = new LineSeries { Title = "Channel 3", Color = OxyColor.FromRgb(39, 174, 96), StrokeThickness = 2 };
            var series4 = new LineSeries { Title = "Channel 4", Color = OxyColor.FromRgb(52, 152, 219), StrokeThickness = 2 };

            _plotModel.Series.Add(series1);
            _plotModel.Series.Add(series2);
            _plotModel.Series.Add(series3);
            _plotModel.Series.Add(series4);

            TemperatureChart.Model = _plotModel;
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

            // Update chart
            UpdateChart();

            // Update timestamp
            UpdateTimestamp.Text = $"Last update: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private void AddDataPoint(List<double> collection, double value)
        {
            collection.Add(value);
            if (collection.Count > MaxDataPoints)
            {
                collection.RemoveAt(0);
            }
        }

        private void UpdateChart()
        {
            if (_plotModel == null) return;

            // Clear existing points
            foreach (var series in _plotModel.Series)
            {
                if (series is LineSeries lineSeries)
                    lineSeries.Points.Clear();
            }

            // Add new points
            List<double>[] datasets = { _channel1Data, _channel2Data, _channel3Data, _channel4Data };
            for (int i = 0; i < datasets.Length; i++)
            {
                var lineSeries = _plotModel.Series[i] as LineSeries;
                if (lineSeries != null)
                {
                    for (int j = 0; j < datasets[i].Count; j++)
                    {
                        lineSeries.Points.Add(new DataPoint(j * 0.5, datasets[i][j])); // 0.5 seconds per point
                    }
                }
            }

            _plotModel.InvalidatePlot(true);
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
            _dataPointCount = 0;

            UpdateChart();
        }
    }
}
