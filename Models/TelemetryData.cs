namespace SHALLControl.Models
{
    public class TelemetryData
    {
        public float Pitch  { get; set; }   // degrees — forward/back tilt
        public float Roll   { get; set; }   // degrees — left/right tilt
        public float Yaw    { get; set; }   // degrees — rotation
        public float Surge  { get; set; }   // longitudinal g-force (accel/brake)
        public float Sway   { get; set; }   // lateral g-force (cornering)
        public float Heave  { get; set; }   // vertical g-force (bumps)
        public float Speed  { get; set; }   // km/h — informational
        public bool  IsValid { get; set; }

        public static TelemetryData Zero => new TelemetryData { IsValid = true };
    }
}
