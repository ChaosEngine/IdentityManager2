/*global angular*/
/// <reference path="../Libs/angular.min.js" />
/// <reference path="../Libs/angular-route.min.js" />

(function (angular) {

    const app = angular.module("ttIdmUsers", ['ngRoute', 'ttIdm', 'ttIdmUI']);
    function safeTemplateUrl(pathBase, templatePath) {
        const base = typeof pathBase === "string" ? pathBase.trim().replace(/\/$/, "") : "";
        if (typeof templatePath !== "string" || templatePath.charAt(0) !== "/") {
            return base + "/assets/Templates.message.html";
        }

        return base + templatePath;
    }

    function config($routeProvider, PathBase) {
        $routeProvider
            .when("/users/list/:filter?/:page?", {
                controller: 'ListUsersCtrl',
                resolve: { users: "idmUsers" },
                templateUrl: safeTemplateUrl(PathBase, '/assets/Templates.users.list.html')
            })
            .when("/users/create", {
                controller: 'NewUserCtrl',
                resolve: {
                    api: function (idmApi) {
                        return idmApi.get();
                    }
                },
                templateUrl: safeTemplateUrl(PathBase, '/assets/Templates.users.new.html')
            })
            .when("/users/edit/:subject", {
                controller: 'EditUserCtrl',
                resolve: { users: "idmUsers" },
                templateUrl: safeTemplateUrl(PathBase, '/assets/Templates.users.edit.html')
            });
    }
    config.$inject = ["$routeProvider", "PathBase"];
    app.config(config);

    function ListUsersCtrl($scope, idmUsers, idmPager, $routeParams, $location) {
        const model = {
            message : null,
            users : null,
            pager : null,
            waiting : true,
            filter : $routeParams.filter,
            page : $routeParams.page || 1
        };
        $scope.model = model;

        $scope.search = function (filter) {
            let url = "/users/list";
            if (filter) {
                url += "/" + filter;
            }
            $location.url(url);
        };

        const itemsPerPage = 10;
        const startItem = (model.page - 1) * itemsPerPage;

        idmUsers.getUsers(model.filter, startItem, itemsPerPage).then(function (result) {
            $scope.model.waiting = false;
            $scope.model.users = result.data.items;
            if (result.data.items && result.data.items.length) {
                $scope.model.pager = new idmPager(result.data, itemsPerPage);
            }
        }, function (error) {
            $scope.model.message = error;
            $scope.model.waiting = false;
        });
    }
    ListUsersCtrl.$inject = ["$scope", "idmUsers", "idmPager", "$routeParams", "$location"];
    app.controller("ListUsersCtrl", ListUsersCtrl);

    function NewUserCtrl($scope, idmUsers, api, ttFeedback) {
        const feedback = new ttFeedback();
        $scope.feedback = feedback;
        if (!api.links.createUser) {
            feedback.errors = "Create Not Supported";
            return;
        }
        else {
            const properties = api.links.createUser.meta
                .map(function (item) {
                    return {
                        meta : item,
                        data : item.dataType === 5 ? false : undefined
                    };
                });
            $scope.properties = properties;
            $scope.create = function (properties) {
                const props = properties.map(function (item) {
                    return {
                        type: item.meta.type,
                        value: item.data.toString()
                    };
                });
                idmUsers.createUser(props)
                    .then(function (result) {
                        $scope.last = result;
                        feedback.message = "Create Success";
                    }, feedback.errorHandler);
            };
        }
    }
    NewUserCtrl.$inject = ["$scope", "idmUsers", "api", "ttFeedback"];
    app.controller("NewUserCtrl", NewUserCtrl);

    function EditUserCtrl($scope, idmUsers, $routeParams, ttFeedback) {
        const feedback = new ttFeedback();
        $scope.feedback = feedback;

        function loadUser() {
            return idmUsers.getUser($routeParams.subject)
                .then(function (result) {
                    $scope.user = result;

                    if (!result.data.properties) {
                        $scope.tab = 1;

                        if (!result.data.roles) {
                            $scope.tab = 2;
                        }
                    }

                }, feedback.errorHandler);
        }
        loadUser();

        $scope.setProperty = function (property) {
            idmUsers.setProperty(property)
                .then(function () {
                    if (property.meta.dataType !== 1) {
                        feedback.message = property.meta.name + " Changed to: " + property.data;
                    }
                    else {
                        feedback.message = property.meta.name + " Changed";
                    }
                    loadUser();
                }, feedback.errorHandler);
        };

        $scope.addClaim = function (claims, claim) {
            idmUsers.addClaim(claims, claim)
                .then(function () {
                    feedback.message = "Claim Added : " + claim.type + ", " + claim.value;
                    loadUser();
                }, feedback.errorHandler);
        };

        $scope.removeClaim = function (claim) {
            idmUsers.removeClaim(claim)
                .then(function () {
                    feedback.message = "Claim Removed : " + claim.data.type + ", " + claim.data.value;
                    loadUser().then(function () {
                        $scope.claim = claim.data;
                    });
                }, feedback.errorHandler);
        };

        $scope.deleteUser = function (user) {
            idmUsers.deleteUser(user)
                .then(function () {
                    feedback.message = "User Deleted";
                    $scope.user = null;
                }, feedback.errorHandler);
        };

        $scope.setRole = function (role) {
            if (role.data) {
                idmUsers.addRole(role)
                    .then(function () {
                        feedback.message = "Role Added : " + role.meta.type;
                        loadUser();
                    }, feedback.errorHandler);
            }
            else {
                idmUsers.removeRole(role)
                    .then(function () {
                        feedback.message = "Role Removed : " + role.meta.type;
                        loadUser();
                    }, feedback.errorHandler);
            }
        };
    }
    EditUserCtrl.$inject = ["$scope", "idmUsers", "$routeParams", "ttFeedback"];
    app.controller("EditUserCtrl", EditUserCtrl);

})(angular);
