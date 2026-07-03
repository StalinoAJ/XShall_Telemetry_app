using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SHALLControl.Tests
{
    /// <summary>
    /// Forza Horizon 5 telemetry simulator.
    /// Sends 232-byte UDP "Sled" packets to port 5300.
    /// 
    /// To test: Launch SHALL XR → select Forza Horizon 5 → click START → run this test.
    /// </summary>
    [TestClass]
    public class ForzaSimulatorTest
    {
        private const int PORT = 5300;

        [TestMethod]
        [TestCategory("Forza Horizon 5")]
        [Description("Simulates Forza driving: accelerate → cruise → corner L/R → brake. Sends UDP to port 5300.")]
        public void Forza_SimulateDrivingSession()
        {
            using (var udp = new UdpClient())
            {
                var sw = Stopwatch.StartNew();
                int packetCount = 0;

                while (sw.Elapsed.TotalSeconds < SimHelper.RUN_SECONDS)
                {
                    double t = sw.Elapsed.TotalSeconds;
                    byte[] pkt = BuildForzaPacket(t);
                    SimHelper.SendUdp(udp, pkt, PORT);
                    packetCount++;
                    Thread.Sleep(1000 / SimHelper.SEND_HZ);
                }

                Trace.WriteLine($"[Forza] Sent {packetCount} packets over {SimHelper.RUN_SECONDS}s to UDP:{PORT}");
                Assert.IsTrue(packetCount > 0, "Should have sent packets");
            }
        }

        private byte[] BuildForzaPacket(double t)
        {
            byte[] d = new byte[232];

            // isRaceOn (offset 0) — must be 1
            SimHelper.WriteInt32(d, 0, 1);

            // Driving simulation phases
            // 0-3s: accelerate, 3-7s: cruise+turn, 7-10s: hard corner, 10-13s: brake, 13-15s: gentle
            float speed, accelX, accelY, accelZ, angVelY;

            if (t < 3)
            {
                // Accelerating
                speed = (float)(t / 3.0 * 120);   // 0→120 km/h
                accelX = 0;
                accelY = 1.0f;                      // gravity
                accelZ = 0.5f;                       // forward surge
                angVelY = 0;
            }
            else if (t < 7)
            {
                // Cruising with gentle sway
                speed = 120;
                accelX = SimHelper.Oscillate(t, 3.0) * 0.15f;  // gentle sway
                accelY = 1.0f;
                accelZ = 0.05f;
                angVelY = SimHelper.Oscillate(t, 4.0) * 0.1f;
            }
            else if (t < 10)
            {
                // Hard cornering (alternating L/R)
                speed = 90;
                accelX = SimHelper.Oscillate(t, 2.0) * 0.8f;    // heavy lateral G
                accelY = 1.0f;
                accelZ = -0.1f;
                angVelY = SimHelper.Oscillate(t, 2.0) * 0.6f;   // strong yaw
            }
            else if (t < 13)
            {
                // Braking hard
                speed = (float)(90 - (t - 10) / 3.0 * 80);  // 90→10 km/h
                accelX = 0;
                accelY = 1.0f;
                accelZ = -0.7f;                               // strong deceleration
                angVelY = 0;
            }
            else
            {
                // Gentle coasting
                speed = 10 + SimHelper.Oscillate(t, 2.0) * 5;
                accelX = SimHelper.Oscillate(t, 1.5) * 0.1f;
                accelY = 1.0f + SimHelper.Oscillate(t, 0.5) * 0.05f; // slight bumps
                accelZ = 0.02f;
                angVelY = SimHelper.Oscillate(t, 2.0) * 0.05f;
            }

            float speedMs = speed / 3.6f;

            // accelX (offset 20), accelY (offset 24), accelZ (offset 28)
            SimHelper.WriteFloat(d, 20, accelX);
            SimHelper.WriteFloat(d, 24, accelY);
            SimHelper.WriteFloat(d, 28, accelZ);

            // velX/Y/Z (offsets 32/36/40) — speed as forward velocity
            SimHelper.WriteFloat(d, 32, 0);           // lateral vel
            SimHelper.WriteFloat(d, 36, 0);           // vertical vel
            SimHelper.WriteFloat(d, 40, speedMs);     // forward vel

            // angVelY (offset 48) — yaw rate
            SimHelper.WriteFloat(d, 48, angVelY);

            return d;
        }
    }
}
