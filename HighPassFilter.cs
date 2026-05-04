using System;

namespace SHALLControl
{
    /// <summary>
    /// Simple 1st-order high-pass filter for transient motion cues (bumps, vibrations).
    /// </summary>
    public class HighPassFilter
    {
        private readonly double _alpha;
        private double _prevInput;
        private double _prevOutput;

        public HighPassFilter(double cutoffHz = 2.0, double sampleRateHz = 20.0)
        {
            double dt = 1.0 / sampleRateHz;
            double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
            _alpha = rc / (rc + dt);
        }

        public double Filter(double input)
        {
            double output = _alpha * (_prevOutput + input - _prevInput);
            _prevInput = input;
            _prevOutput = output;
            return output;
        }

        public void Reset()
        {
            _prevInput = 0;
            _prevOutput = 0;
        }
    }
}
