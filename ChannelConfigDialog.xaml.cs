using System.Windows;

namespace TemperatureMonitor
{
    public partial class ChannelConfigDialog : Window
    {
        private ChannelConfig _config;

        public ChannelConfigDialog(ChannelConfig config)
        {
            InitializeComponent();
            _config = config;
            LoadChannelNames();
        }

        private void LoadChannelNames()
        {
            Channel1TextBox.Text = _config.GetChannelName(1);
            Channel2TextBox.Text = _config.GetChannelName(2);
            Channel3TextBox.Text = _config.GetChannelName(3);
            Channel4TextBox.Text = _config.GetChannelName(4);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _config.Channels[0].Name = Channel1TextBox.Text;
            _config.Channels[1].Name = Channel2TextBox.Text;
            _config.Channels[2].Name = Channel3TextBox.Text;
            _config.Channels[3].Name = Channel4TextBox.Text;

            _config.Save();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}