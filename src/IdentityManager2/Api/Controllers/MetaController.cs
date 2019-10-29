using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityManager2.Api.Models;
using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IdentityManager2.Api.Controllers
{
	[Route(IdentityManagerConstants.MetadataRoutePrefix)]
	[Authorize(IdentityManagerConstants.IdMgrAuthPolicy)]
	[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
	public class MetaController : Controller
	{
		private readonly IIdentityManagerService userManager;
		private readonly LinkGenerator linkGenerator;
		private IdentityManagerMetadata metadata;

		public MetaController(IIdentityManagerService userManager, LinkGenerator linkGenerator)
		{
			this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
			this.linkGenerator = linkGenerator;
		}

		private async Task<IdentityManagerMetadata> GetMetadataAsync()
		{
			if (metadata == null)
			{
				metadata = await userManager.GetMetadataAsync();
				if (metadata == null) throw new InvalidOperationException("GetMetadataAsync returned null");
				metadata.Validate();
			}

			return metadata;
		}

		[Route("")]
		public async Task<IActionResult> Get()
		{
			var meta = await GetMetadataAsync();
			var data = new Dictionary<string, object> { { "currentUser", new { username = User.Identity.Name } } };

			var links = new Dictionary<string, object> { ["users"] = this.linkGenerator.GetPathByAction("GetUsers", "Users") };

			if (meta.RoleMetadata.SupportsListing)
			{
				links["roles"] = this.linkGenerator.GetPathByAction("GetRoles", "Roles");
			}
			if (meta.UserMetadata.SupportsCreate)
			{
				links["createUser"] = new CreateUserLink(this.linkGenerator, "Users", meta.UserMetadata);
			}
			if (meta.RoleMetadata.SupportsCreate)
			{
				links["createRole"] = new CreateRoleLink(this.linkGenerator, "Roles", meta.RoleMetadata);
			}

			return Ok(new
			{
				Data = data,
				Links = links
			});
		}
	}
}