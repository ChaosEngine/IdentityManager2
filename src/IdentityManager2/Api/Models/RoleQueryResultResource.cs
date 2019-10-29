using System;
using System.Collections.Generic;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;
using IdentityManager2.Mappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IdentityManager2.Api.Models
{
    public class RoleQueryResultResource
    {
        public RoleQueryResultResourceData Data { get; set; }
        public object Links { get; set; }

        public RoleQueryResultResource(QueryResult<RoleSummary> result, IUrlHelper url, RoleMetadata meta)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            Data = new RoleQueryResultResourceData(result, url, meta);

            var links = new Dictionary<string, object>();
            if (meta.SupportsCreate)
            {
                links["create"] = new CreateRoleLink(url, meta);
            };
            Links = links;
        }

        public RoleQueryResultResource(QueryResult<RoleSummary> result, LinkGenerator linkGenerator, string controllerName,
            RoleMetadata meta)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (linkGenerator == null) throw new ArgumentNullException(nameof(linkGenerator));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            Data = new RoleQueryResultResourceData(result, linkGenerator, controllerName, meta);

            var links = new Dictionary<string, object>();
            if (meta.SupportsCreate)
            {
                links["create"] = new CreateRoleLink(linkGenerator, controllerName, meta);
            };
            Links = links;
        }
    }

    public class RoleQueryResultResourceData : QueryResult<RoleResultResource>
    {
        //public new IEnumerable<RoleResultResource> Items { get; set; }

        public RoleQueryResultResourceData(QueryResult<RoleSummary> result, IUrlHelper url, RoleMetadata meta)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            RoleResultMappers.MapToResultData(result, this);

            foreach (var role in Items)
            {
                var links = new Dictionary<string, string>
                {
                    { "detail", url.Link(IdentityManagerConstants.RouteNames.GetRole, new { subject = role.Data.Subject }) }
                };

                if (meta.SupportsDelete)
                {
                    links.Add("delete", url.Link(IdentityManagerConstants.RouteNames.DeleteRole, new { subject = role.Data.Subject }));
                }
                role.Links = links;
            }
        }

        public RoleQueryResultResourceData(QueryResult<RoleSummary> result, LinkGenerator linkGenerator, string controllerName,
            RoleMetadata meta)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (linkGenerator == null) throw new ArgumentNullException(nameof(linkGenerator));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            RoleResultMappers.MapToResultData(result, this);

            foreach (var role in Items)
            {
                var links = new Dictionary<string, string>
                {
                    { "detail", linkGenerator.GetPathByAction(IdentityManagerConstants.RouteNames.GetRole, controllerName,
                        new { subject = role.Data.Subject }) }
                };

                if (meta.SupportsDelete)
                {
                    links.Add("delete", linkGenerator.GetPathByAction(IdentityManagerConstants.RouteNames.DeleteRole, controllerName,
                        new { subject = role.Data.Subject }));
                }
                role.Links = links;
            }
        }
    }

    public class RoleResultResource
    {
        public RoleSummary Data { get; set; }
        public object Links { get; set; }
    }
}
