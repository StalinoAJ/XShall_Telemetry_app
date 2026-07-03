using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SHALLControl.Tests
{
    /// <summary>
    /// American Truck Simulator telemetry simulator.
    /// Hosts an HTTP server on port 25555 serving Funbit-format JSON (same as ETS2).
    /// Different driving profile: heavier vehicle, wider turns, lower speeds.
    /// 
    /// To test: Launch SHALL XR → select American Truck Sim → click START → run this test.
    /// NOTE: Cannot run at the same time as ETS2 simulator (both use port 25555).
    /// </summary>
    [TestClass]
    public class ATSSimulatorTest
    {
        private const int PORT = 25555;

        [TestMethod]
        [TestCategory("American Truck Sim")]
        [Description("Simulates ATS telemetry: heavy truck driving with wide turns. HTTP server on port 25555.")]
        public void ATS_SimulateDrivingSession()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{PORT}/");
            listener.Start();

            var sw = Stopwatch.StartNew();
            int requestCount = 0;

            try
            {
                while (sw.Elapsed.TotalSeconds < SimHelper.RUN_SECONDS)
                {
                    var ctx = listener.BeginGetContext(null, null);

                    if (ctx.AsyncWaitHandle.WaitOne(500))
                    {
                        var context = listener.EndGetContext(ctx);
                        double t = sw.Elapsed.TotalSeconds;
                        string json = BuildATSJson(t);

                        byte[] buf = Encoding.UTF8.GetBytes(json);
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = buf.Length;
                        context.Response.OutputStream.Write(buf, 0, buf.Length);
                        context.Response.Close();
                        requestCount++;
                    }
                }
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }

            Trace.WriteLine($"[ATS] Served {requestCount} requests over {SimHelper.RUN_SECONDS}s on HTTP:{PORT}");
            Assert.IsTrue(requestCount > 0, "Should have served telemetry requests. Make sure the app is running with ATS selected and START clicked.");
        }

        private string BuildATSJson(double t)
        {
            float speed, steer, accelX, accelZ, placePitch, placeRoll;

            if (t < 5)
            {
                // Slow acceleration (heavy truck)
                speed = (float)(t / 5.0 * 65);
                steer = 0;
                accelX = 0;
                accelZ = 0.06f;   // heavy truck = less accel
                placePitch = -0.01f;
                placeRoll = 0;
            }
            else if (t < 9)
            {
                // Highway with gentle lane changes
                speed = 65;
                steer = SimHelper.Oscillate(t, 5.0) * 0.2f;
                accelX = SimHelper.Oscillate(t, 5.0) * 0.05f;
                accelZ = 0;
                placePitch = SimHelper.Oscillate(t, 8.0) * 0.008f;
                placeRoll = SimHelper.Oscillate(t, 5.0) * 0.01f;
            }
            else if (t < 13)
            {
                // Wide right turn at intersection
                float turnPhase = (float)((t - 9) / 4.0);
                speed = 25;
                steer = (float)Math.Sin(turnPhase * Math.PI) * 0.6f;
                accelX = steer * 0.12f;
                accelZ = -0.03f;
                placePitch = 0;
                placeRoll = steer * 0.02f;
            }
            else
            {
                // Coming to a stop
                speed = (float)Math.Max(0, 25 - (t - 13) / 2.0 * 25);
                steer = 0;
                accelX = 0;
                accelZ = -0.1f;
                placePitch = 0.008f;
                placeRoll = 0;
            }

            return $@"{{
  ""connected"": true,
  ""truck"": {{
    ""speed"": {Fmt(speed)},
    ""gameSteer"": {Fmt(steer)},
    ""acceleration"": {{ ""x"": {Fmt(accelX)}, ""y"": 0.0, ""z"": {Fmt(accelZ)} }},
    ""placement"": {{ ""x"": 0, ""y"": 0, ""z"": 0, ""heading"": 0, ""pitch"": {Fmt(placePitch)}, ""roll"": {Fmt(placeRoll)} }}
  }}
}}";
        }

        private static string Fmt(float v) =>
            v.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
