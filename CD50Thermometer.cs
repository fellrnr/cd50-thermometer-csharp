using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace TemperatureMonitor
{
    /// <summary>
    /// C# driver for the CD50 USB 4-channel thermometer
    /// Communicates via serial port with 9600 baud rate
    /// </summary>
    public class CD50Thermometer : IDisposable
    {
        private SerialPort? _serialPort;
        private readonly string _port;
        private readonly byte[] _host2DeviceHeader = { 0xAA, 0x55 };
        private readonly byte[] _device2HostHeader = { 0x55, 0xAA };
        private readonly byte[] _connectCommand = { 0x00, 0x03, 0x02 };
        private readonly byte[] _readCommand = { 0x01, 0x03, 0x03 };

        public bool Connected { get; private set; }

        public CD50Thermometer(string port = "COM7")
        {
            _port = port;
            Connected = false;
        }

        /// <summary>
        /// Connect to the thermometer device
        /// </summary>
        public bool Connect()
        {
            try
            {
                _serialPort = new SerialPort(_port, 9600, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                _serialPort.Open();

                // Send initialization command
                byte[] initMessage = new byte[_host2DeviceHeader.Length + _connectCommand.Length];
                Buffer.BlockCopy(_host2DeviceHeader, 0, initMessage, 0, _host2DeviceHeader.Length);
                Buffer.BlockCopy(_connectCommand, 0, initMessage, _host2DeviceHeader.Length, _connectCommand.Length);

                _serialPort.Write(initMessage, 0, initMessage.Length);

                // Read and verify response
                byte[] response = new byte[9];
                int bytesRead = _serialPort.Read(response, 0, 9);

                if (bytesRead == 9 && response[0] == _device2HostHeader[0] && response[1] == _device2HostHeader[1])
                {
                    // Verify checksum
                    byte checksum = 0;
                    for (int i = 0; i < 8; i++)
                        checksum = (byte)((checksum + response[i]) & 0xFF);

                    if (checksum == response[8])
                    {
                        Connected = true;
                        return true;
                    }
                    else
                        throw new InvalidOperationException("Checksum mismatch on connection response!");
                }
                else
                    throw new InvalidOperationException("Invalid response from device!");
            }
            catch (Exception ex)
            {
                Connected = false;
                throw new InvalidOperationException($"Could not connect to {_port}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Read temperatures from all 4 channels
        /// Returns array of 4 temperatures in degrees Celsius
        /// </summary>
        public double[]? ReadTemperatures()
        {
            if (!Connected || _serialPort == null)
                throw new InvalidOperationException("Not connected to device!");

            try
            {
                // Send read command
                byte[] readMessage = new byte[_host2DeviceHeader.Length + _readCommand.Length];
                Buffer.BlockCopy(_host2DeviceHeader, 0, readMessage, 0, _host2DeviceHeader.Length);
                Buffer.BlockCopy(_readCommand, 0, readMessage, _host2DeviceHeader.Length, _readCommand.Length);

                _serialPort.Write(readMessage, 0, readMessage.Length);

                // Read response (13 bytes)
                byte[] response = new byte[13];
                int bytesRead = _serialPort.Read(response, 0, 13);

                if (bytesRead == 13 && response[0] == _device2HostHeader[0] && response[1] == _device2HostHeader[1])
                {
                    // Verify checksum
                    byte checksum = 0;
                    for (int i = 0; i < 12; i++)
                        checksum = (byte)((checksum + response[i]) & 0xFF);

                    if (checksum == response[12])
                    {
                        double[] temperatures = new double[4];
                        for (int i = 0; i < 4; i++)
                        {
                            // Little-endian unsigned short at offset 4 + i*2
                            ushort tempRaw = (ushort)(response[4 + i * 2] | (response[5 + i * 2] << 8));
                            temperatures[i] = tempRaw / 10.0;
                        }
                        return temperatures;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Checksum mismatch in temperature read!");
                        return null;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Invalid response from temperature read!");
                    return null;
                }
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine("Timeout reading temperatures");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading temperatures: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Disconnect from the device
        /// </summary>
        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            Connected = false;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}