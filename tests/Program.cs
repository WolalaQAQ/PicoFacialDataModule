using System.Reflection;
using PicoFacialDataModule;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using Module = PicoFacialDataModule.PicoFacialDataModule;

// No Initialize(): never binds the live UDP port, loads user settings or deploys a DLL.
var flags = BindingFlags.Instance | BindingFlags.NonPublic;
var settings = new ModuleSettings { DisableFaceTracking = true };
Module Create() {
    var m = new Module();
    typeof(Module).GetField("_moduleSettings", flags)!.SetValue(m, settings);
    typeof(Module).GetField("_eyeTrackingParser", flags)!.SetValue(m, new EyeTrackingParser(settings));
    typeof(Module).GetField("_faceTrackingParser", flags)!.SetValue(m, new FaceTrackingParser(settings));
    UnifiedTracking.Data = new UnifiedTrackingData();
    return m;
}
void Send(Module m, byte[] b) => typeof(Module).GetMethod("ProcessReply", flags)!.Invoke(m, new object[] { b });
void Float(byte[] b, int p, float v) => BitConverter.GetBytes(v).CopyTo(b, p);
byte[] Eye(ulong t, float gaze) {
    var b = new byte[73]; b[0] = (byte)'E'; BitConverter.GetBytes(t).CopyTo(b, 1);
    BitConverter.GetBytes(0x14u).CopyTo(b, 9); BitConverter.GetBytes(0x14u).CopyTo(b, 13);
    BitConverter.GetBytes(2u).CopyTo(b, 17); Float(b, 45, gaze); Float(b, 57, .7f); Float(b, 61, .7f);
    return b;
}
byte[] Face(ulong t, float valid, float wide) {
    var b = new byte[225]; b[0] = (byte)'F'; BitConverter.GetBytes(t).CopyTo(b, 1);
    Float(b, 217, valid); Float(b, 221, 0); Float(b, 9 + 47 * 4, wide); return b;
}
int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
var m = Create();
Send(m, Face(1_000_000_000, 0, .8f)); Send(m, Eye(1_010_000_000, .7f));
Check(UnifiedTracking.Data.Eye.Left.Gaze.x == .7f, "Invalid F must not block valid E gaze");
Check(UnifiedTracking.Data.Eye.Left.Openness == .7f, "Invalid F must not block valid E openness");
Send(m, Face(2_000_000_000, 1, .8f)); Send(m, Eye(2_040_000_000, .3f));
Check(UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight == .8f, "Fresh eye-only F shapes must work with face output disabled");
UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight = .2f;
Send(m, Eye(3_000_000_000, .9f));
Check(UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight == .2f, "Stale F must not be replayed");
Check(UnifiedTracking.Data.Eye.Left.Gaze.x == .9f, "Stale F must not stop eyes");
typeof(Module).GetMethod("ResetSession", flags)!.Invoke(m, null);
Send(m, Eye(2_050_000_000, .4f));
Check(UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight == .2f, "New session must discard old F even if timestamps are close");
Console.WriteLine($"PASS: {checks} split receiver regressions");
