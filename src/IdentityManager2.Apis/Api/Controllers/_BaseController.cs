using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    protected readonly ILogger logger;
    protected readonly IIdentityManagerService service;
    protected IdentityManagerMetadata metadata;

    #region Constructors

    protected BaseApiController(IIdentityManagerService service, ILogger logger)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
