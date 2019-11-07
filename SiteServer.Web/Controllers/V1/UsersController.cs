﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using SiteServer.CMS.Api.V1;
using SiteServer.CMS.Core;
using SiteServer.CMS.DataCache;
using SiteServer.CMS.Model;
using SiteServer.CMS.Model.Db;
using SiteServer.Utils;
using SiteServer.Utils.Enumerations;

namespace SiteServer.API.Controllers.V1
{
    [RoutePrefix("v1/users")]
    public class UsersController : ApiController
    {
        private const string Route = "";
        private const string RouteActionsLogin = "actions/login";
        private const string RouteActionsLogout = "actions/logout";
        private const string RouteUser = "{id:int}";
        private const string RouteUserAvatar = "{id:int}/avatar";
        private const string RouteUserLogs = "{id:int}/logs";
        private const string RouteUserResetPassword = "{id:int}/actions/resetPassword";

        [HttpPost, Route(Route)]
        public async Task<IHttpActionResult> Create()
        {
            try
            {
                var request = new AuthenticatedRequest();
                var user = new User(request.GetPostObject<Dictionary<string, object>>());
                if (!ConfigManager.SystemConfigInfo.IsUserRegistrationGroup)
                {
                    user.GroupId = 0;
                }
                var password = request.GetPostString("password");

                var valid = await DataProvider.UserDao.InsertAsync(user, password, PageUtils.GetIpAddress());
                if (valid.UserId == 0)
                {
                    return BadRequest(valid.ErrorMessage);
                }

                return Ok(new
                {
                    Value = await UserManager.GetUserByUserIdAsync(valid.UserId)
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpPut, Route(RouteUser)]
        public async Task<IHttpActionResult> Update(int id)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var body = request.GetPostObject<Dictionary<string, object>>();

                if (body == null) return BadRequest("Could not read user from body");

                var user = await UserManager.GetUserByUserIdAsync(id);
                if (user == null) return NotFound();

                var valid = await DataProvider.UserDao.UpdateAsync(user, body);
                if (valid.User == null)
                {
                    return BadRequest(valid.ErrorMessage);
                }

                return Ok(new
                {
                    Value = valid.User
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpDelete, Route(RouteUser)]
        public async Task<IHttpActionResult> Delete(int id)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var user = await UserManager.GetUserByUserIdAsync(id);
                if (user == null) return NotFound();

                request.UserLogout();
                await DataProvider.UserDao.DeleteAsync(user);

                return Ok(new
                {
                    Value = user
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route(RouteUser)]
        public async Task<IHttpActionResult> Get(int id)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                if (!await DataProvider.UserDao.IsExistsAsync(id)) return NotFound();

                var user = await UserManager.GetUserByUserIdAsync(id);

                return Ok(new
                {
                    Value = user
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route(RouteUserAvatar)]
        public async Task<IHttpActionResult> GetAvatar(int id)
        {
            var user = await UserManager.GetUserByUserIdAsync(id);

            var avatarUrl = !string.IsNullOrEmpty(user?.AvatarUrl) ? user.AvatarUrl : UserManager.DefaultAvatarUrl;
            avatarUrl = PageUtils.AddProtocolToUrl(avatarUrl);

            return Ok(new
            {
                Value = avatarUrl
            });
        }

        [HttpPost, Route(RouteUserAvatar)]
        public async Task<IHttpActionResult> UploadAvatar(int id)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var user = await UserManager.GetUserByUserIdAsync(id);
                if (user == null) return NotFound();

                foreach (string name in HttpContext.Current.Request.Files)
                {
                    var postFile = HttpContext.Current.Request.Files[name];

                    if (postFile == null)
                    {
                        return BadRequest("Could not read image from body");
                    }

                    var fileName = UserManager.GetUserUploadFileName(postFile.FileName);
                    var filePath = UserManager.GetUserUploadPath(user.Id, fileName);
                    
                    if (!EFileSystemTypeUtils.IsImage(PathUtils.GetExtension(fileName)))
                    {
                        return BadRequest("image file extension is not correct");
                    }

                    DirectoryUtils.CreateDirectoryIfNotExists(filePath);
                    postFile.SaveAs(filePath);

                    user.AvatarUrl = UserManager.GetUserUploadUrl(user.Id, fileName);

                    await DataProvider.UserDao.UpdateAsync(user);
                }

                return Ok(new
                {
                    Value = user
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route(Route)]
        public async Task<IHttpActionResult> List()
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var top = request.GetQueryInt("top", 20);
                var skip = request.GetQueryInt("skip");

                var users = await DataProvider.UserDao.GetUsersAsync( ETriState.All, 0, 0, null, null, skip, top);
                var count = await DataProvider.UserDao.GetCountAsync();

                return Ok(new PageResponse(users, top, skip, request.HttpRequest.Url.AbsoluteUri) { Count = count });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(RouteActionsLogin)]
        public async Task<IHttpActionResult> Login()
        {
            try
            {
                var request = new AuthenticatedRequest();

                var account = request.GetPostString("account");
                var password = request.GetPostString("password");
                var isAutoLogin = request.GetPostBool("isAutoLogin");

                var valid = await DataProvider.UserDao.ValidateAsync(account, password, true);
                if (valid.User == null)
                {
                    return BadRequest(valid.ErrorMessage);
                }

                var accessToken = request.UserLogin(valid.UserName, isAutoLogin);
                var expiresAt = DateTime.Now.AddDays(Constants.AccessTokenExpireDays);

                return Ok(new
                {
                    Value = valid.User,
                    AccessToken = accessToken,
                    ExpiresAt = expiresAt
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(RouteActionsLogout)]
        public IHttpActionResult Logout()
        {
            try
            {
                var request = new AuthenticatedRequest();
                var user = request.IsUserLoggin ? request.User : null;
                request.UserLogout();

                return Ok(new
                {
                    Value = user
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(RouteUserLogs)]
        public async Task<IHttpActionResult> CreateLog(int id, [FromBody] UserLogInfo logInfo)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var user = await UserManager.GetUserByUserIdAsync(id);
                if (user == null) return NotFound();

                var retVal = DataProvider.UserLogDao.ApiInsert(user.UserName, logInfo);

                return Ok(new
                {
                    Value = retVal
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route(RouteUserLogs)]
        public async Task<IHttpActionResult> GetLogs(int id)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var user = await UserManager.GetUserByUserIdAsync(id);
                if (user == null) return NotFound();

                var top = request.GetQueryInt("top", 20);
                var skip = request.GetQueryInt("skip");

                var logs = DataProvider.UserLogDao.ApiGetLogs(user.UserName, skip, top);

                return Ok(new PageResponse(logs, top, skip, request.HttpRequest.Url.AbsoluteUri)
                    {Count = await DataProvider.UserDao.GetCountAsync()});
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(RouteUserResetPassword)]
        public async Task<IHttpActionResult> ResetPassword(int id)
        {
            try
            {
                var request = new AuthenticatedRequest();
                var isAuth = request.IsApiAuthenticated && await 
                             AccessTokenManager.IsScopeAsync(request.ApiToken, AccessTokenManager.ScopeUsers) ||
                             request.IsUserLoggin &&
                             request.UserId == id ||
                             request.IsAdminLoggin &&
                             request.AdminPermissions.HasSystemPermissions(ConfigManager.SettingsPermissions.User);
                if (!isAuth) return Unauthorized();

                var user = await UserManager.GetUserByUserIdAsync(id);
                if (user == null) return NotFound();

                var password = request.GetPostString("password");
                var newPassword = request.GetPostString("newPassword");

                if (!DataProvider.UserDao.CheckPassword(password, false, user.Password, EPasswordFormatUtils.GetEnumType(user.PasswordFormat), user.PasswordSalt))
                {
                    return BadRequest("原密码不正确，请重新输入");
                }

                var valid = await DataProvider.UserDao.ChangePasswordAsync(user.UserName, newPassword);
                if (!valid.IsValid)
                {
                    return BadRequest(valid.ErrorMessage);
                }
                
                return Ok(new
                {
                    Value = user
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }
    }
}