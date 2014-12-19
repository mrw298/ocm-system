﻿using Newtonsoft.Json;
using OCM.API.Common.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OCM.API.Common
{
    public enum StandardPermissionAttributes
    {
        Administrator = 1,
        CountryLevel_Editor = 100,
        CountryLevel_Approver = 200
    }

    public class UserManager
    {
        private OCM.Core.Data.OCMEntities dataModel = new Core.Data.OCMEntities();

        public User GetUser(int id)
        {
            var userDetails = dataModel.Users.FirstOrDefault(u => u.ID == id);
            if (userDetails != null)
            {
                return Model.Extensions.User.FromDataModel(userDetails);
            }
            else
            {
                return null;
            }
        }

        public User GetUserFromIdentifier(string Identifier, string SessionToken)
        {
            if (Identifier == null || SessionToken == null) return null;

            var userDetails = dataModel.Users.FirstOrDefault(u => u.Identifier == Identifier);
            if (userDetails != null)
            {
                User user = Model.Extensions.User.FromDataModel(userDetails);
                if (user.CurrentSessionToken != SessionToken)
                    user.IsCurrentSessionTokenValid = false;
                else
                    user.IsCurrentSessionTokenValid = true;

                return user;
            }
            else
            {
                return null;
            }
        }

        public void AddReputationPoints(int userId, int amount)
        {
            var user = Model.Extensions.User.FromDataModel(dataModel.Users.FirstOrDefault(u => u.ID == userId));
            this.AddReputationPoints(user, amount);
        }

        /// <summary>
        /// Add reputation points to a user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="amount"></param>
        public void AddReputationPoints(User user, int amount)
        {
            try
            {
                if (user != null)
                {
                    if (user.ID > 0)
                    {
                        var userData = dataModel.Users.First(u => u.ID == user.ID);
                        if (userData != null)
                        {
                            if (userData.ReputationPoints == null) userData.ReputationPoints = 0;
                            userData.ReputationPoints += amount;
                            dataModel.SaveChanges();
                        }
                    }
                }
            }
            catch (Exception)
            {
                //could not add points to given user
            }
        }

        public void AssignNewSessionToken(int userId)
        {
            var user = dataModel.Users.FirstOrDefault(u => u.ID == userId);
            if (user != null)
            {
                user.CurrentSessionToken = Guid.NewGuid().ToString();
                dataModel.SaveChanges();
            }
        }

        /// <summary>
        /// Apply updates to a user profile
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool UpdateUserProfile(User updatedProfile, bool allowAdminChanges)
        {
            try
            {
                if (updatedProfile != null)
                {
                    if (updatedProfile.ID > 0)
                    {
                        var userData = dataModel.Users.First(u => u.ID == updatedProfile.ID);
                        if (userData != null)
                        {
                            userData.Location = updatedProfile.Location;
                            userData.Profile = updatedProfile.Profile;
                            userData.Username = updatedProfile.Username;
                            userData.WebsiteURL = updatedProfile.WebsiteURL;

                            userData.IsProfilePublic = (updatedProfile.IsProfilePublic != null ? (bool)updatedProfile.IsProfilePublic : false);
                            userData.IsPublicChargingProvider = (updatedProfile.IsPublicChargingProvider != null ? (bool)updatedProfile.IsPublicChargingProvider : false);
                            userData.IsEmergencyChargingProvider = (updatedProfile.IsEmergencyChargingProvider != null ? (bool)updatedProfile.IsEmergencyChargingProvider : false);
                            userData.EmailAddress = updatedProfile.EmailAddress;
                            userData.Latitude = updatedProfile.Latitude;
                            userData.Longitude = updatedProfile.Longitude;

                            //TODO: SyncedSettings
                            if (allowAdminChanges)
                            {
                                userData.ReputationPoints = updatedProfile.ReputationPoints;
                                userData.Identifier = updatedProfile.Identifier;
                                userData.Permissions = updatedProfile.Permissions;
                                userData.IdentityProvider = updatedProfile.IdentityProvider;
                            }

                            dataModel.SaveChanges();

                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                //could not update user
                return false;
            }

            return false;
        }

        /// <summary>
        /// Returns true if users exists but the provided session token is invalid
        /// </summary>
        /// <param name="Identifier"></param>
        /// <param name="SessionToken"></param>
        /// <returns></returns>
        public bool HasUserSessionExpired(string Identifier, string SessionToken)
        {
            var userDetails = dataModel.Users.FirstOrDefault(u => u.Identifier == Identifier);
            if (userDetails != null)
            {
                if (userDetails.CurrentSessionToken == SessionToken) return false;
                else return true;
            }
            return false;
        }

        public List<User> GetUsers()
        {
            List<User> list = new List<User>();
            var userList = dataModel.Users.Where(u => u.Identifier != null).OrderBy(u => u.Username);
            foreach (var user in userList)
            {
                list.Add(Model.Extensions.User.FromDataModel(user));
            }
            return list;
        }

        public static bool HasUserPermission(User user, StandardPermissionAttributes permissionAttribute, string attributeValue)
        {
            if (user != null && attributeValue != null)
            {
                if (user.Permissions != null)
                {
                    if (user.Permissions.Contains("[" + permissionAttribute.ToString() + "=" + attributeValue + "]"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool HasUserPermission(User user, int? CountryID, PermissionLevel requiredLevel, UserPermissionsContainer userPermissions=null)
        {
            if (user != null)
            {   
                if (!String.IsNullOrEmpty(user.Permissions))
                {
                    if (userPermissions == null)
                    {
                        userPermissions = JsonConvert.DeserializeObject<UserPermissionsContainer>(user.Permissions);
                    }

                    if (userPermissions.Permissions != null)
                    {
                        if (userPermissions.Permissions.Any(p => p.Level == PermissionLevel.Admin && (p.CountryID == null || p.CountryID == CountryID)))
                        {
                            //user is admin (for given country or all countries)
                            return true;
                        }

                        if (userPermissions.Permissions.Any(p => p.Level == requiredLevel && (p.CountryID == null || p.CountryID == CountryID)))
                        {
                            //user has required level of access (for given country or all countries)
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool IsUserAdministrator(User user)
        {
            if (HasUserPermission(user, null, PermissionLevel.Admin))
            {
                return true;
            }
            //fallback legacy check
            return HasUserPermission(user, StandardPermissionAttributes.Administrator, "true");
        }

        public bool GrantPermission(User user, StandardPermissionAttributes permissionAttribute, string attributeValue, bool removeOnly, User administrator)
        {
            //to apply permissions we add or remove from the permissions list attached to the user details, we also maintain a string in the legacy semicolon seperated format for apps/code which still requires the older format.
            var userDetails = dataModel.Users.FirstOrDefault(u => u.ID == user.ID);
            if (userDetails != null)
            {
                UserPermissionsContainer userPermissions = new UserPermissionsContainer();
                if (!String.IsNullOrEmpty(user.Permissions))
                {
                    userPermissions = JsonConvert.DeserializeObject<UserPermissionsContainer>(user.Permissions);
                }

                //apply permission to legacypermission tag of user details
                string attributeTag = "[" + permissionAttribute.ToString() + "=" + attributeValue + "];";

                if (userPermissions.LegacyPermissions == null) userPermissions.LegacyPermissions = "";
                if (userPermissions.Permissions == null) userPermissions.Permissions = new List<UserPermission>();

                if (!removeOnly)
                {
                    //add permission

                    //append permission attribute for user

                    //legacy format is [AttributeName1=Value];[AttributeName2=Value]; -legacy  format is maintained as LegacyPermissions  field in JSON format, for older apps (mainly older versions of OCM app)
                    if (!userPermissions.LegacyPermissions.Contains(attributeTag))
                    {
                        if (!userPermissions.LegacyPermissions.EndsWith(";") && userPermissions.LegacyPermissions != "") userPermissions.LegacyPermissions += ";";
                        userPermissions.LegacyPermissions += attributeTag;

                        //add permission to main permission list
                        if (permissionAttribute == StandardPermissionAttributes.CountryLevel_Editor)
                        {
                            var permission = new UserPermission();
                            if (attributeValue != "All")
                            {
                                permission.CountryID = int.Parse(attributeValue);
                            }
                            permission.Level = PermissionLevel.Editor;
                            userPermissions.Permissions.Add(permission);
                        }

                        //TODO: administrator permissions
                        AuditLogManager.Log(administrator, AuditEventType.PermissionGranted, "User: " + user.ID + "; Permission:" + permissionAttribute.ToString(), null);
                    }
                }
                else
                {
                    //remove permission
                    userPermissions.LegacyPermissions = userPermissions.LegacyPermissions.Replace(attributeTag, "");

                    if (permissionAttribute == StandardPermissionAttributes.CountryLevel_Editor)
                    {
                        if (attributeValue != "All")
                        {
                            int countryID = int.Parse(attributeValue);
                            userPermissions.Permissions.RemoveAll(p => p.Level == PermissionLevel.Editor && p.CountryID == countryID);
                        }
                        else
                        {
                            userPermissions.Permissions.RemoveAll(p => p.Level == PermissionLevel.Editor);
                        }
                    }
                    AuditLogManager.Log(administrator, AuditEventType.PermissionRemoved, "User: " + user.ID + "; Permission:" + permissionAttribute.ToString(), null);
                }

                //remove requested permission attribute if it exists
                if (userDetails.PermissionsRequested != null)
                {
                    userDetails.PermissionsRequested = userDetails.PermissionsRequested.Replace(attributeTag, "");
                }

                userDetails.Permissions = JsonConvert.SerializeObject(userPermissions, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                dataModel.SaveChanges();
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ConvertUserPermissions()
        {
            //perform batch upgrade of all user permisions to include JSON formatted permissions and legacy format string
            var userList = dataModel.Users.Where(u => u.Permissions != null);
            foreach (var user in userList)
            {
                if (!user.Permissions.Contains("{"))
                {
                    List<UserPermission> permissions = new List<UserPermission>();

                    //parse permissions
                    var pList = user.Permissions.Split(';');
                    foreach (var p in pList)
                    {
                        var legacyPermission = p.Trim();
                        if (!String.IsNullOrEmpty(legacyPermission))
                        {
                            var permission = new UserPermission();
                            //[CountryLevel_Editor=All];[Administrator=true];
                            bool parsedOK = false;
                            if (legacyPermission.StartsWith("[CountryLevel_Editor"))
                            {
                                permission.Level = PermissionLevel.Editor;
                                if (!legacyPermission.Contains("=All"))
                                {
                                    var countryIDString = legacyPermission.Substring(p.IndexOf("=") + 1, legacyPermission.IndexOf("]") - (legacyPermission.IndexOf("=") + 1));
                                    permission.CountryID = int.Parse(countryIDString);
                                }
                                parsedOK = true;
                            }

                            if (legacyPermission.StartsWith("[Administrator=true]"))
                            {
                                permission.Level = PermissionLevel.Admin;
                                parsedOK = true;
                            }

                            if (!parsedOK)
                            {
                                throw new Exception("Failed to parse permission: User" + user.ID + " :" + user.Permissions);
                            }
                            else
                            {
                                permissions.Add(permission);
                            }
                        }
                    }

                    UserPermissionsContainer allPermissions = new UserPermissionsContainer()
                    {
                        LegacyPermissions = user.Permissions, //preserve permissions string for legacy users
                        Permissions = permissions  //express permission as a list of permission objects
                    };

                    user.Permissions = JsonConvert.SerializeObject(allPermissions, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                }
            }

            dataModel.SaveChanges();
        }

        public void PromoteUserToCountryEditor(int authorizingUserId, int userId, int countryID, bool autoCreateSubscriptions, bool removePermission)
        {
            List<int> countryIds = new List<int>();
            countryIds.Add(countryID);
            PromoteUserToCountryEditor(authorizingUserId, userId, countryIds, autoCreateSubscriptions, removePermission);
        }

        public void PromoteUserToCountryEditor(int authorizingUserId, int userId, List<int> CountryIDs, bool autoCreateSubscriptions, bool removePermission)
        {
            var authorisingUser = GetUser(authorizingUserId);
            var user = GetUser(userId);

            foreach (var countryId in CountryIDs)
            {
                //grant country editor permissions for each country
                GrantPermission(user, StandardPermissionAttributes.CountryLevel_Editor, countryId.ToString(), removePermission, authorisingUser);
            }

            if (!removePermission && autoCreateSubscriptions)
            {
                var subscriptionManager = new UserSubscriptionManager();
                var refDataManager = new ReferenceDataManager();
                var allSubscriptions = subscriptionManager.GetUserSubscriptions(userId);

                foreach (var countryId in CountryIDs)
                {
                    //if no subscription exists for this country for this user, create one
                    if (!allSubscriptions.Any(s => s.CountryID == countryId))
                    {
                        var country = refDataManager.GetCountry(countryId);
                        subscriptionManager.UpdateUserSubscription(new UserSubscription { Title = "All Updates For " + country.Title, IsEnabled = true, DateCreated = DateTime.UtcNow, CountryID = countryId, NotificationFrequencyMins = 720, NotifyComments = true, NotifyMedia = true, NotifyPOIAdditions = true, NotifyPOIEdits = true, NotifyPOIUpdates = true, UserID = userId });
                    }
                }
            }
        }
    }
}