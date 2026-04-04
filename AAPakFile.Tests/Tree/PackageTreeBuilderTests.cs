using System.Collections.ObjectModel;
using System.Collections.Specialized;

using AAPakFile.Editing;

namespace AAPakFile.Tree;

public class PackageTreeBuilderTests
{
    // ── Helper types ──────────────────────────────────────────────────────────

    private record FakeEntry(string Name);

    private static PackageDirectoryNode<FakeEntry> Build(params string[] names)
        => PackageTreeBuilder.Build(names.Select(n => new FakeEntry(n)), static e => e.Name);

    private static PackageDirectoryNode<FakeEntry> GetDir(PackageDirectoryNode<FakeEntry> node, string name)
        => node.Children.OfType<PackageDirectoryNode<FakeEntry>>().Single(d => d.Name == name);

    private static PackageFileNode<FakeEntry> GetFile(PackageDirectoryNode<FakeEntry> node, string name)
        => node.Children.OfType<PackageFileNode<FakeEntry>>().Single(f => f.Name == name);

    // ── Structure ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Build_EmptySequence_ReturnsEmptyRoot()
    {
        var root = Build();

        await Assert.That(root.Name).IsEqualTo(string.Empty);
        await Assert.That(root.Children).IsEmpty();
    }

    [Test]
    public async Task Build_SingleFlatFile_RootHasOneFileChild()
    {
        var root = Build("sky.dds");

        await Assert.That(root.Children).Count().IsEqualTo(1);
        await Assert.That(root.Children[0]).IsTypeOf<PackageFileNode<FakeEntry>>();
        await Assert.That(root.Children[0].Name).IsEqualTo("sky.dds");
    }

    [Test]
    public async Task Build_SingleNestedFile_CreatesIntermediateDirectories()
    {
        var root = Build("a/b/c.txt");

        await Assert.That(root.Children).Count().IsEqualTo(1);
        var aDir = GetDir(root, "a");
        var bDir = GetDir(aDir, "b");
        await Assert.That(bDir.Children).Count().IsEqualTo(1);
        await Assert.That(bDir.Children[0].Name).IsEqualTo("c.txt");
        await Assert.That(bDir.Children[0]).IsTypeOf<PackageFileNode<FakeEntry>>();
    }

    [Test]
    public async Task Build_MultipleFilesSharedDirectory_SingleDirectoryNode()
    {
        var root = Build("data/ui/a.png", "data/ui/b.png", "data/ui/c.png");

        await Assert.That(root.Children).Count().IsEqualTo(1);
        var dataDir = GetDir(root, "data");
        await Assert.That(dataDir.Children).Count().IsEqualTo(1);
        var uiDir = GetDir(dataDir, "ui");
        await Assert.That(uiDir.Children).Count().IsEqualTo(3);
    }

    [Test]
    public async Task Build_MixedRootAndNestedFiles_BothAppearUnderRoot()
    {
        var root = Build("a.txt", "sub/b.txt");

        // Root should have one directory (sub) and one file (a.txt)
        await Assert.That(root.Children).Count().IsEqualTo(2);
        await Assert.That(root.Children.OfType<PackageDirectoryNode<FakeEntry>>().Count()).IsEqualTo(1);
        await Assert.That(root.Children.OfType<PackageFileNode<FakeEntry>>().Count()).IsEqualTo(1);
    }

    // ── Sorting ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Build_Sorting_DirectoriesBeforeFiles()
    {
        var root = Build("z.txt", "aaa/x.txt", "b.txt", "zzz/y.txt");

        // First two children should be directories
        await Assert.That(root.Children[0]).IsTypeOf<PackageDirectoryNode<FakeEntry>>();
        await Assert.That(root.Children[1]).IsTypeOf<PackageDirectoryNode<FakeEntry>>();
        // Last two should be files
        await Assert.That(root.Children[2]).IsTypeOf<PackageFileNode<FakeEntry>>();
        await Assert.That(root.Children[3]).IsTypeOf<PackageFileNode<FakeEntry>>();
    }

    [Test]
    public async Task Build_Sorting_DirectoriesAlphabetical()
    {
        var root = Build("zzz/a.txt", "aaa/b.txt", "mmm/c.txt");

        var dirNames = root.Children.Select(c => c.Name).ToList();
        await Assert.That(dirNames[0]).IsEqualTo("aaa");
        await Assert.That(dirNames[1]).IsEqualTo("mmm");
        await Assert.That(dirNames[2]).IsEqualTo("zzz");
    }

    [Test]
    public async Task Build_Sorting_FilesAlphabetical()
    {
        var root = Build("z.txt", "a.txt", "m.txt");

        var fileNames = root.Children.Select(c => c.Name).ToList();
        await Assert.That(fileNames[0]).IsEqualTo("a.txt");
        await Assert.That(fileNames[1]).IsEqualTo("m.txt");
        await Assert.That(fileNames[2]).IsEqualTo("z.txt");
    }

    [Test]
    public async Task Build_Sorting_CaseInsensitive()
    {
        var root = Build("Banana.txt", "aardvark.txt", "Cherry.txt");

        var names = root.Children.Select(c => c.Name).ToList();
        await Assert.That(names[0]).IsEqualTo("aardvark.txt");
        await Assert.That(names[1]).IsEqualTo("Banana.txt");
        await Assert.That(names[2]).IsEqualTo("Cherry.txt");
    }

    // ── Entry binding ─────────────────────────────────────────────────────────

    [Test]
    public async Task Build_FileNode_EntryIsReferenceEqualToSource()
    {
        var source = new FakeEntry("textures/sky.dds");
        var root = PackageTreeBuilder.Build([source], static e => e.Name);

        var fileNode = GetFile(GetDir(root, "textures"), "sky.dds");
        await Assert.That(ReferenceEquals(fileNode.Entry, source)).IsTrue();
    }

    [Test]
    public async Task Build_GenericOverload_CustomNameSelector_UsesSelector()
    {
        // Use a named type so we can reference it in assertions
        var entries = new[] { new FakeEntry("data/item.txt") };
        var root = PackageTreeBuilder.Build(entries, static e => e.Name);

        await Assert.That(root.Children).Count().IsEqualTo(1);
        await Assert.That(root.Children[0].Name).IsEqualTo("data");
        await Assert.That(root.Children[0]).IsTypeOf<PackageDirectoryNode<FakeEntry>>();
    }

    // ── Convenience overloads ─────────────────────────────────────────────────

    [Test]
    public async Task Build_ConvenienceOverload_ProducesIdenticalStructureToGeneric()
    {
        // Use the generic overload as the reference
        var entries = new[] { new FakeEntry("a/b.txt"), new FakeEntry("a/c.txt"), new FakeEntry("d.txt") };

        var generic = PackageTreeBuilder.Build(entries, static e => e.Name);
        var convenience = PackageTreeBuilder.Build(entries, static e => e.Name); // same

        await Assert.That(generic.Children.Count).IsEqualTo(convenience.Children.Count);
        await Assert.That(generic.Children[0].Name).IsEqualTo(convenience.Children[0].Name);
        await Assert.That(generic.Children[1].Name).IsEqualTo(convenience.Children[1].Name);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Test]
    public async Task Build_DuplicatePaths_LastEntryWins()
    {
        var first = new FakeEntry("data/item.txt");
        var second = new FakeEntry("data/item.txt");
        var root = PackageTreeBuilder.Build([first, second], static e => e.Name);

        var file = GetFile(GetDir(root, "data"), "item.txt");
        await Assert.That(ReferenceEquals(file.Entry, second)).IsTrue();
    }

    [Test]
    public async Task Build_ConsecutiveSlashes_SkippedAsEmptySegments()
    {
        // "a//b.txt" should be treated the same as "a/b.txt"
        var root = Build("a//b.txt");

        await Assert.That(root.Children).Count().IsEqualTo(1);
        var aDir = GetDir(root, "a");
        await Assert.That(aDir.Children).Count().IsEqualTo(1);
        await Assert.That(aDir.Children[0].Name).IsEqualTo("b.txt");
    }

    // ── PackageTreeView: structure ────────────────────────────────────────────

    private static (ObservableCollection<FakeEntry> source, PackageTreeView<FakeEntry> view)
        MakeView(params string[] names)
    {
        var source = new ObservableCollection<FakeEntry>(names.Select(n => new FakeEntry(n)));
        var view = new PackageTreeView<FakeEntry>(
            new ReadOnlyObservableCollection<FakeEntry>(source),
            static e => e.Name);
        return (source, view);
    }

    [Test]
    public async Task PackageTreeView_InitialBuild_MatchesBuilder()
    {
        (_, PackageTreeView<FakeEntry> view) = MakeView("a/b.txt", "a/c.txt", "d.txt");

        var builtRoot = Build("a/b.txt", "a/c.txt", "d.txt");
        await Assert.That(view.Root.Children.Count).IsEqualTo(builtRoot.Children.Count);
        await Assert.That(view.Root.Children[0].Name).IsEqualTo(builtRoot.Children[0].Name);
    }

    [Test]
    public async Task PackageTreeView_AddEntry_InsertsNodeAtSortedPosition()
    {
        (ObservableCollection<FakeEntry> source, PackageTreeView<FakeEntry> view) = MakeView("a/b.txt");
        source.Add(new FakeEntry("a/a.txt")); // should sort before b.txt

        var aDir = view.Root.Children.OfType<PackageDirectoryNode<FakeEntry>>().Single(d => d.Name == "a");
        await Assert.That(aDir.Children).Count().IsEqualTo(2);
        await Assert.That(aDir.Children[0].Name).IsEqualTo("a.txt");
        await Assert.That(aDir.Children[1].Name).IsEqualTo("b.txt");
    }

    [Test]
    public async Task PackageTreeView_RemoveEntry_RemovesNode()
    {
        var entry = new FakeEntry("a/b.txt");
        (ObservableCollection<FakeEntry> source, PackageTreeView<FakeEntry> view) = MakeView("a/b.txt", "a/c.txt");
        source.Remove(entry);

        var aDir = view.Root.Children.OfType<PackageDirectoryNode<FakeEntry>>().Single(d => d.Name == "a");
        await Assert.That(aDir.Children).Count().IsEqualTo(1);
        await Assert.That(aDir.Children[0].Name).IsEqualTo("c.txt");
    }

    [Test]
    public async Task PackageTreeView_RemoveLastFileInDir_PrunesDirectoryNode()
    {
        var entry = new FakeEntry("sub/file.txt");
        var source = new ObservableCollection<FakeEntry> { entry };
        using var view = new PackageTreeView<FakeEntry>(
            new ReadOnlyObservableCollection<FakeEntry>(source),
            static e => e.Name);

        source.Remove(entry);

        await Assert.That(view.Root.Children).IsEmpty();
    }

    [Test]
    public async Task PackageTreeView_AddEntry_RootIsStableObject()
    {
        (ObservableCollection<FakeEntry> source, PackageTreeView<FakeEntry> view) = MakeView("a.txt");
        var rootBefore = view.Root;

        source.Add(new FakeEntry("b.txt"));

        await Assert.That(ReferenceEquals(view.Root, rootBefore)).IsTrue();
    }

    [Test]
    public async Task PackageTreeView_RemoveEntry_UnchangedSiblingNodesPreserved()
    {
        var keep = new FakeEntry("sub/keep.txt");
        var remove = new FakeEntry("sub/remove.txt");
        var source = new ObservableCollection<FakeEntry> { keep, remove };
        using var view = new PackageTreeView<FakeEntry>(
            new ReadOnlyObservableCollection<FakeEntry>(source),
            static e => e.Name);

        var subDir = view.Root.Children.OfType<PackageDirectoryNode<FakeEntry>>().Single(d => d.Name == "sub");
        var keepNode = subDir.Children.OfType<PackageFileNode<FakeEntry>>().Single(f => f.Name == "keep.txt");

        source.Remove(remove);

        var subDirAfter = view.Root.Children.OfType<PackageDirectoryNode<FakeEntry>>().Single(d => d.Name == "sub");
        var keepNodeAfter = subDirAfter.Children.OfType<PackageFileNode<FakeEntry>>().Single(f => f.Name == "keep.txt");

        await Assert.That(ReferenceEquals(subDir, subDirAfter)).IsTrue();
        await Assert.That(ReferenceEquals(keepNode, keepNodeAfter)).IsTrue();
    }

    [Test]
    public async Task PackageTreeView_ReplaceEntry_UpdatesNode()
    {
        var original = new FakeEntry("a/file.txt");
        var replacement = new FakeEntry("a/file.txt");
        var source = new ObservableCollection<FakeEntry> { original };
        using var view = new PackageTreeView<FakeEntry>(
            new ReadOnlyObservableCollection<FakeEntry>(source),
            static e => e.Name);

        source[0] = replacement;

        var aDir = view.Root.Children.OfType<PackageDirectoryNode<FakeEntry>>().Single(d => d.Name == "a");
        await Assert.That(aDir.Children).Count().IsEqualTo(1);
        var fileNode = aDir.Children.OfType<PackageFileNode<FakeEntry>>().Single();
        await Assert.That(ReferenceEquals(fileNode.Entry, replacement)).IsTrue();
    }

    // ── DeferNotifications ────────────────────────────────────────────────────

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");

    [Test]
    public async Task Editor_DeferNotifications_SuppressesIndividualEventsWhileActive(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var events = new List<NotifyCollectionChangedEventArgs>();
            ((INotifyCollectionChanged)editor.Entries)
                .CollectionChanged += (_, e) => events.Add(e);

            using (editor.DeferNotifications())
            {
                await editor.AddOrReplaceFileAsync("a.txt", "hello"u8.ToArray(),
                    cancellationToken: cancellationToken);
                await Assert.That(events).IsEmpty();
            }

            await Assert.That(events).Count().IsEqualTo(1);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task Editor_DeferNotifications_ReplaysAllEventsOnDispose(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var actions = new List<NotifyCollectionChangedAction>();
            ((INotifyCollectionChanged)editor.Entries)
                .CollectionChanged += (_, e) => actions.Add(e.Action);

            using (editor.DeferNotifications())
            {
                await editor.AddOrReplaceFileAsync("a.txt", "a"u8.ToArray(),
                    cancellationToken: cancellationToken);
                await editor.AddOrReplaceFileAsync("b.txt", "b"u8.ToArray(),
                    cancellationToken: cancellationToken);
                editor.DeleteFile("a.txt");
            }

            // 2 adds + 1 remove — all replayed individually (never Reset)
            await Assert.That(actions).Count().IsEqualTo(3);
            await Assert.That(actions.Any(a => a == NotifyCollectionChangedAction.Reset)).IsFalse();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task Editor_DeferNotifications_NestedScopes_EventsFiredOnlyOnOuterDispose(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var eventCount = 0;
            ((INotifyCollectionChanged)editor.Entries)
                .CollectionChanged += (_, _) => eventCount++;

            using (editor.DeferNotifications())
            {
                using (editor.DeferNotifications())
                {
                    await editor.AddOrReplaceFileAsync("a.txt", "a"u8.ToArray(),
                        cancellationToken: cancellationToken);
                    // inner scope disposed — still no events
                }
                await Assert.That(eventCount).IsEqualTo(0);
                // outer scope disposed next
            }

            await Assert.That(eventCount).IsEqualTo(1);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task Editor_DeferNotifications_NoChanges_NoEventFired(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var eventCount = 0;
            ((INotifyCollectionChanged)editor.Entries)
                .CollectionChanged += (_, _) => eventCount++;

            using (editor.DeferNotifications()) { /* no mutations */ }

            await Assert.That(eventCount).IsEqualTo(0);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Regression: delete → re-add → save → delete must remove the node from the tree.
    /// Previously, SaveAsync sorted _files/_entriesList in-place, firing Replace events
    /// that swapped differently-named entries. This caused PackageTreeView._fileIndex to
    /// desync from the actual node tree, so the second delete left a ghost node.
    /// </summary>
    [Test]
    public async Task PackageTreeView_DeleteReaddDelete_NodeRemovedBothTimes(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            // Add two files so the list has more than one entry (triggers the sort-swap bug).
            await editor.AddOrReplaceFileAsync("a.txt", "hello"u8.ToArray(), cancellationToken: cancellationToken);
            await editor.AddOrReplaceFileAsync("b.txt", "world"u8.ToArray(), cancellationToken: cancellationToken);
            await editor.SaveAsync(cancellationToken);

            using var treeView = editor.BuildTree();

            // ── First delete ──────────────────────────────────────────────────
            editor.DeleteFile("a.txt");
            await editor.SaveAsync(cancellationToken);

            await Assert.That(treeView.Root.Children.Any(n => n.Name == "a.txt")).IsFalse();

            // ── Re-add ────────────────────────────────────────────────────────
            await editor.AddOrReplaceFileAsync("a.txt", "again"u8.ToArray(), cancellationToken: cancellationToken);
            await editor.SaveAsync(cancellationToken);

            await Assert.That(treeView.Root.Children.Any(n => n.Name == "a.txt")).IsTrue();

            // ── Second delete — this was the failing case ─────────────────────
            editor.DeleteFile("a.txt");
            await editor.SaveAsync(cancellationToken);

            await Assert.That(treeView.Root.Children.Any(n => n.Name == "a.txt")).IsFalse();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task Editor_DeferNotifications_PreservesEventOrder(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var names = new List<string>();
            ((INotifyCollectionChanged)editor.Entries)
                .CollectionChanged += (_, e) =>
                {
                    if (e.NewItems is not null)
                        foreach (PackageEntry entry in e.NewItems)
                            names.Add(entry.Name);
                };

            using (editor.DeferNotifications())
            {
                await editor.AddOrReplaceFileAsync("first.txt", "1"u8.ToArray(),
                    cancellationToken: cancellationToken);
                await editor.AddOrReplaceFileAsync("second.txt", "2"u8.ToArray(),
                    cancellationToken: cancellationToken);
                await editor.AddOrReplaceFileAsync("third.txt", "3"u8.ToArray(),
                    cancellationToken: cancellationToken);
            }

            await Assert.That(names[0]).IsEqualTo("first.txt");
            await Assert.That(names[1]).IsEqualTo("second.txt");
            await Assert.That(names[2]).IsEqualTo("third.txt");
        }
        finally { File.Delete(path); }
    }
}