using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;
public static partial class WebRootImageFixture
{
    private sealed class Node(string name, bool directory)
    {
        public string Name = name;
        public bool Directory = directory;
        public Node? Parent;
        public byte[] Bytes = [];
        public List<Node> Children = [];
        public uint[] Clusters = [];
        public string Alias = "";
        public string Path => Parent is null ? "" : Parent.Path + "/" + Name;

        public Node AddDirectory(string child)
        {
            Node? existing = Children.FirstOrDefault(x => x.Name.Equals(child, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                Need(existing.Directory && existing.Name == child, "path_collision");
                return existing;
            }

            Node result = new(child, true)
            {
                Parent = this
            };
            Children.Add(result);
            return result;
        }

        public IEnumerable<Node> Descendants()
        {
            foreach (Node child in Children.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                yield return child;
                foreach (Node descendant in child.Descendants())
                    yield return descendant;
            }
        }
    }
}
