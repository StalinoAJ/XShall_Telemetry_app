using System;

namespace SHALLControl
{
    /// <summary>
    /// 2nd-order Butterworth low-pass filter for smoothing telemetry data.
    /// Eliminates jitter/noise so the seat moves smoothly instead of shaking.
    /// </summary>
    public class ButterworthFilter
    {
        private readonly double _a0, _a1, _a2, _b1, _b2;
        private double[] _x = new double[3];
        private double[] _y = new double[3];

        /// <param name="cutoffHz">Cutoff frequency in Hz (e.g. 5.0)</param>
        /// <param name="sampleRateHz">Sample rate in Hz (e.g. 20.0)</param>
        public ButterworthFilter(double cutoffHz = 5.0, double sampleRateHz = 20.0)
        {
            double wc = Math.Tan(Math.PI * cutoffHz / sampleRateHz);
            double k1 = Math.Sqrt(2.0) * wc;
            double k2 = wc * wc;
            double norm = 1.0 + k1 + k2;

            _a0 = k2 / norm;
            _a1 = 2.0 * _a0;
            _a2 = _a0;
            _b1 = 2.0 * _a0 * (1.0 / k2 - 1.0);
            _b2 = 1.0 - (2.0 * _a0 + _b1);
        }

        public double Filter(double input)
        {
            _x[2] = _x[1]; _x[1] = _x[0]; _x[0] = input;
            _y[2] = _y[1]; _y[1] = _y[0];
            _y[0] = _a0 * _x[0] + _a1 * _x[1] + _a2 * _x[2]
                  - _b1 * _y[1] - _b2 * _y[2];
            return _y[0];
        }

        public void Reset()
        {
            _x = new double[3];
            _y = new double[3];
        }
    }
}
