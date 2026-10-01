using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PicoFacialDataModule.Models
{
    public enum PicoBlendshapes
    {
        EyeLookDownL = 0,
        NoseSneerL = 1,
        EyeLookInL = 2,
        BrowInnerUp = 3,
        BrowDownR = 4,
        MouthClose = 5,
        MouthLowerDownR = 6,
        JawShapeOpen = 7,
        MouthUpperUpR = 8,
        MouthShrugUpper = 9,
        MouthFunnel = 10,
        EyeLookInR = 11,
        EyeLookDownR = 12,
        NoseSneerR = 13,
        MouthRollUpper = 14,
        JawShapeRight = 15,
        BrowDownL = 16,
        MouthShrugLower = 17,
        MouthRollLower = 18,
        MouthSmileL = 19,
        MouthPressL = 20,
        MouthSmileR = 21,
        MouthPressR = 22,
        MouthDimpleR = 23,
        MouthLeft = 24,
        JawShapeForward = 25,
        EyeSquintL = 26,
        MouthFrownL = 27,
        EyeBlinkL = 28,
        CheekSquintL = 29,
        BrowOuterUpL = 30,
        EyeLookUpL = 31,
        JawShapeLeft = 32,
        MouthStretchL = 33,
        MouthPucker = 34,
        EyeLookUpR = 35,
        BrowOuterUpR = 36,
        CheekSquintR = 37,
        EyeBlinkR = 38,
        MouthUpperUpL = 39,
        MouthFrownR = 40,
        EyeSquintR = 41,
        MouthStretchR = 42,
        CheekPuff = 43,
        EyeLookOutL = 44,
        EyeLookOutR = 45,
        EyeWideR = 46,
        EyeWideL = 47,
        MouthRight = 48,
        MouthDimpleL = 49,
        MouthLowerDownL = 50,
        TongueShapeOut = 51
    };

    public enum VideoInputType
    {
        VIDEO_INPUT_EYE,
        VIDEO_INPUT_FACE
    }

    /// <summary>
    /// The 52 blendshape slots the module actually reads. The vendor buffer has 72, but slots
    /// 52..71 are the audio-driven viseme block (always zero for this output) and are not sent.
    /// </summary>
    [InlineArray(52)]
    public struct BlendShapes
    {
        private float _element0;

        public float this[PicoBlendshapes shape]
        {
            get => this[(int)shape];
        }
    }

    [InlineArray(2)]
    public struct Float2
    {
        private float _element0;
    }

    /// <summary>
    /// Compact facial frame carried by a <c>'F'</c> datagram: timestamp, the 52 used blendshapes and
    /// the eye/face video-validity flags. LaughingProb, EmotionProb and slots 52..71 are not carried.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PicoFTInfo
    {
        public ulong Timestamp;

        public BlendShapes BlendshapeWeight;

        public Float2 VideoInputValid;

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Timestamp: {Timestamp}");

            foreach (var blendshape in Enum.GetValues<PicoBlendshapes>())
            {
                sb.AppendLine($"{blendshape.ToString()}: {BlendshapeWeight[(int)blendshape]}");
            }

            sb.AppendLine($"VideoInputValid: [{VideoInputValid[0]}, {VideoInputValid[1]}]");

            return sb.ToString();
        }
    }
}
