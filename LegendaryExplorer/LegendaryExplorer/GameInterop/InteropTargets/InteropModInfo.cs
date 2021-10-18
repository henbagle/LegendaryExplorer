﻿namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public record InteropModInfo
    {
        private static int InteropModVersion = 9;
        public string InteropModName { get; }
        public bool CanUseLLE { get; }
        public bool CanUseCamPath { get; init; } = false;
        public bool CanUseAnimViewer { get; init; } = false;
        public string LiveEditorFilename { get; init; }
        public int Version { get; init; } = InteropModVersion;

        public InteropModInfo(string interopModName, bool canUseLLE)
        {
            InteropModName = interopModName;
            CanUseLLE = canUseLLE;
        }
    }
}