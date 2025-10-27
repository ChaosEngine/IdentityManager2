using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;

namespace IdentityManager2.Api.Models
{
    public sealed class MetaResult
    {
        public Dictionary<string, object> Data { get; set; }
        public Dictionary<string, object> Links { get; set; }
    }

    public sealed class AnonymousUserName
    {
        public string username { get; set; }
    }

    public class AnonymousSubject
    {
        public string subject { get; set; }
    }

    public sealed class AnonymousSubjectRole : AnonymousSubject
    {
        public string role { get; set; }
    }

    public sealed class AnonymousDetail
    {
        public string detail { get; set; }
    }

    public sealed class AnonymousUpdate
    {
        public string update { get; set; }
    }

    public sealed class AnonymousTypeDescription
    {
        public string type { get; set; }
        public string description { get; set; }
    }

    public sealed class AnonymousPropertiesDataMetaLink
    {
        public object Data { get; set; }
        public PropertyMetadata Meta { get; set; }
        public object Links { get; set; }
    }

    public sealed class AnonymousRolesDataMetaLink
    {
        public object data { get; set; }
        public AnonymousTypeDescription meta { get; set; }
        public object links { get; set; }
    }

    public sealed class AnonymousRolesActionLinks
    {
        public string add { get; set; }
        public string remove { get; set; }
    }

    public sealed class AnonymousRolesDeleteLink
    {
        public string delete { get; set; }
    }

    public sealed class AnonymousCreateLink
    {
        public string create { get; set; }
    }

    public sealed class AnonymousClaimLinks
    {
        public ClaimValue Data { get; set; }
        public AnonymousRolesDeleteLink Links { get; set; }
    }

    public sealed class AnonymousClaim
    {
        public IEnumerable<AnonymousClaimLinks> Data { get; set; }
        public AnonymousCreateLink Links { get; set; }
    }

    public sealed class AnonymousCreatedRole
    {
        public AnonymousSubject Data { get; set; }
        public AnonymousDetail Links { get; set; }
    }

    public sealed class AnonymousCreatedUser
    {
        public AnonymousSubject Data { get; set; }
        public AnonymousDetail Links { get; set; }
    };
}