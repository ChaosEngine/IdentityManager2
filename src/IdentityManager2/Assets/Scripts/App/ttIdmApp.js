/*global angular*/
/// <reference path="../Libs/angular.min.js" />
/// <reference path="../Libs/angular-route.min.js" />

(function (angular) {
    const app = angular.module("ttIdmApp", ["ngRoute", "ttIdm", "ttIdmUI", "ttIdmUsers", "ttIdmRoles"]);
    function config(PathBase, $routeProvider, $locationProvider) {
        // Configure hash prefix to empty string for compatibility with Angular 1.3.x URLs
        $locationProvider.hashPrefix('');
        
        $routeProvider
            .when("/", {
                templateUrl: PathBase + "/assets/Templates.home.html"
            })
            .when("/error", {
                templateUrl: PathBase + "/assets/Templates.message.html"
            })
            .otherwise({
                redirectTo: "/"
            });
    }
    config.$inject = ["PathBase", "$routeProvider", "$locationProvider"];
    app.config(config);

    function LayoutCtrl($rootScope, PathBase, idmApi, $location, $window, idmErrorService, ShowLoginButton,
        TitleNavBarLinkTarget, LoginPath, LogoutPath) {
        $rootScope.PathBase = PathBase;
        $rootScope.layout = {};

        function removed() {
            

            idmErrorService.clear();
            $rootScope.layout.username = null;
            $rootScope.layout.links = null;
            $rootScope.layout.showLogout = !ShowLoginButton;
            $rootScope.layout.showLogin = ShowLoginButton;
            $rootScope.layout.titleNavBarLinkTarget = TitleNavBarLinkTarget;
        }

        function load() {
            removed();

            if (ShowLoginButton === false) {
                idmApi.get().then(function (api) {
                    $rootScope.layout.username = api.data.currentUser.username;
                    $rootScope.layout.links = api.links;
                }, function (err) {
                    idmErrorService.show(err);
                });
            }
        }

        load();

        $rootScope.login = function () {
            idmErrorService.clear();

            $window.location = PathBase + (LoginPath || "/api/login");
        };

        $rootScope.logout = function() {
            idmErrorService.clear();

            $window.location = PathBase + (LogoutPath || "/api/logout");
        };
    }
    LayoutCtrl.$inject = ["$rootScope", "PathBase", "idmApi", "$location", "$window", "idmErrorService", "ShowLoginButton",
        "TitleNavBarLinkTarget", "LoginPath", "LogoutPath"];
    app.controller("LayoutCtrl", LayoutCtrl);
})(angular);
