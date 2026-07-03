using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SHALLControl.Tests
{
    /// <summary>
    /// Euro Truck Simulator 2 telemetry simulator.
    /// Hosts an HTTP server on port 25555 that serves Funbit-format JSON telemetry.
    /// 
    /// To test: Launch SHALL XR → select Euro Truck Sim 2 → click START → run this test.
    /// </summary>
    [TestClass]
    public class ETS2SimulatorTest
    {
        private const int PORT = 25555;

        [TestMethod]
        [TestCategory("Euro Truck Sim 2")]
        [Description("Simulates ETS2 telemetry server on HTTP port 25555 with Funbit JSON format.")]
        public void ETS2_SimulateDrivingSession()
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

                    // Wait for request or timeout
                    if (ctx.AsyncWaitHandle.WaitOne(500))
                    {
                        var context = listener.EndGetContext(ctx);
                        double t = sw.Elapsed.TotalSeconds;
                        string json = BuildETS2Json(t);

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

            Trace.WriteLine($"[ETS2] Served {requestCount} requests over {SimHelper.RUN_SECONDS}s on HTTP:{PORT}");
            Assert.IsTrue(requestCount > 0, "Should have served telemetry requests. Make sure the app is running with ETS2 selected and START clicked.");
        }

        private string BuildETS2Json(double t)
        {
            float speed, steer, accelX, accelZ, placePitch, placeRoll;

            if (t < 4)
            {
                // Accelerating from stop
                speed = (float)(t / 4.0 * 80);
                steer = 0;
                accelX = 0;
                accelZ = 0.1f;
                placePitch = -0.02f;  // slight uphill
                placeRoll = 0;
            }
            else if (t < 8)
            {
                // Highway cruising, gentle curves
                speed = 80;
                steer = SimHelper.Oscillate(t, 4.0) * 0.3f;
                accelX = SimHelper.Oscillate(t, 4.0) * 0.08f;
                accelZ = 0.01f;
                placePitch = SimHelper.Oscillate(t, 6.0) * 0.01f;
                placeRoll = SimHelper.Oscillate(t, 4.0) * 0.015f;
            }
            else if (t < 12)
            {
                // Roundabout / sharp turn
                speed = 40;
                steer = SimHelper.Oscillate(t, 3.0) * 0.7f;
                accelX = SimHelper.Oscillate(t, 3.0) * 0.2f;
                accelZ = -0.05f;
                placePitch = 0;
                placeRoll = SimHelper.Oscillate(t, 3.0) * 0.03f;
            }
            else
            {
                // Braking to stop
                speed = (float)(40 - (t - 12) / 3.0 * 35);
                if (speed < 0) speed = 0;
                steer = 0;
                accelX = 0;
                accelZ = -0.15f;
                placePitch = 0.01f;
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
