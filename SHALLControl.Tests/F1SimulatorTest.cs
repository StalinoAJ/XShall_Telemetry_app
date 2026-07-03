using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SHALLControl.Tests
{
    /// <summary>
    /// F1 Series telemetry simulator.
    /// Sends F1 2023 format Motion packets (packetId=0) via UDP to port 20777.
    /// 
    /// To test: Launch SHALL XR → select F1 Series → click START → run this test.
    /// </summary>
    [TestClass]
    public class F1SimulatorTest
    {
        private const int PORT = 20777;
        private const int HEADER_SIZE = 24;
        private const int CAR_COUNT = 22;
        private const int CAR_DATA_SIZE = 60;
        private const ushort PACKET_FORMAT = 2023;

        [TestMethod]
        [TestCategory("F1 Series")]
        [Description("Simulates F1 racing: high-speed straights, heavy braking, fast cornering. Sends UDP to port 20777.")]
        public void F1_SimulateRaceSession()
        {
            using (var udp = new UdpClient())
            {
                var sw = Stopwatch.StartNew();
                int packetCount = 0;

                while (sw.Elapsed.TotalSeconds < SimHelper.RUN_SECONDS)
                {
                    double t = sw.Elapsed.TotalSeconds;
                    byte[] pkt = BuildF1Packet(t);
                    SimHelper.SendUdp(udp, pkt, PORT);
                    packetCount++;
                    Thread.Sleep(1000 / SimHelper.SEND_HZ);
                }

                Trace.WriteLine($"[F1] Sent {packetCount} packets over {SimHelper.RUN_SECONDS}s to UDP:{PORT}");
                Assert.IsTrue(packetCount > 0, "Should have sent packets");
            }
        }

        private byte[] BuildF1Packet(double t)
        {
            // Total size: header (24) + 22 cars * 60 bytes = 1344
            byte[] d = new byte[HEADER_SIZE + CAR_COUNT * CAR_DATA_SIZE];

            // Header
            SimHelper.WriteUInt16(d, 0, PACKET_FORMAT);  // packetFormat
            d[5] = 0;    // packetId = 0 (Motion)
            d[23] = 0;   // playerCarIndex = 0

            // Simulate driving
            float gLateral, gLong, gVert, yaw, pitch, roll;

            if (t < 3)
            {
                // Straight acceleration (DRS zone)
                gLateral = 0;
                gLong = 1.5f;                           // strong forward G
                gVert = 1.0f;
                yaw = 0;
                pitch = (float)(-0.05 * t / 3.0);      // slight nose up
                roll = 0;
            }
            else if (t < 6)
            {
                // Heavy braking zone
                gLateral = SimHelper.Oscillate(t, 0.5) * 0.1f;  // slight wobble
                gLong = -2.5f;                                    // extreme braking G
                gVert = 1.0f;
                yaw = 0;
                pitch = 0.08f;                                    // nose dips
                roll = 0;
            }
            else if (t < 10)
            {
                // Fast cornering (chicane — alternating L/R)
                float phase = SimHelper.Oscillate(t, 1.5);
                gLateral = phase * 2.0f;                // 2G lateral
                gLong = 0.3f;
                gVert = 1.0f + SimHelper.Oscillate(t, 0.3) * 0.15f;  // kerb riding
                yaw = phase * 0.3f;
                pitch = 0;
                roll = phase * 0.12f;
            }
            else if (t < 13)
            {
                // Medium-speed corner
                gLateral = SimHelper.Oscillate(t, 3.0) * 1.5f;
                gLong = 0.5f;
                gVert = 1.0f;
                yaw = SimHelper.Oscillate(t, 3.0) * 0.15f;
                pitch = -0.02f;
                roll = SimHelper.Oscillate(t, 3.0) * 0.08f;
            }
            else
            {
                // Pit lane / cool-down
                gLateral = 0;
                gLong = -0.3f;
                gVert = 1.0f;
                yaw = 0;
                pitch = 0;
                roll = 0;
            }

            // Write player car data (car index 0)
            int off = HEADER_SIZE + 0 * CAR_DATA_SIZE;

            // gForceLateral (offset +36), gForceLongitudinal (+40), gForceVertical (+44)
            SimHelper.WriteFloat(d, off + 36, gLateral);
            SimHelper.WriteFloat(d, off + 40, gLong);
            SimHelper.WriteFloat(d, off + 44, gVert);

            // yaw (+48), pitch (+52), roll (+56) — radians
            SimHelper.WriteFloat(d, off + 48, yaw);
            SimHelper.WriteFloat(d, off + 52, pitch);
            SimHelper.WriteFloat(d, off + 56, roll);

            return d;
        }
    }
}
