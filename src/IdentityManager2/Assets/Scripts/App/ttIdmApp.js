﻿/// <reference path="../Libs/angular.min.js" />
/// <reference path="../Libs/angular-route.min.js" />

(function (angular) {
    const app = angular.module("ttIdmApp", ["ngRoute", "ttIdm", "ttIdmUI", "ttIdmUsers", "ttIdmRoles"]);
    function config(PathBase, $routeProvider) {
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
    config.$inject = ["PathBase", "$routeProvider"];
    app.config(config);

    function LayoutCtrl($rootScope, PathBase, idmApi, $location, $window, idmErrorService, ShowLoginButton, TitleNavBarLinkTarget) {
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

            $window.location = PathBase + "/api/login";
        };

        $rootScope.logout = function() {
            idmErrorService.clear();

            $window.location = PathBase + "/api/logout";
        };
    }
    LayoutCtrl.$inject = ["$rootScope", "PathBase", "idmApi", "$location", "$window", "idmErrorService", "ShowLoginButton", "TitleNavBarLinkTarget"];
    app.controller("LayoutCtrl", LayoutCtrl);
})(angular);
