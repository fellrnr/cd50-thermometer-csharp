using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
//using System.Threading.Task;

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
        /// Scans all available COM ports and returns the port that responds correctly to the thermometer protocol
        /// </summary>
        public static string? FindAvailablePort()
        {
            // Get all available COM ports
            string[] ports = SerialPort.GetPortNames();
            
            if (ports.Length == 0)
                return null;

            // Try each port
            foreach (string port in ports)
            {
                try
                {
                    using (var thermometer = new CD50Thermometer(port))
                    {
                        if (thermometer.Connect())
                        {
                            System.Diagnostics.Debug.WriteLine($"Found thermometer on port: {port}");
                            return port;
                        }
                    }
                }
                catch
                {
                    // Port doesn't have a thermometer, continue scanning
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Get all available COM ports
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames().OrderBy(p => p).ToArray();
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
                //_serialPort.XonChar = (char)0x11;
                //_serialPort.XoffChar = (char)0x13;
                _serialPort.Open();
                //_serialPort.RtsEnable = false; //default
                //_serialPort.


                // Send initialization command
                byte[] initMessage = new byte[_host2DeviceHeader.Length + _connectCommand.Length];
                Buffer.BlockCopy(_host2DeviceHeader, 0, initMessage, 0, _host2DeviceHeader.Length);
                Buffer.BlockCopy(_connectCommand, 0, initMessage, _host2DeviceHeader.Length, _connectCommand.Length);

                _serialPort.Write(initMessage, 0, initMessage.Length);

                // Read and verify response
                byte[] response = new byte[9];
                //int bytesRead = _serialPort.Read(response, 0, 9);
                int bytesRead = ReadBytes(9, response);

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
                    {
                        throw new InvalidOperationException("Checksum mismatch on connection response!");
                    }
                }
                else
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        _serialPort.Dispose();
                    }
                    throw new InvalidOperationException($"Invalid response from device! {bytesRead} bytes received");
                }
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
                //  int bytesRead = _serialPort.Read(response, 0, 13);
                int bytesRead = ReadBytes(13, response);

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
                    System.Diagnostics.Debug.WriteLine($"Invalid response from temperature read! {bytesRead} bytes received");
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

        private int ReadBytes(int size, byte[] response)
        {
            if(_serialPort == null)
                throw new InvalidOperationException("Serial port is not initialized.");

            int bytesRead = 0;

            try
            {
                // Loop until we have exactly 9 bytes
                while (bytesRead < size)
                {
                    int remaining = size - bytesRead;
                    // This blocks until data arrives or ReadTimeout occurs
                    int count = _serialPort.Read(response, bytesRead, remaining);
                    bytesRead += count;
                }
                
            }
            catch (TimeoutException te)
            {
                throw te;
            }

            //int bytesRead = _serialPort.Read(response, 0, count);

            return bytesRead;
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