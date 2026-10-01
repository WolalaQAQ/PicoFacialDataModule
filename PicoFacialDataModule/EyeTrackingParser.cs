using PicoFacialDataModule.Models;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Expressions;

namespace PicoFacialDataModule
{
    using static PicoBlendshapes;
    using static UnifiedExpressions;
    using static EyePoseStatus;
    using static VideoInputType;

    public class EyeTrackingParser
    {
        private float eyePupilDilationEyeOpennessThreshold;
        private readonly ShapeGain shapeGain;

        public EyeTrackingParser(ModuleSettings settings)
        {
            this.eyePupilDilationEyeOpennessThreshold = settings.TrackingSettings.eyePupilDilationEyeOpennessThreshold;
            this.shapeGain = new ShapeGain(settings);
        }

        public void Parse(PxrEyePoseDataV2 eyeData, PicoFTInfo picoFTInfo, BridgeMode mode)
        {
            var eye = UnifiedTracking.Data.Eye;
            var blendshapes = picoFTInfo.BlendshapeWeight;
            var face = new UnifiedExpressionsSetter(shapeGain);

            bool faceValid = picoFTInfo.VideoInputValid[(int)VIDEO_INPUT_EYE] == 1;
            bool combinedValid = eyeData.CombinedEyePoseStatus.HasFlag(GAZE_VECTOR_VALID);

            #region LEFT EYE

            // Per-eye gaze is only usable when the bridge runs in enhanced mode AND this eye is gated open.
            bool leftPerEye = mode.Enhance && eyeData.LeftEyePoseStatus.HasFlag(EYE_GAZE_VECTOR_VALID);

            if (leftPerEye || combinedValid)
            {
                eye.Left.Gaze.x = leftPerEye ? eyeData.LeftEyeGazeVectorX : eyeData.CombinedEyeGazeVectorX;
                eye.Left.Gaze.y = leftPerEye ? eyeData.LeftEyeGazeVectorY : eyeData.CombinedEyeGazeVectorY;
            }

            eye.Left.Openness = eyeData.LeftEyePoseStatus.HasFlag(EYE_OPENNESS_VALID)
                ? eyeData.LeftEyeOpenness
                : faceValid ? 1f - blendshapes[EyeBlinkL] : eye.Left.Openness;

            bool leftPupilValid = eyeData.LeftEyePoseStatus.HasFlag(PUPIL_DIAMETER_VALID);
            float leftDilation = eyeData.LeftEyePupilDilation;

            // Legacy fallback (fixed 3.5 mm): only outside enhanced mode, when real pupil data is unavailable.
            if (leftDilation == 0 && !leftPupilValid && !mode.Enhance)
                leftDilation = 35f;

            // Enhanced mode trusts the vendor's PUPIL_DIAMETER_VALID bit. The openness threshold is a
            // stock-era proxy (the stock module never read 0x800): measured, a relaxed eye sits around
            // 0.63-0.69, so the 0.8 gate blocked real pupil almost all the time and only let it through
            // when the user widened the eyes. Normal mode keeps the old gate (its pupil is the fake 3.5 mm).
            bool leftPupilWritable = mode.Enhance
                ? leftPupilValid
                : eyeData.LeftEyeOpenness > eyePupilDilationEyeOpennessThreshold;

            if (leftPupilWritable && leftDilation > 0)
                eye.Left.PupilDiameter_MM = leftDilation / 10;

            #endregion

            #region RIGHT EYE

            bool rightPerEye = mode.Enhance && eyeData.RightEyePoseStatus.HasFlag(EYE_GAZE_VECTOR_VALID);

            if (rightPerEye || combinedValid)
            {
                eye.Right.Gaze.x = rightPerEye ? eyeData.RightEyeGazeVectorX : eyeData.CombinedEyeGazeVectorX;
                eye.Right.Gaze.y = rightPerEye ? eyeData.RightEyeGazeVectorY : eyeData.CombinedEyeGazeVectorY;
            }

            eye.Right.Openness = eyeData.RightEyePoseStatus.HasFlag(EYE_OPENNESS_VALID)
                ? eyeData.RightEyeOpenness
                : faceValid ? 1f - blendshapes[EyeBlinkR] : eye.Right.Openness;

            bool rightPupilValid = eyeData.RightEyePoseStatus.HasFlag(PUPIL_DIAMETER_VALID);
            float rightDilation = eyeData.RightEyePupilDilation;

            if (rightDilation == 0 && !rightPupilValid && !mode.Enhance)
                rightDilation = 35f;

            bool rightPupilWritable = mode.Enhance
                ? rightPupilValid
                : eyeData.RightEyeOpenness > eyePupilDilationEyeOpennessThreshold;

            if (rightPupilWritable && rightDilation > 0)
                eye.Right.PupilDiameter_MM = rightDilation / 10;

            #endregion

            #region Eye Widen

            // Only the facial-derived fields depend on a fresh, valid F sample. E is independent.
            if (!faceValid)
                return;

            // ARKit eyeWide is one of the face model's eye blendshapes and reaches the bridge in the
            // facial prefix, so it is available without opening the per-eye gate. Without this mapping
            // the widen shapes stay 0 and an avatar can only move between closed and open eyes.
            face[EyeWideLeft] = blendshapes[EyeWideL];
            face[EyeWideRight] = blendshapes[EyeWideR];

            // Eye squint is the other eye blendshape the model provides and the module was dropping.
            // VRCFT uses it for v2/EyeSquint(EyesSquint) and, in the legacy lid params, for the
            // squeeze term subtracted from the expanded eyelid.
            face[EyeSquintLeft] = blendshapes[EyeSquintL];
            face[EyeSquintRight] = blendshapes[EyeSquintR];

            #endregion

            #region Eyebrow Expressions

            face[BrowPinchRight] = blendshapes[BrowDownR];
            face[BrowPinchLeft] = blendshapes[BrowDownL];
            face[BrowLowererRight] = blendshapes[BrowDownR];
            face[BrowLowererLeft] = blendshapes[BrowDownL];
            face[BrowInnerUpRight] = blendshapes[BrowInnerUp];
            face[BrowInnerUpLeft] = blendshapes[BrowInnerUp];
            face[BrowOuterUpRight] = blendshapes[BrowOuterUpR];
            face[BrowOuterUpLeft] = blendshapes[BrowOuterUpL];

            #endregion

#if EYEDEBUG
            Console.SetCursorPosition(0,0);
            Console.Write(eyeData);
#endif
        }
    }
}
