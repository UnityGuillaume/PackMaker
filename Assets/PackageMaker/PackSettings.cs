using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;

[CreateAssetMenu(fileName = "Pack", menuName = "Pack")]
public class PackSettings : ScriptableObject, ISerializationCallbackReceiver
{
    public class Node
    {
        public string name;
        public Object obj = null;
        public Node parent;
        public Node[] children = new Node[0];

        public int id;
        
        public Node()
        {
            id = GetHashCode();
        }
    }

    [System.Serializable]
    public class FileEntry
    {
        //this is used to save stuff like the hascode of the node of the path, so they can be restored and used as Id
        [System.Serializable]
        public class PathPartData
        {
            public string name;
            public int id;
        }

        public PathPartData[] pathPart;
        public string path;
        //this will be written when the pack create the pack, so it can revert the file back to their original place
        public string originalPath = null;
        public Object obj = null;
    }

    [NonSerialized]
    public Node root; 

    //bit hacky, but saving the tree node setup was not working because could be too deep
    //so instead just build a list of all files with full path at serialization and rebuild the nodes on deserialization
    public FileEntry[] entries;

    public static PackSettings CreateNewPack()
    {
        PackSettings pack = CreateInstance<PackSettings>();
        
        pack.root = new Node();
        pack.root.name = "/";
        pack.root.parent = null;

        return pack;
    }

    public void DeleteEntry(Node n)
    {
        foreach(Node child in n.children)
            DeleteEntry(child);

        if (n.parent != null)
        {
            ArrayUtility.Remove(ref n.parent.children, n);
        }
    }

    public void OnBeforeSerialize()
    {
        List<FileEntry> fileEntries = new List<FileEntry>();
        foreach (var n in root.children)
        {
            Stack<FileEntry.PathPartData> pathPartStack = new Stack<FileEntry.PathPartData>();
            fileEntries.AddRange(CreateFileEntry(n, "", pathPartStack));
        }

        entries = fileEntries.ToArray();
    }

    public void OnAfterDeserialize()
    {
        root = new Node();
        root.name = "/";
        root.parent = null;

        foreach (var entry in entries)
        {
            InsertPathIntoTree(entry.path, entry.obj, root, entry.pathPart);
        }
    }

    //path part is optional, only used by the deserialization to keep id coherent across reload
    public void InsertPathIntoTree(string path, Object obj, Node startNode, FileEntry.PathPartData[] pathParts = null)
    {
        string[] pathBreak = path.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var currentParent = startNode;
        
        //Don't go until the end, as the end is the file.
        for (int p = 0; p < pathBreak.Length - 1; ++p)
        {
            Node found = null;
            FileEntry.PathPartData part = null;
            
            if(pathParts != null && pathParts.Length > p)
                part = pathParts[p];
        
            for (int i = 0; i < currentParent.children.Length; ++i)
            {
                var child = currentParent.children[i];
        
                if (child.name == pathBreak[p])
                {
                    found = child;
                    break;
                }
            }
        
            // if found, we make it the new level, otherwise we create a new children and set it as new level
            if (found != null)
            {
                currentParent = found;
            }
            else
            {
                Node node = new PackSettings.Node() { parent = currentParent, name = pathBreak[p] };
        
                ArrayUtility.Add(ref currentParent.children, node);
        
                currentParent = node;
            }

            if(part != null)
                currentParent.id = part.id;
        }
        
        bool fileExist = false;
        
        //..but first check if the cart isn't already part of the children
        for (int i = 0; i < currentParent.children.Length; ++i)
        {
            if (currentParent.children[i].obj == obj)
            {
                fileExist = true;
                break;
            }
        }
        
        if (!fileExist)
        {
            Node objNode = new Node() { name = pathBreak[pathBreak.Length - 1], parent = currentParent, obj = obj };
            
            if(pathParts != null)
                objNode.id = pathParts[pathParts.Length - 1].id;
            
            ArrayUtility.Add(ref currentParent.children, objNode);
        }
    }

    //this will recursively create all files entry of a node and children
    protected List<FileEntry> CreateFileEntry(Node n, string path, Stack<FileEntry.PathPartData> pathPart)
    {
        string fullPath = path + n.name;
        List<FileEntry> output = new List<FileEntry>();
        
        pathPart.Push(new FileEntry.PathPartData() { name = n.name, id = n.id} );

        if (n.obj != null)
        {//this is a leaf
            FileEntry entry = new FileEntry() { obj = n.obj, path = fullPath, pathPart = pathPart.Reverse().ToArray()};
            output.Add(entry);
        }
        else
        {//still can have children
            fullPath += '/';
            foreach (var c in n.children)
                output.AddRange(CreateFileEntry(c, fullPath, pathPart));
        }

        pathPart.Pop();
        
        return output;
    }
}
#endif
