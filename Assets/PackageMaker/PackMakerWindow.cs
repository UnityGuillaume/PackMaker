using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;

class PackMakerWindow : EditorWindow
{
    PackExplorerView m_ExplorerView;
    TreeViewState m_ExplorerTreeState = null;

    PackSettings m_EditedPack = null;
    int m_EditedPackIndex = -1;

    PackSettings[] m_AvailablePacks;
    string[] m_PacksNames;
    
    [MenuItem("Content Team Tools/Pack Maker")]
    public static void Open()
    {
        GetWindow<PackMakerWindow>();
    }

    void OnEnable()
    {
        if (m_ExplorerTreeState == null)
        {
            m_ExplorerTreeState = new TreeViewState();
        }


        m_ExplorerView = new PackExplorerView(m_ExplorerTreeState);

        RefreshPacksList();
        
        if(m_EditedPack != null)
            m_ExplorerView.LoadNewPack(m_EditedPack);

        Undo.undoRedoPerformed += m_ExplorerView.Reload;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= m_ExplorerView.Reload;
    }

    void OnGUI()
    {
        Rect dropdownRect = new Rect(0,0, position.width - 132, EditorGUIUtility.singleLineHeight);

        EditorGUI.BeginChangeCheck();
        m_EditedPackIndex = EditorGUI.Popup(dropdownRect, m_EditedPackIndex, m_PacksNames);

        if (EditorGUI.EndChangeCheck())
        {
            if (m_EditedPackIndex == m_PacksNames.Length - 1)
            {
                string newPath = EditorUtility.SaveFilePanelInProject("Save a new Pack Settings", "Pack", "asset", "Choose where to save your new pack");

                if (!String.IsNullOrEmpty(newPath))
                {
                    PackSettings setting = PackSettings.CreateNewPack();
                    m_EditedPack = setting; 
                    m_ExplorerView.LoadNewPack(m_EditedPack);
                    AssetDatabase.CreateAsset(setting, newPath.Replace(Application.dataPath, "Assets/"));
                    AssetDatabase.Refresh();
                    RefreshPacksList();
                }
            }
            else if(m_EditedPackIndex != -1)
            {
                m_EditedPack = m_AvailablePacks[m_EditedPackIndex];
                m_ExplorerView.LoadNewPack(m_EditedPack);
            }
        }
        
        Rect buildSingleRect = new Rect(position.width - 130,0, 128, EditorGUIUtility.singleLineHeight);

        if (m_EditedPack != null && m_EditedPack.entries.Length > 0)
        {
            if (!String.IsNullOrEmpty(m_EditedPack.entries[0].originalPath))
            {
                if (GUI.Button(buildSingleRect, "Revert Pack"))
                {
                    RevertPackFiles();
                }
            }
            else
            {
                if (GUI.Button(buildSingleRect, "Build This pack"))
                {
                    BuildPack(m_EditedPack);
                }
            }
        }

        Rect exploreRect = new Rect(0, dropdownRect.height, position.width, position.height - dropdownRect.height);
        m_ExplorerView.OnGUI(exploreRect);
    }


    void BuildPack(PackSettings pack)
    {
        //this will force to regenerate the files list
        pack.OnBeforeSerialize();

        string packDestination = Application.dataPath + "/_PACK_EXPORT/" + pack.name + "/";
        string relativePackDestination = "Assets/_PACK_EXPORT/" + pack.name + "/";
        
        if (Directory.Exists(packDestination))
        {
           Debug.LogErrorFormat("That folder {0} already exist, you should revert before re_exporting", relativePackDestination);
           return;
        }

        Undo.SetCurrentGroupName("Create Pack Hierarchy");
        int group = Undo.GetCurrentGroup();
        
        AssetDatabase.CreateFolder("Assets", "_PACK_EXPORT");
        AssetDatabase.CreateFolder("Assets/_PACK_EXPORT", pack.name);

        string folderRoot = "Assets/_PACK_EXPORT/" + pack.name;
        
        int fileCount = 1;
         
        //AssetDatabase.StartAssetEditing();
        foreach (var file in pack.entries)
        {
            EditorUtility.DisplayProgressBar("Moving Assets", 
                string.Format("Exporting file {0}/{1}", fileCount, pack.entries.Length),
                fileCount/(float)pack.entries.Length);
                
            string assetPath = AssetDatabase.GetAssetPath(file.obj);

            string folderHierarchy = Path.GetDirectoryName(file.path);

            string currentPath = folderRoot;
            for (int i = 0; i < file.pathPart.Length - 1; ++i)
            {
                if(!AssetDatabase.IsValidFolder(currentPath + "/" + file.pathPart[i].name))
                    AssetDatabase.CreateFolder(currentPath, file.pathPart[i].name);
                
                currentPath += "/" + file.pathPart[i].name;
            }

            //Directory.CreateDirectory(packDestination + folderHierarchy);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            file.originalPath = assetPath;
            
            string result = AssetDatabase.MoveAsset(assetPath, relativePackDestination + file.path);

            if (!String.IsNullOrEmpty(result))
            {
                Debug.LogError(result);
            }

            fileCount++;
        }
        //AssetDatabase.StopAssetEditing();
        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        
        Undo.CollapseUndoOperations( group );
    }

    void RevertPackFiles()
    {
        foreach (var entry in m_EditedPack.entries)
        {
            string assetPath = AssetDatabase.GetAssetPath(entry.obj);
            string result = AssetDatabase.MoveAsset(assetPath, entry.originalPath);

            if (!String.IsNullOrEmpty(result))
            {
                Debug.LogError(result);
            }

            entry.originalPath = null;
        }
        
        Directory.Delete(Application.dataPath + "/_PACK_EXPORT", true);
        File.Delete(Application.dataPath + "/_PACK_EXPORT.meta");
        
        AssetDatabase.Refresh();
    }

    void RefreshPacksList()
    {
        m_AvailablePacks = new PackSettings[0];
        m_PacksNames = new string[0];
        m_EditedPackIndex = -1;
        
        string[] packsIDs = AssetDatabase.FindAssets("t:PackSettings");

        foreach (string id in packsIDs)
        {
            PackSettings setting = AssetDatabase.LoadAssetAtPath<PackSettings>(AssetDatabase.GUIDToAssetPath(id));

            if (setting == m_EditedPack)
                m_EditedPackIndex = m_AvailablePacks.Length;

            ArrayUtility.Add(ref m_AvailablePacks, setting);
            ArrayUtility.Add(ref m_PacksNames, setting.name);
        }
        
        ArrayUtility.Add(ref m_PacksNames, "New...");
    }
}

// === Explorer for packs ====

class PackExplorerView : TreeView
{
    public readonly string ROW_DRAG_OP = "RowDragOp";
    
    static Texture2D[] s_Icons =
    {
        EditorGUIUtility.FindTexture ("Folder Icon"),
        EditorGUIUtility.FindTexture ("Prefab Icon")

    };
    
    int currentTopID = 1;
    PackSettings m_EditedPack = null;

    List<PackTreeItem> m_DraggedItem = new List<PackTreeItem>();

    public bool IsDragging { get; protected set; }

    public PackExplorerView(TreeViewState treeViewState)
        : base(treeViewState)
    {
        Reload();
        IsDragging = false;
    }

    public void LoadNewPack(PackSettings setting)
    {
        m_EditedPack = setting;
        Reload();
    }

    protected override bool CanStartDrag(CanStartDragArgs args)
    {
        return true;
    }

    protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
    {
        base.SetupDragAndDrop(args);

        m_DraggedItem.Clear();
        foreach (var itm in args.draggedItemIDs)
        {
            m_DraggedItem.Add(FindItem(itm, rootItem) as PackTreeItem);
        }
        
        DragAndDrop.objectReferences = new UnityEngine.Object[0];
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData(ROW_DRAG_OP, "");
        DragAndDrop.StartDrag("Tree view Item");

        IsDragging = true;
    }

    protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
    {
        if (args.dragAndDropPosition == DragAndDropPosition.OutsideItems)
        {
            var data = DragAndDrop.GetGenericData(ROW_DRAG_OP);

            if (data == null)
            {//we're dropping from outside, adding asset
                if (args.performDrop)
                {
                    AddDraggedAssetToPack(m_EditedPack.root);
                }

                return DragAndDropVisualMode.Link;
            }
            else
            {// dropping Tree view item, just move them to root
                
                if(args.performDrop)
                    MoveDraggedObject(m_EditedPack.root);
                
                return DragAndDropVisualMode.Move;
            }
        }

        if (args.dragAndDropPosition == DragAndDropPosition.UponItem || args.dragAndDropPosition == DragAndDropPosition.BetweenItems)
        {
            PackTreeItem itm = args.parentItem as PackTreeItem;
            
            var data = DragAndDrop.GetGenericData(ROW_DRAG_OP);

            if (data == null)
            {
                //we're dropping from outside, adding asset

                if (args.performDrop)
                {
                    AddDraggedAssetToPack(itm.nodeEntry);
                }
                
                return DragAndDropVisualMode.Link;
            }
            else
            {

                //you can't drag'n drop on something your dragging...
                if (m_DraggedItem.Contains(itm) || itm == null)
                {
                    return DragAndDropVisualMode.None;
                }

                if (itm.nodeEntry.obj != null)
                {
                    //this is a file, you can't drag stuff on a file
                    return DragAndDropVisualMode.None;
                }
                else
                {
                    if (args.performDrop)
                    {
                        //ok move node to that new parent


                        MoveDraggedObject(itm.nodeEntry);
                        IsDragging = false;
                    }

                    return DragAndDropVisualMode.Move;
                }
            }
        }

        return DragAndDropVisualMode.None;;
    }

    void AddDraggedAssetToPack(PackSettings.Node startingNode)
    {
        Undo.RecordObject(m_EditedPack, "Added asset to pack");
        
        foreach (var path in DragAndDrop.paths)
        {
            string fullPath = path.Replace("Assets", Application.dataPath).Replace('\\', '/');
            string parentPath = fullPath.Substring(0, fullPath.LastIndexOf('/'));
            
            string[] allFiles = null;
        
            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory))
                allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
            else
                allFiles = new string[] { fullPath };
        
            foreach (var file in allFiles)
            {
                if (file.Contains(".meta"))
                    continue;
        
                string shortPath = file.Replace(parentPath, "");
                
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file.Replace(Application.dataPath, "Assets"));
        
                m_EditedPack.InsertPathIntoTree(shortPath, obj, startingNode);
            }
        }
        
        Reload();
    }

    void MoveDraggedObject(PackSettings.Node newParent)
    {
        Undo.RecordObject(m_EditedPack, "Move Object");
                    
        foreach (var entry in m_DraggedItem)
        {
            ArrayUtility.Remove(ref entry.nodeEntry.parent.children, entry.nodeEntry);
                        
            entry.nodeEntry.parent = newParent;
            ArrayUtility.Add(ref entry.nodeEntry.parent.children, entry.nodeEntry);
        }
                    
        Reload();
    }

    protected override TreeViewItem BuildRoot ()
    {
        var root = new TreeViewItem {id = 0, depth = -1, displayName = "Root"};
        root.parent = null;
        root.children = new List<TreeViewItem>();

        currentTopID = 1;
                
        if (m_EditedPack != null)
        {
            for (int i = 0; i < m_EditedPack.root.children.Length; ++i)
            {
                root.AddChild(RecursiveAddItem(m_EditedPack.root.children[i], 0));
            }
        }
        
        SetupDepthsFromParentsAndChildren(root);
        
        // Return root of the tree
        return root;
    }

    TreeViewItem RecursiveAddItem(PackSettings.Node node, int level)
    {
        //hashcode is not unique but that should be enough her, hopefully. Need to have consistent ID across reload
        //as otherwise selected thing get lost.
        TreeViewItem ret = new PackTreeItem() { id = node.id, displayName = node.name, nodeEntry = node, icon = s_Icons[node.obj == null ? 0 : 1]};
        currentTopID += 1;
        
        for (int i = 0; i < node.children.Length; ++i)
        {
            ret.AddChild(RecursiveAddItem(node.children[i], level + 1));
        }

        return ret;
    }

    protected override void KeyEvent()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
        {
            Undo.RecordObject(m_EditedPack, "Removing Files from pack");
            foreach (int index in GetSelection())
            {
                
                PackTreeItem itm = FindItem(index, rootItem) as PackTreeItem;
                
                if(itm != null)
                {
                    ArrayUtility.Remove(ref itm.nodeEntry.parent.children, itm.nodeEntry);
                    itm.parent.children.Remove(itm);
                }
            }
            
            Reload();
        }
        
        base.KeyEvent();
    }

    protected override void ContextClickedItem(int id)
    {
        var itm = FindItem(id, rootItem) as PackTreeItem;
        
        if (itm.nodeEntry.obj != null)
        {
            return;
        }
        
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Create New Folder..."), false, CreateFolderInsideItem, itm);
        menu.ShowAsContext();

        base.ContextClickedItem(id);
        
        Repaint();
    }

    protected override void ContextClicked()
    {
        if(m_EditedPack == null)
            return;
        
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Create New Folder..."), false, () =>
        {
            PackSettings.Node n = new PackSettings.Node() { name = "New Folder", parent = m_EditedPack.root };
            ArrayUtility.Add(ref m_EditedPack.root.children, n);
            Reload();
        });
        
        menu.ShowAsContext();
        
        Repaint();
    }

    void CreateFolderInsideItem(object item)
    {
        PackTreeItem packItem = item as PackTreeItem;
        
        //TODO : check if a New Folder already exist under that parent to don't have 2 folder named the same
        PackSettings.Node n = new PackSettings.Node() { name = "New Folder", parent = packItem.nodeEntry };

        ArrayUtility.Add(ref packItem.nodeEntry.children, n);
        
        Reload();
    }

    protected override bool CanRename(TreeViewItem item)
    {
        PackTreeItem packItem = item as PackTreeItem;

        if (packItem.nodeEntry.obj != null)
            return false;

        return true;
    }

    protected override void RenameEnded(RenameEndedArgs args)
    {
        base.RenameEnded(args);

        if (args.acceptedRename)
        {
            PackTreeItem itm = FindItem(args.itemID, rootItem) as PackTreeItem;
            itm.nodeEntry.name = args.newName;
            itm.displayName = args.newName;
        }
    }
}

class PackTreeItem : TreeViewItem
{
    public PackSettings.Node nodeEntry;
}

#endif