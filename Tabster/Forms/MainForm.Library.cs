﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using RecentFilesMenuItem;
using Tabster.Core.Types;
using Tabster.Data;
using Tabster.Data.Binary;
using Tabster.Data.Library;
using Tabster.Data.Processing;
using Tabster.Database;
using Tabster.Printing;
using Tabster.Properties;
using Tabster.Utilities;
using Tabster.WinForms.Extensions;

#endregion

namespace Tabster
{
    internal enum PreviewPanelOrientation
    {
        Hidden,
        Horizontal,
        Vertical
    }
}

namespace Tabster.Forms
{
    internal partial class MainForm
    {
        private readonly List<TablatureLibraryItem<TablatureFile>> _libraryCache = new List<TablatureLibraryItem<TablatureFile>>();
        private bool _changingLibraryView;
        private List<ITablatureFileExporter> _fileExporters = new List<ITablatureFileExporter>();
        private List<ITablatureFileImporter> _fileImporters = new List<ITablatureFileImporter>();

        //used to prevent double-triggering of OnSelectedIndexChanged for tablibrary when using navigation menu

        /// <summary>
        ///     Returns whether the library tab is currently focused.
        /// </summary>
        private bool IsViewingLibrary()
        {
            return tabControl1.SelectedTab == display_library;
        }

        private TablatureLibraryItem<TablatureFile> GetSelectedLibraryItem()
        {
            return listViewLibrary.SelectedObject != null ? (TablatureLibraryItem<TablatureFile>) listViewLibrary.SelectedObject : null;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            var item = GetSelectedLibraryItem();

            if (item != null)
            {
                PopoutTab(item.File, item.FileInfo);
            }
        }

        private void ExportTab(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                using (var sfd = new SaveFileDialog
                {
                    Title = Resources.ExportTabDialogTitle,
                    AddExtension = true,
                    Filter = string.Format("{0} (*{1})|*{1}", Resources.TabsterFile, Constants.TablatureFileExtension),
                    FileName = GetSelectedLibraryItem().File.ToFriendlyString()
                })
                {
                    var filters = sfd.SetTabsterFilter(_fileExporters, alphabeticalOrder: true);

                    if (sfd.ShowDialog() != DialogResult.Cancel)
                    {
                        //native file format
                        if (sfd.FilterIndex == 1)
                        {
                            GetSelectedLibraryItem().File.Save(sfd.FileName);
                        }

                        else
                        {
                            var exporter = filters[sfd.FilterIndex - 2].Exporter; //FilterIndex is not 0-based and native Tabster format uses first index
                            var args = new TablatureFileExportArguments(TablatureFontManager.GetFont());

                            try
                            {
                                exporter.Export(GetSelectedLibraryItem().File, sfd.FileName, args);
                            }

                            catch (Exception ex)
                            {
                                Logging.GetLogger().Error(Resources.ExportErrorDialogCaption, ex);
                                MessageBox.Show(Resources.ExportErrorDialogCaption, Resources.ExportErrorDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateTabControls(bool beginPreviewLoadTimer)
        {
            if (listViewLibrary.SelectedItems.Count > 0)
            {
                var pathCol = listViewLibrary.SelectedItem.SubItems[olvColLocation.Index];

                if (pathCol != null)
                {
                    if (pathCol.Text != null)
                    {
                        var openedExternally = TablatureViewForm.GetInstance(this).IsFileOpen(GetSelectedLibraryItem().FileInfo);

                        deleteTabToolStripMenuItem.Enabled = librarycontextdelete.Enabled = !openedExternally;
                        detailsToolStripMenuItem.Enabled = librarycontextdetails.Enabled = !openedExternally;
                    }
                }
            }

            else
            {
                deleteTabToolStripMenuItem.Enabled = false;
                detailsToolStripMenuItem.Enabled = false;
            }

            menuItem3.Enabled = GetSelectedLibraryItem() != null;

            if (beginPreviewLoadTimer)
            {
                PreviewDisplayDelay.Stop();
                PreviewDisplayDelay.Start();
            }
        }

        private TablaturePrintDocumentSettings GetPrintSettings()
        {
            return new TablaturePrintDocumentSettings
            {
                Title = GetSelectedLibraryItem().File.ToFriendlyString(),
                PrintColor = Settings.Default.PrintColor,
                DisplayTitle = true,
                DisplayPrintTime = Settings.Default.PrintTimestamp,
                DisplayPageNumbers = Settings.Default.PrintPageNumbers
            };
        }

        private void printbtn_Click(object sender, EventArgs e)
        {
            PreviewEditor.Print(GetPrintSettings());
        }

        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PreviewEditor.PrintPreview(GetPrintSettings());
        }

        private void printSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPreferences(PreferencesDialog.PreferencesSection.Printing);
        }

        private void NewTab(object sender, EventArgs e)
        {
            using (var n = new NewTabDialog())
            {
                if (n.ShowDialog() == DialogResult.OK)
                {
                    var item = _libraryManager.Add(n.Tab);
                    PopoutTab(item.File, item.FileInfo);
                }
            }
        }

        private void PopoutTab(TablatureFile file, FileInfo fileInfo, bool updateRecentFiles = true)
        {
            TablatureViewForm.GetInstance(this).LoadTablature(file, fileInfo);

            if (updateRecentFiles)
            {
                _recentFilesManager.Add(new RecentFile(file, fileInfo));
                recentlyViewedMenuItem.Add(new RecentMenuItem(fileInfo) {DisplayText = file.ToFriendlyString()});
            }

            var libraryItem = _libraryManager.FindTablatureItemByFile(file);
            if (libraryItem != null)
            {
                libraryItem.Views += 1;
                libraryItem.LastViewed = DateTime.UtcNow;
            }

            LoadTabPreview();
        }

        private void SearchSimilarTabs(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                txtSearchArtist.Text = sender == searchByArtistToolStripMenuItem || sender == searchByArtistAndTitleToolStripMenuItem
                    ? GetSelectedLibraryItem().File.Artist
                    : "";

                txtSearchTitle.Text = sender == searchByTitleToolStripMenuItem || sender == searchByArtistAndTitleToolStripMenuItem
                    ? TablatureUtilities.RemoveVersionConventionFromTitle(GetSelectedLibraryItem().File.Title)
                    : "";

                searchTypeList.SelectDefault();
                tabControl1.SelectedTab = display_search;
                onlinesearchbtn.PerformClick();
            }
        }

        private void DeleteTab(object sender, EventArgs e)
        {
            if (!IsViewingLibrary())
                return;

            var selectedItem = GetSelectedLibraryItem();

            if (selectedItem != null)
            {
                var removed = false;

                if (SelectedLibrary() == LibraryType.Playlist)
                {
                    var selectedPlaylist = GetSelectedPlaylist();

                    if (selectedPlaylist != null)
                    {
                        if (MessageBox.Show(Resources.RemoveTabFromPlaylistDialogCaption, Resources.RemoveTabFromPlaylistDialogTitle,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            selectedPlaylist.Remove(selectedItem.FileInfo.FullName);
                            removed = true;
                            _playlistManager.Update(selectedPlaylist);
                        }
                    }
                }

                else
                {
                    if (MessageBox.Show(Resources.DeleteTabDialogCaption, Resources.DeleteTabDialogTitle,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _libraryManager.Remove(selectedItem);
                        removed = true;
                    }
                }

                if (removed)
                {
                    RemoveSelectedTablatureLibraryItem();
                }
            }
        }

        private void OpenTabLocation(object sender, EventArgs e)
        {
            if (!IsViewingLibrary())
                return;

            if (GetSelectedLibraryItem() != null)
            {
                if (MonoUtilities.GetPlatform() == MonoUtilities.Platform.Windows)
                    Process.Start("explorer.exe ", @"/select, " + GetSelectedLibraryItem().FileInfo.FullName);
                else
                    Process.Start(GetSelectedLibraryItem().FileInfo.DirectoryName);
            }
        }

        private void BrowseTab(object sender, EventArgs e)
        {
            if (!IsViewingLibrary())
                return;

            using (var ofd = new OpenFileDialog
            {
                Title = Resources.OpenTabDialogTitle,
                AddExtension = true,
                Multiselect = false,
                Filter = string.Format("{0} (*{1})|*{1}", Resources.TabsterFiles, Constants.TablatureFileExtension)
            })
            {
                if (ofd.ShowDialog() != DialogResult.Cancel)
                {
                    var tab = _libraryManager.GetTablatureFileProcessor().Load(ofd.FileName);

                    if (tab != null)
                    {
                        var item = _libraryManager.Add(tab);
                        PopoutTab(item.File, item.FileInfo);
                    }
                }
            }
        }

        private void sidemenu_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            _changingLibraryView = true;
        }

        private void sidemenu_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _changingLibraryView = false;

            var viewsColumnVisible = olvColViews.IsVisible;

            var shouldViewsColumnBeVisible = SelectedLibrary() != LibraryType.Playlist;

            if (viewsColumnVisible != shouldViewsColumnBeVisible)
            {
                //hide visibility toggle from user
                olvColViews.Hideable = shouldViewsColumnBeVisible;
                olvColViews.IsVisible = shouldViewsColumnBeVisible;
                listViewLibrary.RebuildColumns();
            }

            if (!string.IsNullOrEmpty(txtLibraryFilter.Text))
            {
                txtLibraryFilter.Clear();
            }

            else
            {
                BuildLibraryCache(false);
            }
        }

        private void ToggleFavorite(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                GetSelectedLibraryItem().Favorited = !GetSelectedLibraryItem().Favorited;

                //remove item from favorites display
                if (!GetSelectedLibraryItem().Favorited && SelectedLibrary() == LibraryType.MyFavorites)
                {
                    RemoveSelectedTablatureLibraryItem();
                }
            }
        }

        private void sidemenu_MouseClick(object sender, MouseEventArgs e)
        {
            var selectedNode = sidemenu.HitTest(e.X, e.Y).Node;

            if (selectedNode != null)
            {
                if (e.Button == MouseButtons.Right && SelectedLibrary() == LibraryType.Playlist)
                {
                    sidemenu.SelectedNode = selectedNode;
                    deleteplaylistcontextmenuitem.Visible = true;
                    PlaylistMenu.Show(sidemenu.PointToScreen(e.Location));
                }
            }
        }

        private void listViewLibrary_CellRightClick(object sender, CellRightClickEventArgs e)
        {
            var selectedItem = GetSelectedLibraryItem();

            if (selectedItem == null)
                return;

            //check if playlists already contains tablature
            foreach (var item in librarycontextaddtoplaylist.DropDownItems)
            {
                var toolItem = item as ToolStripMenuItem;

                if (toolItem != null && toolItem.Tag != null)
                {
                    var associatedPlaylist = toolItem.Tag as TablaturePlaylist;
                    toolItem.Enabled = associatedPlaylist.Find(x => x.FileInfo.FullName.Equals(selectedItem.FileInfo.FullName)) == null;
                }
            }

            librarycontextfavorites.Text = GetSelectedLibraryItem().Favorited ? Resources.RemoveFromFavorites : Resources.AddToFavorites;

            e.MenuStrip = LibraryMenu;
        }

        private LibraryType SelectedLibrary()
        {
            var selectedNode = sidemenu.SelectedNode;

            if (selectedNode == null)
                return LibraryType.AllTabs;

            if (selectedNode.Parent != null && selectedNode.Parent.Name == "node_playlists")
                return LibraryType.Playlist;

            switch (sidemenu.SelectedNode.Name)
            {
                case "node_alltabs":
                    return LibraryType.AllTabs;
                case "node_mytabs":
                    return LibraryType.MyTabs;
                case "node_mydownloads":
                    return LibraryType.MyDownloads;
                case "node_myimports":
                    return LibraryType.MyImports;
                case "node_myfavorites":
                    return LibraryType.MyFavorites;
            }

            return LibraryType.TabType;
        }

        private bool TablatureLibraryItemVisible(LibraryType selectedLibrary, TablatureLibraryItem<TablatureFile> item)
        {
            var libraryMatch =
                selectedLibrary == LibraryType.Playlist ||
                selectedLibrary == LibraryType.AllTabs ||
                (selectedLibrary == LibraryType.MyTabs && item.File.SourceType == TablatureSourceType.UserCreated) ||
                (selectedLibrary == LibraryType.MyDownloads && item.File.SourceType == TablatureSourceType.Download) ||
                (selectedLibrary == LibraryType.MyImports && item.File.SourceType == TablatureSourceType.FileImport) ||
                (selectedLibrary == LibraryType.MyFavorites && item.Favorited) ||
                (selectedLibrary == LibraryType.TabType && sidemenu.SelectedNode.Tag.ToString() == item.File.Type.ToString());

            var searchValue = txtLibraryFilter.Text;

            if (libraryMatch)
            {
                return searchValue == null || (item.File.Artist.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               item.File.Title.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               item.FileInfo.FullName.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               (item.File.Comment != null && item.File.Comment.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                               item.File.Contents.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return false;
        }

        private void BuildLibraryCache(bool persistSelectedItem = true)
        {
            var selectedLibrary = SelectedLibrary();

            var items = new List<TablatureLibraryItem<TablatureFile>>();

            if (selectedLibrary == LibraryType.Playlist)
            {
                var selectedPlaylist = GetSelectedPlaylist();

                //todo improve this so we aren't creating arbitary items
                foreach (var tab in selectedPlaylist)
                {
                    var file = (TablatureFile) tab.File;

                    var dummyItem = new TablatureLibraryItem<TablatureFile>(file, tab.FileInfo);
                    items.Add(dummyItem);
                }
            }

            else
            {
                items.AddRange(_libraryManager.GetTablatureItems());
            }

            var currentItem = GetSelectedLibraryItem();

            _libraryCache.Clear();

            foreach (var item in items)
            {
                var visible = TablatureLibraryItemVisible(selectedLibrary, item);

                if (visible)
                    _libraryCache.Add(item);
            }

            listViewLibrary.SetObjects(_libraryCache);

            if (listViewLibrary.Items.Count > 0)
            {
                //persistant library selection
                if (persistSelectedItem && currentItem != null && _libraryCache.Contains(currentItem))
                {
                    listViewLibrary.SelectObject(currentItem);
                }

                else
                {
                    listViewLibrary.Items[0].Selected = true;
                }
            }
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Title = Resources.ImportTabDialogTitle,
                Filter = string.Format("{0} (*{1})|*{1}", Resources.TabsterFile, Constants.TablatureFileExtension),
                Multiselect = false
            })
            {
                ofd.SetTabsterFilter(_fileImporters, allSupportedTypesOption: false, alphabeticalOrder: true); //todo implement "all supported types" handling

                if (ofd.ShowDialog() != DialogResult.Cancel)
                {
                    //native file format
                    if (ofd.FilterIndex == 1)
                    {
                        var file = _libraryManager.GetTablatureFileProcessor().Load(ofd.FileName);

                        if (file != null)
                        {
                            _libraryManager.Add(file);
                        }
                    }

                    else // third-party format
                    {
                        var importer = _fileImporters[ofd.FilterIndex - 2]; //FilterIndex is not 0-based and native Tabster format uses first index

                        AttributedTablature importedTab = null;

                        try
                        {
                            importedTab = importer.Import(ofd.FileName);
                        }

                        catch
                        {
                            MessageBox.Show(Resources.ImportErrorDialogCaption, Resources.ImportErrorDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        if (importedTab != null)
                        {
                            using (var nd = new NewTabDialog(importedTab.Artist, importedTab.Title, importedTab.Type))
                            {
                                if (nd.ShowDialog() == DialogResult.OK)
                                {
                                    var tab = nd.Tab;
                                    tab.Contents = importedTab.Contents;
                                    tab.Source = new Uri(ofd.FileName);
                                    tab.SourceType = TablatureSourceType.FileImport;
                                    _libraryManager.Add(tab);
                                    UpdateDetails();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void viewTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = GetSelectedLibraryItem();

            if (item != null)
            {
                PopoutTab(item.File, item.FileInfo);
            }
        }

        private void UpdateDetails()
        {
            lblcount.Text = string.Format("Total Tabs: {0}", _libraryManager.Count);
            lblplaylists.Text = string.Format("Playlists: {0}", _playlistManager.Count);
        }

        private void RemoveSelectedTablatureLibraryItem()
        {
            RemoveTablatureLibraryItem(listViewLibrary.SelectedIndex);
        }

        private void RemoveTablatureLibraryItem(int index)
        {
            var item = _libraryCache[index];
            listViewLibrary.RemoveObject(item);

            _libraryCache.Remove(item);

            listViewLibrary.SelectedIndex = index > 0 ? index - 1 : 0;

            UpdateDetails();
        }

        private void TabDetails(object sender, EventArgs e)
        {
            if (!IsViewingLibrary())
                return;

            if (GetSelectedLibraryItem() != null)
            {
                using (var details = new TabDetailsDialog(GetSelectedLibraryItem(), _playlistManager) {Icon = Icon})
                {
                    if (details.ShowDialog() == DialogResult.OK)
                    {
                        listViewLibrary.UpdateObject(listViewLibrary.SelectedObject);
                        LoadTablatureData(GetSelectedLibraryItem());
                    }
                }
            }
        }

        private void PlaylistDetails(object sender, EventArgs e)
        {
            if (SelectedLibrary() == LibraryType.Playlist)
            {
                var playlist = GetSelectedPlaylist();
                var selectedNode = sidemenu.SelectedNode;

                using (var pdd = new PlaylistDetailsDialog(playlist))
                {
                    if (pdd.ShowDialog() == DialogResult.OK)
                    {
                        if (pdd.PlaylistRenamed)
                        {
                            selectedNode.Text = playlist.Name;
                            _playlistManager.Update(playlist);
                        }
                    }
                }
            }
        }

        private void PreviewDisplayDelay_Tick(object sender, EventArgs e)
        {
            PreviewDisplayDelay.Stop();
            LoadTabPreview();
            LoadTablatureData(GetSelectedLibraryItem());
        }

        private void ClearTabPreview()
        {
            previewToolStrip.Enabled = false;
            lblpreviewtitle.Text = "";
            PreviewEditor.Clear();
        }

        private static void UpdateInfoLabel(Label label, string str)
        {
            label.Text = string.IsNullOrEmpty(str) ? Resources.NotAvailableAbbreviation : str;
        }

        private void LoadTablatureData(TablatureLibraryItem<TablatureFile> libraryItem)
        {
            //tablature information
            UpdateInfoLabel(lblCurrentArtist, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Artist);
            UpdateInfoLabel(lblCurrentTitle, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Title);
            UpdateInfoLabel(lblCurrentType, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Type.Name);
            UpdateInfoLabel(lblCurrentTuning, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Tuning.Name);
            UpdateInfoLabel(lblCurrentSubtitle, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Subtitle);
            UpdateInfoLabel(lblCurrentDifficulty, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Difficulty.Name);
            UpdateInfoLabel(lblCurrentAuthor, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Author);
            UpdateInfoLabel(lblCurrentCopyright, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Copyright);
            UpdateInfoLabel(lblCurrentAlbum, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Album);
            UpdateInfoLabel(lblCurrentGenre, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Genre);
            UpdateInfoLabel(lblCurrentComment, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.Comment);

            txtLyrics.Text = libraryItem == null ? "" : libraryItem.File.Lyrics;

            //file information
            UpdateInfoLabel(lblCurrentLocation, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.FileInfo.FullName);
            UpdateInfoLabel(lblCurrentLength, libraryItem == null ? Resources.NotAvailableAbbreviation : string.Format("{0:n0} {1}", libraryItem.FileInfo.Length, Resources.Bytes));
            UpdateInfoLabel(lblCurrentFormat, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.FileHeader.Version.ToString());
            UpdateInfoLabel(lblCurrentCreated, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.FileInfo.CreationTime.ToString());
            UpdateInfoLabel(lblCurrentModified, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.FileInfo.LastWriteTime.ToString());
            UpdateInfoLabel(lblCurrentCompressed, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.FileHeader.Compression == CompressionMode.None ? Resources.No : Resources.Yes);
            UpdateInfoLabel(lblCurrentEncoding, libraryItem == null ? Resources.NotAvailableAbbreviation : libraryItem.File.FileAttributes.Encoding.EncodingName);
        }

        private void LoadTabPreview(bool startViewCountTimer = true)
        {
            PreviewDisplayTimer.Stop();

            if (GetSelectedLibraryItem() != null)
            {
                lblpreviewtitle.Text = GetSelectedLibraryItem().File.ToFriendlyString();

                var openedExternally = TablatureViewForm.GetInstance(this).IsFileOpen(GetSelectedLibraryItem().FileInfo);

                PreviewEditor.Visible = !openedExternally;

                if (openedExternally)
                {
                    lblLibraryPreview.Visible = true;
                }

                else
                {
                    lblLibraryPreview.Visible = false;

                    PreviewEditor.LoadTablature(GetSelectedLibraryItem().File);

                    if (startViewCountTimer)
                    {
                        PreviewDisplayTimer.Start();
                    }
                }

                librarySplitContainer.Panel2.Enabled = !openedExternally;
                previewToolStrip.Enabled = true;
            }

            else
            {
                ClearTabPreview();
            }
        }

        #region Tab Viewer Manager Events

        private void TabHandler_OnTabClosed(object sender, ITablatureFile file)
        {
            if (GetSelectedLibraryItem() != null)
            {
                LoadTabPreview();
                UpdateTabControls(false);
            }
        }

        #endregion

        #region Searching

        private void txtLibraryFilter_TextChanged(object sender, EventArgs e)
        {
            BuildLibraryCache();
        }

        #endregion

        #region Playlists

        private void DeletePlaylist(object sender, EventArgs e)
        {
            if (SelectedLibrary() == LibraryType.Playlist)
            {
                var playlistItem = GetSelectedPlaylist();

                if (playlistItem != null && MessageBox.Show(Resources.DeletePlaylistDialogCaption, Resources.DeletePlaylistDialogTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _playlistManager.Remove(playlistItem);
                    RemovePlaylistNode(playlistItem);
                    UpdateDetails();
                }
            }
        }

        private void NewPlaylist(object sender, EventArgs e)
        {
            using (var p = new NewPlaylistDialog())
            {
                if (p.ShowDialog() == DialogResult.OK)
                {
                    if (string.IsNullOrEmpty(p.PlaylistName))
                    {
                        MessageBox.Show(Resources.InvalidNameDialogText, Resources.InvalidNameDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var playlist = new TablaturePlaylist(p.PlaylistName) {Created = DateTime.UtcNow};

                    var item = GetSelectedLibraryItem();

                    // new playlist
                    if (sender == newPlaylistToolStripMenuItem)
                    {
                        // 'add to' new playlist
                        if (item != null)
                        {
                            playlist.Add(new TablaturePlaylistItem(item.File, item.FileInfo));
                        }
                    }

                    _playlistManager.Update(playlist);

                    AddPlaylistNode(playlist);
                    PopulatePlaylistMenu();
                    UpdateDetails();
                }
            }
        }

        private TablaturePlaylist GetSelectedPlaylist()
        {
            return sidemenu.SelectedNode.Tag as TablaturePlaylist;
        }

        private void AddPlaylistNode(TablaturePlaylist playlist, bool select = false)
        {
            var playlistRootNode = sidemenu.Nodes["node_playlists"];

            //check if tablaturePlaylist node already exists
            var node = playlistRootNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Tag.Equals(playlist));

            if (node == null)
            {
                node = new TreeNode(playlist.Name) {NodeFont = sidemenu.FirstNode.FirstNode.NodeFont, Tag = playlist};
                playlistRootNode.Nodes.Add(node);

                if (!playlistRootNode.IsExpanded)
                    playlistRootNode.ExpandAll();
            }

            if (select)
                sidemenu.SelectedNode = node;
        }

        private void RemovePlaylistNode(TablaturePlaylist playlist)
        {
            foreach (TreeNode node in sidemenu.Nodes["node_playlists"].Nodes)
            {
                if (node.Tag.Equals(playlist))
                {
                    sidemenu.Nodes.Remove(node);
                    break;
                }
            }
        }

        /// <summary>
        ///     Populates 'add to' playlist menu.
        /// </summary>
        private void PopulatePlaylistMenu()
        {
            librarycontextaddtoplaylist.DropDownItems.Clear();

            foreach (var playlist in _playlistManager.GetPlaylists())
            {
                var menuItem = new ToolStripMenuItem(playlist.Name) {Tag = playlist};

                menuItem.Click += (s, e) =>
                {
                    var playlistItem = ((ToolStripMenuItem) s).Tag as TablaturePlaylist;

                    if (playlistItem != null)
                    {
                        var libraryItem = GetSelectedLibraryItem();

                        playlistItem.Add(new TablaturePlaylistItem(libraryItem.File, libraryItem.FileInfo));
                        _playlistManager.Update(playlistItem);
                    }
                };

                librarycontextaddtoplaylist.DropDownItems.Add(menuItem);
            }

            if (librarycontextaddtoplaylist.DropDownItems.Count > 0)
                librarycontextaddtoplaylist.DropDownItems.Add(new ToolStripSeparator());

            librarycontextaddtoplaylist.DropDownItems.Add(newPlaylistToolStripMenuItem);
        }

        #endregion

        #region Preview Display

        private void PreviewDisplayTimer_Tick(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                GetSelectedLibraryItem().Views += 1;
                GetSelectedLibraryItem().LastViewed = DateTime.UtcNow;
            }

            PreviewDisplayTimer.Stop();
        }

        #endregion

        #region Nested type: LibraryType

        private enum LibraryType
        {
            AllTabs,
            MyDownloads,
            MyTabs,
            MyImports,
            MyFavorites,
            TabType,
            Playlist
        }

        #endregion
    }
}