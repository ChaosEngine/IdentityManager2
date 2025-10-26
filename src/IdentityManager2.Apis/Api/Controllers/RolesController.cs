using IdentityManager2.Api.Models;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;
using IdentityManager2.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.String;

namespace IdentityManager2.Api.Controllers
{
    [ApiController]
    [Route(IdentityManagerConstants.RoleRoutePrefix)]
    [Authorize(IdentityManagerConstants.IdMgrAuthPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class RolesController : ControllerBase
    {
        private readonly IIdentityManagerService service;
        private IdentityManagerMetadata metadata;

        #region Constructors

        public RolesController(IIdentityManagerService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        #endregion

        [NonAction]
        public async Task<IdentityManagerMetadata> GetMetadataAsync()
        {
            if (metadata == null)
            {
                metadata = await service.GetMetadataAsync();
                if (metadata == null) throw new InvalidOperationException("GetMetadataAsync returned null");
                metadata.Validate();
            }

            return metadata;
        }

        #region Endpoints

        // GET api/roles
        [HttpGet]
        [Route("", Name = IdentityManagerConstants.RouteNames.GetRoles)]
        [EndpointName("roles-get-roles")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<RoleQueryResultResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetRolesAsync(string filter = null, int start = 0, int count = 100)
        {
            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsListing)
            {
                return MethodNotAllowed();
            }

            var result = await service.QueryRolesAsync(filter, start, count);
            if (result.IsSuccess)
            {
                try
                {
                    return Ok(new RoleQueryResultResource(result.Result, Url, meta.RoleMetadata));
                }
                catch (Exception exp)
                {
                    throw new ArgumentNullException(exp.ToString());
                }
            }

            return BadRequest(result.ToError());
        }

        // POST 
        [HttpPost]
        [Route("", Name = IdentityManagerConstants.RouteNames.CreateRole)]
        [EndpointName("roles-create-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<AnonymousCreatedRole>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> CreateRoleAsync([FromBody] PropertyValue[] properties)
        {
            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsCreate)
            {
                return MethodNotAllowed();
            }

            var errors = ValidateCreateProperties(meta.RoleMetadata, properties);

            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }

            if (ModelState.IsValid)
            {
                var result = await service.CreateRoleAsync(properties);
                if (result.IsSuccess)
                {
                    var url = Url.Link(IdentityManagerConstants.RouteNames.GetRole, new AnonymousSubject { subject = result.Result.Subject });

                    var resource = new AnonymousCreatedRole
                    {
                        Data = new AnonymousSubject { subject = result.Result.Subject },
                        Links = new AnonymousDetail { detail = url }
                    };
                    return Created(url, resource);
                }

                ModelState.AddModelError("", errors.ToString());
            }

            return BadRequest(ModelState.ToError());
        }

        [HttpGet("{subject}", Name = IdentityManagerConstants.RouteNames.GetRole)]
        [EndpointName("roles-get-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<RoleDetailResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetRoleAsync(string subject)
        {
            if (IsNullOrWhiteSpace(subject))
            {
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsListing)
            {
                return MethodNotAllowed();
            }

            var result = await service.GetRoleAsync(subject);

            if (result.IsSuccess)
            {
                if (result.Result == null)
                {
                    return NotFound();
                }

                var response = Ok(new RoleDetailResource(result.Result, Url, meta.RoleMetadata));
                return response;
            }
            return BadRequest(result.ToError());
        }

        [HttpDelete]
        [Route("{subject}", Name = IdentityManagerConstants.RouteNames.DeleteRole)]
        [EndpointName("roles-delete-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> DeleteRoleAsync(string subject)
        {
            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsDelete)
            {
                return MethodNotAllowed();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToError());
            }

            var result = await service.DeleteRoleAsync(subject);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return BadRequest(result.ToError());
        }

        [HttpPut]
        [Route("{subject}/properties/{type}", Name = IdentityManagerConstants.RouteNames.UpdateRoleProperty)]
        [EndpointName("roles-set-property")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> SetPropertyAsync(string subject, string type)
        {
            if (IsNullOrWhiteSpace(subject))
            {
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            type = type.FromBase64UrlEncoded();
            var value = await Request.Body.ReadAsStringAsync();

            var meta = await GetMetadataAsync();

            ValidateUpdateProperty(meta.RoleMetadata, type, value);

            if (ModelState.IsValid)
            {
                var result = await service.SetRolePropertyAsync(subject, type, value);

                if (result.IsSuccess)
                {
                    return NoContent();
                }

                ModelState.AddErrors(result);
            }

            return BadRequest(ModelState.ToError());
        }

        #endregion

        #region Helpers

        [NonAction]
        private IEnumerable<string> ValidateCreateProperties(RoleMetadata roleMetadata, IEnumerable<PropertyValue> properties)
        {
            if (roleMetadata == null) throw new ArgumentNullException(nameof(roleMetadata));
            properties = properties ?? Enumerable.Empty<PropertyValue>();

            var meta = roleMetadata.GetCreateProperties();
            return meta.Validate(properties);
        }

        [NonAction]
        private void ValidateUpdateProperty(RoleMetadata roleMetadata, string type, string value)
        {
            if (roleMetadata == null) throw new ArgumentNullException(nameof(roleMetadata));

            if (IsNullOrWhiteSpace(type))
            {
                ModelState.AddModelError("", Messages.PropertyTypeRequired);
                return;
            }

            var prop = roleMetadata.UpdateProperties.SingleOrDefault(x => x.Type == type);
            if (prop == null)
            {
                ModelState.AddModelError("", Format(Messages.PropertyInvalid, type));
            }
            else
            {
                var error = prop.Validate(value);
                if (error != null)
                {
                    ModelState.AddModelError("", error);
                }
            }
        }

        [NonAction]
        private IActionResult MethodNotAllowed()
        {
            return StatusCode(405);
        }

        #endregion
    }
}
