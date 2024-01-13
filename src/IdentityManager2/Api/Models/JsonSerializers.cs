using System.Collections.Generic;
using System.Text.Json.Serialization;
using IdentityManager2.Api.Models;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

[JsonSerializable(typeof(PageModelParams))]
partial class PageModelParams_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IEnumerable<PropertyMetadata>))]
[JsonSerializable(typeof(IEnumerable<PropertyDataType>))]
[JsonSerializable(typeof(PropertyDataType))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(AnonymousSubject))]
[JsonSerializable(typeof(AnonymousSubjectRole))]
[JsonSerializable(typeof(AnonymousUserName))]
[JsonSerializable(typeof(AnonymousDetail))]
[JsonSerializable(typeof(UserDetailResource))]
[JsonSerializable(typeof(UserDetailDataResource))]
[JsonSerializable(typeof(UserQueryResultResource))]
[JsonSerializable(typeof(UserQueryResultResourceData))]
[JsonSerializable(typeof(MetaResult))]
public partial class MetaResult_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(UserQueryResultResource))]
public partial class UserQueryResultResource_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(UserDetailDataResource))]
[JsonSerializable(typeof(IEnumerable<AnonymousPropertiesDataMetaLink>))]
[JsonSerializable(typeof(AnonymousUpdate))]
[JsonSerializable(typeof(AnonymousClaim))]
[JsonSerializable(typeof(AnonymousRolesDataMetaLink[]))]
[JsonSerializable(typeof(AnonymousRolesActionLinks))]
[JsonSerializable(typeof(UserDetailResource))]
public partial class UserDetailResource_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(RoleQueryResultResource))]
public partial class RoleQueryResultResource_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(RoleDetailResource))]
public partial class RoleDetailResource_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PropertyValue[]))]
public partial class ArrayPropertyValue_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ClaimValue))]
public partial class ClaimValue_Context: JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AnonymousCreatedRole))]
public partial class AnonymousCreatedRole_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AnonymousCreatedUser))]
public partial class AnonymousCreatedUser_Context : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ModelStateDictionary))]
public partial class ModelStateDictionary_Context : JsonSerializerContext { }


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ErrorModel))]
public partial class ErrorModel_Context : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<string>))]
public partial class ListStringErrors_Context : JsonSerializerContext { }




[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.SerializableError))]
public partial class SerializableError_Context : JsonSerializerContext { }
