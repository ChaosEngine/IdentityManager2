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
    [Route(IdentityManagerConstants.UserRoutePrefix)]
    [Authorize(IdentityManagerConstants.IdMgrAuthPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class UsersController : ControllerBase
    {
        private readonly IIdentityManagerService service;
        private IdentityManagerMetadata metadata;

        #region Constructors

        public UsersController(IIdentityManagerService service)
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

        [HttpGet]
        [Route("", Name = IdentityManagerConstants.RouteNames.GetUsers)]
        [EndpointName("users-get-users")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<UserQueryResultResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetUsersAsync(string filter = null, int start = 0, int count = 100)
        {
            var result = await service.QueryUsersAsync(filter, start, count);
            if (result.IsSuccess)
            {
                var meta = await GetMetadataAsync();

                var resource = new UserQueryResultResource(result.Result, Url, meta.UserMetadata);
                return Ok(resource);
            }

            return BadRequest(result.ToError());
        }

        [HttpPost("", Name = IdentityManagerConstants.RouteNames.CreateUser)]
        [EndpointName("users-create-user")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<AnonymousCreatedUser>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> CreateUserAsync([FromBody] PropertyValue[] properties)
        {
            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsCreate)
            {
                return MethodNotAllowed();
            }

            var errors = ValidateCreateProperties(meta.UserMetadata, properties);

            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }

            if (ModelState.IsValid)
            {
                var result = await service.CreateUserAsync(properties);
                if (result.IsSuccess)
                {
                    var url = Url.Link(IdentityManagerConstants.RouteNames.GetUser, new AnonymousSubject { subject = result.Result.Subject });
                    var resource = new AnonymousCreatedUser
                    {
                        Data = new AnonymousSubject { subject = result.Result.Subject },
                        Links = new AnonymousDetail { detail = url }
                    };

                    return Created(url, resource);
                }

                ModelState.AddModelError("errors", result.Errors.Aggregate((workingSentence, next) => workingSentence + " " + next));
                if (result.Errors.Count > 0)
                    return BadRequest(ModelState);
            }

            return BadRequest(400);
        }

        [HttpGet("{subject}", Name = IdentityManagerConstants.RouteNames.GetUser)]
        [EndpointName("users-get-user")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<UserDetailResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetUserAsync(string subject)
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

            var result = await service.GetUserAsync(subject);
            if (result.IsSuccess)
            {
                if (result.Result == null)
                {
                    return NotFound();
                }

                var meta = await GetMetadataAsync();
                RoleSummary[] roles = null;
                if (!IsNullOrWhiteSpace(meta.RoleMetadata.RoleClaimType))
                {
                    var roleResult = await service.QueryRolesAsync(null, -1, -1);
                    if (!roleResult.IsSuccess)
                    {
                        return BadRequest(roleResult.Errors);
                    }

                    roles = roleResult.Result.Items.ToArray();
                }

                return Ok(new UserDetailResource(result.Result, Url, meta, roles));
            }

            return BadRequest(result.ToError());
        }

        [HttpDelete]
        [Route("{subject}", Name = IdentityManagerConstants.RouteNames.DeleteUser)]
        [EndpointName("users-delete-user")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> DeleteUserAsync(string subject)
        {
            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsDelete)
            {
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToError());
            }

            var result = await service.DeleteUserAsync(subject);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return BadRequest(result.ToError());
        }

        [HttpPut]
        [Route("{subject}/properties/{type}", Name = IdentityManagerConstants.RouteNames.UpdateUserProperty)]
        [EndpointName("users-set-property")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
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
            ValidateUpdateProperty(meta.UserMetadata, type, value);

            if (ModelState.IsValid)
            {
                var result = await service.SetUserPropertyAsync(subject, type, value);
                if (result.IsSuccess)
                {
                    return NoContent();
                }

                ModelState.AddErrors(result);
            }

            return BadRequest(ModelState.ToError());
        }

        #region Claims and Roles

        [HttpPost]
        [Route("{subject}/claims", Name = IdentityManagerConstants.RouteNames.AddClaim)]
        [EndpointName("users-add-claim")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> AddClaimAsync(string subject, [FromBody] ClaimValue model)
        {
            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsClaims)
            {
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            if (model == null)
            {
                ModelState.AddModelError("", Messages.ClaimDataRequired);
            }

            if (ModelState.IsValid)
            {
                // ReSharper disable once PossibleNullReferenceException
                var result = await service.AddUserClaimAsync(subject, model.Type, model.Value);
                if (result.IsSuccess)
                {
                    return NoContent();
                }

                ModelState.AddErrors(result);
            }

            return BadRequest(ModelState.ToError());
        }

        [HttpDelete]
        [Route("{subject}/claims/{type}/{value}", Name = IdentityManagerConstants.RouteNames.RemoveClaim)]
        [EndpointName("users-delete-claim")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> RemoveClaimAsync(string subject, string type, string value)
        {
            type = type.FromBase64UrlEncoded();
            value = value.FromBase64UrlEncoded();

            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsClaims)
            {
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject) ||
                IsNullOrWhiteSpace(type) ||
                IsNullOrWhiteSpace(value))
            {
                return NotFound();
            }

            var result = await service.RemoveUserClaimAsync(subject, type, value);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return BadRequest(result.ToError());
        }

        [HttpPost]
        [Route("{subject}/roles/{role}", Name = IdentityManagerConstants.RouteNames.AddRole)]
        [EndpointName("users-add-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> AddRoleAsync(string subject, string role)
        {
            var meta = await GetMetadataAsync();
            if (IsNullOrWhiteSpace(meta.RoleMetadata.RoleClaimType))
            {
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                return NotFound();
            }

            role = role.FromBase64UrlEncoded();

            var result = await service.AddUserClaimAsync(subject, meta.RoleMetadata.RoleClaimType, role);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return BadRequest(result.ToError());
        }

        [HttpDelete]
        [Route("{subject}/roles/{role}", Name = IdentityManagerConstants.RouteNames.RemoveRole)]
        [EndpointName("users-delete-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> RemoveRoleAsync(string subject, string role)
        {
            var meta = await GetMetadataAsync();
            if (IsNullOrWhiteSpace(meta.RoleMetadata.RoleClaimType))
            {
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                return NotFound();
            }

            role = role.FromBase64UrlEncoded();

            var result = await service.RemoveUserClaimAsync(subject, meta.RoleMetadata.RoleClaimType, role);
            if (result.IsSuccess)
            {
                return NoContent();
            }

            return BadRequest(result.ToError());
        }

        #endregion

        #endregion

        #region Helpers

        [NonAction]
        private IEnumerable<string> ValidateCreateProperties(UserMetadata userMetadata, IEnumerable<PropertyValue> properties)
        {
            if (userMetadata == null) throw new ArgumentNullException(nameof(userMetadata));
            properties = properties ?? Enumerable.Empty<PropertyValue>();

            var meta = userMetadata.GetCreateProperties();
            return meta.Validate(properties);
        }

        [NonAction]
        private void ValidateUpdateProperty(UserMetadata userMetadata, string type, string value)
        {
            if (userMetadata == null) throw new ArgumentNullException(nameof(userMetadata));

            if (IsNullOrWhiteSpace(type))
            {
                ModelState.AddModelError("", Messages.PropertyTypeRequired);
                return;
            }

            var prop = userMetadata.UpdateProperties.SingleOrDefault(x => x.Type == type);
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
