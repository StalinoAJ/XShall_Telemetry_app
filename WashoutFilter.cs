using System;

namespace SHALLControl
{
    /// <summary>
    /// A composite filter that combines sustained (Low-Pass) and transient (High-Pass) cues.
    /// This provides a smoother experience by handling slow tilts and fast bumps separately.
    /// </summary>
    public class WashoutFilter
    {
        private readonly ButterworthFilter _lowPass;
        private readonly HighPassFilter _highPass;

        public WashoutFilter(double lpCutoffHz = 2.0, double hpCutoffHz = 0.5, double sampleRateHz = 20.0)
        {
            _lowPass = new ButterworthFilter(lpCutoffHz, sampleRateHz);
            _highPass = new HighPassFilter(hpCutoffHz, sampleRateHz);
        }

        /// <param name="sustained">Slow-moving data (like gravity tilt coordination from G-forces)</param>
        /// <param name="transient">Fast-moving data (like rotational change or sudden bumps)</param>
        public double Filter(double sustained, double transient)
        {
            // Low-pass the sustained forces (slow tilt)
            double lpPart = _lowPass.Filter(sustained);
            
            // High-pass the transients (bumps)
            double hpPart = _highPass.Filter(transient);
            
            return lpPart + hpPart;
        }

        public void Reset()
        {
            _lowPass.Reset();
            _highPass.Reset();
        }
    }
}
