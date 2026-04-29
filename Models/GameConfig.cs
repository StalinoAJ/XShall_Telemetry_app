namespace SHALLControl.Models
{
    public class GameConfig
    {
        public string Name        { get; set; }
        public string PluginType  { get; set; }   // "Forza" | "ETS2" | "F1"
        public int    UdpPort     { get; set; }
        public string ExePath     { get; set; }

        // Per-axis intensity multipliers (0.0 – 3.0, default 1.0)
        public float PitchScale  { get; set; } = 1.0f;
        public float RollScale   { get; set; } = 1.0f;
        public float YawScale    { get; set; } = 1.0f;

        // Safety clamp (max degrees sent to seat)
        public int MaxAngle      { get; set; } = 15;
    }
}
