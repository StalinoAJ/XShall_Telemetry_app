using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SHALLControl.Tests
{
    /// <summary>
    /// Shared helpers for all telemetry simulators.
    /// Provides smooth sine-wave driving phase simulation.
    /// </summary>
    internal static class SimHelper
    {
        /// <summary>Duration each simulator sends data for (seconds).</summary>
        public const int RUN_SECONDS = 15;

        /// <summary>Send rate (Hz).</summary>
        public const int SEND_HZ = 20;

        /// <summary>Smooth sine value from 0..1 over a cycle.</summary>
        public static float SinePhase(double elapsed, double periodSec)
            => (float)((Math.Sin(2 * Math.PI * elapsed / periodSec - Math.PI / 2) + 1) / 2);

        /// <summary>Oscillating value between -1 and 1.</summary>
        public static float Oscillate(double elapsed, double periodSec)
            => (float)Math.Sin(2 * Math.PI * elapsed / periodSec);

        /// <summary>Send a UDP datagram to localhost:port.</summary>
        public static void SendUdp(UdpClient client, byte[] data, int port)
        {
            client.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, port));
        }

        /// <summary>Write a float into a byte array at offset (little-endian).</summary>
        public static void WriteFloat(byte[] buf, int offset, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            Buffer.BlockCopy(b, 0, buf, offset, 4);
        }

        /// <summary>Write an int into a byte array at offset (little-endian).</summary>
        public static void WriteInt32(byte[] buf, int offset, int value)
        {
            byte[] b = BitConverter.GetBytes(value);
            Buffer.BlockCopy(b, 0, buf, offset, 4);
        }

        /// <summary>Write a ushort into a byte array at offset (little-endian).</summary>
        public static void WriteUInt16(byte[] buf, int offset, ushort value)
        {
            byte[] b = BitConverter.GetBytes(value);
            Buffer.BlockCopy(b, 0, buf, offset, 2);
        }
    }
}
