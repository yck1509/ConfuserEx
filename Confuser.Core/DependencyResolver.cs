using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Confuser.Core
{
    /// <summary>
    /// Resolves dependency between protections.
    /// </summary>
    class DependencyResolver
    {
        /// <summary>
        /// A node of dependency graph.
        /// </summary>
        class DependencyGraphNode
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="DependencyGraphNode"/> class.
            /// </summary>
            /// <param name="protection">Content of the node, or null if this is root node.</param>
            public DependencyGraphNode(Protection protection)
            {
                this.Protection = protection;
                this.TargetNodes = new List<DependencyGraphNode>();
            }

            /// <summary>
            /// The content protection of the node, or null if this is root node.
            /// </summary>
            public Protection Protection { get; private set; }

            /// <summary>
            /// Nodes pointed by this node.
            /// </summary>
            public List<DependencyGraphNode> TargetNodes { get; private set; }
        }


        IList<Protection> protections;
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyResolver"/> class.
        /// </summary>
        /// <param name="protections">The protections for resolution.</param>
        public DependencyResolver(IList<Protection> protections)
        {
            this.protections = protections;
        }

        /// <summary>
        /// Sort the protection according to their dependency.
        /// </summary>
        /// <returns>Sorted protections with respect to dependencies.</returns>
        /// <exception cref="T:CircularDependencyException">
        /// The protections contain circular dependencies.
        /// </exception>
        public IList<Protection> SortDependency()
        {
            /* Here we do a topological sort of the protections.
             * First we construct a dependency graph of the protections.
             * Every protection has a root node with null content as implicit parent.
             * Then the graph is sorted starting from the null root node.
             */

            var nodes = protections
                .ToDictionary(prot => prot, prot => new DependencyGraphNode(prot));

            var id2Nodes = protections
                .ToDictionary(prot => prot.FullId, prot => nodes[prot]);

            DependencyGraphNode root = new DependencyGraphNode(null);

            foreach (var prot in protections)
            {
                DependencyGraphNode protNode = nodes[prot];
                root.TargetNodes.Add(protNode);

                Type protType = prot.GetType();

                BeforeProtectionAttribute before = protType
                    .GetCustomAttributes(typeof(BeforeProtectionAttribute), false)
                    .Cast<BeforeProtectionAttribute>()
                    .SingleOrDefault();
                if (before != null)
                {
                    //protNode --> targetNodes
                    IEnumerable<DependencyGraphNode> targetNodes = before.Ids.Select(id => id2Nodes[id]);
                    foreach (var node in targetNodes)
                    {
                        protNode.TargetNodes.Add(node);
                    }
                }

                AfterProtectionAttribute after = protType
                    .GetCustomAttributes(typeof(AfterProtectionAttribute), false)
                    .Cast<AfterProtectionAttribute>()
                    .SingleOrDefault();
                if (after != null)
                {
                    //targetNodes --> protNode
                    IEnumerable<DependencyGraphNode> targetNodes = after.Ids.Select(id => id2Nodes[id]);
                    foreach (var node in targetNodes)
                    {
                        node.TargetNodes.Add(protNode);
                    }
                }
            }

            var sortedNodes = SortNodes(root);

            //First one must be root node.
            Debug.Assert(sortedNodes.Length >= 1 && sortedNodes[0].Protection == null);
            return sortedNodes.Skip(1).Select(node => node.Protection).ToList();
        }

        /// <summary>
        /// Topologically sort the dependency graph using DFS.
        /// </summary>
        /// <param name="root">The root node.</param>
        /// <returns>Topological sorted nodes.</returns>
        DependencyGraphNode[] SortNodes(DependencyGraphNode root)
        {
            List<DependencyGraphNode> ret = new List<DependencyGraphNode>();
            HashSet<DependencyGraphNode> visited = new HashSet<DependencyGraphNode>();
            Action<DependencyGraphNode> visit = null;
            visit = node =>
            {
                if (ret.Contains(node))
                    return;
                visited.Add(node);
                foreach (var targetNode in node.TargetNodes)
                {
                    if (visited.Contains(targetNode))
                        throw new CircularDependencyException(node.Protection, targetNode.Protection);
                    visit(targetNode);
                }
                ret.Add(node);
                visited.Remove(node);
            };
            visit(root);
            ret.Reverse();
            return ret.ToArray();
        }
    }

    /// <summary>
    /// The exception that is thrown when there exists circular dependency between protections.
    /// </summary>
    class CircularDependencyException : Exception
    {
        /// <summary>
        /// First protection that involved in circular dependency.
        /// </summary>
        public Protection ProtectionA { get; private set; }
        /// <summary>
        /// Second protection that involved in circular dependency.
        /// </summary>
        public Protection ProtectionB { get; private set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
        /// </summary>
        /// <param name="a">The first protection.</param>
        /// <param name="b">The second protection.</param>
        internal CircularDependencyException(Protection a, Protection b)
            : base(string.Format("The protections '{0}' and '{1}' has a circular dependency between them.", a, b))
        {
            Debug.Assert(a != null);
            Debug.Assert(b != null);
            ProtectionA = a;
            ProtectionB = b;
        }
    }
}
