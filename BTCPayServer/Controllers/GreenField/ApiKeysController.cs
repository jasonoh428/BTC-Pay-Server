using System.Threading.Tasks;
using System.Linq;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Security.GreenField;
using NBitcoin.DataEncoders;
using NBitcoin;

namespace BTCPayServer.Controllers.GreenField
{
    [ApiController]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.GreenfieldAPIKeys)]
    public class ApiKeysController : ControllerBase
    {
        private readonly APIKeyRepository _apiKeyRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApiKeysController(APIKeyRepository apiKeyRepository, UserManager<ApplicationUser> userManager)
        {
            _apiKeyRepository = apiKeyRepository;
            _userManager = userManager;
        }
    
        [HttpGet("~/api/v1/api-keys/current")]
        public async Task<ActionResult<ApiKeyData>> GetKey()
        {
            if (!ControllerContext.HttpContext.GetAPIKey(out var apiKey))
            {
                return NotFound();
            }
            var data = await _apiKeyRepository.GetKey(apiKey);
            return Ok(FromModel(data));
        }

        [HttpPost("~/api/v1/api-keys")]
        [Authorize(Policy = Policies.Unrestricted, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
        public async Task<ActionResult<ApiKeyData>> CreateKey(CreateApiKeyRequest request)
        {
            if (request is null)
                return BadRequest();
            var key = new APIKeyData()
            {
                Id = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20)),
                Type = APIKeyType.Permanent,
                UserId = _userManager.GetUserId(User),
                Label = request.Label
            };
            key.Permissions = string.Join(";", request.Permissions.Select(p => p.ToString()).Distinct().ToArray());
            await _apiKeyRepository.CreateKey(key);
            return Ok(FromModel(key));
        }

        [HttpDelete("~/api/v1/api-keys/current")]
        [Authorize(Policy = Policies.Unrestricted, AuthenticationSchemes = AuthenticationSchemes.GreenfieldAPIKeys)]
        public async Task<IActionResult> RevokeKey()
        {
            if (!ControllerContext.HttpContext.GetAPIKey(out var apiKey))
            {
                return NotFound();
            }
            await _apiKeyRepository.Remove(apiKey, _userManager.GetUserId(User));
            return Ok();
        }
        
        private static ApiKeyData FromModel(APIKeyData data)
        {
            return new ApiKeyData()
            {
                Permissions = Permission.ToPermissions(data.Permissions).ToArray(),
                ApiKey = data.Id,
                Label = data.Label ?? string.Empty
            };
        }
    }
}
