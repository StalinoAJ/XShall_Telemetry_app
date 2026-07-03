using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SHALLControl.Tests
{
    /// <summary>
    /// Dirt Rally / Dirt Rally 2.0 telemetry simulator.
    /// Sends 264-byte Codemasters UDP packets (extradata=3) to port 20777.
    /// 
    /// To test: Launch SHALL XR → select Dirt Rally → click START → run this test.
    /// NOTE: Cannot run at the same time as F1 simulator (both use port 20777).
    /// </summary>
    [TestClass]
    public class DirtRallySimulatorTest
    {
        private const int PORT = 20777;
        private const int PACKET_SIZE = 264;

        [TestMethod]
        [TestCategory("Dirt Rally")]
        [Description("Simulates Dirt Rally stage: jumps, hairpins, gravel. Sends UDP to port 20777.")]
        public void DirtRally_SimulateRallyStage()
        {
            using (var udp = new UdpClient())
            {
                var sw = Stopwatch.StartNew();
                int packetCount = 0;

                while (sw.Elapsed.TotalSeconds < SimHelper.RUN_SECONDS)
                {
                    double t = sw.Elapsed.TotalSeconds;
                    byte[] pkt = BuildDirtRallyPacket(t);
                    SimHelper.SendUdp(udp, pkt, PORT);
                    packetCount++;
                    Thread.Sleep(1000 / SimHelper.SEND_HZ);
                }

                Trace.WriteLine($"[DirtRally] Sent {packetCount} packets over {SimHelper.RUN_SECONDS}s to UDP:{PORT}");
                Assert.IsTrue(packetCount > 0, "Should have sent packets");
            }
        }

        private byte[] BuildDirtRallyPacket(double t)
        {
            byte[] d = new byte[PACKET_SIZE];

            float time, speedMs, fwdX, fwdY, fwdZ, rightX, rightY, rightZ;
            float gLat, gLong;

            // time must be > 0 for plugin to accept
            time = (float)t + 1.0f;

            if (t < 3)
            {
                // Launch off start line
                speedMs = (float)(t / 3.0 * 30);  // 0→108 km/h
                fwdX = 0; fwdY = 0; fwdZ = 1;     // facing forward
                rightX = 1; rightY = 0; rightZ = 0;
                gLat = 0;
                gLong = 0.3f;
            }
            else if (t < 6)
            {
                // Fast section with jumps
                speedMs = 30;
                float jumpPhase = SimHelper.Oscillate(t, 0.8);
                fwdX = 0;
                fwdY = jumpPhase * 0.3f;   // nose up/down over jumps
                fwdZ = (float)Math.Sqrt(1 - fwdY * fwdY);
                rightX = 1; rightY = SimHelper.Oscillate(t, 0.5) * 0.1f; rightZ = 0;
                gLat = SimHelper.Oscillate(t, 1.2) * 0.2f;
                gLong = SimHelper.Oscillate(t, 0.8) * 0.15f;
            }
            else if (t < 10)
            {
                // Tight hairpin turns (alternating)
                speedMs = 15;
                float turnPhase = SimHelper.Oscillate(t, 2.5);
                fwdX = turnPhase * 0.15f;
                fwdY = -0.05f;  // slight downhill
                fwdZ = (float)Math.Sqrt(Math.Max(0, 1 - fwdX * fwdX - fwdY * fwdY));
                rightX = (float)Math.Sqrt(Math.Max(0, 1 - turnPhase * 0.2f * turnPhase * 0.2f));
                rightY = turnPhase * 0.2f;  // roll into turn
                rightZ = 0;
                gLat = turnPhase * 0.5f;
                gLong = -0.2f;
            }
            else if (t < 13)
            {
                // Gravel section — bumpy, moderate speed
                speedMs = 22;
                float bumpH = SimHelper.Oscillate(t, 0.3) * 0.1f;
                float bumpR = SimHelper.Oscillate(t, 0.4) * 0.08f;
                fwdX = SimHelper.Oscillate(t, 1.5) * 0.05f;
                fwdY = bumpH;
                fwdZ = (float)Math.Sqrt(Math.Max(0, 1 - fwdX * fwdX - fwdY * fwdY));
                rightX = 1; rightY = bumpR; rightZ = 0;
                gLat = SimHelper.Oscillate(t, 1.0) * 0.3f;
                gLong = SimHelper.Oscillate(t, 0.5) * 0.2f;
            }
            else
            {
                // Braking into finish
                speedMs = (float)Math.Max(0, 22 - (t - 13) / 2.0 * 22);
                fwdX = 0; fwdY = 0.03f; fwdZ = 1;
                rightX = 1; rightY = 0; rightZ = 0;
                gLat = 0;
                gLong = -0.4f;
            }

            // Packet offsets (Codemasters format):
            // time@0, lapTime@4, ..., speed(m/s)@28,
            // velXYZ@32-40, fwdXYZ@44-52, rightXYZ@56-64
            // gLat@136, gLong@140

            SimHelper.WriteFloat(d, 0, time);
            SimHelper.WriteFloat(d, 4, time);         // lapTime = time
            SimHelper.WriteFloat(d, 28, speedMs);

            // Velocity (simplified: forward only)
            SimHelper.WriteFloat(d, 32, 0);           // velX
            SimHelper.WriteFloat(d, 36, 0);           // velY
            SimHelper.WriteFloat(d, 40, speedMs);     // velZ

            // Forward direction vector
            SimHelper.WriteFloat(d, 44, fwdX);
            SimHelper.WriteFloat(d, 48, fwdY);
            SimHelper.WriteFloat(d, 52, fwdZ);

            // Right direction vector
            SimHelper.WriteFloat(d, 56, rightX);
            SimHelper.WriteFloat(d, 60, rightY);
            SimHelper.WriteFloat(d, 64, rightZ);

            // G-forces (at offsets 136, 140)
            SimHelper.WriteFloat(d, 136, gLat);
            SimHelper.WriteFloat(d, 140, gLong);

            return d;
        }
    }
}
