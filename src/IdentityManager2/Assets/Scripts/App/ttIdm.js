/*global angular*/
/// <reference path="../Libs/angular.min.js" />

(function (angular) {
    const app = angular.module("ttIdm", []);

    function ttSafeDom($window) {
        const win = $window || window;
        let ttPolicy = null;

        // Hoisted patterns keep the policy fast because createHTML may be hit often by Angular templates.
        const RE_RISKY_TAG_BLOCK = /<\s*(script|iframe|object|embed|link|meta|base)\b[^>]*>[\s\S]*?<\s*\/\s*\1\s*>/gi;
        const RE_RISKY_TAG_SINGLE = /<\s*(script|iframe|object|embed|link|meta|base)\b[^>]*\/?\s*>/gi;
        const RE_INLINE_HANDLER = /\son[a-z0-9_-]+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi;
        const RE_JS_DATA_URL_DOUBLE = /\s(href|src|xlink:href)\s*=\s*"\s*(javascript:|data:)[^"]*"/gi;
        const RE_JS_DATA_URL_SINGLE = /\s(href|src|xlink:href)\s*=\s*'\s*(javascript:|data:)[^']*'/gi;
        const RE_JS_DATA_URL_PLAIN = /\s(href|src|xlink:href)\s*=\s*(javascript:|data:)[^\s>]*/gi;
        const RE_HAS_RISKY_TAG = /<\s*\/?\s*(script|iframe|object|embed|link|meta|base)\b/i;
        const RE_HAS_INLINE_HANDLER_OR_JS_URL = /\son[a-z0-9_-]+\s*=|javascript:|data:/i;
        const RE_PATH_PROTOCOL = /^\s*(javascript:|data:)/i;

        function sanitizeHtmlTemplate(input) {
            // Do not use DOMParser/innerHTML here: with a default TT policy this can recurse into createHTML.
            if (typeof input !== "string" || input === "") {
                return "";
            }

            // Fast path: when there is no tag, no inline handler and no dangerous protocol there is nothing to sanitize.
            if (input.indexOf("<") === -1 && !RE_HAS_INLINE_HANDLER_OR_JS_URL.test(input)) {
                return input;
            }

            let output = input;

            // Remove dangerous full elements first.
            output = output.replace(RE_RISKY_TAG_BLOCK, "");
            output = output.replace(RE_RISKY_TAG_SINGLE, "");

            // Remove inline event handlers like onclick=... (quoted and unquoted).
            output = output.replace(RE_INLINE_HANDLER, "");

            // Strip javascript:/data: URL payloads from href/src/xlink:href.
            output = output.replace(RE_JS_DATA_URL_DOUBLE, "");
            output = output.replace(RE_JS_DATA_URL_SINGLE, "");
            output = output.replace(RE_JS_DATA_URL_PLAIN, "");

            return output;
        }

        function reject(reason, value) {
            // eslint-disable-next-line no-console
            console.warn("Trusted Types guard rejected value:", reason, value);
            return null;
        }

        if (win.trustedTypes && typeof win.trustedTypes.createPolicy === "function") {
            try {
                ttPolicy = win.trustedTypes.createPolicy("default", {
                    createHTML: function (input) {
                        if (typeof input !== "string")
                            return reject("non-string html", input);

                        if (input === "")
                            return "";

                        if (RE_HAS_RISKY_TAG.test(input))
                            return reject("risky html tag", input);

                        if (RE_HAS_INLINE_HANDLER_OR_JS_URL.test(input))
                            return reject("inline handler or dangerous scheme html", input);

                        return sanitizeHtmlTemplate(input);
                    },
                    createScriptURL: function (url) {
                        const safeUrl = normalizeUrl(url);
                        if (!safeUrl) {
                            return reject("invalid script url", url);
                        }

                        return safeUrl;
                    }
                });
            } catch (e) {
                // eslint-disable-next-line no-console
                console.warn("Trusted Types policy init skipped:", e && e.message ? e.message : e);
            }
        }

        function normalizePathBase(pathBase) {
            if (typeof pathBase !== "string") {
                return "";
            }

            let trimmed = pathBase.trim();
            if (!trimmed)
                return "";

            if (trimmed.charAt(0) !== "/")
                trimmed = "/" + trimmed;

            if (trimmed.length > 1 && trimmed.charAt(trimmed.length - 1) === "/")
                trimmed = trimmed.substring(0, trimmed.length - 1);

            return trimmed;
        }

        function isSafeRelativePath(path) {
            return typeof path === "string" &&
                path.length > 0 &&
                path.charAt(0) === "/" &&
                path.charAt(1) !== "/" &&
                path.indexOf("\\") === -1 &&
                path.indexOf("<") === -1 &&
                path.indexOf(">") === -1 &&
                !RE_PATH_PROTOCOL.test(path);
        }

        function normalizeUrl(candidate) {
            if (typeof candidate !== "string" || !candidate.trim()) {
                return null;
            }

            try {
                const parsed = new URL(candidate, win.location.origin);
                if ((parsed.protocol !== "http:" && parsed.protocol !== "https:") || parsed.origin !== win.location.origin) {
                    return null;
                }

                if (parsed.pathname.indexOf("<") !== -1 || parsed.pathname.indexOf(">") !== -1) {
                    return null;
                }

                return parsed.toString();
            } catch {
                return null;
            }
        }

        function safeJoin(pathBase, relativePath, fallbackPath) {
            const normalizedBase = normalizePathBase(pathBase);
            const usePath = isSafeRelativePath(relativePath) ? relativePath : fallbackPath;
            if (!isSafeRelativePath(usePath)) {
                return normalizedBase || "/";
            }

            const joined = normalizedBase + usePath;
            const safeUrl = normalizeUrl(joined);
            if (!safeUrl) {
                return normalizedBase + fallbackPath;
            }

            if (ttPolicy) {
                const trusted = ttPolicy.createScriptURL(safeUrl);
                return trusted ? trusted.toString() : normalizedBase + fallbackPath;
            }

            return safeUrl;
        }

        function safeDomId(rawId) {
            if (typeof rawId !== "string") {
                return null;
            }

            const trimmed = rawId.trim();
            if (!/^[A-Za-z][\w\-:.]*$/.test(trimmed)) {
                return null;
            }

            return trimmed;
        }

        return {
            safeTemplateUrl: function (pathBase, templatePath) {
                return safeJoin(pathBase, templatePath, "/assets/Templates.message.html");
            },
            safeApiUrl: function (pathBase, apiPath) {
                return safeJoin(pathBase, apiPath, "/api");
            },
            safeNavigationUrl: function (pathBase, routePath, fallbackPath) {
                return safeJoin(pathBase, routePath, fallbackPath || "/");
            },
            safeDomId: safeDomId
        };
    }
    ttSafeDom.$inject = ["$window"];
    app.factory("ttSafeDom", ttSafeDom);

    function config($httpProvider) {
        function intercept($q, $injector, idmErrorService, PathBase, $rootScope, ttSafeDom) {
            let inprogressRefreshRequest = null;
            const refreshUrl = ttSafeDom.safeApiUrl(PathBase, "/api/login/refresh");

            return {
                'request': function (config) {
                    idmErrorService.clear();

                    // Add anti-forgery token for non-GET requests
                    if (config.method && config.method !== 'GET') {
                        try {
                            const rvt = document.querySelector('input[name="__RequestVerificationToken"]');
                            if (rvt && rvt.value) {
                                if (!config.headers) {
                                    config.headers = {};
                                }
                                config.headers.RequestVerificationToken = rvt.value;
                            } else {
                                // eslint-disable-next-line no-console
                                console.warn("Anti-forgery token not present in DOM; state-changing request proceeds without CSRF protection.");
                            }
                        } catch (e) {
                            // eslint-disable-next-line no-console
                            console.error("Failed to extract antiforgery token:", e);
                        }
                    }

                    return config;
                },
                'responseError': function (response) {
                    if (response.config.url === refreshUrl) {
                        return $q.reject(response);
                    }

                    switch (response.status) {
                        case 401:
                            {
                                const deferred = $q.defer();

                                if (!inprogressRefreshRequest) {
                                    inprogressRefreshRequest = $injector.get("$http").get(refreshUrl);
                                }

                                inprogressRefreshRequest.then(
                                    function () {
                                        inprogressRefreshRequest = null;

                                        $injector.get("$http")(response.config).then(
                                            function (retryResponse) {
                                                deferred.resolve(retryResponse);
                                            },
                                            function (retryResponse) {
                                                deferred.reject(retryResponse);
                                            });
                                    },
                                    function () {
                                        inprogressRefreshRequest = null;
                                        response.data = { message: "Session has expired" };

                                        $rootScope.layout.username = null;
                                        $rootScope.layout.links = null;
                                        $rootScope.showLogin = true;
                                        $rootScope.showLogout = false;

                                        return deferred.reject(response);
                                    });

                                return deferred.promise;
                            }
                        default:
                            return $q.reject(response);
                    }
                }
            };
        }

        intercept.$inject = ["$q", "$injector", "idmErrorService", "PathBase", "$rootScope", "ttSafeDom"];
        $httpProvider.interceptors.push(intercept);
    }
    config.$inject = ["$httpProvider"];
    app.config(config);

    function idmErrorService($rootScope, $timeout) {
        const svc = {
            show: function (err) {
                $timeout(function () {
                    if (err instanceof Array) {
                        $rootScope.errors = err;
                    }
                    else {
                        $rootScope.errors = [err];
                    }
                }, 100);
            },
            clear: function () {
                $rootScope.errors = null;
            }
        };

        return svc;
    }

    idmErrorService.$inject = ["$rootScope", "$timeout"];
    app.factory("idmErrorService", idmErrorService);

    function idmApi($http, $q, PathBase, ttSafeDom) {
        let cache = null;
        const apiUrl = ttSafeDom.safeApiUrl(PathBase, "/api");

        return {
            get: function () {
                if (cache) {
                    const d = $q.defer();
                    d.resolve(cache);
                    return d.promise;
                }

                return $http.get(apiUrl).then(function (resp) {
                    cache = resp.data;
                    return cache;
                },
                    function (resp) {
                        cache = null;
                        if (resp.status === 403) {
                            throw "You are not authorized to use this service.";
                        } else {
                            throw resp.data && resp.data.message ||
                            "Failed to access IdentityManager API.";
                        }
                    });
            }
        };
    }

    idmApi.$inject = ["$http", "$q", "PathBase", "ttSafeDom"];
    app.factory("idmApi", idmApi);

    function idmUsers($http, idmApi) {
        function nop() {
        }

        function mapResponseData(response) {
            return response.data;
        }

        function errorHandler(msg) {
            msg = msg || "Unexpected Error";
            return function (response) {
                throw response.data.errors || response.data.message || msg;
            };
        }

        const svc = idmApi.get().then(function (api) {
            svc.getUsers = function (filter, start, count) {
                return $http.get(api.links.users, { params: { filter: filter, start: start, count: count } })
                    .then(mapResponseData, errorHandler("Error Getting Users"));
            };

            svc.getUser = function (subject) {
                return $http.get(api.links.users + "/" + encodeURIComponent(subject))
                    .then(mapResponseData, errorHandler("Error Getting User"));
            };

            if (api.links.createUser) {
                svc.createUser = function (properties) {
                    return $http.post(api.links.createUser.href, properties)
                        .then(mapResponseData, errorHandler("Error Creating User"));
                };
            }

            svc.deleteUser = function (user) {
                return $http.delete(user.links.delete)
                    .then(nop, errorHandler("Error Deleting User"));
            };

            svc.setProperty = function (property) {
                if (property.data === 0) {
                    property.data = "0";
                }
                if (property.data === false) {
                    property.data = "false";
                }
                return $http.put(property.links.update, property.data)
                    .then(nop, errorHandler(property.meta && property.meta.name && "Error Setting " + property.meta.name || "Error Setting Property"));
            };

            svc.addClaim = function (claims, claim) {
                return $http.post(claims.links.create, claim)
                    .then(nop, errorHandler("Error Adding Claim"));
            };

            svc.removeClaim = function (claim) {
                return $http.delete(claim.links.delete)
                    .then(nop, errorHandler("Error Removing Claim"));
            };

            svc.addRole = function (role) {
                return $http.post(role.links.add)
                    .then(nop, errorHandler("Error Adding Role"));
            };

            svc.removeRole = function (role) {
                return $http.delete(role.links.remove)
                    .then(nop, errorHandler("Error Removing Role"));
            };
        });

        return svc;
    }
    idmUsers.$inject = ["$http", "idmApi"];
    app.factory("idmUsers", idmUsers);

    function idmRoles($http, idmApi) {
        function nop() {
        }

        function mapResponseData(response) {
            return response.data;
        }

        function errorHandler(msg) {
            msg = msg || "Unexpected Error";
            return function (response) {
                throw response.data.errors || response.data.message || msg;
            };
        }

        const svc = idmApi.get().then(function (api) {
            svc.getRoles = function (filter, start, count) {
                return $http.get(api.links.roles, { params: { filter: filter, start: start, count: count } })
                    .then(mapResponseData, errorHandler("Error Getting Roles"));
            };

            svc.getRole = function (subject) {
                return $http.get(api.links.roles + "/" + encodeURIComponent(subject))
                    .then(mapResponseData, errorHandler("Error Getting Role"));
            };

            svc.setProperty = function (property) {
                if (property.data === 0) {
                    property.data = "0";
                }

                if (property.data === false) {
                    property.data = "false";
                }

                return $http.put(property.links.update, property.data)
                    .then(nop, errorHandler(property.meta && property.meta.name && "Error Setting " + property.meta.name || "Error Setting Property"));
            };

            if (api.links.createRole) {
                svc.createRole = function (properties) {
                    return $http.post(api.links.createRole.href, properties)
                        .then(mapResponseData, errorHandler("Error Creating Role"));
                };
            }

            svc.deleteRole = function (role) {
                return $http.delete(role.links.delete)
                    .then(nop, errorHandler("Error Deleting Role"));
            };
        });

        return svc;
    }
    idmRoles.$inject = ["$http", "idmApi"];
    app.factory("idmRoles", idmRoles);
})(angular);

(function (angular) {
    const modelElem = document.getElementById("model");
    let model = {};

    if (modelElem && typeof modelElem.textContent === "string") {
        try {
            model = JSON.parse(modelElem.textContent.trim());
        } catch (e) {
            // eslint-disable-next-line no-console
            console.error("Failed to parse IdentityManager model JSON.", e);
        }
    }

    for (let key in model) {
        angular.module("ttIdm").constant(key, model[key]);
    }
})(angular);
