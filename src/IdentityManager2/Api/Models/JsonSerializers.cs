using System.Collections.Generic;
using System.Text.Json.Serialization;
using IdentityManager2.Api.Models;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

[JsonSerializable(typeof(PageModelParams))]
partial class PageModelParams_Context : JsonSerializerContext { }
