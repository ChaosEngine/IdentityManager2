using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace IdentityManager2.Api.Controllers;

/*
public static class AreaApiConstants
{
    public const string AreaName = "idmgr2";
}
// */

[ApiController]
//[Area(AreaApiConstants.AreaName)]
[Authorize(IdentityManagerConstants.IdMgrAuthPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IIdentityManagerService service;
    protected IdentityManagerMetadata metadata;

    #region Constructors

    protected BaseApiController(IIdentityManagerService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    #endregion

    [NonAction]
    protected async Task<IdentityManagerMetadata> GetMetadataAsync()
    {
        if (metadata == null)
        {
            metadata = await service.GetMetadataAsync();
            if (metadata == null) throw new InvalidOperationException("GetMetadataAsync returned null");
            metadata.Validate();
        }

        return metadata;
    }
}
