using System.Runtime.InteropServices;
using System.Text;

namespace PicoFacialDataModule.Models
{
    [Flags]
    public enum EyePoseStatus : uint
    {
        GAZE_POINT_VALID = (1 << 0),
        GAZE_VECTOR_VALID = (1 << 1),
        EYE_OPENNESS_VALID = (1 << 2),
        EYE_PUPIL_DILATION_VALID = (1 << 3),
        EYE_POSITION_GUIDE_VALID = (1 << 4),
        EYE_PUPIL_POSITION_VALID = (1 << 5),
        EYE_CONVERGENCE_DISTANCE_VALID = (1 << 6),
        EYE_GAZE_POINT_VALID = (1 << 7),
        EYE_GAZE_VECTOR_VALID = (1 << 8),
        PUPIL_DISTANCE_VALID = (1 << 9),
        CONVERGENCE_DISTANCE_VALID = (1 << 10),
        PUPIL_DIAMETER_VALID = (1 << 11),
    };

    /// <summary>
    /// Compact eye frame carried by an <c>'E'</c> datagram: only the validity bits, gaze vectors,
    /// openness and pupil diameters the module reads. The vendor's 3D gaze points, position guides
    /// and foveated slots are not carried. Unlike the old 536-byte packet, <see cref="Timestamp"/>
    /// is the real eye-service timestamp (the shared struct previously read zero padding).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PxrEyePoseDataV2
    {
        public ulong Timestamp;

        public EyePoseStatus LeftEyePoseStatus;
        public EyePoseStatus RightEyePoseStatus;
        public EyePoseStatus CombinedEyePoseStatus;

        public float LeftEyeGazeVectorX;
        public float LeftEyeGazeVectorY;
        public float LeftEyeGazeVectorZ;

        public float RightEyeGazeVectorX;
        public float RightEyeGazeVectorY;
        public float RightEyeGazeVectorZ;

        public float CombinedEyeGazeVectorX;
        public float CombinedEyeGazeVectorY;
        public float CombinedEyeGazeVectorZ;

        public float LeftEyeOpenness;
        public float RightEyeOpenness;

        public float LeftEyePupilDilation;
        public float RightEyePupilDilation;

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Timestamp: {Timestamp}");

            sb.AppendLine($"LeftEyePoseStatus: {(uint)LeftEyePoseStatus:B8}");
            sb.AppendLine($"RightEyePoseStatus: {(uint)RightEyePoseStatus:B8}");
            sb.AppendLine($"CombinedEyePoseStatus: {(uint)CombinedEyePoseStatus:B8}");

            sb.AppendLine($"LeftEyeGazeVector: {LeftEyeGazeVectorX}, {LeftEyeGazeVectorY}, {LeftEyeGazeVectorZ}");
            sb.AppendLine($"RightEyeGazeVector: {RightEyeGazeVectorX}, {RightEyeGazeVectorY}, {RightEyeGazeVectorZ}");
            sb.AppendLine($"CombinedEyeGazeVector: {CombinedEyeGazeVectorX}, {CombinedEyeGazeVectorY}, {CombinedEyeGazeVectorZ}");

            sb.AppendLine($"LeftEyeOpenness: {LeftEyeOpenness}");
            sb.AppendLine($"RightEyeOpenness: {RightEyeOpenness}");

            sb.AppendLine($"LeftEyePupilDilation: {LeftEyePupilDilation}");
            sb.AppendLine($"RightEyePupilDilation: {RightEyePupilDilation}");

            return sb.ToString();
        }
    }
}
