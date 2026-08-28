using System;
using System.Collections.Generic;
using System.Linq;
using HCS.Blazor.Client.Pages.Organization;

namespace HCS.Blazor.Client.Components;

public sealed class DepartmentTreeNode
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<DepartmentTreeNode> Children { get; init; } = [];
    public bool Collapsed { get; set; }
    public bool HasChildren => Children.Count > 0;
}

public static class DepartmentTreeSelectHelper
{
    public static List<DepartmentTreeNode> Build(IEnumerable<DepartmentCatalogDto> departments)
    {
        var nodes = departments
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new DepartmentTreeNode { Id = x.Id, Name = $"{x.Code} — {x.Name}" })
            .ToDictionary(x => x.Id);
        var roots = new List<DepartmentTreeNode>();
        foreach (var department in departments)
        {
            if (!nodes.TryGetValue(department.Id, out var node)) continue;
            if (department.ParentId is { } parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        return roots;
    }

    public static void ExpandAll(IEnumerable<DepartmentTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.Collapsed = false;
            ExpandAll(node.Children);
        }
    }
}
