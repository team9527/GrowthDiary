﻿using FlyLolo.JWT;
using GrowthDiary.Common;
using GrowthDiary.IService;
using GrowthDiary.Model;
using GrowthDiary.Wx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace GrowthDiary.Controllers
{
    [Route("api/[controller]")]
    public class AccountController : BaseController
    {
        private readonly WXOptions _options;
        private readonly IUserService _userService;
        private readonly ITokenHelper _tokenHelper = null;
        public AccountController(IOptionsMonitor<WXOptions> options, IUserService userService, ITokenHelper tokenHelper)
        {
            _options = options.Get("WXOptions");
            _userService = userService;
            _tokenHelper = tokenHelper;
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Get([FromServices] IHttpClientFactory httpClientFactory)
        {
            string loginCode;
            if (Request.Query.Keys.Contains("loginCode"))
            {
                loginCode = Request.Query["loginCode"];

                if (string.IsNullOrEmpty(loginCode))
                {
                    return new JsonResult(new ApiResult(ReturnCode.ArgsError));
                }
            }
            else
            {
                return new JsonResult(new ApiResult(ReturnCode.ArgsError));
            }

            Code2Session session = null;
            string url = string.Format(_options.Code2Session, _options.AppId, _options.Secret, loginCode);

            using (var client = httpClientFactory.CreateClient())
            {
                using var res = client.GetAsync(url);
                if (res.Result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var str = res.Result.Content.ReadAsStringAsync().Result;
                    session = JsonConvert.DeserializeObject<Code2Session>(str);
                }
            }

            if (string.IsNullOrEmpty(session.Openid))
            {
                return new JsonResult(new ApiResult(ReturnCode.GeneralError));
            }
            //小程序返回的Openid验证
            UserSearchModel userSearchModel = new UserSearchModel { WxOpenId = session.Openid };
            UserViewModel user = _userService.Find(userSearchModel);

            if (null == user)
            {
                //用户名密码方式验证
                if (Request.Query.Keys.Contains("usercode"))
                {
                    userSearchModel.UserCode = Request.Query["usercode"];
                    user = _userService.Find(userSearchModel);
                    if (null == user || (!user.Password.Equals(Request.Query["userPassword"])))
                    {
                        return new JsonResult(new ApiResult(ReturnCode.LoginError));
                    }

                    user.NickName = Request.Query["nickName"];
                    user.Gender = Request.Query["gender"];
                    user.Country = Request.Query["country"];
                    user.Province = Request.Query["province"];
                    user.City = Request.Query["city"];
                    user.Language = Request.Query["language"];
                    user.AvatarUrl = Request.Query["avatarUrl"];
                    user.WxOpenId = session.Openid;

                    try
                    {
                        _userService.UpdateOneAsync(user, "NickName", "Gender", "Country", "Province", "City", "Language", "AvatarUrl", "WxOpenId");
                    }
                    catch (System.Exception)
                    {
                        return new JsonResult(new ApiResult(ReturnCode.GeneralError));
                    }
                }
                else
                {
                    return new JsonResult(new ApiResult(ReturnCode.LoginError));
                }
            }
            var token = _tokenHelper.CreateToken(user);
            if (null == token)
            {
                return StatusCode(401);
            }
            return new JsonResult(new ApiResult<ComplexToken>(token, ReturnCode.Success));
        }
    }
}
