using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Expressions;

namespace PicoFacialDataModule
{
    public class UnifiedExpressionsSetter
    {
        private readonly ShapeGain? gain;

        /// <param name="gain">Optional per-channel gain stage applied to every assigned weight.</param>
        public UnifiedExpressionsSetter(ShapeGain? gain = null) { this.gain = gain; }

        public float this[UnifiedExpressions index]
        {
            set
            {
                UnifiedTracking.Data.Shapes[(int)index].Weight = gain?.Apply(index, value) ?? value;
            }
        }
    }
}
