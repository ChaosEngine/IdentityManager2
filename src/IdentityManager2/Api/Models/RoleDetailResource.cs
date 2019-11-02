﻿using System;
using System.Collections.Generic;
using System.Linq;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;
using IdentityManager2.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IdentityManager2.Api.Models
{
    public class RoleDetailResource
    {
        public RoleDetailDataResource Data { get; set; }
        public object Links { get; set; }

        public RoleDetailResource(RoleDetail role, IUrlHelper url, RoleMetadata meta)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            Data = new RoleDetailDataResource(role, url, meta);

            var links = new Dictionary<string, string>();
            if (meta.SupportsDelete)
            {
                links["delete"] = url.Link(IdentityManagerConstants.RouteNames.DeleteRole, new { subject = role.Subject });
            }
            Links = links;
        }

        public RoleDetailResource(RoleDetail role, LinkGenerator linkGenerator, string controllerName, string rootPathBase,
            RoleMetadata meta)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (linkGenerator == null) throw new ArgumentNullException(nameof(linkGenerator));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            Data = new RoleDetailDataResource(role, linkGenerator, controllerName, rootPathBase, meta);

            var links = new Dictionary<string, string>();
            if (meta.SupportsDelete)
            {
                links["delete"] = linkGenerator.GetPathByAction(IdentityManagerConstants.RouteNames.DeleteRole, controllerName,
                    new { subject = role.Subject }, rootPathBase);
            }
            Links = links;
        }
    }

    public class RoleDetailDataResource : Dictionary<string, object>
    {
        public RoleDetailDataResource(RoleDetail role, IUrlHelper url, RoleMetadata meta)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            this["name"] = role.Name;
            this["subject"] = role.Subject;

            if (role.Properties != null)
            {
                var props =
                    from p in role.Properties
                    let m = (from m in meta.UpdateProperties where m.Type == p.Type select m).SingleOrDefault()
                    where m != null
                    select new
                    {
                        Data = m.Convert(p.Value),
                        Meta = m,
                        Links = new
                        {
                            update = url.Link(IdentityManagerConstants.RouteNames.UpdateRoleProperty,
                                new
                                {
                                    subject = role.Subject,
                                    type = p.Type.ToBase64UrlEncoded()
                                })
                        }
                    };

                if (props.Any())
                {
                    this["properties"] = props.ToArray();
                }
            }
        }

        public RoleDetailDataResource(RoleDetail role, LinkGenerator linkGenerator, string controllerName, string rootPathBase,
            RoleMetadata meta)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (linkGenerator == null) throw new ArgumentNullException(nameof(linkGenerator));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            this["name"] = role.Name;
            this["subject"] = role.Subject;

            if (role.Properties != null)
            {
                var props =
                    from p in role.Properties
                    let m = (from m in meta.UpdateProperties where m.Type == p.Type select m).SingleOrDefault()
                    where m != null
                    select new
                    {
                        Data = m.Convert(p.Value),
                        Meta = m,
                        Links = new
                        {
                            update = linkGenerator.GetPathByAction(IdentityManagerConstants.RouteNames.UpdateRoleProperty,
                                controllerName,
                                new
                                {
                                    subject = role.Subject,
                                    type = p.Type.ToBase64UrlEncoded()
                                },
                                rootPathBase)
                        }
                    };

                if (props.Any())
                {
                    this["properties"] = props.ToArray();
                }
            }
        }
    }
}
