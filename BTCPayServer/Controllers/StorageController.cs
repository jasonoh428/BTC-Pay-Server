using System.Threading.Tasks;
using BTCPayServer.Storage.Services;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Storage
{
    [Route("Storage")]
    public class StorageController
    {
        private readonly FileService _FileService;

        public StorageController(FileService fileService)
        {
            _FileService = fileService;
        }

        [HttpGet("{fileId}")]
        public async Task<IActionResult> GetFile(string fileId)
        {
            var url = await _FileService.GetFileUrl(fileId);
            return new RedirectResult(url);
        }
    }
}
