﻿using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Tools.InterpEditor.InterpExperiments
{
    /// <summary>
    /// Class that holds toolset development experiments. Actual experiment code should be in the Experiments classes
    /// </summary>
    public partial class InterpExperimentsMenuControl : MenuItem
    {
        public InterpExperimentsMenuControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands() { }

        public InterpEditorWindow GetIEWindow()
        {
            if (Window.GetWindow(this) is InterpEditorWindow iew)
            {
                return iew;
            }

            return null;
        }

        // EXPERIMENTS: EXKYWOR------------------------------------------------------------
        #region Exkywor's experiments
        private void AddPresetDirectorGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Director", GetIEWindow());
        }

        private void AddPresetCameraGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Camera", GetIEWindow());
        }

        private void AddPresetActorGroup_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetGroup("Actor", GetIEWindow());
        }

        private void AddPresetGestureTrack_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetTrack("Gesture", GetIEWindow());
        }

        private void AddPresetGestureTrack2_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsE.AddPresetTrack("Gesture2", GetIEWindow());
        }
        #endregion

        // EXPERIMENTS: HenBagle------------------------------------------------------------
        #region HenBagle's Experiments
        private void OpenFovoLineAudio_Click(object sender, RoutedEventArgs e)
        {
            bool isMale = (string) (sender as FrameworkElement)?.Tag == "M";
            InterpEditorExperimentsH.OpenFovoLineAudio(isMale, GetIEWindow());
            var IEWindow = GetIEWindow();
        }

        private void OpenFovoLineFXA_Click(object sender, RoutedEventArgs e)
        {
            bool isMale = (string) (sender as FrameworkElement)?.Tag == "M";
            InterpEditorExperimentsH.OpenFovoLineFXA(isMale, GetIEWindow());
        }

        private void OpenFovoLineDlg_Click(object sender, RoutedEventArgs e)
        {
            InterpEditorExperimentsH.OpenFovoLineDialogueEditor(GetIEWindow());
        }
        #endregion
    }
}