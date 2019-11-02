using System;
using System.Collections.Generic;
using IdentityManager2.Core.Metadata;
using IdentityManager2.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IdentityManager2.Api.Models
{
	public class CreateRoleLink : Dictionary<string, object>
	{
		public CreateRoleLink(IUrlHelper url, RoleMetadata roleMetadata)
		{
			if (url == null) throw new ArgumentNullException(nameof(url));
			if (roleMetadata == null) throw new ArgumentNullException(nameof(roleMetadata));

			this["href"] = url.Link(IdentityManagerConstants.RouteNames.CreateRole, null);
			this["meta"] = roleMetadata.GetCreateProperties();
		}

		public CreateRoleLink(LinkGenerator linkGenerator, string controllerName, string rootPathBase, RoleMetadata roleMetadata)
		{
			if (linkGenerator == null) throw new ArgumentNullException(nameof(linkGenerator));
			if (roleMetadata == null) throw new ArgumentNullException(nameof(roleMetadata));

			this["href"] = linkGenerator.GetPathByAction(IdentityManagerConstants.RouteNames.CreateRole, controllerName,
				null, rootPathBase);
			this["meta"] = roleMetadata.GetCreateProperties();
		}
	}
}
