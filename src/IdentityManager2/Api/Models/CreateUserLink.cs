using System;
using System.Collections.Generic;
using IdentityManager2.Core.Metadata;
using IdentityManager2.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IdentityManager2.Api.Models
{
    public class CreateUserLink : Dictionary<string, object>
    {
        public CreateUserLink(IUrlHelper url, UserMetadata userMetadata)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (userMetadata == null) throw new ArgumentNullException(nameof(userMetadata));

            this["href"] = url.Link(IdentityManagerConstants.RouteNames.CreateUser, null);
            this["meta"] = userMetadata.GetCreateProperties();
        }
        
        public CreateUserLink(LinkGenerator linkGenerator, string controllerName, string rootPathBase, UserMetadata userMetadata)
        {
            if (linkGenerator == null) throw new ArgumentNullException(nameof(linkGenerator));
            if (userMetadata == null) throw new ArgumentNullException(nameof(userMetadata));

            this["href"] = linkGenerator.GetPathByAction(IdentityManagerConstants.RouteNames.CreateUser, controllerName, null, rootPathBase);
            this["meta"] = userMetadata.GetCreateProperties();
        }
    }
}