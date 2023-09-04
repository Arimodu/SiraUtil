﻿using SiraUtil.Affinity;

namespace SiraUtil.Tools.FPFC
{
    internal class FPFCFixDaemon : IAffinity
    {
        private readonly IFPFCSettings _fpfcSettings;

        public FPFCFixDaemon(IFPFCSettings fpfcSettings)
        {
            _fpfcSettings = fpfcSettings;
        }

        [AffinityPatch(typeof(OculusVRHelper), nameof(OculusVRHelper.hasInputFocus), AffinityMethodType.Getter)]
        [AffinityPatch(typeof(UnityXRHelper), nameof(UnityXRHelper.hasInputFocus), AffinityMethodType.Getter)]
        protected void ForceInputFocus(ref bool __result)
        {
            if (_fpfcSettings.Enabled)
                __result = true;
        }
    }
}