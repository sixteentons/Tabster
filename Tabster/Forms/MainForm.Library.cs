﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using Tabster.Controls.Extensions;
using Tabster.Core.Printing;
using Tabster.Core.Types;
using Tabster.Data;
using Tabster.Data.Library;
using Tabster.Data.Processing;
using Tabster.Properties;

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
        private readonly List<ITablatureFileExporter> _fileExporters = new List<ITablatureFileExporter>();
        private readonly List<TablatureLibraryItem> _libraryCache = new List<TablatureLibraryItem>();
        private bool _changingLibraryView;
        private List<ITablatureFileImporter> _fileImporters = new List<ITablatureFileImporter>();

        //used to prevent double-triggering of OnSelectedIndexChanged for tablibrary when using navigation menu

        /// <summary>
        ///     Returns whether the library tab is currently focused.
        /// </summary>
        private bool IsViewingLibrary()
        {
            return tabControl1.SelectedTab == display_library;
        }

        private TablatureLibraryItem GetSelectedLibraryItem()
        {
            return listViewLibrary.SelectedObject != null ? (TablatureLibraryItem) listViewLibrary.SelectedObject : null;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                PopoutTab(GetSelectedLibraryItem().Document);
            }
        }

        private void ExportTab(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                using (var sfd = new SaveFileDialog
                {
                    Title = "Export Tab - Tabster",
                    AddExtension = true,
                    Filter = string.Format("Tabster File (*{0})|*{0}", TablatureDocument.FILE_EXTENSION),
                    FileName = GetSelectedLibraryItem().Document.ToFriendlyString()
                })
                {
                    sfd.SetTabsterFilter(_fileExporters);

                    if (sfd.ShowDialog() != DialogResult.Cancel)
                    {
                        //native file format
                        if (sfd.FilterIndex == 1)
                        {
                            File.Copy(GetSelectedLibraryItem().FileInfo.FullName, sfd.FileName);
                        }

                        else
                        {
                            var exporter = _fileExporters[sfd.FilterIndex - 2]; //FilterIndex is not 0-based and native Tabster format uses first index
                            exporter.Export(GetSelectedLibraryItem().Document, sfd.FileName);
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
                        var openedExternally = Program.TabbedViewer.IsFileOpen(GetSelectedLibraryItem().Document);

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
                Title = GetSelectedLibraryItem().Document.ToFriendlyString(),
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
            OpenPreferences("Printing");
        }

        private void NewTab(object sender, EventArgs e)
        {
            using (var n = new NewTabDialog())
            {
                if (n.ShowDialog() == DialogResult.OK)
                {
                    var TablatureLibraryItem = Program.TablatureFileLibrary.Add(n.Tab);
                    Program.TablatureFileLibrary.Save();
                    UpdateLibraryItem(TablatureLibraryItem);
                    PopoutTab(n.Tab);
                }
            }
        }

        private void PopoutTab(TablatureDocument tab, bool updateRecentFiles = true)
        {
            Program.TabbedViewer.LoadTablature(tab);

            if (updateRecentFiles)
                recentlyViewedMenuItem.Add(tab.FileInfo, tab.ToFriendlyString());

            var libraryItem = Program.TablatureFileLibrary.GetLibraryItem(tab);

            if (libraryItem != null)
            {
                libraryItem.Views += 1;
                libraryItem.LastViewed = DateTime.UtcNow;

                Program.TablatureFileLibrary.Save();

                LoadTabPreview();
            }
        }

        private void SearchSimilarTabs(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                txtSearchArtist.Text = sender == searchByArtistToolStripMenuItem || sender == searchByArtistAndTitleToolStripMenuItem
                    ? GetSelectedLibraryItem().Document.Artist
                    : "";

                txtSearchTitle.Text = sender == searchByTitleToolStripMenuItem || sender == searchByArtistAndTitleToolStripMenuItem
                    ? RemoveVersionConventionFromTitle(GetSelectedLibraryItem().Document.Title)
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

            if (GetSelectedLibraryItem() != null)
            {
                var removed = false;

                if (SelectedLibrary() == LibraryType.Playlist)
                {
                    var selectedPlaylist = GetSelectedPlaylist();

                    if (selectedPlaylist != null)
                    {
                        if (MessageBox.Show(string.Format("Are you sure you want to remove this tab from the playlist?{0}{0}{1}",
                            Environment.NewLine, GetSelectedLibraryItem().Document.ToFriendlyString()), "Remove Tab",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            selectedPlaylist.Remove(GetSelectedLibraryItem().Document);
                            removed = true;
                            selectedPlaylist.Save();
                        }
                    }
                }

                else
                {
                    if (MessageBox.Show(string.Format("Are you sure you want to delete this tab?{0}{0}{1}",
                        Environment.NewLine, GetSelectedLibraryItem().Document.ToFriendlyString()), "Delete Tab",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Program.TablatureFileLibrary.Remove(GetSelectedLibraryItem(), true);
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
                Process.Start("explorer.exe ", @"/select, " + GetSelectedLibraryItem().FileInfo.FullName);
            }
        }

        private void BrowseTab(object sender, EventArgs e)
        {
            if (!IsViewingLibrary())
                return;

            using (var ofd = new OpenFileDialog
            {
                Title = "Open File - Tabster",
                AddExtension = true,
                Multiselect = false,
                Filter = string.Format("Tabster Files (*{0})|*{0}", TablatureDocument.FILE_EXTENSION)
            })
            {
                if (ofd.ShowDialog() != DialogResult.Cancel)
                {
                    var tab = _tablatureProcessor.Load(ofd.FileName);

                    if (tab != null)
                    {
                        PopoutTab(tab);
                    }
                }
            }
        }

        private void AutoScrollChange(object sender, EventArgs e)
        {
            var item = ((ToolStripMenuItem) sender);
            var text = item.Text;

            foreach (ToolStripMenuItem menuItem in toolStripButton3.DropDownItems)
            {
                menuItem.Checked = menuItem.Text == item.Text;
            }

            PreviewEditor.AutoScroll = text == "On";
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

                Program.TablatureFileLibrary.Save();

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
            if (GetSelectedLibraryItem() == null)
                return;

            //check if playlists already contain 
            foreach (var item in librarycontextaddtoplaylist.DropDownItems)
            {
                var toolItem = item as ToolStripMenuItem;

                if (toolItem != null && toolItem.Tag != null)
                {
                    var playlistPath = toolItem.Tag.ToString();
                    var associatedPlaylist = Program.TablatureFileLibrary.FindPlaylistByPath(playlistPath);
                    var alreadyExistsInPlaylist = associatedPlaylist.Contains(GetSelectedLibraryItem().Document);

                    toolItem.Enabled = !alreadyExistsInPlaylist;
                }
            }

            librarycontextfavorites.Text = GetSelectedLibraryItem().Favorited ? "Remove from favorites" : "Add to favorites";

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

        private IEnumerable<TablatureLibraryItem> GetLibraryCollection(LibraryType libraryType)
        {
            var items = new List<TablatureLibraryItem>();

            if (libraryType == LibraryType.Playlist)
            {
                var selectedPlaylist = GetSelectedPlaylist();

                items.AddRange(selectedPlaylist.Select(tab => new TablatureLibraryItem(tab)));
            }

            else
            {
                items.AddRange(Program.TablatureFileLibrary);
            }

            return items.ToArray();
        }

        private bool TablatureLibraryItemVisible(LibraryType selectedLibrary, TablatureDocument tab, string searchValue)
        {
            var libraryMatch =
                selectedLibrary == LibraryType.Playlist ||
                selectedLibrary == LibraryType.AllTabs ||
                (selectedLibrary == LibraryType.MyTabs && tab.SourceType == TablatureSourceType.UserCreated) ||
                (selectedLibrary == LibraryType.MyDownloads && tab.SourceType == TablatureSourceType.Download) ||
                (selectedLibrary == LibraryType.MyImports && tab.SourceType == TablatureSourceType.FileImport) ||
                (selectedLibrary == LibraryType.MyFavorites && Program.TablatureFileLibrary.GetLibraryItem(tab).Favorited) ||
                (selectedLibrary == LibraryType.TabType && sidemenu.SelectedNode.Tag.ToString() == tab.Type.ToString());

            if (libraryMatch)
            {
                return searchValue == null || (tab.Artist.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               tab.Title.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               tab.FileInfo.FullName.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               tab.Comment.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               tab.Contents.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return false;
        }

        private void BuildLibraryCache(bool persistSelectedItem = true)
        {
            var selectedLibrary = SelectedLibrary();

            var items = GetLibraryCollection(selectedLibrary);

            var filterValue = txtLibraryFilter.Text;

            var currentItem = GetSelectedLibraryItem();

            _libraryCache.Clear();

            foreach (var item in items)
            {
                var visible = TablatureLibraryItemVisible(selectedLibrary, item.Document, filterValue);

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
            using (var i = new ImportDialog(_fileImporters))
            {
                if (i.ShowDialog() == DialogResult.OK)
                {
                    Program.TablatureFileLibrary.Add(i.Tab);
                    Program.TablatureFileLibrary.Save();
                    UpdateLibraryItem(Program.TablatureFileLibrary.GetLibraryItem(i.Tab));
                }
            }
        }

        private void viewTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (GetSelectedLibraryItem() != null)
            {
                PopoutTab(GetSelectedLibraryItem().Document);
            }
        }

        private void UpdateDetails()
        {
            lblcount.Text = string.Format("Total Tabs: {0}", Program.TablatureFileLibrary.TotalItems);
            lblplaylists.Text = string.Format("Playlists: {0}", Program.TablatureFileLibrary.Playlists.Count);
        }

        private void UpdateLibraryItem(TablatureLibraryItem item, bool append = true)
        {
            /*
            var objValues = new object[]
                                {
                                    item.Title,
                                    item.Artist,
                                    item.Type.ToFriendlyString(),
                                    item.Document.Created,
                                    item.FileInfo.LastWriteTime,
                                    item.Views,
                                    item.FileInfo.FullName
                                };

            if (append)
            {
                tablibrary.Rows.Add(objValues);

                if (tablibrary.SortedColumn != null)
                    tablibrary.Sort(tablibrary.SortedColumn, tablibrary.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
            }

            else if (tablibrary.SelectedRows.Count > 0)
            {
                var selectedIndex = tablibrary.SelectedRows[0].Index;
                tablibrary.Rows[selectedIndex].SetValues(objValues);
            }*/
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
                using (var details = new TabDetailsDialog(GetSelectedLibraryItem().Document, Program.TablatureFileLibrary) {Icon = Icon})
                {
                    if (details.ShowDialog() == DialogResult.OK)
                    {
                        listViewLibrary.UpdateObject(listViewLibrary.SelectedObject);
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
                        }
                    }
                }
            }
        }

        private void PreviewDisplayDelay_Tick(object sender, EventArgs e)
        {
            PreviewDisplayDelay.Stop();
            LoadTabPreview();
        }

        private void ClearTabPreview()
        {
            previewToolStrip.Enabled = false;
            lblpreviewtitle.Text = "";
            PreviewEditor.Clear();
        }

        private void LoadTabPreview(bool startViewCountTimer = true)
        {
            PreviewDisplayTimer.Stop();

            if (GetSelectedLibraryItem() != null)
            {
                lblpreviewtitle.Text = GetSelectedLibraryItem().Document.ToFriendlyString();

                var openedExternally = Program.TabbedViewer.IsFileOpen(GetSelectedLibraryItem().Document);

                PreviewEditor.Visible = !openedExternally;

                if (openedExternally)
                {
                    lblLibraryPreview.Visible = true;
                }

                else
                {
                    lblLibraryPreview.Visible = false;

                    PreviewEditor.LoadTablature(GetSelectedLibraryItem().Document);

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

        private void TabHandler_OnTabClosed(object sender, TablatureDocument tabDocument)
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
                var playlist = GetSelectedPlaylist();

                if (playlist != null && MessageBox.Show("Are you sure you want to delete this playlist?", "Delete Playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Program.TablatureFileLibrary.Remove(playlist, true);
                    RemovePlaylistNode(playlist);
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
                        MessageBox.Show("Please enter a valid playlist name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var playlist = new TablaturePlaylistDocument(p.PlaylistName);

                    //add tab to new playlist
                    if (sender == newPlaylistToolStripMenuItem && GetSelectedLibraryItem() != null)
                    {
                        playlist.Add(GetSelectedLibraryItem().Document);
                        playlist.Save();
                    }

                    Program.TablatureFileLibrary.Add(playlist);
                    AddPlaylistNode(playlist);
                }
            }
        }

        private TablaturePlaylistDocument GetSelectedPlaylist()
        {
            var playlistPath = sidemenu.SelectedNode.Tag.ToString();
            return Program.TablatureFileLibrary.FindPlaylistByPath(playlistPath);
        }

        private void AddPlaylistNode(TablaturePlaylistDocument playlist, bool select = false)
        {
            var playlistRootNode = sidemenu.Nodes["node_playlists"];

            //check if playlist node already exists
            var node = playlistRootNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Tag.ToString().Equals(playlist.FileInfo.FullName, StringComparison.OrdinalIgnoreCase));

            if (node == null)
            {
                node = new TreeNode(playlist.Name) {NodeFont = sidemenu.FirstNode.FirstNode.NodeFont, Tag = playlist.FileInfo.FullName};
                playlistRootNode.Nodes.Add(node);

                if (!playlistRootNode.IsExpanded)
                    playlistRootNode.ExpandAll();
            }

            if (select)
                sidemenu.SelectedNode = node;
        }

        private void RemovePlaylistNode(TablaturePlaylistDocument playlist)
        {
            foreach (TreeNode node in sidemenu.Nodes["node_playlists"].Nodes)
            {
                if (node.Tag.ToString().Equals(playlist.FileInfo.FullName))
                {
                    sidemenu.Nodes.Remove(node);
                    break;
                }
            }
        }

        private void PopulatePlaylists()
        {
            var playlistNode = sidemenu.Nodes["node_playlists"];

            if (playlistNode.Nodes.Count > 0)
                playlistNode.Nodes.Clear();

            if (librarycontextaddtoplaylist.DropDownItems.Count > 0)
                librarycontextaddtoplaylist.DropDownItems.Clear();

            librarycontextaddtoplaylist.DropDownItems.Clear();

            foreach (var playlist in Program.TablatureFileLibrary.Playlists)
            {
                AddPlaylistNode(playlist);

                var menuItem = new ToolStripMenuItem(playlist.Name) {Tag = playlist.FileInfo.FullName};

                menuItem.Click += (s, e) =>
                {
                    var path = ((ToolStripMenuItem) s).Tag.ToString();

                    var pf = Program.TablatureFileLibrary.FindPlaylistByPath(path);

                    if (pf != null)
                    {
                        pf.Add(GetSelectedLibraryItem().Document);
                        pf.Save();
                    }
                };

                librarycontextaddtoplaylist.DropDownItems.Add(menuItem);
            }

            sidemenu.ExpandAll();

            if (librarycontextaddtoplaylist.DropDownItems.Count > 0)
            {
                librarycontextaddtoplaylist.DropDownItems.Add(new ToolStripSeparator());
            }

            librarycontextaddtoplaylist.DropDownItems.Add(newPlaylistToolStripMenuItem);

            UpdateDetails();
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