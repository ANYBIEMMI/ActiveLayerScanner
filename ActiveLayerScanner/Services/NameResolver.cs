using System;
using System.Collections.Generic;
using System.Linq;
using ActiveLayerScanner.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace ActiveLayerScanner.Services
{
    /// <summary>Resolves friendly names for solution components (table-backed types and metadata-backed types).</summary>
    public static class NameResolver
    {
        // componenttype -> (table, id column, name column)
        private static readonly Dictionary<int, Tuple<string, string, string>> Tables = new Dictionary<int, Tuple<string, string, string>>
        {
            {20, T("role","roleid","name")},
            {26, T("savedquery","savedqueryid","name")},
            {29, T("workflow","workflowid","name")},
            {31, T("report","reportid","name")},
            {36, T("template","templateid","title")},
            {59, T("savedqueryvisualization","savedqueryvisualizationid","name")},
            {60, T("systemform","formid","name")},
            {61, T("webresource","webresourceid","name")},
            {62, T("sitemap","sitemapid","sitemapname")},
            {63, T("connectionrole","connectionroleid","name")},
            {80, T("appmodule","appmoduleid","name")},
            {91, T("pluginassembly","pluginassemblyid","name")},
            {92, T("sdkmessageprocessingstep","sdkmessageprocessingstepid","name")},
            {95, T("serviceendpoint","serviceendpointid","name")},
            {300, T("canvasapp","canvasappid","displayname")},
            {380, T("environmentvariabledefinition","environmentvariabledefinitionid","displayname")},
            {381, T("environmentvariablevalue","environmentvariablevalueid","schemaname")}
        };

        private static Tuple<string, string, string> T(string a, string b, string c) => Tuple.Create(a, b, c);

        public static void Resolve(IOrganizationService svc, IList<SolutionComponentRef> comps)
        {
            foreach (var group in comps.Where(c => string.IsNullOrEmpty(c.DisplayName)).GroupBy(c => c.ComponentType).ToList())
            {
                if (!Tables.TryGetValue(group.Key, out var t)) continue;
                try
                {
                    var byId = group.GroupBy(c => c.ObjectId).ToDictionary(g => g.Key, g => g.ToList());
                    foreach (var chunk in Chunk(byId.Keys.ToList(), 200))
                    {
                        var q = new QueryExpression(t.Item1) { ColumnSet = new ColumnSet(t.Item3) };
                        q.Criteria.AddCondition(t.Item2, ConditionOperator.In, chunk.Cast<object>().ToArray());
                        foreach (var e in SolutionService.RetrieveAll(svc, q))
                        {
                            var name = e.GetAttributeValue<string>(t.Item3);
                            if (string.IsNullOrEmpty(name)) continue;
                            foreach (var c in byId[e.Id]) c.DisplayName = name;
                        }
                    }
                }
                catch { /* table unavailable or not readable: fall back to layer name */ }
            }

            if (comps.Any(c => string.IsNullOrEmpty(c.DisplayName) && (c.ComponentType == 1 || c.ComponentType == 2 || c.ComponentType == 10)))
                ResolveEntityMetadata(svc, comps);
            if (comps.Any(c => string.IsNullOrEmpty(c.DisplayName) && c.ComponentType == 9))
                ResolveOptionSets(svc, comps);
        }

        private static void ResolveEntityMetadata(IOrganizationService svc, IList<SolutionComponentRef> comps)
        {
            try
            {
                var resp = (RetrieveAllEntitiesResponse)svc.Execute(new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
                    RetrieveAsIfPublished = true
                });
                var names = new Dictionary<Guid, string>();
                foreach (var e in resp.EntityMetadata)
                {
                    if (e.MetadataId.HasValue) names[e.MetadataId.Value] = Label(e.DisplayName, e.LogicalName);
                    foreach (var a in e.Attributes ?? new AttributeMetadata[0])
                        if (a.MetadataId.HasValue) names[a.MetadataId.Value] = $"{e.LogicalName}.{a.LogicalName} ({Label(a.DisplayName, a.LogicalName)})";
                    foreach (var r in (e.OneToManyRelationships ?? new OneToManyRelationshipMetadata[0]).Cast<RelationshipMetadataBase>()
                                      .Concat(e.ManyToManyRelationships ?? new ManyToManyRelationshipMetadata[0]))
                        if (r.MetadataId.HasValue) names[r.MetadataId.Value] = r.SchemaName;
                }
                foreach (var c in comps.Where(c => string.IsNullOrEmpty(c.DisplayName) && (c.ComponentType == 1 || c.ComponentType == 2 || c.ComponentType == 10)))
                    if (names.TryGetValue(c.ObjectId, out var n)) c.DisplayName = n;
            }
            catch { }
        }

        private static void ResolveOptionSets(IOrganizationService svc, IList<SolutionComponentRef> comps)
        {
            try
            {
                var resp = (RetrieveAllOptionSetsResponse)svc.Execute(new RetrieveAllOptionSetsRequest { RetrieveAsIfPublished = true });
                var names = resp.OptionSetMetadata.Where(o => o.MetadataId.HasValue)
                    .ToDictionary(o => o.MetadataId.Value, o => Label(o.DisplayName, o.Name));
                foreach (var c in comps.Where(c => string.IsNullOrEmpty(c.DisplayName) && c.ComponentType == 9))
                    if (names.TryGetValue(c.ObjectId, out var n)) c.DisplayName = n;
            }
            catch { }
        }

        private static string Label(Label l, string logical)
        {
            var text = l?.UserLocalizedLabel?.Label;
            return string.IsNullOrEmpty(text) ? logical : $"{text} ({logical})";
        }

        private static IEnumerable<List<Guid>> Chunk(List<Guid> list, int size)
        {
            for (var i = 0; i < list.Count; i += size) yield return list.Skip(i).Take(size).ToList();
        }
    }
}
