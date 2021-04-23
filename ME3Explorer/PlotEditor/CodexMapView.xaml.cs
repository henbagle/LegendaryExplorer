using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Explorer.PlotEditor.Dialogs;
using ME3Explorer;
using ME3ExplorerCore.Gammtek;
using ME3ExplorerCore.Packages;
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;

namespace ME3Explorer.PlotEditor
{
	/// <summary>
	///   Interaction logic for CodexMapView.xaml.
	/// </summary>
	public partial class CodexMapView : NotifyPropertyChangedControlBase
	{
        public static IMEPackage package;

		/// <summary>
		///   Initializes a new instance of the <see cref="CodexMapView" /> class.
		/// </summary>
		public CodexMapView()
		{
            try
            {
                InitializeComponent();
            }
            catch (Exception e)
            {

                 throw;
            }
            //SetFromCodexMap(new BioCodexMap());
        }

        private ObservableCollection<BioCodexPage> _codexPages;
        private ObservableCollection<BioCodexSection> _codexSections;
        private BioCodexPage _selectedCodexPage;
        private BioCodexSection _selectedCodexSection;

        public bool CanRemoveCodexPage
        {
            get
            {
                if (CodexPages == null || CodexPages.Count <= 0)
                {
                    return false;
                }

                return SelectedCodexPage != null;
            }
        }

        public bool CanRemoveCodexSection
        {
            get
            {
                if (CodexSections == null || CodexSections.Count <= 0)
                {
                    return false;
                }

                return SelectedCodexSection != null;
            }
        }

        public ObservableCollection<BioCodexPage> CodexPages
        {
            get => _codexPages;
            set
            {
                SetProperty(ref _codexPages, value);
                OnPropertyChanged(nameof(CanRemoveCodexPage));
                //CodexPagesListBox.ItemsSource = CodexPages;
            }
        }

        public ObservableCollection<BioCodexSection> CodexSections
        {
            get => _codexSections;
            set
            {
                SetProperty(ref _codexSections, value);
                OnPropertyChanged(nameof(CanRemoveCodexSection));
            }
        }

        public BioCodexPage SelectedCodexPage
        {
            get => _selectedCodexPage;
            set
            {
                SetProperty(ref _selectedCodexPage, value);
                OnPropertyChanged(nameof(CanRemoveCodexPage));
            }
        }

        public BioCodexSection SelectedCodexSection
        {
            get => _selectedCodexSection;
            set
            {
                SetProperty(ref _selectedCodexSection, value);
                OnPropertyChanged(nameof(CanRemoveCodexSection));
            }
        }

        public void AddCodexPage()
        {
            if (CodexPages == null)
            {
                CodexPages = InitCollection<BioCodexPage>();
            }

            var dlg = new NewObjectDialog
            {
                ContentText = "New codex page",
                ObjectId = GetMaxCodexPageId() + 1
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
            {
                return;
            }
            AddCodexPage(new BioCodexPage { ID = dlg.ObjectId });
            
        }

        public void AddCodexPage(BioCodexPage codexPage)
        {
            if (CodexPages == null)
            {
                CodexPages = InitCollection<BioCodexPage>();
            }

            if (codexPage.ID < 0 || CodexPages.Any(el => el.ID == codexPage.ID))
            {
                return;
            }

            CodexPages.Add(codexPage);
            SelectedCodexPage = codexPage;
        }

        public void AddCodexSection()
        {
            if (CodexSections == null)
            {
                CodexSections = InitCollection<BioCodexSection>();
            }

            var dlg = new NewObjectDialog
            {
                ContentText = "New codex section",
                ObjectId = GetMaxCodexSectionId() + 1
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
            {
                return;
            }

            CodexSections.Add(new BioCodexSection { ID = dlg.ObjectId });
        }

        // Does not replace existing
        public void AddCodexSection(BioCodexSection codexSection)
        {
            if (CodexSections == null)
            {
                CodexSections = InitCollection<BioCodexSection>();
            }

            if (codexSection.ID < 0 || CodexSections.Any(el => el.ID == codexSection.ID))
            {
                return;
            }

            CodexSections.Add(codexSection);

            SelectedCodexSection = codexSection;
        }

        public void ChangeCodexPageId()
        {
            if (SelectedCodexPage == null)
            {
                return;
            }

            var dlg = new ChangeObjectIdDialog
            {
                ContentText = $"Change id of codex page #{SelectedCodexPage.ID}",
                ObjectId = SelectedCodexPage.ID
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || dlg.ObjectId == SelectedCodexPage.ID)
            {
                return;
            }
            SelectedCodexPage.ID = dlg.ObjectId;


        }

        public void ChangeCodexSectionId()
        {
            if (SelectedCodexSection == null)
            {
                return;
            }

            var dlg = new ChangeObjectIdDialog
            {
                ContentText = $"Change id of codex section #{SelectedCodexSection.ID}",
                ObjectId = SelectedCodexSection.ID
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || dlg.ObjectId == SelectedCodexSection.ID)
            {
                return;
            }
            SelectedCodexSection.ID = dlg.ObjectId;
        }

        public void CopyCodexPage()
        {
            if (SelectedCodexPage == null)
            {
                return;
            }

            var dlg = new CopyObjectDialog
            {
                ContentText = $"Copy codex page #{SelectedCodexPage.ID}",
                ObjectId = GetMaxCodexPageId() + 1
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedCodexPage.ID == dlg.ObjectId)
            {
                return;
            }

            AddCodexPage(SelectedCodexPage.Clone(dlg.ObjectId));
        }

        public void CopyCodexSection()
        {
            if (SelectedCodexSection == null)
            {
                return;
            }

            var dlg = new CopyObjectDialog
            {
                ContentText = $"Copy codex section #{SelectedCodexSection.ID}",
                ObjectId = GetMaxCodexSectionId() + 1
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedCodexSection.ID == dlg.ObjectId)
            {
                return;
            }

            AddCodexSection(SelectedCodexSection.Clone(dlg.ObjectId));
        }

        public void GoToCodexPage(BioCodexPage codexPage)
        {
            CodexTabControl.SelectedValue = CodexPagesTab;
            SelectedCodexPage = codexPage;
            CodexPagesListBox.ScrollIntoView(SelectedCodexPage);
            CodexPagesListBox.Focus();
        }

        public void GoToCodexSection(BioCodexSection codexSection)
        {
            CodexTabControl.SelectedValue = CodexSectionsTab;
            SelectedCodexSection = codexSection;
            CodexSectionsListBox.ScrollIntoView(SelectedCodexSection);
            CodexSectionsListBox.Focus();
        }

        public static bool TryFindCodexMap(IMEPackage pcc, out ExportEntry export, out int dataOffset)
        {
            export = null;
            dataOffset = -1;

            try
            {
                export = pcc.Exports.First(exp => exp.ClassName == "BioCodexMap");
            }
            catch
            {
                return false;
            }

            dataOffset = export.propsEnd();

            return true;
        }

        public void Open(IMEPackage pcc)
        {
            if (!TryFindCodexMap(pcc, out ExportEntry export, out int dataOffset))
            {
                return;
            }

            var codexMap = export.GetBinaryData<BioCodexMap>();
            CodexPages = InitCollection(codexMap.Pages.OrderBy(p => p.ID));
            CodexSections = InitCollection(codexMap.Sections.OrderBy(s => s.ID));

            foreach (var page in CodexPages)
            {
                page.TitleAsString = GlobalFindStrRefbyID(page.Title, pcc.Game, null);
            }

            foreach (var section in CodexSections)
            {
                section.TitleAsString = GlobalFindStrRefbyID(section.Title, pcc.Game, null);
            }

            package = pcc;

        }

        public void RemoveCodexPage()
        {
            if (CodexPages == null || SelectedCodexPage == null)
            {
                return;
            }

            var index = CodexPages.IndexOf(SelectedCodexPage);

            if (!CodexPages.Remove(SelectedCodexPage))
            {
                return;
            }

            if (CodexPages.Any())
            {
                SelectedCodexPage = ((index - 1) >= 0)
                    ? CodexPages[index - 1]
                    : CodexPages.First();
            }
        }

        public void RemoveCodexSection()
        {
            if (CodexSections == null || SelectedCodexSection == null)
            {
                return;
            }

            var index = CodexSections.IndexOf(SelectedCodexSection);

            if (!CodexSections.Remove(SelectedCodexSection))
            {
                return;
            }

            if (CodexSections.Any())
            {
                SelectedCodexSection = ((index - 1) >= 0)
                    ? CodexSections[index - 1]
                    : CodexSections.First();
            }
        }

        public void SaveToPcc(IMEPackage pcc)
        {
            ExportEntry export;
            try
            {
                export = pcc.Exports.First(exp => exp.ClassName == "BioCodexMap");
            }
            catch
            {
                return;
            }

            BioCodexMap codexMap = new BioCodexMap
            {
                Pages = CodexPages.ToList(),
                Sections = CodexSections.ToList()
            };

            export.WriteBinary(codexMap);
        }
        
        public BioCodexMap ToCodexMap()
        {
            var codexMap = new BioCodexMap
            {
                Pages = CodexPages.ToList(),
                Sections = CodexSections.ToList()
            };

            return codexMap;
        }

        protected void SetFromCodexMap(BioCodexMap codexMap)
        {
            if (codexMap == null)
            {
                return;
            }

            CodexPages = InitCollection(codexMap.Pages.OrderBy(p => p.ID));
            CodexSections = InitCollection(codexMap.Sections.OrderBy(s => s.ID));
        }
        
        private static ObservableCollection<T> InitCollection<T>()
        {
            return new ObservableCollection<T>();
        }

        
        private static ObservableCollection<T> InitCollection<T>(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(collection));
            }

            return new ObservableCollection<T>(collection);
        }

        private int GetMaxCodexPageId()
        {
            return CodexPages.Any() ? CodexPages.Max(pair => pair.ID) : -1;
        }

        private int GetMaxCodexSectionId()
        {
            return CodexSections.Any() ? CodexSections.Max(pair => pair.ID) : -1;
        }

        private void ChangeCodexPageId_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ChangeCodexPageId();
        }

        private void CopyCodexPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CopyCodexPage();
        }

        private void RemoveCodexPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RemoveCodexPage();
        }

        private void ChangeCodexSectionId_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ChangeCodexSectionId();
        }

        private void CopyCodexSection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CopyCodexSection();
        }

        private void RemoveCodexSection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RemoveCodexSection();
        }

        private void AddCodexSection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddCodexSection();
        }

        private void AddCodexPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddCodexPage();
        }

        private void txt_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if(package != null)
            {
                txt_cdxPgeDesc.Text = GlobalFindStrRefbyID(SelectedCodexPage?.Description ?? 0, package);
                txt_cdxPgeTitle.Text = GlobalFindStrRefbyID(SelectedCodexPage?.Title ?? 0, package);
                txt_cdxSecDesc.Text = GlobalFindStrRefbyID(SelectedCodexSection?.Description ?? 0, package);
                txt_cdxSecTitle.Text = GlobalFindStrRefbyID(SelectedCodexSection?.Title ?? 0, package);

                if (SelectedCodexPage != null) SelectedCodexPage.TitleAsString = txt_cdxPgeTitle.Text;
                if (SelectedCodexSection != null) SelectedCodexSection.TitleAsString = txt_cdxSecTitle.Text;
            }
        }
    }
}
