using System;
using System.Collections.Generic;
using System.Linq;
using ActiveLayerScanner.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace ActiveLayerScanner.Services
{
    public static class SolutionService
    {
        public static List<SolutionItem> LoadSolutions(IOrganizationService svc)
        {
            var q = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "ismanaged"),
                Criteria = { Conditions = { new ConditionExpression("isvisible", ConditionOperator.Equal, true) } },
                Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
            };
            return RetrieveAll(svc, q).Select(e => new SolutionItem
            {
                Id = e.Id,
                UniqueName = e.GetAttributeValue<string>("uniquename"),
                FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                Version = e.GetAttributeValue<string>("version"),
                IsManaged = e.GetAttributeValue<bool>("ismanaged")
            }).ToList();
        }

        public static List<Entity> RetrieveAll(IOrganizationService svc, QueryExpression q)
        {
            var result = new List<Entity>();
            q.PageInfo = new PagingInfo { PageNumber = 1, Count = 5000 };
            while (true)
            {
                var page = svc.RetrieveMultiple(q);
                result.AddRange(page.Entities);
                if (!page.MoreRecords) break;
                q.PageInfo.PageNumber++;
                q.PageInfo.PagingCookie = page.PagingCookie;
            }
            return result;
        }
    }

    public static class ComponentService
    {
        // Fallback map (componenttype -> SolutionComponentName) used when the summary table lacks a logical name.
        private static readonly Dictionary<int, string> TypeNames = new Dictionary<int, string>
        {
            {1,"Entity"},{2,"Attribute"},{9,"OptionSet"},{10,"EntityRelationship"},{14,"EntityKey"},{20,"Role"},
            {26,"SavedQuery"},{29,"Workflow"},{31,"Report"},{36,"EmailTemplate"},{50,"RibbonCustomization"},
            {59,"SavedQueryVisualization"},{60,"SystemForm"},{61,"WebResource"},{62,"SiteMap"},{63,"ConnectionRole"},
            {65,"HierarchyRule"},{66,"CustomControl"},{68,"CustomControlDefaultConfig"},{80,"AppModule"},
            {91,"PluginAssembly"},{92,"SdkMessageProcessingStep"},{95,"ServiceEndpoint"},{150,"RoutingRule"},
            {371,"Connector"},{372,"Connector"},{380,"EnvironmentVariableDefinition"},{381,"EnvironmentVariableValue"},
            {300,"CanvasApp"},{432,"Connector"}
        };

        public static List<SolutionComponentRef> GetComponents(IOrganizationService svc, Guid solutionId)
        {
            var list = new List<SolutionComponentRef>();
            var q = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype"),
                Criteria = { Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId) } }
            };
            var names = LoadSummaryNames(svc, solutionId);
            foreach (var e in SolutionService.RetrieveAll(svc, q))
            {
                var type = e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0;
                var objectId = e.GetAttributeValue<Guid>("objectid");
                names.TryGetValue(objectId, out var summary);
                summary = summary ?? Tuple.Create<string, string>(null, null);
                var logical = summary.Item2;
                if (string.IsNullOrEmpty(logical)) TypeNames.TryGetValue(type, out logical);
                if (string.IsNullOrEmpty(logical)) continue; // type not supported by the layers API
                list.Add(new SolutionComponentRef
                {
                    ObjectId = objectId,
                    ComponentType = type,
                    SolutionComponentName = logical,
                    DisplayName = summary.Item1
                });
            }
            NameResolver.Resolve(svc, list);
            return list;
        }

        // msdyn_solutioncomponentsummary gives friendly names; failures here are non-fatal.
        private static Dictionary<Guid, Tuple<string, string>> LoadSummaryNames(IOrganizationService svc, Guid solutionId)
        {
            var map = new Dictionary<Guid, Tuple<string, string>>();
            try
            {
                var q = new QueryExpression("msdyn_solutioncomponentsummary") { ColumnSet = new ColumnSet(true) };
                q.Criteria.AddCondition("msdyn_solutionid", ConditionOperator.Equal, solutionId);
                foreach (var e in SolutionService.RetrieveAll(svc, q))
                {
                    var id = e.GetAttributeValue<Guid>("msdyn_objectid");
                    if (id == Guid.Empty) continue;
                    var name = e.GetAttributeValue<string>("msdyn_displayname") ?? e.GetAttributeValue<string>("msdyn_name");
                    map[id] = Tuple.Create(name, e.GetAttributeValue<string>("msdyn_componentlogicalname"));
                }
            }
            catch { /* table not available in this environment */ }
            return map;
        }
    }

    public static class LayerService
    {
        private static readonly ColumnSet LightColumns = new ColumnSet(
            "msdyn_solutionname", "msdyn_order", "msdyn_publishername", "msdyn_overwritetime", "msdyn_changes", "msdyn_name");

        private static QueryExpression LayerQuery(Guid componentId, string name, ColumnSet cols)
        {
            var q = new QueryExpression("msdyn_componentlayer") { ColumnSet = cols };
            q.Criteria.AddCondition("msdyn_componentid", ConditionOperator.Equal, componentId.ToString());
            q.Criteria.AddCondition("msdyn_solutioncomponentname", ConditionOperator.Equal, name);
            q.Orders.Add(new OrderExpression("msdyn_order", OrderType.Descending));
            return q;
        }

        /// <summary>Scans components in batches. Returns rows only for components with an Active layer over at least one other layer.</summary>
        public static List<ComponentRow> Scan(IOrganizationService svc, IList<SolutionComponentRef> comps,
            Func<bool> cancelled, Action<int, int> progress)
        {
            var rows = new List<ComponentRow>();
            const int batch = 50;
            for (var i = 0; i < comps.Count; i += batch)
            {
                if (cancelled()) break;
                var chunk = comps.Skip(i).Take(batch).ToList();
                var req = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true },
                    Requests = new OrganizationRequestCollection()
                };
                foreach (var c in chunk)
                    req.Requests.Add(new RetrieveMultipleRequest { Query = LayerQuery(c.ObjectId, c.SolutionComponentName, LightColumns) });

                var resp = (ExecuteMultipleResponse)ExecuteWithRetry(svc, req);
                foreach (var item in resp.Responses)
                {
                    if (item.Fault != null) continue; // component without layer data
                    var layers = ((RetrieveMultipleResponse)item.Response).EntityCollection.Entities.Select(ToLayer).ToList();
                    var active = layers.FirstOrDefault(l => l.IsActive);
                    if (active == null || layers.Count < 2) continue;
                    var c = chunk[item.RequestIndex];
                    var below = layers.Where(l => !l.IsActive && l.Order < active.Order).OrderByDescending(l => l.Order).FirstOrDefault()
                                ?? layers.First(l => !l.IsActive);
                    rows.Add(new ComponentRow
                    {
                        ComponentId = c.ObjectId,
                        SolutionComponentName = c.SolutionComponentName,
                        ComponentType = c.SolutionComponentName,
                        Name = !string.IsNullOrEmpty(c.DisplayName) ? c.DisplayName : (!string.IsNullOrEmpty(active.Name) ? active.Name : c.ObjectId.ToString()),
                        BaseLayer = below.SolutionName,
                        LayerCount = layers.Count,
                        ActiveModifiedOn = active.OverwriteTime,
                        Changes = active.Changes
                    });
                }
                progress(Math.Min(i + batch, comps.Count), comps.Count);
            }
            return rows;
        }

        /// <summary>Loads the Active layer and the layer right below it, including component JSON, for diffing.</summary>
        public static Tuple<LayerInfo, LayerInfo> GetActiveAndBase(IOrganizationService svc, ComponentRow row)
        {
            var cols = new ColumnSet("msdyn_solutionname", "msdyn_order", "msdyn_publishername", "msdyn_overwritetime", "msdyn_changes", "msdyn_componentjson");
            var layers = svc.RetrieveMultiple(LayerQuery(row.ComponentId, row.SolutionComponentName, cols)).Entities.Select(ToLayer).ToList();
            var active = layers.FirstOrDefault(l => l.IsActive);
            var below = active == null ? null : layers.Where(l => !l.IsActive && l.Order < active.Order).OrderByDescending(l => l.Order).FirstOrDefault();
            return Tuple.Create(below, active);
        }

        private static LayerInfo ToLayer(Entity e) => new LayerInfo
        {
            SolutionName = e.GetAttributeValue<string>("msdyn_solutionname"),
            Name = e.GetAttributeValue<string>("msdyn_name"),
            Order = e.GetAttributeValue<int>("msdyn_order"),
            Publisher = e.GetAttributeValue<string>("msdyn_publishername"),
            OverwriteTime = e.GetAttributeValue<DateTime?>("msdyn_overwritetime"),
            Changes = e.GetAttributeValue<string>("msdyn_changes"),
            Json = e.GetAttributeValue<string>("msdyn_componentjson")
        };

        internal static OrganizationResponse ExecuteWithRetry(IOrganizationService svc, OrganizationRequest req, int attempts = 4)
        {
            for (var n = 1; ; n++)
            {
                try { return svc.Execute(req); }
                catch (System.ServiceModel.FaultException<OrganizationServiceFault> ex) when (n < attempts && IsThrottle(ex))
                {
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, n)));
                }
            }
        }

        private static bool IsThrottle(System.ServiceModel.FaultException<OrganizationServiceFault> ex)
        {
            var code = ex.Detail?.ErrorCode ?? 0;
            return code == -2147015902 || code == -2147015903 || code == -2147015898;
        }
    }

    public static class CleanupService
    {
        /// <summary>Removes the Active (unmanaged) layer of each component. Irreversible.</summary>
        public static List<CleanupResult> Remove(IOrganizationService svc, IList<ComponentRow> rows,
            Func<bool> cancelled, Action<int, int> progress)
        {
            var results = new List<CleanupResult>();
            for (var i = 0; i < rows.Count; i++)
            {
                if (cancelled()) break;
                var r = rows[i];
                try
                {
                    var req = new OrganizationRequest("RemoveActiveCustomizations");
                    req["SolutionComponentName"] = r.SolutionComponentName;
                    req["ComponentId"] = r.ComponentId;
                    LayerService.ExecuteWithRetry(svc, req);
                    results.Add(new CleanupResult { Row = r, Success = true });
                }
                catch (Exception ex)
                {
                    results.Add(new CleanupResult { Row = r, Success = false, Error = ex.Message });
                }
                progress(i + 1, rows.Count);
            }
            return results;
        }
    }
}
