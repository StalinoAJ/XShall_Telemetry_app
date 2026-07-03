using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SHALLControl.Tests
{
    /// <summary>
    /// SnowRunner telemetry simulator.
    /// Sends 48-byte UDP packets to port 21777 (community bridge format).
    /// 
    /// To test: Launch SHALL XR → select SnowRunner → click START → run this test.
    /// </summary>
    [TestClass]
    public class SnowRunnerSimulatorTest
    {
        private const int PORT = 21777;

        [TestMethod]
        [TestCategory("SnowRunner")]
        [Description("Simulates SnowRunner off-road: slow crawl, terrain tilts, mud. Sends UDP to port 21777.")]
        public void SnowRunner_SimulateOffRoadSession()
        {
            using (var udp = new UdpClient())
            {
                var sw = Stopwatch.StartNew();
                int packetCount = 0;

                while (sw.Elapsed.TotalSeconds < SimHelper.RUN_SECONDS)
                {
                    double t = sw.Elapsed.TotalSeconds;
                    byte[] pkt = BuildSnowRunnerPacket(t);
                    SimHelper.SendUdp(udp, pkt, PORT);
                    packetCount++;
                    Thread.Sleep(1000 / SimHelper.SEND_HZ);
                }

                Trace.WriteLine($"[SnowRunner] Sent {packetCount} packets over {SimHelper.RUN_SECONDS}s to UDP:{PORT}");
                Assert.IsTrue(packetCount > 0, "Should have sent packets");
            }
        }

        private byte[] BuildSnowRunnerPacket(double t)
        {
            byte[] d = new byte[48];

            // SnowRunner is slow and terrain-heavy
            float speed, steer, pitch, roll, yaw, accelX, accelZ;

            if (t < 4)
            {
                // Slow start in mud
                speed = (float)(t / 4.0 * 15);    // max 15 km/h
                steer = 0;
                pitch = (float)(-0.15 * t / 4.0); // climbing slope (radians)
                roll = SimHelper.Oscillate(t, 1.0) * 0.05f;   // uneven ground
                yaw = 0;
                accelX = SimHelper.Oscillate(t, 0.8) * 0.08f; // sway in mud
                accelZ = 0.1f;
            }
            else if (t < 8)
            {
                // Steep terrain — heavy pitch and roll
                speed = 12;
                steer = SimHelper.Oscillate(t, 6.0) * 0.3f;
                pitch = SimHelper.Oscillate(t, 3.0) * 0.25f;    // big terrain pitch
                roll = SimHelper.Oscillate(t, 2.0) * 0.2f;      // heavy body roll
                yaw = SimHelper.Oscillate(t, 6.0) * 0.1f;
                accelX = SimHelper.Oscillate(t, 2.0) * 0.15f;
                accelZ = SimHelper.Oscillate(t, 3.0) * 0.1f;
            }
            else if (t < 12)
            {
                // River crossing — very slow, lots of sway
                speed = 5;
                steer = SimHelper.Oscillate(t, 4.0) * 0.4f;
                pitch = -0.1f + SimHelper.Oscillate(t, 1.5) * 0.08f;
                roll = SimHelper.Oscillate(t, 1.0) * 0.15f;     // water sway
                yaw = SimHelper.Oscillate(t, 4.0) * 0.08f;
                accelX = SimHelper.Oscillate(t, 1.0) * 0.2f;
                accelZ = 0.02f;
            }
            else
            {
                // Coming to rest on incline
                speed = (float)Math.Max(0, 5 - (t - 12) / 3.0 * 5);
                steer = 0;
                pitch = -0.12f;   // parked on slope
                roll = 0.06f;     // slight tilt
                yaw = 0;
                accelX = 0;
                accelZ = -0.05f;
            }

            // Packet layout:
            // speed@0, rpm@4, steer@8, pitch@12, roll@16, yaw@20
            // accelX@24, accelY@28, accelZ@32, suspFL@36, suspFR@40, suspRL@44
            SimHelper.WriteFloat(d, 0, speed);
            SimHelper.WriteFloat(d, 4, speed * 100);   // fake RPM
            SimHelper.WriteFloat(d, 8, steer);
            SimHelper.WriteFloat(d, 12, pitch);
            SimHelper.WriteFloat(d, 16, roll);
            SimHelper.WriteFloat(d, 20, yaw);
            SimHelper.WriteFloat(d, 24, accelX);
            SimHelper.WriteFloat(d, 28, 1.0f);         // accelY (gravity)
            SimHelper.WriteFloat(d, 32, accelZ);

            return d;
        }
    }
}
