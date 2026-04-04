using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using AAPakFile.Editing;
using AAPakFile.Tree;

using Microsoft.Win32;
// System.Windows.Shapes (via WPF implicit usings) also has a Path class — alias to avoid ambiguity.
using Path = System.IO.Path;

namespace AAPakFile.Demo;

public partial class MainWindow
{
    private PackageEditor? _editor;
    private PackageTreeView<PackageEntry>? _treeView;

    private CancellationTokenSource? _cts;

    // Unsubscribe actions for all active dir.Children.CollectionChanged subscriptions.
    private readonly List<Action> _treeSubscriptions = [];

    // Carried on every TreeViewItem.Tag so context menu handlers know the node and its full path.
    private record NodeTag(PackageTreeNode<PackageEntry> Node, string FullPath);

    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Open ─────────────────────────────────────────────────────────────────

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Open package", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;

        OpenButton.IsEnabled = false;
        try
        {
            await LoadPakFileAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowError("Failed to open package", ex);
        }
        finally
        {
            OpenButton.IsEnabled = true;
        }
    }

    private async Task LoadPakFileAsync(string path)
    {
        // Tear down subscriptions before clearing items and disposing the old tree.
        ClearTreeSubscriptions();
        PackageTree.Items.Clear();
        _treeView?.Dispose();
        _treeView = null;
        if (_editor is not null)
        {
            await _editor.DisposeAsync();
            _editor = null;
        }

        SetEditingButtonsEnabled(false);
        StatusText.Text = "Loading…";

        _editor = await PackageFile.OpenEditorAsync(path).ConfigureAwait(true);
        _treeView = _editor.BuildTree();

        foreach (var child in _treeView.Root.Children)
            PackageTree.Items.Add(CreateItem(child, ""));

        // Keep the top-level view in sync with live tree mutations.
        SubscribeToChildren(PackageTree.Items, _treeView.Root, "");

        StatusText.Text = $"{Path.GetFileName(path)}  —  {_editor.Entries.Count:N0} files";
        SetEditingButtonsEnabled(true);
    }

    // ── Export ───────────────────────────────────────────────────────────────

    private async void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;

        var dialog = new OpenFolderDialog { Title = "Export all files to…" };
        if (dialog.ShowDialog(this) != true) return;

        BeginOperation("Exporting…");
        try
        {
            await ExportEntriesAsync(_editor.Entries, "", dialog.FolderName, _cts!.Token);
            StatusText.Text = $"Exported {_editor.Entries.Count:N0} files";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Export failed", ex); }
        finally { EndOperation(); }
    }

    private async void ExportFile_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        if ((PackageTree.SelectedItem as TreeViewItem)?.Tag is not NodeTag
            {
                Node: PackageFileNode<PackageEntry> fileNode
            }) return;

        var entry = fileNode.Entry;
        var dialog = new SaveFileDialog { FileName = Path.GetFileName(entry.Name), Title = "Export file" };
        if (dialog.ShowDialog(this) != true) return;

        BeginOperation("Exporting…");
        try
        {
            var ct = _cts!.Token;
            await using var src = entry.OpenRead();
            await using var dst = File.Create(dialog.FileName);
            await src.CopyToAsync(dst, ct);
            StatusText.Text = $"Exported {Path.GetFileName(entry.Name)}";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Export failed", ex); }
        finally { EndOperation(); }
    }

    private async void ExportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        if ((PackageTree.SelectedItem as TreeViewItem)?.Tag is not NodeTag
            {
                Node: PackageDirectoryNode<PackageEntry>, FullPath: var prefix
            }) return;

        var dialog = new OpenFolderDialog { Title = "Export folder to…" };
        if (dialog.ShowDialog(this) != true) return;

        BeginOperation("Exporting…");
        try
        {
            await ExportEntriesAsync(_editor.Entries, prefix + "/", dialog.FolderName, _cts!.Token);
            StatusText.Text = $"Exported folder '{prefix}'";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Export failed", ex); }
        finally { EndOperation(); }
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        if ((PackageTree.SelectedItem as TreeViewItem)?.Tag is not NodeTag
            {
                Node: PackageFileNode<PackageEntry> fileNode
            }) return;

        var entry = fileNode.Entry;
        var dialog = new OpenFileDialog { Title = $"Replace '{entry.Name}'", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;

        BeginOperation("Importing…");
        try
        {
            var ct = _cts!.Token;
            await using var stream = File.OpenRead(dialog.FileName);
            await _editor.AddOrReplaceFileAsync(entry.Name, stream, cancellationToken: ct);
            await _editor.SaveAsync(ct);
            StatusText.Text = $"Replaced '{entry.Name}'";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Import failed", ex); }
        finally { EndOperation(); }
    }

    private async void DeleteFile_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        if ((PackageTree.SelectedItem as TreeViewItem)?.Tag is not NodeTag
            {
                Node: PackageFileNode<PackageEntry> fileNode
            }) return;

        var entry = fileNode.Entry;
        var result = MessageBox.Show(
            $"Delete '{entry.Name}' from the package?",
            "Confirm Delete",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        BeginOperation("Deleting…");
        try
        {
            _editor.DeleteFile(entry.Name);
            await _editor.SaveAsync(_cts!.Token);
            StatusText.Text = $"Deleted '{entry.Name}'";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Delete failed", ex); }
        finally { EndOperation(); }
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        if ((PackageTree.SelectedItem as TreeViewItem)?.Tag is not NodeTag
            {
                Node: PackageDirectoryNode<PackageEntry>, FullPath: var prefix
            }) return;

        var dialog = new OpenFileDialog
        {
            Title = $"Add files to '{(prefix.Length == 0 ? "(root)" : prefix)}'",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var files = dialog.FileNames;
        BeginOperation("Adding files…");
        try
        {
            var ct = _cts!.Token;
            var total = files.Length;
            var done = 0;
            var progress = MakeProgress();

            using (_editor.DeferNotifications())
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var entryName = prefix.Length == 0
                        ? Path.GetFileName(file)
                        : prefix + "/" + Path.GetFileName(file);
                    await using var stream = File.OpenRead(file);
                    await _editor.AddOrReplaceFileAsync(entryName, stream, cancellationToken: ct);
                    progress.Report((++done, total));
                }
            }

            await _editor.SaveAsync(ct);
            StatusText.Text =
                $"Added {done:N0} file{(done == 1 ? "" : "s")} to '{(prefix.Length == 0 ? "(root)" : prefix)}'";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Add files failed", ex); }
        finally { EndOperation(); }
    }

    /// <summary>
    /// Exports all entries whose <see cref="PackageEntry.Name"/> starts with <paramref name="prefix"/>
    /// into <paramref name="outputFolder"/>, preserving the relative directory hierarchy.
    /// Pass an empty <paramref name="prefix"/> to export all entries.
    /// </summary>
    private async Task ExportEntriesAsync(
        IEnumerable<PackageEntry> entries, string prefix, string outputFolder, CancellationToken ct)
    {
        var toExport = string.IsNullOrEmpty(prefix)
            ? entries.ToList()
            : entries.Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = toExport.Count;
        var done = 0;
        var progress = MakeProgress();

        foreach (var entry in toExport)
        {
            ct.ThrowIfCancellationRequested();
            var relativeName = entry.Name[prefix.Length..]; // strip prefix, e.g. "textures/sky.dds"
            var destPath = Path.Combine(outputFolder, relativeName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await using var src = entry.OpenRead();
            await using var dst = File.Create(destPath);
            await src.CopyToAsync(dst, ct);
            progress.Report((++done, total));
        }
    }

    // ── Import ───────────────────────────────────────────────────────────────

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;

        var dialog = new OpenFolderDialog { Title = "Import from folder" };
        if (dialog.ShowDialog(this) != true) return;

        BeginOperation("Importing…");
        try
        {
            var ct = _cts!.Token;
            var sourceFolder = dialog.FolderName;
            var files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
            var total = files.Length;
            var done = 0;
            var progress = MakeProgress();

            using (_editor.DeferNotifications())
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Path.GetRelativePath(sourceFolder, file).Replace('\\', '/');
                    await using var stream = File.OpenRead(file);
                    await _editor.AddOrReplaceFileAsync(name, stream, cancellationToken: ct);
                    progress.Report((++done, total));
                }
            }
            // DeferNotifications disposed — PackageTreeView has processed all CollectionChanged events.

            await _editor.SaveAsync(ct);
            StatusText.Text = $"Imported {done:N0} files  —  {_editor.Entries.Count:N0} total";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Import failed", ex); }
        finally { EndOperation(); }
    }

    private async void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Import from ZIP",
            Filter = "ZIP archives (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        BeginOperation("Importing…");
        try
        {
            var ct = _cts!.Token;
            await using var archive = await ZipFile.OpenReadAsync(dialog.FileName, ct);
            var zipEntries = archive.Entries
                .Where(z => !z.FullName.EndsWith('/') && !z.FullName.EndsWith('\\'))
                .ToList();
            var total = zipEntries.Count;
            var done = 0;
            var progress = MakeProgress();

            using (_editor.DeferNotifications())
            {
                foreach (var zipEntry in zipEntries)
                {
                    ct.ThrowIfCancellationRequested();
                    await using var stream = zipEntry.Open();
                    // SizeHint is important for non-seekable ZIP decompression streams —
                    // it lets the editor allocate the correct slot size without buffering.
                    await _editor.AddOrReplaceFileAsync(
                        zipEntry.FullName, stream,
                        new PackageWriteOptions { SizeHint = zipEntry.Length },
                        ct);
                    progress.Report((++done, total));
                }
            }

            await _editor.SaveAsync(ct);
            StatusText.Text = $"Imported {done:N0} files from ZIP  —  {_editor.Entries.Count:N0} total";
        }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled."; }
        catch (Exception ex) { ShowError("Import failed", ex); }
        finally { EndOperation(); }
    }

    // ── Tree construction ─────────────────────────────────────────────────────

    private TreeViewItem CreateItem(PackageTreeNode<PackageEntry> node, string parentPath)
    {
        var item = new TreeViewItem();
        var fullPath = parentPath.Length == 0 ? node.Name : parentPath + "/" + node.Name;
        item.Tag = new NodeTag(node, fullPath);

        if (node is PackageDirectoryNode<PackageEntry> dir)
        {
            item.Header = "📁 " + dir.Name;
            if (dir.Children.Count > 0)
            {
                item.Items.Add(new TreeViewItem()); // placeholder — shows expand arrow
                item.Expanded += OnDirExpanded;
            }
        }
        else
        {
            item.Header = "📄 " + node.Name;
        }

        return item;
    }

    private void OnDirExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem
            {
                Tag: NodeTag { Node: PackageDirectoryNode<PackageEntry> dir, FullPath: var fp }
            } item) return;

        item.Expanded -= OnDirExpanded;
        item.Items.Clear(); // remove placeholder

        foreach (var child in dir.Children)
            item.Items.Add(CreateItem(child, fp));

        // Subscribe so future adds/removes in this directory update the items in place,
        // preserving expanded state of all sibling and ancestor nodes.
        SubscribeToChildren(item.Items, dir, fp);

        e.Handled = true;
    }

    /// <summary>
    /// Subscribes to <paramref name="dir"/>.Children.CollectionChanged and mirrors Add/Remove
    /// events directly into <paramref name="items"/>, without rebuilding the whole tree.
    /// Stores an unsubscribe action in <see cref="_treeSubscriptions"/> for cleanup on reload.
    /// </summary>
    private void SubscribeToChildren(ItemCollection items, PackageDirectoryNode<PackageEntry> dir, string fullPath)
    {
        // CollectionChanged is only accessible via INotifyCollectionChanged on ReadOnlyObservableCollection.
        INotifyCollectionChanged incc = dir.Children;
        incc.CollectionChanged += Handler;
        _treeSubscriptions.Add(() => incc.CollectionChanged -= Handler);
        return;

        void Handler(object? _, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var idx = e.NewStartingIndex;
                    foreach (PackageTreeNode<PackageEntry> node in e.NewItems!)
                        items.Insert(idx++, CreateItem(node, fullPath));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (var i = 0; i < e.OldItems!.Count; i++)
                        items.RemoveAt(e.OldStartingIndex);
                    break;
            }
        }
    }

    private void ClearTreeSubscriptions()
    {
        foreach (var unsub in _treeSubscriptions) unsub();
        _treeSubscriptions.Clear();
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private void PackageTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ensure the right-clicked node is selected so context menu targets the right item.
        var item = FindAncestorOrSelf<TreeViewItem>((DependencyObject)e.OriginalSource);
        item?.IsSelected = true;
    }

    private void PackageTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var tag = (PackageTree.SelectedItem as TreeViewItem)?.Tag as NodeTag;
        if (tag is null)
        {
            e.Handled = true;
            return;
        }

        var isFile = tag.Node is PackageFileNode<PackageEntry>;
        var isDir = tag.Node is PackageDirectoryNode<PackageEntry>;

        ExportFileMenuItem.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
        ImportFileMenuItem.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
        DeleteFileMenuItem.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
        ExportFolderMenuItem.Visibility = isDir ? Visibility.Visible : Visibility.Collapsed;
        AddFilesMenuItem.Visibility = isDir ? Visibility.Visible : Visibility.Collapsed;
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }

        return null;
    }

    // ── Operation helpers ─────────────────────────────────────────────────────

    private void BeginOperation(string label)
    {
        _cts = new CancellationTokenSource();
        OpenButton.IsEnabled = false;
        SetEditingButtonsEnabled(false);
        OperationProgress.Value = 0;
        OperationProgress.IsIndeterminate = true;
        OperationProgress.Visibility = Visibility.Visible;
        ProgressLabel.Text = label;
        ProgressLabel.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Visible;
    }

    private void EndOperation()
    {
        _cts?.Dispose();
        _cts = null;
        OpenButton.IsEnabled = true;
        SetEditingButtonsEnabled(_editor is not null);
        OperationProgress.Visibility = Visibility.Collapsed;
        ProgressLabel.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;
    }

    private void SetEditingButtonsEnabled(bool enabled)
    {
        ExportAllButton.IsEnabled = enabled;
        ImportFolderButton.IsEnabled = enabled;
        ImportZipButton.IsEnabled = enabled;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private IProgress<(int Done, int Total)> MakeProgress()
    {
        return new Progress<(int Done, int Total)>(p =>
        {
            if (p.Total > 0)
            {
                OperationProgress.IsIndeterminate = false;
                OperationProgress.Maximum = p.Total;
                OperationProgress.Value = p.Done;
                ProgressLabel.Text = $"{p.Done:N0} / {p.Total:N0} files";
            }
            else
            {
                OperationProgress.IsIndeterminate = true;
                ProgressLabel.Text = $"{p.Done:N0} files…";
            }
        });
    }

    private void ShowError(string title, Exception ex)
    {
        StatusText.Text = $"Error: {ex.Message}";
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // ── Lifetime ─────────────────────────────────────────────────────────────

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _cts?.Cancel();
        ClearTreeSubscriptions();
        _treeView?.Dispose();
        if (_editor is not null)
            await _editor.DisposeAsync();
    }
}